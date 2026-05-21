using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CoreObject : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionText = "코어 활성화";
    [SerializeField] private float activationTime = 2f;
    [SerializeField] private bool requirePlayerStayInRange = true;
    [SerializeField] private float interactionStayRadius = 3f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Vector2 bossSpawnOffset = new Vector2(0f, 3f);

    [Header("Return Beacon")]
    [SerializeField] private GameObject returnBeaconPrefab;
    [SerializeField] private Transform returnBeaconSpawnPoint;
    [SerializeField] private Vector2 returnBeaconSpawnOffset = new Vector2(0f, -2f);

    [Header("Core Alert")]
    [SerializeField] private bool alertNearbyEnemiesOnActivate = true;
    [SerializeField] private float alertRadius = 18f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("State")]
    [SerializeField] private bool destroyCoreAfterActivation;
    [SerializeField] private RadarTarget radarTarget;

    private readonly Collider2D[] enemyBuffer = new Collider2D[128];

    private Coroutine activationRoutine;
    private bool activated;
    private bool activating;
    private GameObject spawnedBoss;

    public string InteractionText
    {
        get
        {
            if (activated)
            {
                return "이미 활성화된 코어";
            }

            if (activating)
            {
                return "코어 활성화 중";
            }

            return interactionText;
        }
    }

    private void Reset()
    {
        radarTarget = GetComponent<RadarTarget>();
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (activated || activating)
        {
            return false;
        }

        if (interactor == null)
        {
            return false;
        }

        return true;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        activationRoutine = StartCoroutine(ActivateRoutine(interactor));
    }

    private IEnumerator ActivateRoutine(GameObject interactor)
    {
        activating = true;

        float timer = 0f;

        while (timer < activationTime)
        {
            if (interactor == null)
            {
                activating = false;
                activationRoutine = null;
                yield break;
            }

            if (requirePlayerStayInRange)
            {
                float distance = Vector2.Distance(transform.position, interactor.transform.position);

                if (distance > interactionStayRadius)
                {
                    activating = false;
                    activationRoutine = null;
                    yield break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        activating = false;
        activationRoutine = null;

        CompleteActivation(interactor);
    }

    private void CompleteActivation(GameObject interactor)
    {
        if (activated)
        {
            return;
        }

        activated = true;

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.BossBattle);
        }

        if (alertNearbyEnemiesOnActivate)
        {
            AlertNearbyEnemies();
        }

        SpawnBoss(interactor);

        if (destroyCoreAfterActivation)
        {
            gameObject.SetActive(false);
        }
    }

    private void AlertNearbyEnemies()
    {
        if (enemyLayer.value == 0)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            alertRadius,
            enemyBuffer,
            enemyLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null)
            {
                continue;
            }

            EnemyBaseAI enemyAI = hit.GetComponentInParent<EnemyBaseAI>();

            if (enemyAI != null)
            {
                enemyAI.AlertTo(transform.position);
            }
        }
    }

    private void SpawnBoss(GameObject interactor)
    {
        Vector3 spawnPosition = ResolveBossSpawnPosition();

        if (bossPrefab == null)
        {
            Debug.LogWarning("bossPrefab이 없어 보스 대신 귀환 비콘을 바로 생성합니다.", this);
            SpawnReturnBeaconDirectly();
            return;
        }

        spawnedBoss = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);

        EnemyBaseAI bossAI = spawnedBoss.GetComponent<EnemyBaseAI>();

        if (bossAI != null && interactor != null)
        {
            bossAI.SetTarget(interactor.transform);
        }

        BossDummyController bossController = spawnedBoss.GetComponent<BossDummyController>();

        if (bossController == null)
        {
            bossController = spawnedBoss.AddComponent<BossDummyController>();
        }

        bossController.ConfigureReturnBeacon(
            returnBeaconPrefab,
            ResolveReturnBeaconSpawnPosition()
        );
    }

    private void SpawnReturnBeaconDirectly()
    {
        if (returnBeaconPrefab == null)
        {
            Debug.LogWarning("returnBeaconPrefab이 없습니다.", this);
            return;
        }

        Instantiate(returnBeaconPrefab, ResolveReturnBeaconSpawnPosition(), Quaternion.identity);
    }

    private Vector3 ResolveBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.position;
        }

        return transform.position + (Vector3)bossSpawnOffset;
    }

    private Vector3 ResolveReturnBeaconSpawnPosition()
    {
        if (returnBeaconSpawnPoint != null)
        {
            return returnBeaconSpawnPoint.position;
        }

        return transform.position + (Vector3)returnBeaconSpawnOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionStayRadius);
    }
}