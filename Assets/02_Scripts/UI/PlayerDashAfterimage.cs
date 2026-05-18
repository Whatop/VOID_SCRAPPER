using UnityEngine;

[DisallowMultipleComponent]
public class PlayerDashAfterimage : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerDash playerDash;

    [Tooltip("플레이어 기체 이미지 루트. 보통 VisualRoot.")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("비워두면 VisualRoot 아래 SpriteRenderer를 자동 수집.")]
    [SerializeField] private SpriteRenderer[] sourceRenderers;

    [Header("Afterimage")]
    [SerializeField] private Transform afterimageParent;
    [SerializeField] private float spawnInterval = 0.035f;
    [SerializeField] private float lifeTime = 0.22f;
    [SerializeField] private Color afterimageColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private int sortingOrderOffset = -1;
    [SerializeField] private bool copySourceMaterial;

    private float spawnTimer;
    private bool wasDashing;

    private void Reset()
    {
        playerDash = GetComponentInParent<PlayerDash>();
        visualRoot = transform;
    }

    private void Awake()
    {
        CacheReferences();
        CacheSourceRenderers();
    }

    private void Update()
    {
        if (playerDash == null)
        {
            return;
        }

        bool isDashing = playerDash.IsDashing;

        if (isDashing && !wasDashing)
        {
            SpawnAfterimage();
            spawnTimer = spawnInterval;
        }

        wasDashing = isDashing;

        if (!isDashing)
        {
            spawnTimer = 0f;
            return;
        }

        spawnTimer -= Time.deltaTime;

        if (spawnTimer > 0f)
        {
            return;
        }

        spawnTimer = Mathf.Max(0.01f, spawnInterval);
        SpawnAfterimage();
    }

    private void CacheReferences()
    {
        if (playerDash == null)
        {
            playerDash = GetComponentInParent<PlayerDash>();
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void CacheSourceRenderers()
    {
        if (sourceRenderers != null && sourceRenderers.Length > 0)
        {
            return;
        }

        if (visualRoot == null)
        {
            return;
        }

        sourceRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void SpawnAfterimage()
    {
        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            CacheSourceRenderers();
        }

        if (sourceRenderers == null || sourceRenderers.Length == 0)
        {
            return;
        }

        GameObject root = new GameObject("DashAfterimage");

        if (afterimageParent != null)
        {
            root.transform.SetParent(afterimageParent, true);
        }

        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        bool copiedAny = false;

        foreach (SpriteRenderer source in sourceRenderers)
        {
            if (source == null)
            {
                continue;
            }

            if (!source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null)
            {
                continue;
            }

            GameObject child = new GameObject($"{source.name}_Ghost");
            child.transform.SetParent(root.transform, true);
            child.transform.position = source.transform.position;
            child.transform.rotation = source.transform.rotation;
            child.transform.localScale = source.transform.lossyScale;

            SpriteRenderer ghost = child.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.drawMode = source.drawMode;
            ghost.size = source.size;
            ghost.maskInteraction = source.maskInteraction;
            ghost.sortingLayerID = source.sortingLayerID;
            ghost.sortingOrder = source.sortingOrder + sortingOrderOffset;

            if (copySourceMaterial)
            {
                ghost.sharedMaterial = source.sharedMaterial;
            }

            Color finalColor = afterimageColor;
            finalColor.a = Mathf.Clamp01(afterimageColor.a);
            ghost.color = finalColor;

            copiedAny = true;
        }

        if (!copiedAny)
        {
            Destroy(root);
            return;
        }

        SpriteAfterimageGhost ghostController = root.AddComponent<SpriteAfterimageGhost>();
        ghostController.Initialize(lifeTime);
    }
}