using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
    [Header("Region 1 Repeat Encounter")]
    [SerializeField] private GameObject region1RepeatBossPrefab;
    [SerializeField] private BossCampaignDefinition region1RepeatBossDefinition;
    [SerializeField] private string region1RepeatSignalSubtitle = "약탈자 지휘 신호 감지";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private bool forceRegion1RepeatBoss;
#endif
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Vector2 bossSpawnOffset = new Vector2(0f, 3f);
    [Tooltip("기존 1해역 표시명 호환용입니다.")]
    [SerializeField] private string bossDisplayName = "구획 관리자";
    [SerializeField] private bool animateBossHealthBar = true;

    [Header("Boss Intro")]
    [SerializeField] private bool useBossIntroSequence = true;
    [SerializeField] private CoreBossIntroSequence bossIntroSequence;

    [Header("Boss Background Presentation")]
    [SerializeField] private bool useTemporaryBossBackgroundPresentation = true;
    [SerializeField] private SpaceBackgroundGenerator2D spaceBackgroundGenerator;

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

    [Header("Objective / Location Reveal")]
    [Tooltip("기존 프리팹 호환 필드입니다. 켜져 있으면 코어 추적 신호 시스템을 사용합니다.")]
    [SerializeField] private bool requireObjectiveSignals = true;
    [Tooltip("켜면 신호 2개는 코어 사용 해금이 아니라 지도/레이더 위치 공개 조건으로만 사용합니다.")]
    [SerializeField] private bool objectiveSignalsRevealLocationOnly = true;
    [Tooltip("코어 위치 공개 전 월드 외형을 숨깁니다. 직접 근접 발견 시 즉시 나타납니다.")]
    [SerializeField] private bool hideUntilCoreRevealed = true;
    [SerializeField] private bool failOpenWithoutActiveRun = true;
    [SerializeField] private string lockedInteractionText = "코어 추적 신호가 부족합니다";

    [Header("Direct Core Discovery")]
    [SerializeField] private bool allowDirectWorldDiscovery = true;
    [SerializeField, Min(0.1f)] private float directDiscoveryRadius = 5f;
    [SerializeField] private bool showDirectDiscoveryMessage = true;
    [SerializeField] private string directDiscoveryMessage = "구획 제어 코어를 발견했습니다.";

    [Header("State")]
    [Tooltip("활성화 후 방전된 코어 외형을 남깁니다. 켜져 있으면 기존 Destroy/Hide 옵션보다 우선합니다.")]
    [SerializeField] private bool preserveSpentCoreVisualAfterActivation;
    [SerializeField] private bool destroyCoreAfterActivation;
    [SerializeField] private bool hideCoreInsteadOfDisable = true;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private CoreActivationPresentation coreActivationPresentation;

    [Header("Completed Core Pickup")]
    [Tooltip("활성화 연출이 끝난 직후 생성할 기존 RewardPickup 프리팹입니다.")]
    [SerializeField] private GameObject completedCorePickupPrefab;
    [FormerlySerializedAs("coreShardRewardPoint")]
    [Tooltip("완성된 코어 픽업 생성 위치입니다. 비워두면 코어 루트 위치를 사용합니다.")]
    [SerializeField] private Transform completedCoreDropPoint;
    [SerializeField] private Vector2 completedCoreInitialVelocity = new Vector2(0f, 0.85f);

    private readonly Collider2D[] enemyBuffer = new Collider2D[128];

    private Coroutine activationRoutine;
    private bool activated;
    private bool activating;
    private bool completedCorePickupSpawned;
    private bool worldPresenceRetired;
    private bool temporaryBossPresentationRequested;
    private bool temporaryBossPresentationHandedOff;
    private GameObject spawnedBoss;
    private ResolvedBossEncounter resolvedBossEncounter;
    private bool hasResolvedBossEncounter;

    private ExpeditionObjectiveDirector objectiveDirector;
    private Transform directDiscoveryPlayer;
    private bool directLocationDiscovered;
    private Collider2D[] objectiveGateColliders;
    private Renderer[] objectiveGateRenderers;
    private bool[] objectiveGateColliderStates;
    private bool[] objectiveGateRendererStates;
    private bool trackingLockOverrideActive;
    private bool trackingLocked;

    private readonly struct ResolvedBossEncounter
    {
        public GameObject Prefab { get; }
        public BossCampaignDefinition CampaignDefinition { get; }
        public string DisplayName { get; }
        public string SignalSubtitle { get; }
        public bool IsRepeatReplacement { get; }

        public ResolvedBossEncounter(
            GameObject prefab,
            BossCampaignDefinition campaignDefinition,
            string displayName,
            string signalSubtitle,
            bool isRepeatReplacement)
        {
            Prefab = prefab;
            CampaignDefinition = campaignDefinition;
            DisplayName = displayName;
            SignalSubtitle = signalSubtitle;
            IsRepeatReplacement = isRepeatReplacement;
        }
    }

    public bool IsLocationRevealed => IsCoreLocationRevealed();
    public bool IsInteractionUnlocked => IsObjectiveGateSatisfied();
    public bool IsActivated => activated;
    public RadarTarget RadarTarget => radarTarget;

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
        hasResolvedBossEncounter = false;
        BindObjectiveDirector();
        ApplyObjectiveGateState();
    }

    private void Start()
    {
        BindObjectiveDirector();
        ApplyObjectiveGateState();
    }

    private void Update()
    {
        TryDirectWorldDiscovery();
    }

    private void OnDisable()
    {
        UnbindObjectiveDirector();

        if (activating)
        {
            RaiseActivationProgress(0f, false);
        }

        bossIntroSequence?.CancelCoreActivationCameraLock();

        if (temporaryBossPresentationRequested && !temporaryBossPresentationHandedOff)
        {
            AbortTemporaryBossBackgroundPresentation();
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

        if (useBossIntroSequence)
        {
            EnsureIntroSequence();
            bossIntroSequence?.BeginCoreActivationCameraLock();
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
        bossIntroSequence?.CancelCoreActivationCameraLock();
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

        if (useBossIntroSequence)
        {
            EnsureIntroSequence();
            bossIntroSequence?.BeginCoreActivationCameraLock();
        }

        AudioManager.PlayAt(SoundEventIds.CoreActivate, transform.position);

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        ResolvedBossEncounter encounter = ResolveBossEncounter();
        GameObject resolvedBossPrefab = encounter.Prefab;

        if (resolvedBossPrefab == null)
        {
            ExpeditionDepth currentDepth = ResolveCurrentDepth();

            if (currentDepth == ExpeditionDepth.FinalNetwork)
            {
                activated = false;

                if (radarTarget != null)
                {
                    radarTarget.SetVisible(IsCoreLocationRevealed());
                }

                ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
                if (hud != null)
                {
                    hud.ShowWarning("최종보스 프리팹이 연결되지 않았습니다.");
                }

                Debug.LogError("Final Boss Prefab이 없어 중앙 물류망 보스전을 시작할 수 없습니다.", this);
                bossIntroSequence?.CancelCoreActivationCameraLock();
                yield break;
            }

            Debug.LogWarning(
                $"{CampaignProgressionCatalog.GetRegionShortName(currentDepth)} 보스 프리팹이 없어 귀환 비콘만 생성합니다.",
                this
            );
            yield return PlayActivationPresentationWithoutIntro();
            DropCompletedCorePickup();
            bossIntroSequence?.CancelCoreActivationCameraLock();
            SpawnReturnBeaconDirectly();
            HandleCoreAfterActivation();
            yield break;
        }

        // 코어 활성화가 완료된 즉시 보스 음악으로 전환한다.
        // 실제 BossBattle GameState 전환은 인트로 종료 시점에 유지한다.
        BeginTemporaryBossBackgroundPresentation();
        GameAudioLoopController.EnterBossIntroMusic();

        if (useBossIntroSequence)
        {
            EnsureIntroSequence();

            if (bossIntroSequence != null)
            {
                bossIntroSequence.ConfigureEncounterSignalSubtitle(encounter.SignalSubtitle);
                bossIntroSequence.ConfigureEncounterIntroVariant(
                    encounter.IsRepeatReplacement
                );
                bossIntroSequence.ConfigureSalvageDevourerIntroVariant(
                    IsLiveSalvageDevourerEncounter(encounter)
                );
                yield return bossIntroSequence.PlayIntroRoutine(
                    interactor,
                    resolvedBossPrefab,
                    ResolveBossSpawnPosition(),
                    transform.position,
                    HandleCoreActivationPresentationCompleted,
                    HandleBossCreatedByIntro,
                    HandleBossReveal,
                    HandleBossBattleStart
                );
            }
            else
            {
                yield return PlayActivationPresentationWithoutIntro();
                HandleCoreActivationPresentationCompleted();
                SpawnBossImmediate(interactor);
                HandleBossReveal();
                HandleBossBattleStart();
            }
        }
        else
        {
            yield return PlayActivationPresentationWithoutIntro();
            HandleCoreActivationPresentationCompleted();
            SpawnBossImmediate(interactor);
            HandleBossReveal();
            HandleBossBattleStart();
        }

        if (spawnedBoss == null)
        {
            AbortTemporaryBossBackgroundPresentation();
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
        if (spawnedBoss != null)
        {
            FrigateTriadBossController frigateTriad =
                spawnedBoss.GetComponent<FrigateTriadBossController>();
            if (frigateTriad != null && !frigateTriad.BeginGameplay())
            {
                Debug.LogWarning(
                    "Salvage Devourer gameplay authority could not activate at the intro handoff.",
                    this
                );
                return;
            }

            PirateCommanderBossController raiderCommander =
                spawnedBoss.GetComponent<PirateCommanderBossController>();
            raiderCommander?.BeginCombat();
        }

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
        GameObject resolvedBossPrefab = ResolveBossEncounter().Prefab;

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

        if (bossObject.GetComponent<FrigateTriadBossController>() != null)
        {
            bossController.enabled = false;
        }

        ResolvedBossEncounter encounter = ResolveBossEncounter();
        bossController.ConfigureCampaignDefinition(encounter.CampaignDefinition);

        PirateCommanderBossController raiderCommander =
            bossObject.GetComponent<PirateCommanderBossController>();
        if (raiderCommander != null)
        {
            Vector3 arenaCenter = bossIntroSequence != null
                ? bossIntroSequence.ResolveEncounterArenaCenter(transform.position)
                : transform.position;
            Vector2 arenaHalfExtents = bossIntroSequence != null
                ? bossIntroSequence.ResolveRaiderEncounterArenaHalfExtents()
                : new Vector2(8.4f, 8.4f);
            raiderCommander.ConfigureEncounter(arenaCenter, arenaHalfExtents, interactor);
        }
        ResolveSpaceBackgroundGenerator();
        bossController.ConfigureEncounterBackgroundPresentation(
            temporaryBossPresentationRequested ? spaceBackgroundGenerator : null
        );
        temporaryBossPresentationHandedOff = temporaryBossPresentationRequested &&
                                               spaceBackgroundGenerator != null;

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

    private ResolvedBossEncounter ResolveBossEncounter()
    {
        if (hasResolvedBossEncounter)
        {
            return resolvedBossEncounter;
        }

        ExpeditionDepth depth = ResolveCurrentDepth();
        CampaignBossId bossId = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.CurrentBossId
            : CampaignProgressionCatalog.GetBossId(depth);
        BossCampaignDefinition definition = CampaignProgressionCatalog.GetBossDefinition(depth);
        GameObject prefab = depth switch
        {
            ExpeditionDepth.Normal => bossPrefab,
            ExpeditionDepth.DeepZone1 when bossId == CampaignBossId.SalvageDevourer =>
                region2BossPrefab != null ? region2BossPrefab : bossPrefab,
            ExpeditionDepth.DeepZone1 => bossPrefab,
            ExpeditionDepth.DeepZone2 => region3BossPrefab != null ? region3BossPrefab : bossPrefab,
            ExpeditionDepth.FinalNetwork => finalBossPrefab,
            _ => bossPrefab
        };

        bool useRepeatReplacement = depth == ExpeditionDepth.Normal && ShouldUseRegion1RepeatBoss();
        if (useRepeatReplacement &&
            region1RepeatBossPrefab != null &&
            region1RepeatBossDefinition != null)
        {
            prefab = region1RepeatBossPrefab;
            definition = region1RepeatBossDefinition;
        }
        else
        {
            useRepeatReplacement = false;
        }

        string displayName = definition != null
            ? definition.DisplayName
            : ResolveFallbackBossDisplayName(depth);
        string signalSubtitle = useRepeatReplacement
            ? region1RepeatSignalSubtitle
            : string.Empty;

        resolvedBossEncounter = new ResolvedBossEncounter(
            prefab,
            definition,
            displayName,
            signalSubtitle,
            useRepeatReplacement
        );
        hasResolvedBossEncounter = true;
        return resolvedBossEncounter;
    }

    private BossCampaignDefinition ResolveBossCampaignDefinition()
    {
        return ResolveBossEncounter().CampaignDefinition;
    }

    private string ResolveBossDisplayName()
    {
        return ResolveBossEncounter().DisplayName;
    }

    private string ResolveFallbackBossDisplayName(ExpeditionDepth depth)
    {

        if (depth == ExpeditionDepth.Normal && !string.IsNullOrWhiteSpace(bossDisplayName))
        {
            return bossDisplayName;
        }

        return CampaignProgressionCatalog.GetBossDisplayName(
            CampaignProgressionCatalog.GetBossId(depth)
        );
    }

    private bool ShouldUseRegion1RepeatBoss()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (forceRegion1RepeatBoss)
        {
            return true;
        }
#endif

        return PermanentProgress.Instance != null &&
               PermanentProgress.Instance.HasDefeatedCampaignBoss(
                   CampaignBossId.SectorAdministrator
               );
    }

    private bool IsLiveSalvageDevourerEncounter(ResolvedBossEncounter encounter)
    {
        if (ResolveCurrentDepth() != ExpeditionDepth.DeepZone1 ||
            encounter.Prefab == null ||
            encounter.CampaignDefinition == null ||
            encounter.CampaignDefinition.BossId != CampaignBossId.SalvageDevourer)
        {
            return false;
        }

        return encounter.Prefab.GetComponent<FrigateTriadBossController>() != null;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Force Live Region-2 Boss Intro")]
    private void DevelopmentForceLiveRegion2BossIntro()
    {
        if (activated || activating || RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun || RunManager.Instance.IsCompletingRun ||
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1 ||
            RunManager.Instance.CurrentRun.CurrentBossId != CampaignBossId.SalvageDevourer)
        {
            Debug.LogWarning(
                "The live Region-2 intro requires an active DeepZone1 / SalvageDevourer run.",
                this
            );
            return;
        }

        GameObject playerObject = FindPlayerObject();
        if (playerObject == null)
        {
            Debug.LogWarning("The live Region-2 intro requires the current Player object.", this);
            return;
        }

        activationRoutine = StartCoroutine(DevelopmentForceActivationRoutine(playerObject));
    }

    private IEnumerator DevelopmentForceActivationRoutine(GameObject playerObject)
    {
        activating = true;
        RaiseActivationProgress(1f, false);
        activating = false;
        activationRoutine = null;
        yield return CompleteActivationRoutine(playerObject);
    }
#endif

    private Vector3 ResolveBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.position;
        }

        return transform.position + (Vector3)bossSpawnOffset;
    }

    private IEnumerator PlayActivationPresentationWithoutIntro()
    {
        if (coreActivationPresentation == null)
        {
            coreActivationPresentation = GetComponent<CoreActivationPresentation>();
        }

        if (coreActivationPresentation != null)
        {
            yield return coreActivationPresentation.PlayActivationRoutine();
        }
    }

    private void BeginTemporaryBossBackgroundPresentation()
    {
        if (!useTemporaryBossBackgroundPresentation || temporaryBossPresentationRequested)
        {
            return;
        }

        ResolveSpaceBackgroundGenerator();

        if (spaceBackgroundGenerator == null)
        {
            return;
        }

        temporaryBossPresentationHandedOff = false;
        temporaryBossPresentationRequested =
            spaceBackgroundGenerator.BeginBossEncounterPresentation() != null;
    }

    private void HandleCoreActivationPresentationCompleted()
    {
        DropCompletedCorePickup();

        if (temporaryBossPresentationRequested && spaceBackgroundGenerator != null)
        {
            spaceBackgroundGenerator.ReleaseCoreActivationScreenOverlay();
        }
    }

    private void AbortTemporaryBossBackgroundPresentation()
    {
        if (!temporaryBossPresentationRequested)
        {
            return;
        }

        if (spaceBackgroundGenerator != null)
        {
            spaceBackgroundGenerator.RestoreExplorationPresentation(-1f, false);
        }

        temporaryBossPresentationRequested = false;
        temporaryBossPresentationHandedOff = false;
    }

    private void ResolveSpaceBackgroundGenerator()
    {
        if (spaceBackgroundGenerator == null)
        {
            spaceBackgroundGenerator = FindFirstObjectByType<SpaceBackgroundGenerator2D>(
                FindObjectsInactive.Include
            );
        }
    }

    private void DropCompletedCorePickup()
    {
        if (completedCorePickupSpawned)
        {
            return;
        }

        if (ShouldDeferSalvageDevourerCoreRewardToBossDeath())
        {
            completedCorePickupSpawned = true;
            RetireOriginalCoreWorldPresence();
            return;
        }

        BossCampaignDefinition definition = ResolveBossCampaignDefinition();
        int amount = CampaignBossRewardService.ResolveCoreShardReward(
            definition,
            ResolveCurrentDepth()
        );

        if (amount <= 0)
        {
            completedCorePickupSpawned = true;
            return;
        }

        if (completedCorePickupPrefab == null)
        {
            Debug.LogWarning("완성된 코어 RewardPickup 프리팹이 연결되지 않았습니다.", this);
            return;
        }

        Vector3 spawnPosition = completedCoreDropPoint != null
            ? completedCoreDropPoint.position
            : transform.position;
        GameObject pickupObject = Instantiate(
            completedCorePickupPrefab,
            spawnPosition,
            Quaternion.identity
        );
        RewardPickup pickup = pickupObject != null
            ? pickupObject.GetComponent<RewardPickup>()
            : null;

        if (pickup == null)
        {
            if (pickupObject != null)
            {
                Destroy(pickupObject);
            }

            Debug.LogWarning("완성된 코어 프리팹에 RewardPickup이 없습니다.", this);
            return;
        }

        pickup.InitializeCurrency(
            CurrencyType.CoreShards,
            amount,
            completedCoreInitialVelocity
        );
        completedCorePickupSpawned = true;
        RetireOriginalCoreWorldPresence();
    }

    private bool ShouldDeferSalvageDevourerCoreRewardToBossDeath()
    {
        if (RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            RunManager.Instance.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1 ||
            RunManager.Instance.CurrentRun.CurrentBossId != CampaignBossId.SalvageDevourer)
        {
            return false;
        }

        return IsLiveSalvageDevourerEncounter(ResolveBossEncounter());
    }

    private void RetireOriginalCoreWorldPresence()
    {
        if (worldPresenceRetired)
        {
            return;
        }

        worldPresenceRetired = true;
        HideCoreVisualsAndColliders();

        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
            radarTarget.enabled = false;
        }
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
        MarkCoreLocationDiscovered(false);
        ApplyObjectiveGateState();
    }

    private bool IsObjectiveGateSatisfied()
    {
        if (trackingLockOverrideActive)
        {
            return !trackingLocked;
        }

        if (!requireObjectiveSignals || objectiveSignalsRevealLocationOnly)
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

    private bool IsCoreLocationRevealed()
    {
        if (trackingLockOverrideActive)
        {
            return !trackingLocked;
        }

        if (!requireObjectiveSignals)
        {
            return true;
        }

        if (directLocationDiscovered)
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

    private void TryDirectWorldDiscovery()
    {
        if (trackingLockOverrideActive || !Application.isPlaying || activated || directLocationDiscovered || !allowDirectWorldDiscovery)
        {
            return;
        }

        if (directDiscoveryPlayer == null)
        {
            PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>();
            directDiscoveryPlayer = playerController != null ? playerController.transform : null;
        }

        if (directDiscoveryPlayer == null)
        {
            return;
        }

        float radius = Mathf.Max(0.1f, directDiscoveryRadius);
        if (((Vector2)directDiscoveryPlayer.position - (Vector2)transform.position).sqrMagnitude <= radius * radius)
        {
            MarkCoreLocationDiscovered(true);
        }
    }

    public void MarkCoreLocationDiscovered(bool notifyPlayer = true)
    {
        if (trackingLockOverrideActive && trackingLocked)
        {
            return;
        }

        bool wasRevealed = IsCoreLocationRevealed();
        directLocationDiscovered = true;

        if (radarTarget != null)
        {
            radarTarget.SetMapDiscovered(true);
            MapDiscoveryController.Instance?.DiscoverTarget(radarTarget, true);
        }

        ApplyObjectiveGateState();

        if (!wasRevealed && notifyPlayer && showDirectDiscoveryMessage)
        {
            ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
            hud?.ShowWarning(directDiscoveryMessage);
            AudioManager.Play(SoundEventIds.UiUnlock);
        }
    }

    public void SetTrackingLocked(bool locked)
    {
        trackingLockOverrideActive = true;
        trackingLocked = locked;

        if (locked)
        {
            directLocationDiscovered = false;
        }

        ApplyObjectiveGateState();
    }

    private void ApplyObjectiveGateState()
    {
        if (activated)
        {
            return;
        }

        bool interactionAvailable = IsObjectiveGateSatisfied();
        bool locationRevealed = IsCoreLocationRevealed();

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
                    target.enabled = interactionAvailable && original;
                }
            }
        }

        if (objectiveGateRenderers != null)
        {
            for (int i = 0; i < objectiveGateRenderers.Length; i++)
            {
                Renderer target = objectiveGateRenderers[i];

                if (target != null)
                {
                    bool original = objectiveGateRendererStates != null && i < objectiveGateRendererStates.Length
                        ? objectiveGateRendererStates[i]
                        : true;
                    target.enabled = (!hideUntilCoreRevealed || locationRevealed) && original;
                }
            }
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(locationRevealed);

            if (locationRevealed)
            {
                radarTarget.SetMapDiscovered(true);
                MapDiscoveryController.Instance?.DiscoverTarget(radarTarget, false);
            }
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
