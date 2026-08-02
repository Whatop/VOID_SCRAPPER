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

    [Header("Boss - Region Prefabs")]
    [Tooltip("기존 보스 프리팹이자 1해역 구획 관리자입니다.")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private GameObject region2BossPrefab;
    [SerializeField] private GameObject region3BossPrefab;
    [SerializeField] private GameObject finalBossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Vector2 bossSpawnOffset = new Vector2(0f, 3f);
    [Tooltip("기존 1해역 표시명 호환용입니다.")]
    [SerializeField] private string bossDisplayName = "구획 관리자";
    [SerializeField] private bool animateBossHealthBar = true;

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

    [Header("Objective Gate")]
    [Tooltip("고가치 목표 신호를 모으기 전에는 코어를 사용할 수 없습니다.")]
    [SerializeField] private bool requireObjectiveSignals = true;
    [SerializeField] private bool hideUntilCoreRevealed = true;
    [SerializeField] private bool failOpenWithoutActiveRun = true;
    [SerializeField] private string lockedInteractionText = "코어 추적 신호가 부족합니다";

    [Header("State")]
    [Tooltip("활성화 후 방전된 코어 외형을 남깁니다. 켜져 있으면 기존 Destroy/Hide 옵션보다 우선합니다.")]
    [SerializeField] private bool preserveSpentCoreVisualAfterActivation = true;
    [SerializeField] private bool destroyCoreAfterActivation;
    [SerializeField] private bool hideCoreInsteadOfDisable = true;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private CoreActivationPresentation coreActivationPresentation;

    [Header("Core Shard Reward Point")]
    [Tooltip("보스 처치 후 코어 조각이 생성될 위치입니다. 비워두면 코어 루트 위치를 사용합니다.")]
    [SerializeField] private Transform coreShardRewardPoint;

    private readonly Collider2D[] enemyBuffer = new Collider2D[128];

    private Coroutine activationRoutine;
    private bool activated;
    private bool activating;
    private GameObject spawnedBoss;

    private ExpeditionObjectiveDirector objectiveDirector;
    private Collider2D[] objectiveGateColliders;
    private Renderer[] objectiveGateRenderers;
    private bool[] objectiveGateColliderStates;
    private bool[] objectiveGateRendererStates;

    public string InteractionText
    {
        get
        {
            if (!IsObjectiveGateSatisfied())
            {
                return lockedInteractionText;
            }

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
        coreActivationPresentation = GetComponent<CoreActivationPresentation>();
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

        if (coreActivationPresentation == null)
        {
            coreActivationPresentation = GetComponent<CoreActivationPresentation>();
        }

        CaptureObjectiveGateState();
    }

    private void OnEnable()
    {
        BindObjectiveDirector();
        ApplyObjectiveGateState();
    }

    private void Start()
    {
        BindObjectiveDirector();
        ApplyObjectiveGateState();
    }

    private void OnDisable()
    {
        UnbindObjectiveDirector();

        if (activating)
        {
            RaiseActivationProgress(0f, false);
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null &&
               IsObjectiveGateSatisfied() &&
               !activated &&
               !activating;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        AudioManager.PlayAt(SoundEventIds.CoreInteractLoop, transform.position);
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
        AudioManager.PlayAt(SoundEventIds.CoreActivate, transform.position);

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        GameObject resolvedBossPrefab = ResolveBossPrefab();

        if (resolvedBossPrefab == null)
        {
            ExpeditionDepth currentDepth = ResolveCurrentDepth();

            if (currentDepth == ExpeditionDepth.FinalNetwork)
            {
                activated = false;

                if (radarTarget != null)
                {
                    radarTarget.SetVisible(true);
                }

                ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
                if (hud != null)
                {
                    hud.ShowWarning("최종보스 프리팹이 연결되지 않았습니다.");
                }

                Debug.LogError("Final Boss Prefab이 없어 중앙 물류망 보스전을 시작할 수 없습니다.", this);
                yield break;
            }

            Debug.LogWarning(
                $"{CampaignProgressionCatalog.GetRegionShortName(currentDepth)} 보스 프리팹이 없어 귀환 비콘만 생성합니다.",
                this
            );
            SpawnReturnBeaconDirectly();
            HandleCoreAfterActivation();
            yield break;
        }

        // 코어 활성화가 완료된 즉시 보스 음악으로 전환한다.
        // 실제 BossBattle GameState 전환은 인트로 종료 시점에 유지한다.
        GameAudioLoopController.EnterBossIntroMusic();

        if (useBossIntroSequence)
        {
            EnsureIntroSequence();

            if (bossIntroSequence != null)
            {
                yield return bossIntroSequence.PlayIntroRoutine(
                    interactor,
                    resolvedBossPrefab,
                    ResolveBossSpawnPosition(),
                    transform.position,
                    HandleBossCreatedByIntro,
                    HandleBossReveal,
                    HandleBossBattleStart
                );
            }
            else
            {
                SpawnBossImmediate(interactor);
                HandleBossReveal();
                HandleBossBattleStart();
            }
        }
        else
        {
            SpawnBossImmediate(interactor);
            HandleBossReveal();
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

    private void HandleBossReveal()
    {
        ShowBossHealthBar();
    }

    private void HandleBossBattleStart()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(
                ResolveCurrentDepth() == ExpeditionDepth.FinalNetwork
                    ? GameState.FinalBossBattle
                    : GameState.BossBattle
            );
        }

        if (alertNearbyEnemiesOnBattleStart)
        {
            AlertNearbyEnemies();
        }
    }

    private void ShowBossHealthBar()
    {
        if (spawnedBoss == null || BossHealthBarUI.Instance == null)
        {
            return;
        }

        EnemyHealth bossHealth = spawnedBoss.GetComponent<EnemyHealth>();

        if (bossHealth == null)
        {
            return;
        }

        if (animateBossHealthBar)
        {
            BossHealthBarUI.Instance.ShowBossAnimated(bossHealth, ResolveBossDisplayName());
        }
        else
        {
            BossHealthBarUI.Instance.ShowBoss(bossHealth, ResolveBossDisplayName());
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
        GameObject resolvedBossPrefab = ResolveBossPrefab();

        if (resolvedBossPrefab == null)
        {
            Debug.LogWarning("현재 해역 보스 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        spawnedBoss = Instantiate(resolvedBossPrefab, ResolveBossSpawnPosition(), Quaternion.identity);
        ConfigureSpawnedBoss(spawnedBoss, interactor);

        if (spawnedBoss != null)
        {
            AudioManager.PlayAt(SoundEventIds.BossSpawn, spawnedBoss.transform.position);
        }
    }

    private void ConfigureSpawnedBoss(GameObject bossObject, GameObject interactor)
    {
        if (bossObject == null)
        {
            return;
        }

        // 체력바와 등장 사운드는 실제 보스 등장 연출이 끝난 시점에 표시한다.
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

        bossController.ConfigureCampaignDefinition(ResolveBossCampaignDefinition());
        bossController.ConfigureCoreShardRewardPoint(ResolveCoreShardRewardPosition());

        if (ResolveCurrentDepth() == ExpeditionDepth.FinalNetwork)
        {
            FinalBossSettlementSupportPhase supportPhase =
                bossObject.GetComponent<FinalBossSettlementSupportPhase>();

            if (supportPhase == null)
            {
                bossObject.AddComponent<FinalBossSettlementSupportPhase>();
            }
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

    private ExpeditionDepth ResolveCurrentDepth()
    {
        return RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.ExpeditionDepth
            : ExpeditionDepth.Normal;
    }

    private GameObject ResolveBossPrefab()
    {
        return ResolveCurrentDepth() switch
        {
            ExpeditionDepth.Normal => bossPrefab,
            ExpeditionDepth.DeepZone1 => region2BossPrefab != null ? region2BossPrefab : bossPrefab,
            ExpeditionDepth.DeepZone2 => region3BossPrefab != null ? region3BossPrefab : bossPrefab,
            ExpeditionDepth.FinalNetwork => finalBossPrefab,
            _ => bossPrefab
        };
    }

    private BossCampaignDefinition ResolveBossCampaignDefinition()
    {
        return CampaignProgressionCatalog.GetBossDefinition(ResolveCurrentDepth());
    }

    private string ResolveBossDisplayName()
    {
        BossCampaignDefinition definition = ResolveBossCampaignDefinition();

        if (definition != null)
        {
            return definition.DisplayName;
        }

        ExpeditionDepth depth = ResolveCurrentDepth();

        if (depth == ExpeditionDepth.Normal && !string.IsNullOrWhiteSpace(bossDisplayName))
        {
            return bossDisplayName;
        }

        return CampaignProgressionCatalog.GetBossDisplayName(
            CampaignProgressionCatalog.GetBossId(depth)
        );
    }

    private Vector3 ResolveBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.position;
        }

        return transform.position + (Vector3)bossSpawnOffset;
    }

    private Vector3 ResolveCoreShardRewardPosition()
    {
        return coreShardRewardPoint != null
            ? coreShardRewardPoint.position
            : transform.position;
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

    private void CaptureObjectiveGateState()
    {
        objectiveGateColliders = GetComponentsInChildren<Collider2D>(true);
        objectiveGateRenderers = GetComponentsInChildren<Renderer>(true);
        objectiveGateColliderStates = new bool[objectiveGateColliders.Length];
        objectiveGateRendererStates = new bool[objectiveGateRenderers.Length];

        for (int i = 0; i < objectiveGateColliders.Length; i++)
        {
            objectiveGateColliderStates[i] = objectiveGateColliders[i] != null && objectiveGateColliders[i].enabled;
        }

        for (int i = 0; i < objectiveGateRenderers.Length; i++)
        {
            objectiveGateRendererStates[i] = objectiveGateRenderers[i] != null && objectiveGateRenderers[i].enabled;
        }
    }

    private void BindObjectiveDirector()
    {
        if (!requireObjectiveSignals || !Application.isPlaying)
        {
            return;
        }

        ExpeditionObjectiveDirector resolved = ExpeditionObjectiveDirector.Instance;

        if (objectiveDirector == resolved)
        {
            return;
        }

        UnbindObjectiveDirector();
        objectiveDirector = resolved;

        if (objectiveDirector != null)
        {
            objectiveDirector.ProgressChanged += HandleObjectiveProgressChanged;
            objectiveDirector.CoreRevealedEvent += HandleCoreRevealed;
        }
    }

    private void UnbindObjectiveDirector()
    {
        if (objectiveDirector == null)
        {
            return;
        }

        objectiveDirector.ProgressChanged -= HandleObjectiveProgressChanged;
        objectiveDirector.CoreRevealedEvent -= HandleCoreRevealed;
        objectiveDirector = null;
    }

    private void HandleObjectiveProgressChanged(int current, int required)
    {
        ApplyObjectiveGateState();
    }

    private void HandleCoreRevealed()
    {
        ApplyObjectiveGateState();
    }

    private bool IsObjectiveGateSatisfied()
    {
        if (!requireObjectiveSignals)
        {
            return true;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return failOpenWithoutActiveRun;
        }

        if (objectiveDirector == null)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }

        return objectiveDirector != null && objectiveDirector.CoreRevealed;
    }

    private void ApplyObjectiveGateState()
    {
        if (activated)
        {
            return;
        }

        bool available = IsObjectiveGateSatisfied();

        if (objectiveGateColliders == null || objectiveGateRendererStates == null)
        {
            CaptureObjectiveGateState();
        }

        if (objectiveGateColliders != null)
        {
            for (int i = 0; i < objectiveGateColliders.Length; i++)
            {
                Collider2D target = objectiveGateColliders[i];

                if (target != null)
                {
                    bool original = objectiveGateColliderStates != null && i < objectiveGateColliderStates.Length
                        ? objectiveGateColliderStates[i]
                        : true;
                    target.enabled = available && original;
                }
            }
        }

        if (hideUntilCoreRevealed && objectiveGateRenderers != null)
        {
            for (int i = 0; i < objectiveGateRenderers.Length; i++)
            {
                Renderer target = objectiveGateRenderers[i];

                if (target != null)
                {
                    bool original = objectiveGateRendererStates != null && i < objectiveGateRendererStates.Length
                        ? objectiveGateRendererStates[i]
                        : true;
                    target.enabled = available && original;
                }
            }
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(available);
        }
    }

    private void HandleCoreAfterActivation()
    {
        if (preserveSpentCoreVisualAfterActivation ||
            (coreActivationPresentation != null && coreActivationPresentation.KeepsSpentVisual))
        {
            DisableCoreCollidersOnly();
            return;
        }

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

    private void DisableCoreCollidersOnly()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
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