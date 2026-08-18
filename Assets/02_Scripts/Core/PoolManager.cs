using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public class PoolManager : MonoBehaviour
{
    [System.Serializable]
    public class PoolDefinition
    {
        public GameObject prefab;
        public int initialSize = 10;
        public int maxSize = 50;
    }

    public static PoolManager Instance { get; private set; }

    [Header("Pool Definitions")]
    [SerializeField] private PoolDefinition[] poolDefinitions;
    [SerializeField] private bool prewarmOnAwake = true;

    private readonly Dictionary<GameObject, IObjectPool<GameObject>> pools = new Dictionary<GameObject, IObjectPool<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new Dictionary<GameObject, GameObject>();
    private readonly Dictionary<GameObject, Transform> prefabRoots = new Dictionary<GameObject, Transform>();
    private readonly List<GameObject> staleInstanceKeys = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        BuildConfiguredPools();

        if (prewarmOnAwake)
        {
            PrewarmConfiguredPools();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildConfiguredPools()
    {
        if (poolDefinitions == null)
        {
            return;
        }

        foreach (PoolDefinition definition in poolDefinitions)
        {
            CreatePool(definition);
        }
    }

    private void PrewarmConfiguredPools()
    {
        if (poolDefinitions == null)
        {
            return;
        }

        foreach (PoolDefinition definition in poolDefinitions)
        {
            if (definition == null || definition.prefab == null)
            {
                continue;
            }

            if (!pools.TryGetValue(definition.prefab, out IObjectPool<GameObject> pool))
            {
                continue;
            }

            int count = Mathf.Max(0, definition.initialSize);
            if (count == 0)
            {
                continue;
            }

            List<GameObject> cache = new List<GameObject>(count);
            for (int i = 0; i < count; i++)
            {
                cache.Add(pool.Get());
            }

            foreach (GameObject instance in cache)
            {
                pool.Release(instance);
            }
        }
    }

    private void CreatePool(PoolDefinition definition)
    {
        if (definition == null || definition.prefab == null)
        {
            return;
        }

        if (pools.ContainsKey(definition.prefab))
        {
            return;
        }

        int initialSize = Mathf.Max(1, definition.initialSize);
        int maxSize = Mathf.Max(initialSize, definition.maxSize);

        GameObject prefab = definition.prefab;
        Transform root = new GameObject($"Pool_{prefab.name}").transform;
        root.SetParent(transform);
        prefabRoots[prefab] = root;

        IObjectPool<GameObject> pool = new ObjectPool<GameObject>(
            createFunc: () => CreateInstance(prefab, root),
            actionOnGet: _ => { },
            actionOnRelease: instance => ReturnToPool(instance, root),
            actionOnDestroy: DestroyInstance,
            collectionCheck: false,
            defaultCapacity: initialSize,
            maxSize: maxSize
        );

        pools[prefab] = pool;
    }

    private GameObject CreateInstance(GameObject prefab, Transform root)
    {
        GameObject instance = Instantiate(prefab, root);
        instance.SetActive(false);
        instanceToPrefab[instance] = prefab;
        return instance;
    }

    private void ReturnToPool(GameObject instance, Transform root)
    {
        if (instance == null)
        {
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(root);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
    }

    private void DestroyInstance(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        instanceToPrefab.Remove(instance);
        Destroy(instance);
    }

    private bool EnsurePool(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        if (pools.ContainsKey(prefab))
        {
            return true;
        }

        // Inspector에 등록하지 않았더라도 기본 설정으로 런타임 풀을 자동 생성합니다.
        CreatePool(new PoolDefinition
        {
            prefab = prefab,
            initialSize = 1,
            maxSize = 32
        });

        return pools.ContainsKey(prefab);
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!EnsurePool(prefab))
        {
            Debug.LogWarning("요청한 프리팹의 풀이 존재하지 않아 생성하지 못했습니다.", this);
            return null;
        }

        GameObject instance = pools[prefab].Get();
        instance.transform.SetParent(null);

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.IsValid() &&
            activeScene.isLoaded &&
            instance.scene != activeScene)
        {
            SceneManager.MoveGameObjectToScene(instance, activeScene);
        }
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        if (!instanceToPrefab.TryGetValue(instance, out GameObject prefab))
        {
            Destroy(instance);
            return;
        }

        if (!pools.TryGetValue(prefab, out IObjectPool<GameObject> pool))
        {
            Destroy(instance);
            return;
        }

        if (!instance.activeSelf)
        {
            return;
        }

        pool.Release(instance);
    }

    public void ReleaseAfter(GameObject instance, float delay)
    {
        if (instance == null)
        {
            return;
        }

        StartCoroutine(ReleaseAfterRoutine(instance, delay));
    }

    private IEnumerator ReleaseAfterRoutine(GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (instance != null && instance.activeInHierarchy)
        {
            Release(instance);
        }
    }

    public GameObject SpawnAutoRelease(GameObject prefab, Vector3 position, float duration)
    {
        GameObject instance = Get(prefab, position, Quaternion.identity);

        if (instance != null)
        {
            ReleaseAfter(instance, duration);
        }

        return instance;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        staleInstanceKeys.Clear();

        foreach (KeyValuePair<GameObject, GameObject> pair in instanceToPrefab)
        {
            if (pair.Key == null)
            {
                staleInstanceKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleInstanceKeys.Count; i++)
        {
            instanceToPrefab.Remove(staleInstanceKeys[i]);
        }

        staleInstanceKeys.Clear();
    }
}
