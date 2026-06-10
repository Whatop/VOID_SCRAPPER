using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CoreObject : MonoBehaviour, IInteractable
{
    public static event Action<CoreObject, float, bool> ActivationProgressChanged;

    [Header("Interaction")]
    [SerializeField] private string interactionText = "코어 활성화";
    [SerializeField] private float activationTime = 2f;
    [SerializeField] private bool requirePlayerStayInRange = true;
    [SerializeField] private float interactionStayRadius = 3f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Vector2 bossSpawnOffset = new Vector2(0f, 3f);

    [Header("Boss Intro")]
    [SerializeField] private bool useBossIntroSequence = true;
    [SerializeField] private CoreBossIntroSequence bossIntroSequence;

    [Header("Return Beacon")]
    [SerializeField] private GameObject returnBeaconPrefab;
    [SerializeField] private Transform returnBeaconSpawnPoint;
    [SerializeField] private Vector2 returnBeaconSpawnOffset = new Vector2(0f, -2f);

    [Header("Wormhole Portal Optional")]
    [SerializeField] private GameObject wormholePortalPrefab;
    [SerializeField] private Transform wormholePortalSpawnPoint;
    [SerializeField] private Vector2 wormholePortalSpawnOffset = Vector2.zero;

    [Header("Core Alert")]
    [SerializeField] private bool alertNearbyEnemiesOnBattleStart = true;
    [SerializeField] private float alertRadius = 18f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("State")]
    [SerializeField] private bool destroyCoreAfterActivation;
    [SerializeField] private bool hideCoreInsteadOfDisable = true;
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
        bossIntroSequence = GetComponent<CoreBossIntroSequence>();
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (bossIntroSequence == null)
        {
            bossIntroSequence = GetComponent<CoreBossIntroSequence>();
        }
    }

    private void OnDisable()
    {
        if (activating)
        {
            RaiseActivationProgress(0f, false);
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null && !activated && !activating;
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

        RaiseActivationProgress(0f, true);

        while (timer < activationTime)
        {
            if (interactor == null)
            {
                CancelActivationProgress();
                yield break;
            }

            if (requirePlayerStayInRange)
            {
                float distance = Vector2.Distance(transform.position, interactor.transform.position);

                if (distance > interactionStayRadius)
                {
                    CancelActivationProgress();
                    yield break;
                }
            }

            timer += Time.deltaTime;
            RaiseActivationProgress(Mathf.Clamp01(timer / Mathf.Max(0.01f, activationTime)), true);

            yield return null;
        }

        activating = false;
        activationRoutine = null;

        RaiseActivationProgress(1f, false);

        yield return CompleteActivationRoutine(interactor);
    }

    private void CancelActivationProgress()
    {
        activating = false;
        activationRoutine = null;
        RaiseActivationProgress(0f, false);
    }

    private void RaiseActivationProgress(float ratio, bool visible)
    {
        ActivationProgressChanged?.Invoke(this, Mathf.Clamp01(ratio), visible);
    }

    private IEnumerator CompleteActivationRoutine(GameObject interactor)
    {
        if (activated)
        {
            yield break;
        }

        activated = true;

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        if (bossPrefab == null)
        {
            Debug.LogWarning("bossPrefab이 없어 보스 대신 귀환 비콘을 바로 생성합니다.", this);
            SpawnReturnBeaconDirectly();
            HandleCoreAfterActivation();
            yield break;
        }

        if (useBossIntroSequence)
        {
            EnsureIntroSequence();

            if (bossIntroSequence != null)
            {
                yield return bossIntroSequence.PlayIntroRoutine(
                    interactor,
                    bossPrefab,
                    ResolveBossSpawnPosition(),
                    transform.position,
                    HandleBossCreatedByIntro,
                    HandleBossBattleStart
                );
            }
            else
            {
                SpawnBossImmediate(interactor);
                HandleBossBattleStart();
            }
        }
        else
        {
            SpawnBossImmediate(interactor);
            HandleBossBattleStart();
        }

        HandleCoreAfterActivation();
    }

    private void EnsureIntroSequence()
    {
        if (bossIntroSequence != null)
        {
            return;
        }

        bossIntroSequence = GetComponent<CoreBossIntroSequence>();

        if (bossIntroSequence == null)
        {
            bossIntroSequence = gameObject.AddComponent<CoreBossIntroSequence>();
        }
    }

    private void HandleBossCreatedByIntro(GameObject bossObject)
    {
        spawnedBoss = bossObject;
        ConfigureSpawnedBoss(spawnedBoss, FindPlayerObject());
    }

    private void HandleBossBattleStart()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.BossBattle);
        }

        if (alertNearbyEnemiesOnBattleStart)
        {
            AlertNearbyEnemies();
        }
    }

    private GameObject FindPlayerObject()
    {
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            return playerHealth.gameObject;
        }

        return GameObject.FindGameObjectWithTag("Player");
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

    private void SpawnBossImmediate(GameObject interactor)
    {
        spawnedBoss = Instantiate(bossPrefab, ResolveBossSpawnPosition(), Quaternion.identity);
        ConfigureSpawnedBoss(spawnedBoss, interactor);
    }

    private void ConfigureSpawnedBoss(GameObject bossObject, GameObject interactor)
    {
        if (bossObject == null)
        {
            return;
        }

        EnemyHealth bossHealth = bossObject.GetComponent<EnemyHealth>();

        if (bossHealth != null && BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.ShowBoss(bossHealth, "구획 관리자");
        }

        EnemyBaseAI bossAI = bossObject.GetComponent<EnemyBaseAI>();

        if (bossAI != null && interactor != null)
        {
            bossAI.SetTarget(interactor.transform);
        }

        BossDummyController bossController = bossObject.GetComponent<BossDummyController>();

        if (bossController == null)
        {
            bossController = bossObject.AddComponent<BossDummyController>();
        }

        if (wormholePortalPrefab != null)
        {
            bossController.ConfigureExitObjects(
                returnBeaconPrefab,
                ResolveReturnBeaconSpawnPosition(),
                wormholePortalPrefab,
                ResolveWormholePortalSpawnPosition()
            );
        }
        else
        {
            bossController.ConfigureReturnBeacon(
                returnBeaconPrefab,
                ResolveReturnBeaconSpawnPosition()
            );
        }
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

    private Vector3 ResolveWormholePortalSpawnPosition()
    {
        if (wormholePortalSpawnPoint != null)
        {
            return wormholePortalSpawnPoint.position;
        }

        return transform.position + (Vector3)wormholePortalSpawnOffset;
    }

    private void HandleCoreAfterActivation()
    {
        if (!destroyCoreAfterActivation)
        {
            return;
        }

        if (hideCoreInsteadOfDisable)
        {
            HideCoreVisualsAndColliders();
            return;
        }

        gameObject.SetActive(false);
    }

    private void HideCoreVisualsAndColliders()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionStayRadius);
    }
}