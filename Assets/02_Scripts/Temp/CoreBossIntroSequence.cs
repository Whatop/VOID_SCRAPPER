using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CoreBossIntroSequence : MonoBehaviour
{
    private enum IntroPhase
    {
        Inactive,
        CoreFocus,
        ArenaWide,
        FormationArrival,
        BossReveal,
        PlayerHandoff,
        Complete
    }

    [Header("Warning")]
    [SerializeField] private WarningMessageUI warningMessageUI;
    [SerializeField] private string activationWarningMessage = "고 에너지 방출 감지!";
    [SerializeField] private float warningDuration = 1.4f;
    [SerializeField] private float delayAfterWarning = 0.25f;

    [Header("Motion Title - Core Activation")]
    [SerializeField] private bool useMotionTitleForCoreActivation = true;
    [SerializeField] private EventTitleDirector coreActivationTitleDirector;
    [SerializeField] private EventTitleType coreActivationTitleType = EventTitleType.CoreReaction;
    [SerializeField] private string coreActivationTitle = "코어 활성화";
    [SerializeField] private string coreActivationSubtitle = "구획 관리자 신호 감지";
    [SerializeField] private float coreActivationTitleWait = 1.2f;
    [SerializeField] private bool fallbackToWarningMessageIfTitleMissing = true;

    [Header("Player Lock")]
    [SerializeField] private bool lockPlayerInput = true;
    [SerializeField] private bool makePlayerInvincibleDuringIntro = true;
    [SerializeField] private float playerInvincibleExtraTime = 0.5f;
    [SerializeField] private MonoBehaviour[] extraPlayerComponentsToDisable;

    [Header("HUD")]
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private bool hideStatusAndResourceUIDuringIntro = true;

    [Header("Core Focus / Activation")]
    [SerializeField] private CoreActivationPresentation coreActivationPresentation;
    [SerializeField] private GungeonStyleCamera2D gungeonCamera;
    [SerializeField] private bool focusCameraOnCore = true;
    [Range(0.35f, 1.25f)]
    [SerializeField] private float coreFocusZoomMultiplier = 0.68f;
    [Min(0.05f)]
    [SerializeField] private float coreFocusZoomDuration = 0.55f;
    [Min(0f)]
    [SerializeField] private float coreFocusSettleDuration = 0.12f;
    [SerializeField] private AnimationCurve coreFocusZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Min(0f)]
    [SerializeField] private float delayAfterCorePulse = 0.12f;

    [Header("Camera")]
    [SerializeField] private CameraZoomController2D cameraZoomController;
    [SerializeField] private float wideZoomMultiplier = 3.5f;

    [Tooltip("일반 시야에서 보스 전체 시야로 부드럽게 넓어지는 시간입니다.")]
    [Min(0.05f)]
    [SerializeField] private float zoomOutDuration = 1.35f;

    [Tooltip("보스 시야에서 플레이어 중심과 기본 줌으로 함께 복귀하는 시간입니다.")]
    [Min(0.05f)]
    [SerializeField] private float zoomInDuration = 1.05f;

    [SerializeField] private AnimationCurve zoomOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve zoomInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool resetCameraZoomOnBattleStart = true;
    [SerializeField] private bool returnCameraZoomAfterIntro = true;
    [SerializeField] private bool createCameraZoomControllerIfMissing = true;
    [SerializeField, Min(0.001f)] private float gameplayFramingPositionTolerance = 0.02f;
    [SerializeField, Min(0.0001f)] private float gameplayFramingZoomTolerance = 0.008f;
    [SerializeField, Min(0.05f)] private float gameplayFramingSettleTimeout = 0.4f;

    [Header("Background Coverage")]
    [SerializeField] private SpaceBackgroundGenerator2D spaceBackgroundGenerator;
    [SerializeField] private bool prepareBackgroundBeforeWideZoom = true;
    [SerializeField] private bool syncStarfieldScaleWithCameraZoom = true;

    [Header("Arena")]
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(14f, 14f);

    [Range(0.2f, 1f)]
    [SerializeField] private float verticalSpaceScale = 0.7f;

    [Tooltip("코어보다 위쪽을 보스전 중심으로 쓰기 위한 오프셋")]
    [SerializeField] private Vector2 arenaCenterOffset = new Vector2(0f, 2.5f);

    [Tooltip("Boss Spawn Point가 코어와 겹칠 때 추가로 위로 올리는 오프셋")]
    [SerializeField] private Vector2 bossBattlePositionOffset = new Vector2(0f, 2.5f);

    [Header("Laser Manager Ships")]
    [Tooltip("켜면 보스의 BossPatternController가 1페이즈 4대를 소유합니다. 2페이즈에는 기존 4대를 육각형 어깨 위치로 이동시키고 상/하 2대만 추가합니다.")]
    [SerializeField] private bool useBossPatternGuardianSystem = true;
    [SerializeField] private GameObject laserManagerShipPrefab;
    [SerializeField] private bool createRuntimePlaceholderIfMissing = true;
    [SerializeField] private bool keepManagerShipsUntilBossDeath = true;
    [SerializeField] private float managerShipBaseRotationZ = 45f;
    [SerializeField] private float managerShipStartExtraDistance = 7f;
    [SerializeField] private float managerShipMoveDuration = 0.9f;
    [SerializeField] private AnimationCurve managerShipMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Raider Barricade Carriers")]
    [SerializeField] private RaiderBarricadeCarrier raiderBarricadeCarrierPrefab;
    [Tooltip("Raider-only arena scale relative to the authored shared Boss arena.")]
    [SerializeField, Range(0.5f, 1f)] private float raiderArenaSizeMultiplier = 0.7f;
    [SerializeField] private Material raiderBarrierMaterial;
    [SerializeField, Min(0f)] private float raiderCarrierStartExtraDistance = 7f;
    [SerializeField, Min(0.05f)] private float raiderCarrierMoveDuration = 0.9f;
    [SerializeField] private AnimationCurve raiderCarrierMoveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Color raiderBarrierColor = new Color(0.92f, 0.12f, 0.035f, 0.58f);
    [SerializeField] private Color raiderBarrierFieldColor = new Color(0.42f, 0.025f, 0.012f, 0.07f);
    [SerializeField] private Color raiderBarrierNoiseColor = new Color(1f, 0.28f, 0.06f, 0.5f);
    [SerializeField, Min(0.01f)] private float raiderBarrierThickness = 0.32f;
    [SerializeField, Min(0.01f)] private float raiderBarrierCoreWidth = 0.04f;
    [SerializeField, Min(0.01f)] private float raiderBarrierFieldWidth = 0.28f;
    [SerializeField, Min(0.01f)] private float raiderBarrierNoiseWidth = 0.22f;
    [SerializeField, Min(0.1f)] private float raiderBarrierPulseDuration = 2.1f;

    [Header("Raider Arena Covers")]
    [SerializeField] private Vector2 raiderLeftCoverNormalizedOffset = new Vector2(-0.42f, -0.10f);
    [SerializeField] private Vector2 raiderRightCoverNormalizedOffset = new Vector2(0.42f, -0.10f);
    [SerializeField, Min(0f)] private float raiderCoverWallClearance = 1f;
    [SerializeField, Min(0f)] private float raiderCoverMinimumPassage = 1.5f;

    [Header("Runtime Placeholder")]
    [SerializeField] private Color runtimePlaceholderColor = new Color(0.15f, 0.8f, 1f, 1f);
    [SerializeField] private float runtimePlaceholderSize = 0.45f;
    [SerializeField] private float runtimePlaceholderLineWidth = 0.05f;

    [Header("Laser Wall")]
    [SerializeField] private BossArenaLaserWall laserWallPrefab;
    [SerializeField] private float wallThickness = 0.45f;
    [SerializeField] private bool createSolidLaserWalls = true;
    [SerializeField] private bool cleanupArenaObjectsOnBossDeath = true;
    [SerializeField] private float wallDamage = 4f;
    [SerializeField] private float wallDamageInterval = 0.5f;
    [SerializeField] private Material laserLineMaterial;
    [SerializeField] private Color laserLineColor = Color.white;
    [SerializeField] private string laserWallLayerName = "Default";
    [SerializeField] private string laserSortingLayerName = "Default";
    [SerializeField] private int laserSortingOrder = 30;
    [SerializeField] private float delayBeforeWallActivation = 0.2f;
    [SerializeField] private float delayAfterWallActivation = 0.55f;

    [Header("Boss Arrival")]
    [SerializeField] private Vector2 bossArrivalDirection = Vector2.up;
    [SerializeField] private float bossArrivalDistance = 11f;
    [SerializeField] private float bossArrivalDuration = 1.15f;
    [Tooltip("보스 공개 시 아레나 중심에서 고정된 도착 지점 쪽으로 카메라 초점을 치우치는 비율입니다.")]
    [Range(0f, 0.65f)]
    [SerializeField] private float bossArrivalFocusBias = 0.35f;
    [Tooltip("넓은 아레나 시야를 유지한 채 보스 쪽으로 초점을 이동하는 시간입니다.")]
    [Min(0.05f)]
    [SerializeField] private float bossRevealFocusPanDuration = 0.75f;
    [SerializeField] private AnimationCurve bossRevealFocusPanCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve bossMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool faceBossToPlayerWhenArrived = true;
    [SerializeField] private float bossRotationOffset = -90f;

    [Header("Boss Reveal")]
    [Min(0f)]
    [SerializeField] private float bossHealthBarLeadTime = 0.55f;
    [SerializeField] private bool useBossAnimatorTriggers = true;
    [SerializeField] private string bossIntroEnterTrigger = "BossIntroEnter";
    [SerializeField] private string bossIntroRevealTrigger = "BossIntroReveal";
    [SerializeField] private string bossIntroReadyTrigger = "BossIntroReady";
    [SerializeField] private bool useFallbackBossRevealScale = true;
    [Range(0.5f, 1f)]
    [SerializeField] private float bossRevealStartScale = 0.82f;
    [Range(1f, 1.35f)]
    [SerializeField] private float bossRevealOvershootScale = 1.08f;
    [Min(0.05f)]
    [SerializeField] private float bossRevealScaleDuration = 0.36f;
    [SerializeField] private float bossRevealShakeAmplitude;
    [SerializeField] private float bossRevealShakeDuration = 0.22f;

    [Header("Debug")]
    [SerializeField] private bool logSequence;

    private readonly List<LaserGuardianDrone> spawnedManagerShips = new List<LaserGuardianDrone>(4);
    private readonly List<RaiderBarricadeCarrier> spawnedRaiderCarriers =
        new List<RaiderBarricadeCarrier>(4);
    private readonly List<GameObject> spawnedWallObjects = new List<GameObject>(4);
    private readonly List<MonoBehaviour> disabledBossComponents = new List<MonoBehaviour>();
    private readonly List<ComponentEnabledState> extraDisabledPlayerComponents = new List<ComponentEnabledState>();
    private readonly BossArenaCover[] raiderEncounterCovers = new BossArenaCover[2];
    private readonly Collider2D[] raiderCoverOverlapResults = new Collider2D[16];

    private PlayerLockState playerLockState;
    private EnemyHealth trackedBossHealth;
    private GameObject spawnedBoss;
    private bool cameraInputOffsetLockHeld;
    private Animator spawnedBossAnimator;
    private Transform spawnedBossVisualRoot;
    private Vector3 spawnedBossBaseScale = Vector3.one;
    private bool isPlaying;
    private bool introWideZoomHoldActive;
    private bool introCancellationRequested;
    private RunManager observedRunManager;
    private bool cinematicHudModeHeld;
    private bool cinematicHudRestoreInProgress;
    private bool coreActivationTitlePresented;
    private string encounterSignalSubtitleOverride;
    private bool useRaiderIntroVariant;
    private bool useSalvageDevourerIntroVariant;
    private bool salvageBossTitlePresented;
    private SalvageDevourerCorridorController activeSalvageCorridor;
    private FrigateTriadBossController activeSalvageTriad;
    private IntroPhase currentPhase;

    public GameObject SpawnedBoss => spawnedBoss;
    public bool IsPlaying => isPlaying;

    private struct ComponentEnabledState
    {
        public MonoBehaviour component;
        public bool wasEnabled;
    }

    private struct PlayerLockState
    {
        public bool hasState;
        public Rigidbody2D rb;

        public PlayerController2D controller;
        public bool controllerControlWasEnabled;
        public bool controllerMovementWasLocked;

        public PlayerDash dash;
        public bool dashWasEnabled;

        public PlayerWeaponController weaponController;
        public bool weaponWasEnabled;
        public bool weaponExternalInputWasLocked;

        public PlayerInteractor interactor;
        public bool interactorWasEnabled;

        public MonoBehaviour radarScanner;
        public bool radarWasEnabled;

        public EmergencyReturnController emergencyReturnController;
        public bool emergencyReturnWasEnabled;
    }

    private void Reset()
    {
        cameraZoomController = FindFirstObjectByType<CameraZoomController2D>();
        gungeonCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        warningMessageUI = FindFirstObjectByType<WarningMessageUI>();
        expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        coreActivationPresentation = GetComponent<CoreActivationPresentation>();
        coreActivationTitleDirector = EventTitleDirector.Instance;
    }

    private void OnDisable()
    {
        introCancellationRequested = true;
        if (IsSalvageDevourerIntroRuntimeOwned())
        {
            CleanupSalvageDevourerIntro(true);
        }
        UnbindRunEnd();
        SetIntroPhase(IntroPhase.Inactive);
        StopAllCoroutines();
        CloseCoreActivationPresentation();

        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        ReleaseCinematicHudMode();

        if (gungeonCamera != null)
        {
            gungeonCamera.CancelCinematicFocusBlend();
            gungeonCamera.ClearCinematicFocus(true);
        }

        ReleaseCameraInputOffsetLock();

        ReleaseIntroWideZoomHold(false);
        ReleaseRaiderBattleCameraProfile(true);
        DestroySpawnedRaiderCarriers();
        DestroySpawnedWalls();
        ClearRaiderCoverReferences();

        if (returnCameraZoomAfterIntro || resetCameraZoomOnBattleStart)
        {
            ResetCameraZoom();
        }

        if (isPlaying)
        {
            if (spawnedBossVisualRoot != null)
            {
                spawnedBossVisualRoot.localScale = spawnedBossBaseScale;
            }

            RestorePlayer();
            EnableBossForBattle();
            isPlaying = false;
        }
    }

    private void OnDestroy()
    {
        if (IsSalvageDevourerIntroRuntimeOwned())
        {
            CleanupSalvageDevourerIntro(true);
        }
    }

    public IEnumerator PlayIntroRoutine(
        GameObject interactor,
        GameObject bossPrefab,
        Vector3 bossBattlePosition,
        Vector3 arenaCenter,
        Action coreActivationCompletedCallback,
        Action<GameObject> bossCreatedCallback,
        Action bossRevealCallback,
        Action battleStartCallback)
    {
        if (isPlaying)
        {
            yield break;
        }

        if (useSalvageDevourerIntroVariant)
        {
            yield return PlaySalvageDevourerIntroRoutine(
                interactor,
                bossPrefab,
                coreActivationCompletedCallback,
                bossCreatedCallback,
                bossRevealCallback,
                battleStartCallback
            );
            yield break;
        }

        isPlaying = true;
        introCancellationRequested = false;
        SetIntroPhase(IntroPhase.CoreFocus);
        spawnedBoss = null;
        spawnedBossAnimator = null;
        spawnedBossVisualRoot = null;
        spawnedBossBaseScale = Vector3.one;
        coreActivationTitlePresented = false;

        ResolveReferences();
        BindRunEnd();

        AcquireCameraInputOffsetLock();

        Vector3 effectiveArenaCenter = arenaCenter + (Vector3)arenaCenterOffset;
        Vector3 effectiveBossBattlePosition = bossBattlePosition + (Vector3)bossBattlePositionOffset;
        Vector3 resolvedWideArenaCameraCenter = ResolveWideArenaCameraCenter(effectiveArenaCenter);

        if (useRaiderIntroVariant)
        {
            PrepareRaiderArenaCovers(effectiveArenaCenter);
        }

        if (logSequence)
        {
            Debug.Log("코어 보스 인트로 시작", this);
        }

        AcquireCinematicHudMode();

        if (lockPlayerInput)
        {
            LockPlayer(interactor);
        }

        ApplyIntroInvincibility(interactor);

        // 1. 카메라가 코어로 들어간 뒤 코어 활성화 애니메이션과 노란 펄스를 보여준다.
        if (focusCameraOnCore && gungeonCamera != null)
        {
            Transform focusTarget = coreActivationPresentation != null
                ? coreActivationPresentation.FocusTarget
                : transform;
            gungeonCamera.SetCinematicFocus(
                focusTarget != null ? focusTarget.position : transform.position
            );
        }

        yield return AnimateCameraZoomOnlyRoutine(
            coreFocusZoomMultiplier,
            coreFocusZoomDuration,
            coreFocusZoomCurve
        );

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (coreFocusSettleDuration > 0f)
        {
            yield return Wait(coreFocusSettleDuration);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (coreActivationPresentation != null)
        {
            yield return coreActivationPresentation.PlayActivationRoutine();
        }
        else
        {
            GungeonStyleCamera2D.RequestShake(0.12f, 0.18f);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        coreActivationCompletedCallback?.Invoke();

        if (delayAfterCorePulse > 0f)
        {
            yield return Wait(delayAfterCorePulse);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (!useRaiderIntroVariant)
        {
            yield return PlayCoreActivationNoticeRoutine();
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        // 2. 전장 전체가 보이도록 카메라를 넓히고 관리 기체가 봉쇄선을 만든다.
        SetIntroPhase(IntroPhase.ArenaWide);
        if (gungeonCamera != null)
        {
            gungeonCamera.SetCinematicFocus(resolvedWideArenaCameraCenter);
        }

        PrepareBackgroundForWideZoom();
        yield return AnimateCameraAndBackgroundZoomRoutine(
            ResolveIntroWideZoomMultiplier(),
            zoomOutDuration,
            zoomOutCurve,
            true
        );

        if (ShouldAbortIntro())
        {
            yield break;
        }

        // 스나이퍼 차징 취소나 무기 컴포넌트 상태 변경이 ResetZoom을 호출해도
        // 관리 기체 진입과 보스 등장 전까지 넓어진 화면을 유지한다.
        HoldIntroWideZoom();

        // 마지막 줌 값은 Update에서 기록되고 실제 카메라 중심/경계 보정은 LateUpdate에 적용됩니다.
        // 관리 기체를 같은 프레임에 만들지 않고 실제 렌더 카메라가 안정된 뒤 진입을 시작합니다.
        yield return WaitForWideArenaFramingSettleRoutine(resolvedWideArenaCameraCenter);

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (useRaiderIntroVariant)
        {
            yield return PlayCoreActivationNoticeRoutine();

            if (ShouldAbortIntro())
            {
                yield break;
            }
        }

        // 보스 오브젝트는 화면 밖에서 먼저 생성해 관리 기체 시스템을 하나로 통합한다.
        // 시각적으로는 아직 화면 밖이므로 기존 등장 순서는 유지된다.
        spawnedBoss = SpawnBossForIntro(bossPrefab, effectiveBossBattlePosition);
        bossCreatedCallback?.Invoke(spawnedBoss);

        if (spawnedBoss == null)
        {
            Debug.LogError("Boss intro could not continue because the Boss failed to spawn.", this);

            CloseCoreActivationPresentation();
            ReleaseCinematicHudMode();

            if (gungeonCamera != null)
            {
                gungeonCamera.ClearCinematicFocus(true);
            }

            ReleaseIntroWideZoomHold(false);
            ResetCameraZoom();
            ReleaseCameraInputOffsetLock();
            RestorePlayer();
            isPlaying = false;
            yield break;
        }

        BossPatternController bossPatternController = spawnedBoss != null
            ? spawnedBoss.GetComponent<BossPatternController>()
            : null;

        bool useRaiderBarricadeIntro = useRaiderIntroVariant &&
                                       spawnedBoss.GetComponent<PirateCommanderBossController>() != null;
        bool useBossOwnedManagers = !useRaiderBarricadeIntro &&
                                    useBossPatternGuardianSystem &&
                                    bossPatternController != null;

        DisableBossForIntro(spawnedBoss);
        CacheBossPresentation(spawnedBoss);
        TriggerBossAnimator(bossIntroEnterTrigger);
        SetIntroPhase(IntroPhase.FormationArrival);

        if (useRaiderBarricadeIntro)
        {
            DestroySpawnedWalls();
            DestroySpawnedManagerShips();
            yield return SpawnAndMoveRaiderBarricadeCarriersRoutine(effectiveArenaCenter);

            if (ShouldAbortIntro())
            {
                yield break;
            }

            if (delayBeforeWallActivation > 0f)
            {
                yield return Wait(delayBeforeWallActivation);
            }

            ActivateRaiderBarrier(effectiveArenaCenter);

            if (delayAfterWallActivation > 0f)
            {
                yield return Wait(delayAfterWallActivation);
            }
        }
        else if (useBossOwnedManagers)
        {
            // 과거 IntroSequence가 별도로 만들던 4대를 제거하고 보스 패턴 쪽 4대만 사용한다.
            DestroySpawnedWalls();
            DestroySpawnedManagerShips();

            bossPatternController.ConfigureBossArena(
                effectiveArenaCenter,
                arenaHalfExtents,
                verticalSpaceScale
            );

            yield return bossPatternController.PlayIntroGuardianEntryRoutine();
        }
        else
        {
            yield return SpawnAndMoveManagerShipsRoutine(effectiveArenaCenter);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        // 3. 보스를 화면 밖에서 중앙으로 진입시킨다.
        yield return MoveBossArrivalRoutine(
            spawnedBoss,
            effectiveBossBattlePosition,
            interactor
        );

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (!useRaiderBarricadeIntro)
        {
            if (delayBeforeWallActivation > 0f)
            {
                yield return Wait(delayBeforeWallActivation);
            }

            if (ShouldAbortIntro())
            {
                yield break;
            }

            if (useBossOwnedManagers)
            {
                bossPatternController.ActivatePhase1BoundaryLasers();
            }
            else
            {
                ActivateLaserWallsFromManagerShips();
            }
        }

        TrackBossDeathForCleanup(spawnedBoss);

        if (!useRaiderBarricadeIntro && delayAfterWallActivation > 0f)
        {
            yield return Wait(delayAfterWallActivation);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        // 4. 넓은 아레나 줌을 유지한 채 보스 쪽으로 초점만 치우쳐 등장 연출을 재생한다.
        SetIntroPhase(IntroPhase.BossReveal);
        yield return BlendBossRevealFocusRoutine(
            effectiveArenaCenter,
            effectiveBossBattlePosition
        );

        if (ShouldAbortIntro())
        {
            yield break;
        }

        TriggerBossAnimator(bossIntroRevealTrigger);
        yield return PlayBossRevealScaleRoutine();

        if (ShouldAbortIntro())
        {
            yield break;
        }

        AudioManager.PlayAt(
            SoundEventIds.BossSpawn,
            spawnedBoss != null ? spawnedBoss.transform.position : effectiveBossBattlePosition
        );

        // 코어 활성화 Motion을 이미 사용하므로 별도의 보스 Motion 타이틀은 재생하지 않습니다.
        // 보스 스케일 연출이 끝난 직후 체력바의 가로 펼침/HP 채움 연출을 시작합니다.
        bossRevealCallback?.Invoke();

        if (bossHealthBarLeadTime > 0f)
        {
            yield return Wait(bossHealthBarLeadTime);
        }

        if (ShouldAbortIntro())
        {
            yield break;
        }

        TriggerBossAnimator(bossIntroReadyTrigger);

        CloseCoreActivationPresentation();
        PrepareRaiderBattleCameraProfile();
        ReleaseIntroWideZoomHold(true);

        // 5. 실제 카메라 중심에서 플레이어 중심과 기본 줌을 같은 진행도로 복귀시킨다.
        SetIntroPhase(IntroPhase.PlayerHandoff);
        Transform playerCameraTarget = ResolvePlayerCameraTarget(interactor);
        yield return AnimateCameraHandoffToPlayerRoutine(playerCameraTarget);

        if (ShouldAbortIntro())
        {
            yield break;
        }

        // 플레이어 프레이밍이 완성된 뒤 HUD를 먼저 복구하고 전투/입력을 순서대로 연다.
        yield return ReleaseCinematicHudModeRoutine();

        if (ShouldAbortIntro())
        {
            yield break;
        }

        battleStartCallback?.Invoke();
        EnableBossForBattle();

        if (lockPlayerInput)
        {
            RestorePlayer();
        }

        SetIntroPhase(IntroPhase.Complete);

        if (!keepManagerShipsUntilBossDeath)
        {
            DestroySpawnedManagerShips();
        }

        if (logSequence)
        {
            Debug.Log("코어 보스 인트로 종료. 보스전 시작.", this);
        }

        isPlaying = false;
        ReleaseCameraInputOffsetLock();
    }

    public void ConfigureEncounterSignalSubtitle(string subtitle)
    {
        encounterSignalSubtitleOverride = subtitle ?? string.Empty;
    }

    public void ConfigureEncounterIntroVariant(bool useRaiderBarricadeIntro)
    {
        useRaiderIntroVariant = useRaiderBarricadeIntro;
    }

    public void ConfigureSalvageDevourerIntroVariant(bool useDedicatedIntro)
    {
        useSalvageDevourerIntroVariant = useDedicatedIntro;
    }

    private IEnumerator PlaySalvageDevourerIntroRoutine(
        GameObject interactor,
        GameObject bossPrefab,
        Action coreActivationCompletedCallback,
        Action<GameObject> bossCreatedCallback,
        Action bossRevealCallback,
        Action battleStartCallback)
    {
        isPlaying = true;
        introCancellationRequested = false;
        salvageBossTitlePresented = false;
        spawnedBoss = null;
        activeSalvageCorridor = null;
        activeSalvageTriad = null;
        coreActivationTitlePresented = false;
        SetIntroPhase(IntroPhase.CoreFocus);

        ResolveReferences();
        BindRunEnd();
        AcquireCameraInputOffsetLock();
        AcquireCinematicHudMode();

        if (lockPlayerInput)
        {
            LockPlayer(interactor);
        }

        ApplyIntroInvincibility(interactor);

        if (focusCameraOnCore && gungeonCamera != null)
        {
            Transform focusTarget = coreActivationPresentation != null
                ? coreActivationPresentation.FocusTarget
                : transform;
            gungeonCamera.SetCinematicFocus(
                focusTarget != null ? focusTarget.position : transform.position
            );
        }

        yield return AnimateCameraZoomOnlyRoutine(
            coreFocusZoomMultiplier,
            coreFocusZoomDuration,
            coreFocusZoomCurve
        );

        if (ShouldAbortIntro())
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        if (coreFocusSettleDuration > 0f)
        {
            yield return Wait(coreFocusSettleDuration);
        }

        if (coreActivationPresentation != null)
        {
            yield return coreActivationPresentation.PlayActivationRoutine();
        }
        else
        {
            GungeonStyleCamera2D.RequestShake(0.12f, 0.18f);
        }

        if (ShouldAbortIntro())
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        coreActivationCompletedCallback?.Invoke();

        if (delayAfterCorePulse > 0f)
        {
            yield return Wait(delayAfterCorePulse);
        }

        yield return AnimateCameraZoomOnlyRoutine(1f, zoomInDuration, zoomInCurve);

        if (ShouldAbortIntro())
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        ExpeditionMapGenerator mapGenerator =
            FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
        activeSalvageCorridor = mapGenerator != null
            ? mapGenerator.CurrentRegion2BossCorridorRuntime
            : null;

        if (activeSalvageCorridor == null)
        {
            Debug.LogError(
                "The dedicated Salvage Devourer intro could not resolve the Phase-2 corridor runtime.",
                this
            );
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        CloseCoreActivationPresentation();
        ResetCameraZoom();

        if (gungeonCamera != null)
        {
            gungeonCamera.BeginCinematicFocusBlend(
                activeSalvageCorridor.CameraStartCenter,
                bossRevealFocusPanDuration,
                bossRevealFocusPanCurve
            );

            while (gungeonCamera.IsCinematicFocusBlendActive)
            {
                if (ShouldAbortIntro())
                {
                    CleanupSalvageDevourerIntro(true);
                    yield break;
                }

                yield return null;
            }

            if (!gungeonCamera.TryReleaseUnownedCinematicFocusAtCurrentPosition())
            {
                Debug.LogError(
                    "The Salvage Devourer intro could not hand camera ownership to the corridor.",
                    this
                );
                CleanupSalvageDevourerIntro(true);
                yield break;
            }
        }

        if (!activeSalvageCorridor.PrepareCorridorRuntime())
        {
            Debug.LogError(
                "The dedicated Salvage Devourer intro could not prepare the Phase-2 corridor runtime.",
                this
            );
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        SetIntroPhase(IntroPhase.ArenaWide);
        yield return null;

        if (ShouldAbortIntro() || bossPrefab == null)
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        FrigateTriadBossController triadPrefab =
            bossPrefab.GetComponent<FrigateTriadBossController>();
        Vector3 introRootPosition = triadPrefab != null
            ? triadPrefab.ResolveIntroRootPosition(activeSalvageCorridor.CameraStartCenter)
            : activeSalvageCorridor.CameraStartCenter;
        spawnedBoss = Instantiate(bossPrefab, introRootPosition, Quaternion.identity);
        spawnedBoss.name = bossPrefab.name;
        bossCreatedCallback?.Invoke(spawnedBoss);
        activeSalvageTriad = spawnedBoss != null
            ? spawnedBoss.GetComponent<FrigateTriadBossController>()
            : null;

        if (activeSalvageTriad == null ||
            !activeSalvageTriad.PrepareForIntro(activeSalvageCorridor))
        {
            Debug.LogError(
                "The live Region-2 Boss prefab does not provide a valid dormant Frigate Triad authority.",
                this
            );
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        TrackBossDeathForCleanup(spawnedBoss);
        SetIntroPhase(IntroPhase.FormationArrival);
        yield return activeSalvageTriad.PlayEntryRoutine();

        if (ShouldAbortIntro() || activeSalvageTriad == null ||
            !activeSalvageTriad.IsEntryComplete)
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        AudioManager.PlayAt(SoundEventIds.BossSpawn, spawnedBoss.transform.position);
        SetIntroPhase(IntroPhase.BossReveal);
        EventTitleDirector titleDirector = coreActivationTitleDirector != null
            ? coreActivationTitleDirector
            : EventTitleDirector.Instance;

        if (titleDirector != null)
        {
            titleDirector.ShowBossEncounter(
                activeSalvageTriad.BossDisplayName,
                activeSalvageTriad.BossSubtitle
            );
            salvageBossTitlePresented = true;

            while (titleDirector != null && titleDirector.IsPlaying)
            {
                if (ShouldAbortIntro())
                {
                    CleanupSalvageDevourerIntro(true);
                    yield break;
                }

                yield return null;
            }

            salvageBossTitlePresented = false;
        }
        else
        {
            ShowWarning();
            yield return Wait(delayAfterWarning);
        }

        bossRevealCallback?.Invoke();

        if (bossHealthBarLeadTime > 0f)
        {
            yield return Wait(bossHealthBarLeadTime);
        }

        if (ShouldAbortIntro() || activeSalvageTriad == null ||
            !activeSalvageTriad.BeginFormationFollowing())
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        SetIntroPhase(IntroPhase.PlayerHandoff);
        yield return ReleaseCinematicHudModeRoutine();

        if (ShouldAbortIntro())
        {
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        if (lockPlayerInput)
        {
            RestorePlayer();
        }

        battleStartCallback?.Invoke();

        if (activeSalvageTriad == null || activeSalvageCorridor == null ||
            !activeSalvageTriad.IsGameplayActive ||
            !activeSalvageCorridor.BeginScroll())
        {
            Debug.LogError(
                "The dedicated Salvage Devourer intro failed at the gameplay/scroll handoff.",
                this
            );
            CleanupSalvageDevourerIntro(true);
            yield break;
        }

        SetIntroPhase(IntroPhase.Complete);
        isPlaying = false;
        ReleaseCameraInputOffsetLock();

        // The Frigate controller now owns formation lifetime and delegates corridor cleanup.
        activeSalvageTriad = null;
        activeSalvageCorridor = null;
    }

    private void CleanupSalvageDevourerIntro(bool destroyUnhandedBoss)
    {
        if (salvageBossTitlePresented)
        {
            EventTitleDirector titleDirector = coreActivationTitleDirector != null
                ? coreActivationTitleDirector
                : EventTitleDirector.Instance;
            titleDirector?.StopCurrentAndClear();
            salvageBossTitlePresented = false;
        }

        FrigateTriadBossController triad = activeSalvageTriad;
        SalvageDevourerCorridorController corridor = activeSalvageCorridor;
        activeSalvageTriad = null;
        activeSalvageCorridor = null;
        triad?.CleanupBossRuntime();

        if (triad == null)
        {
            corridor?.CleanupCorridorRuntime();
        }

        if (destroyUnhandedBoss && isPlaying && spawnedBoss != null)
        {
            Destroy(spawnedBoss);
            spawnedBoss = null;
        }

        CloseCoreActivationPresentation();
        ReleaseCinematicHudMode();

        if (gungeonCamera != null)
        {
            gungeonCamera.CancelCinematicFocusBlend(false);
            gungeonCamera.ClearCinematicFocus(true);
        }

        ResetCameraZoom();
        RestorePlayer();
        ReleaseCameraInputOffsetLock();
        isPlaying = false;
        SetIntroPhase(IntroPhase.Inactive);
    }

    private bool IsSalvageDevourerIntroRuntimeOwned()
    {
        return useSalvageDevourerIntroVariant &&
               (isPlaying || activeSalvageCorridor != null ||
                activeSalvageTriad != null || salvageBossTitlePresented);
    }

    public Vector3 ResolveEncounterArenaCenter(Vector3 coreWorldPosition)
    {
        return coreWorldPosition + (Vector3)arenaCenterOffset;
    }

    public Vector2 ResolveRaiderEncounterArenaHalfExtents()
    {
        return GetRaiderEffectiveHalfExtents();
    }

    private void AcquireCinematicHudMode()
    {
        cinematicHudModeHeld = false;

        if (!hideStatusAndResourceUIDuringIntro ||
            expeditionHUD == null ||
            !expeditionHUD.isActiveAndEnabled)
        {
            return;
        }

        expeditionHUD.SetCinematicMode(this, true);
        cinematicHudModeHeld = true;
    }

    private void ReleaseCinematicHudMode()
    {
        if (cinematicHudRestoreInProgress)
        {
            cinematicHudRestoreInProgress = false;

            if (expeditionHUD != null &&
                expeditionHUD.gameObject.scene.IsValid() &&
                expeditionHUD.gameObject.scene.isLoaded)
            {
                expeditionHUD.CompleteCinematicVisibilityTransition();
            }
        }

        if (!cinematicHudModeHeld)
        {
            return;
        }

        cinematicHudModeHeld = false;

        if (expeditionHUD == null ||
            !expeditionHUD.gameObject.scene.IsValid() ||
            !expeditionHUD.gameObject.scene.isLoaded)
        {
            return;
        }

        expeditionHUD.SetCinematicMode(this, false);
    }

    private IEnumerator ReleaseCinematicHudModeRoutine()
    {
        if (!cinematicHudModeHeld)
        {
            yield break;
        }

        cinematicHudModeHeld = false;

        if (expeditionHUD == null ||
            !expeditionHUD.gameObject.scene.IsValid() ||
            !expeditionHUD.gameObject.scene.isLoaded)
        {
            yield break;
        }

        cinematicHudRestoreInProgress = true;
        expeditionHUD.ReleaseCinematicMode(this);

        while (expeditionHUD != null &&
               expeditionHUD.isActiveAndEnabled &&
               expeditionHUD.IsCinematicVisibilityTransitionActive)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            yield return null;
        }

        cinematicHudRestoreInProgress = false;
    }

    public void BeginCoreActivationCameraLock()
    {
        ResolveReferences();
        AcquireCameraInputOffsetLock();
    }

    public void CancelCoreActivationCameraLock()
    {
        ReleaseCameraInputOffsetLock();
    }

    private void AcquireCameraInputOffsetLock()
    {
        if (cameraInputOffsetLockHeld || gungeonCamera == null)
        {
            return;
        }

        gungeonCamera.SetCinematicInputOffsetLocked(true);
        cameraInputOffsetLockHeld = true;
    }

    private void ReleaseCameraInputOffsetLock()
    {
        if (!cameraInputOffsetLockHeld)
        {
            return;
        }

        if (gungeonCamera != null)
        {
            gungeonCamera.SetCinematicInputOffsetLocked(false);
        }

        cameraInputOffsetLockHeld = false;
    }

    private void ResolveReferences()
    {
        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        }

        if (cameraZoomController == null)
        {
            cameraZoomController = FindFirstObjectByType<CameraZoomController2D>(FindObjectsInactive.Include);
        }

        if (cameraZoomController == null && createCameraZoomControllerIfMissing)
        {
            if (gungeonCamera == null)
            {
                gungeonCamera = GungeonStyleCamera2D.Instance;
            }

            Camera mainCamera = Camera.main;
            GameObject zoomOwner = gungeonCamera != null
                ? gungeonCamera.gameObject
                : mainCamera != null ? mainCamera.gameObject : null;

            if (zoomOwner != null)
            {
                cameraZoomController = zoomOwner.GetComponent<CameraZoomController2D>();

                if (cameraZoomController == null)
                {
                    cameraZoomController = zoomOwner.AddComponent<CameraZoomController2D>();
                }

                cameraZoomController.Bind(mainCamera);
            }
        }

        if (spaceBackgroundGenerator == null)
        {
            spaceBackgroundGenerator = FindFirstObjectByType<SpaceBackgroundGenerator2D>(FindObjectsInactive.Include);
        }

        if (warningMessageUI == null)
        {
            warningMessageUI = FindFirstObjectByType<WarningMessageUI>(FindObjectsInactive.Include);
        }

        if (gungeonCamera == null)
        {
            gungeonCamera = GungeonStyleCamera2D.Instance;

            if (gungeonCamera == null)
            {
                gungeonCamera = FindFirstObjectByType<GungeonStyleCamera2D>(FindObjectsInactive.Include);
            }
        }

        if (coreActivationPresentation == null)
        {
            coreActivationPresentation = GetComponent<CoreActivationPresentation>();

            if (coreActivationPresentation == null)
            {
                coreActivationPresentation = gameObject.AddComponent<CoreActivationPresentation>();
            }
        }

        if (coreActivationTitleDirector == null)
        {
            coreActivationTitleDirector = EventTitleDirector.Instance;
        }

    }

    private Vector2 GetEffectiveHalfExtents()
    {
        return new Vector2(
            Mathf.Max(0.1f, arenaHalfExtents.x),
            Mathf.Max(0.1f, arenaHalfExtents.y * verticalSpaceScale)
        );
    }

    private Vector2 GetRaiderEffectiveHalfExtents()
    {
        return GetEffectiveHalfExtents() * Mathf.Clamp(raiderArenaSizeMultiplier, 0.5f, 1f);
    }

    private void PrepareRaiderArenaCovers(Vector2 arenaCenter)
    {
        ResolveNearestRaiderArenaCovers(arenaCenter);

        Vector2 halfExtents = GetRaiderEffectiveHalfExtents();
        MoveRaiderArenaCover(
            raiderEncounterCovers[0],
            arenaCenter,
            halfExtents,
            raiderLeftCoverNormalizedOffset
        );
        MoveRaiderArenaCover(
            raiderEncounterCovers[1],
            arenaCenter,
            halfExtents,
            raiderRightCoverNormalizedOffset
        );

        Physics2D.SyncTransforms();
        ValidateRaiderCoverPassage();
    }

    private void ResolveNearestRaiderArenaCovers(Vector2 arenaCenter)
    {
        ClearRaiderCoverReferences();
        BossArenaCover[] covers = FindObjectsByType<BossArenaCover>(FindObjectsSortMode.None);

        for (int i = 0; i < covers.Length; i++)
        {
            BossArenaCover cover = covers[i];
            if (cover == null || !cover.IsValid)
            {
                continue;
            }

            int index = cover.Side == BossArenaCoverSide.Left ? 0 : 1;
            BossArenaCover current = raiderEncounterCovers[index];
            if (current == null ||
                Vector2.SqrMagnitude((Vector2)cover.transform.position - arenaCenter) <
                Vector2.SqrMagnitude((Vector2)current.transform.position - arenaCenter))
            {
                raiderEncounterCovers[index] = cover;
            }
        }
    }

    private void MoveRaiderArenaCover(
        BossArenaCover cover,
        Vector2 arenaCenter,
        Vector2 arenaHalfExtents,
        Vector2 normalizedOffset)
    {
        if (cover == null || !cover.IsValid)
        {
            return;
        }

        Bounds coverBounds = cover.WorldBounds;
        Vector2 colliderExtents = coverBounds.extents;
        Vector2 colliderCenterOffset = (Vector2)coverBounds.center - (Vector2)cover.transform.position;
        Vector2 desiredRootPosition = arenaCenter + Vector2.Scale(arenaHalfExtents, normalizedOffset);
        Vector2 desiredColliderLocalPosition = desiredRootPosition + colliderCenterOffset - arenaCenter;
        Vector2 safeColliderHalfExtents = new Vector2(
            Mathf.Max(0f, arenaHalfExtents.x - raiderCoverWallClearance - colliderExtents.x),
            Mathf.Max(0f, arenaHalfExtents.y - raiderCoverWallClearance - colliderExtents.y)
        );
        Vector2 clampedColliderLocalPosition = new Vector2(
            Mathf.Clamp(
                desiredColliderLocalPosition.x,
                -safeColliderHalfExtents.x,
                safeColliderHalfExtents.x
            ),
            Mathf.Clamp(
                desiredColliderLocalPosition.y,
                -safeColliderHalfExtents.y,
                safeColliderHalfExtents.y
            )
        );
        Vector2 resolvedRootPosition =
            arenaCenter + clampedColliderLocalPosition - colliderCenterOffset;
        bool adjusted = Vector2.SqrMagnitude(resolvedRootPosition - desiredRootPosition) > 0.0001f;
        bool placementClear = false;

        const float inwardStep = 0.25f;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            if (IsRaiderCoverPositionClear(cover, resolvedRootPosition, coverBounds))
            {
                placementClear = true;
                break;
            }

            Vector2 local = resolvedRootPosition - arenaCenter;
            local.x = Mathf.MoveTowards(local.x, 0f, inwardStep);
            resolvedRootPosition = arenaCenter + local;
            adjusted = true;
        }

        if (!placementClear)
        {
            placementClear = IsRaiderCoverPositionClear(
                cover,
                resolvedRootPosition,
                coverBounds
            );
        }

        cover.SetEncounterPosition(resolvedRootPosition);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (adjusted && logSequence)
        {
            Debug.Log(
                $"[BossIntro] Raider cover '{cover.name}' adjusted from " +
                $"{desiredRootPosition} to {resolvedRootPosition} for arena clearance.",
                cover
            );
        }

        if (!placementClear)
        {
            Debug.LogWarning(
                $"[BossIntro] Raider cover '{cover.name}' could not find a fully clear normalized " +
                $"position near {resolvedRootPosition}. Inspect the generated arena layout.",
                cover
            );
        }
#endif
    }

    private bool IsRaiderCoverPositionClear(
        BossArenaCover movingCover,
        Vector2 rootPosition,
        Bounds currentBounds)
    {
        Vector2 centerOffset = (Vector2)currentBounds.center - (Vector2)movingCover.transform.position;
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(Physics2D.AllLayers);
        int count = Physics2D.OverlapBox(
            rootPosition + centerOffset,
            currentBounds.size,
            0f,
            filter,
            raiderCoverOverlapResults
        );
        bool blocked = false;

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = raiderCoverOverlapResults[i];
            raiderCoverOverlapResults[i] = null;
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            BossArenaCover otherCover = collider.GetComponentInParent<BossArenaCover>();
            if (otherCover != null)
            {
                continue;
            }

            if (!collider.transform.IsChildOf(transform))
            {
                blocked = true;
            }
        }

        return !blocked;
    }

    private void ValidateRaiderCoverPassage()
    {
        BossArenaCover left = raiderEncounterCovers[0];
        BossArenaCover right = raiderEncounterCovers[1];
        if (left == null || right == null)
        {
            return;
        }

        float passage = right.WorldBounds.min.x - left.WorldBounds.max.x;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (passage < raiderCoverMinimumPassage)
        {
            Debug.LogWarning(
                $"[BossIntro] Raider cover passage is {passage:0.00} world units; " +
                $"the configured minimum is {raiderCoverMinimumPassage:0.00}.",
                this
            );
        }
#endif
    }

    private void ClearRaiderCoverReferences()
    {
        raiderEncounterCovers[0] = null;
        raiderEncounterCovers[1] = null;
    }

    private Vector2 GetCurrentEncounterHalfExtents()
    {
        return useRaiderIntroVariant
            ? GetRaiderEffectiveHalfExtents()
            : GetEffectiveHalfExtents();
    }

    private float ResolveIntroWideZoomMultiplier()
    {
        float multiplier = Mathf.Max(1f, wideZoomMultiplier);
        return useRaiderIntroVariant
            ? Mathf.Max(1f, multiplier * Mathf.Clamp(raiderArenaSizeMultiplier, 0.5f, 1f))
            : multiplier;
    }

    private Vector3 ResolveWideArenaCameraCenter(Vector3 arenaCenter)
    {
        if (gungeonCamera == null)
        {
            return arenaCenter;
        }

        float introWideZoomMultiplier = ResolveIntroWideZoomMultiplier();
        float wideOrthographicSize = cameraZoomController != null
            ? cameraZoomController.BaseOrthographicSize * introWideZoomMultiplier
            : Camera.main != null
                ? Camera.main.orthographicSize * introWideZoomMultiplier
                : 0f;

        return gungeonCamera.ResolveClampedCameraCenter(arenaCenter, wideOrthographicSize);
    }

    private IEnumerator WaitForWideArenaFramingSettleRoutine(Vector3 resolvedArenaCenter)
    {
        // Ensure the final zoom write has passed through the direct camera rig's LateUpdate once.
        yield return null;

        float timeout = Mathf.Max(0.05f, gameplayFramingSettleTimeout);
        float elapsed = 0f;
        float targetOrthographicSize = cameraZoomController != null
            ? cameraZoomController.BaseOrthographicSize * ResolveIntroWideZoomMultiplier()
            : 0f;

        while (elapsed < timeout)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            bool cameraSettled = gungeonCamera == null ||
                                 gungeonCamera.IsAtCinematicFocusCenter(
                                     resolvedArenaCenter,
                                     gameplayFramingPositionTolerance
                                 );
            bool zoomSettled = cameraZoomController == null ||
                               Mathf.Abs(
                                   cameraZoomController.CurrentOrthographicSize - targetOrthographicSize
                               ) <= Mathf.Max(0.0001f, gameplayFramingZoomTolerance);

            if (cameraSettled && zoomSettled)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (logSequence)
                {
                    Debug.Log(
                        $"[BossIntro] Wide arena framing settled before manager arrival: " +
                        $"camera={(gungeonCamera != null ? gungeonCamera.transform.position : resolvedArenaCenter):F2}, " +
                        $"target={resolvedArenaCenter:F2}, " +
                        $"ortho={(cameraZoomController != null ? cameraZoomController.CurrentOrthographicSize : 0f):F3}",
                        this
                    );
                }
#endif
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            yield return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logSequence)
        {
            Debug.LogWarning(
                "[BossIntro] Wide arena framing settle timed out; manager arrival continues without changing camera ownership.",
                this
            );
        }
#endif
    }

    private IEnumerator SpawnAndMoveManagerShipsRoutine(Vector3 arenaCenter)
    {
        DestroySpawnedManagerShips();
        spawnedManagerShips.Clear();

        Vector2 half = GetEffectiveHalfExtents();

        Vector2[] cornerSigns =
        {
            new Vector2(-1f, -1f),
            new Vector2(-1f,  1f),
            new Vector2( 1f,  1f),
            new Vector2( 1f, -1f),
        };

        float[] rotationZ =
        {
            managerShipBaseRotationZ + 0f,
            managerShipBaseRotationZ - 90f,
            managerShipBaseRotationZ + 180f,
            managerShipBaseRotationZ + 90f
        };

        string[] names =
        {
            "LaserManagerShip_01_BottomLeft",
            "LaserManagerShip_02_TopLeft",
            "LaserManagerShip_03_TopRight",
            "LaserManagerShip_04_BottomRight"
        };

        Vector3[] startPositions = new Vector3[4];
        Vector3[] endPositions = new Vector3[4];

        for (int i = 0; i < 4; i++)
        {
            Vector2 sign = cornerSigns[i];
            Vector3 cornerOffset = new Vector3(sign.x * half.x, sign.y * half.y, 0f);
            Vector3 outward = cornerOffset.sqrMagnitude > 0.001f ? cornerOffset.normalized : Vector3.up;

            endPositions[i] = arenaCenter + cornerOffset;
            startPositions[i] = endPositions[i] + outward * Mathf.Max(0f, managerShipStartExtraDistance);

            LaserGuardianDrone managerShip = CreateManagerShip(
                names[i],
                i + 1,
                startPositions[i],
                rotationZ[i],
                Color.white
            );

            if (managerShip != null)
            {
                spawnedManagerShips.Add(managerShip);
            }
        }

        float duration = Mathf.Max(0.01f, managerShipMoveDuration);
        float timer = 0f;

        while (timer < duration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = managerShipMoveCurve != null ? managerShipMoveCurve.Evaluate(t) : t;

            for (int i = 0; i < spawnedManagerShips.Count; i++)
            {
                LaserGuardianDrone ship = spawnedManagerShips[i];

                if (ship == null)
                {
                    continue;
                }

                ship.transform.position = Vector3.LerpUnclamped(startPositions[i], endPositions[i], eased);
                ship.ApplySlotRotation(rotationZ[i]);
            }

            yield return null;
        }

        for (int i = 0; i < spawnedManagerShips.Count; i++)
        {
            LaserGuardianDrone ship = spawnedManagerShips[i];

            if (ship == null)
            {
                continue;
            }

            ship.transform.position = endPositions[i];
            ship.ApplySlotRotation(rotationZ[i]);
        }
    }

    private IEnumerator SpawnAndMoveRaiderBarricadeCarriersRoutine(Vector3 arenaCenter)
    {
        DestroySpawnedRaiderCarriers();

        if (raiderBarricadeCarrierPrefab == null)
        {
            Debug.LogError(
                "Raider intro requires a RaiderBarricadeCarrier prefab.",
                this
            );
            yield break;
        }

        Vector2 half = GetRaiderEffectiveHalfExtents();
        Vector3[] outwardDirections =
        {
            Vector3.up,
            Vector3.down,
            Vector3.right,
            Vector3.left
        };
        Vector3[] anchorPositions =
        {
            arenaCenter + Vector3.up * half.y,
            arenaCenter + Vector3.down * half.y,
            arenaCenter + Vector3.right * half.x,
            arenaCenter + Vector3.left * half.x
        };
        string[] carrierNames =
        {
            "RaiderBarricadeCarrier_North",
            "RaiderBarricadeCarrier_South",
            "RaiderBarricadeCarrier_East",
            "RaiderBarricadeCarrier_West"
        };
        RaiderBarricadeCarrier.ArenaSide[] carrierSides =
        {
            RaiderBarricadeCarrier.ArenaSide.North,
            RaiderBarricadeCarrier.ArenaSide.South,
            RaiderBarricadeCarrier.ArenaSide.East,
            RaiderBarricadeCarrier.ArenaSide.West
        };

        float extraDistance = Mathf.Max(0f, raiderCarrierStartExtraDistance);
        float duration = Mathf.Max(0.05f, raiderCarrierMoveDuration);

        for (int i = 0; i < anchorPositions.Length; i++)
        {
            Vector3 startPosition = anchorPositions[i] + outwardDirections[i] * extraDistance;
            RaiderBarricadeCarrier carrier = Instantiate(
                raiderBarricadeCarrierPrefab,
                startPosition,
                Quaternion.identity
            );
            carrier.name = carrierNames[i];
            carrier.BeginArrival(
                startPosition,
                anchorPositions[i],
                carrierSides[i],
                duration,
                raiderCarrierMoveCurve
            );
            spawnedRaiderCarriers.Add(carrier);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            yield return null;
        }

        for (int i = 0; i < spawnedRaiderCarriers.Count; i++)
        {
            RaiderBarricadeCarrier carrier = spawnedRaiderCarriers[i];
            if (carrier != null)
            {
                carrier.CompleteArrival();
            }
        }
    }

    private void ActivateRaiderBarrier(Vector3 arenaCenter)
    {
        DestroySpawnedWalls();

        for (int i = 0; i < spawnedRaiderCarriers.Count; i++)
        {
            RaiderBarricadeCarrier carrier = spawnedRaiderCarriers[i];
            if (carrier != null)
            {
                carrier.PlayBarrierDeployment();
            }
        }

        Vector2 half = GetRaiderEffectiveHalfExtents();
        Vector2 bottomLeft = (Vector2)arenaCenter + new Vector2(-half.x, -half.y);
        Vector2 topLeft = (Vector2)arenaCenter + new Vector2(-half.x, half.y);
        Vector2 topRight = (Vector2)arenaCenter + new Vector2(half.x, half.y);
        Vector2 bottomRight = (Vector2)arenaCenter + new Vector2(half.x, -half.y);

        CreateRaiderArenaWallBetween("RaiderArenaBarrier_Left", bottomLeft, topLeft);
        CreateRaiderArenaWallBetween("RaiderArenaBarrier_Top", topLeft, topRight);
        CreateRaiderArenaWallBetween("RaiderArenaBarrier_Right", topRight, bottomRight);
        CreateRaiderArenaWallBetween("RaiderArenaBarrier_Bottom", bottomRight, bottomLeft);
    }

    private void CreateRaiderArenaWallBetween(string objectName, Vector2 start, Vector2 end)
    {
        BossArenaLaserWall wall = CreateArenaWallBetween(
            objectName,
            start,
            end,
            raiderBarrierColor,
            raiderBarrierThickness
        );
        if (wall == null)
        {
            return;
        }

        float length = Vector2.Distance(start, end);
        LineRenderer authoritativeLine = wall.GetOrCreateLineRenderer();
        if (authoritativeLine == null)
        {
            Debug.LogError(
                $"Raider arena wall '{wall.name}' could not provide its authoritative LineRenderer. " +
                "The initialized physical wall will remain active without the enhanced Raider visuals.",
                wall
            );
            return;
        }

        RaiderArenaBoundaryPresentation presentation =
            wall.gameObject.GetComponent<RaiderArenaBoundaryPresentation>();
        if (presentation == null)
        {
            presentation = wall.gameObject.AddComponent<RaiderArenaBoundaryPresentation>();
        }

        presentation.Configure(
            authoritativeLine,
            length,
            raiderBarrierMaterial,
            laserSortingLayerName,
            laserSortingOrder,
            raiderBarrierColor,
            raiderBarrierFieldColor,
            raiderBarrierNoiseColor,
            raiderBarrierCoreWidth,
            raiderBarrierFieldWidth,
            raiderBarrierNoiseWidth,
            raiderBarrierPulseDuration
        );
    }

    private LaserGuardianDrone CreateManagerShip(
        string objectName,
        int runtimeIndex,
        Vector3 position,
        float rotationZ,
        Color tint)
    {
        GameObject shipObject;

        if (laserManagerShipPrefab != null)
        {
            shipObject = Instantiate(
                laserManagerShipPrefab,
                position,
                Quaternion.Euler(0f, 0f, rotationZ)
            );
        }
        else
        {
            if (!createRuntimePlaceholderIfMissing)
            {
                return null;
            }

            shipObject = CreateRuntimePlaceholderShip(objectName, position, rotationZ);
        }

        shipObject.name = objectName;

        LaserGuardianDrone drone = shipObject.GetComponent<LaserGuardianDrone>();

        if (drone == null)
        {
            drone = shipObject.AddComponent<LaserGuardianDrone>();
        }

        drone.SetRuntimeIndex(runtimeIndex);
        drone.ApplySlotRotation(rotationZ);
        drone.SetTint(tint);

        return drone;
    }

    private GameObject CreateRuntimePlaceholderShip(string objectName, Vector3 position, float rotationZ)
    {
        GameObject shipObject = new GameObject(objectName);
        shipObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationZ));

        shipObject.AddComponent<LaserGuardianDrone>();

        LineRenderer lineRenderer = shipObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 5;

        float size = Mathf.Max(0.05f, runtimePlaceholderSize);

        lineRenderer.SetPosition(0, new Vector3(0f, size, 0f));
        lineRenderer.SetPosition(1, new Vector3(size, 0f, 0f));
        lineRenderer.SetPosition(2, new Vector3(0f, -size, 0f));
        lineRenderer.SetPosition(3, new Vector3(-size, 0f, 0f));
        lineRenderer.SetPosition(4, new Vector3(0f, size, 0f));

        lineRenderer.startWidth = Mathf.Max(0.001f, runtimePlaceholderLineWidth);
        lineRenderer.endWidth = Mathf.Max(0.001f, runtimePlaceholderLineWidth);
        lineRenderer.startColor = runtimePlaceholderColor;
        lineRenderer.endColor = runtimePlaceholderColor;
        lineRenderer.sortingLayerName = laserSortingLayerName;
        lineRenderer.sortingOrder = laserSortingOrder + 1;

        if (laserLineMaterial != null)
        {
            lineRenderer.material = laserLineMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }

        return shipObject;
    }

    private void ActivateLaserWallsFromManagerShips()
    {
        DestroySpawnedWalls();

        if (spawnedManagerShips.Count < 4)
        {
            return;
        }

        CreateLaserWallBetween("BossArenaWall_Left", spawnedManagerShips[0], spawnedManagerShips[1]);
        CreateLaserWallBetween("BossArenaWall_Top", spawnedManagerShips[1], spawnedManagerShips[2]);
        CreateLaserWallBetween("BossArenaWall_Right", spawnedManagerShips[2], spawnedManagerShips[3]);
        CreateLaserWallBetween("BossArenaWall_Bottom", spawnedManagerShips[3], spawnedManagerShips[0]);
    }

    private void CreateLaserWallBetween(string objectName, LaserGuardianDrone startDrone, LaserGuardianDrone endDrone)
    {
        if (startDrone == null || endDrone == null)
        {
            return;
        }

        CreateArenaWallBetween(
            objectName,
            startDrone.transform.position,
            endDrone.transform.position,
            laserLineColor,
            wallThickness
        );
    }

    private BossArenaLaserWall CreateArenaWallBetween(
        string objectName,
        Vector2 start,
        Vector2 end,
        Color lineColor,
        float thickness)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            return null;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;

        BossArenaLaserWall wall;

        if (laserWallPrefab != null)
        {
            wall = Instantiate(laserWallPrefab);
            wall.name = objectName;
        }
        else
        {
            GameObject wallObject = new GameObject(objectName);
            wall = wallObject.AddComponent<BossArenaLaserWall>();
        }

        wall.Initialize(
            center,
            direction,
            length,
            thickness,
            createSolidLaserWalls,
            wallDamage,
            wallDamageInterval,
            laserLineMaterial,
            lineColor,
            laserSortingLayerName,
            laserSortingOrder,
            laserWallLayerName
        );

        spawnedWallObjects.Add(wall.gameObject);
        return wall;
    }

    private GameObject SpawnBossForIntro(GameObject bossPrefab, Vector3 bossBattlePosition)
    {
        if (bossPrefab == null)
        {
            return null;
        }

        Vector2 arrivalDirection = bossArrivalDirection;

        if (arrivalDirection.sqrMagnitude <= 0.001f)
        {
            arrivalDirection = Vector2.up;
        }

        arrivalDirection.Normalize();

        Vector3 startPosition = bossBattlePosition + (Vector3)(arrivalDirection * Mathf.Max(0.1f, bossArrivalDistance));
        GameObject bossObject = Instantiate(bossPrefab, startPosition, Quaternion.identity);
        bossObject.name = bossPrefab.name;

        Rigidbody2D bossRb = bossObject.GetComponent<Rigidbody2D>();

        if (bossRb != null)
        {
            bossRb.linearVelocity = Vector2.zero;
            bossRb.angularVelocity = 0f;
        }

        return bossObject;
    }

    private IEnumerator MoveBossArrivalRoutine(
        GameObject bossObject,
        Vector3 bossBattlePosition,
        GameObject interactor)
    {
        if (bossObject == null)
        {
            yield break;
        }

        Transform bossTransform = bossObject.transform;
        Vector3 startPosition = bossTransform.position;
        Vector3 endPosition = bossBattlePosition;

        float duration = Mathf.Max(0.01f, bossArrivalDuration);
        float timer = 0f;

        while (timer < duration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = bossMoveCurve != null ? bossMoveCurve.Evaluate(t) : t;

            bossTransform.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);

            if (faceBossToPlayerWhenArrived && interactor != null)
            {
                FaceTransformToTarget(bossTransform, interactor.transform.position, bossRotationOffset);
            }

            yield return null;
        }

        bossTransform.position = endPosition;

        if (faceBossToPlayerWhenArrived && interactor != null)
        {
            FaceTransformToTarget(bossTransform, interactor.transform.position, bossRotationOffset);
        }

        Rigidbody2D bossRb = bossObject.GetComponent<Rigidbody2D>();

        if (bossRb != null)
        {
            bossRb.linearVelocity = Vector2.zero;
            bossRb.angularVelocity = 0f;
        }
    }

    private IEnumerator BlendBossRevealFocusRoutine(
        Vector3 arenaCenter,
        Vector3 fixedBossArrivalDestination)
    {
        if (gungeonCamera == null)
        {
            yield break;
        }

        Vector3 requestedFocusPosition = Vector3.Lerp(
            arenaCenter,
            fixedBossArrivalDestination,
            Mathf.Clamp01(bossArrivalFocusBias)
        );
        Vector2 safeArenaHalfExtents = GetCurrentEncounterHalfExtents();
        requestedFocusPosition.x = Mathf.Clamp(
            requestedFocusPosition.x,
            arenaCenter.x - safeArenaHalfExtents.x,
            arenaCenter.x + safeArenaHalfExtents.x
        );
        requestedFocusPosition.y = Mathf.Clamp(
            requestedFocusPosition.y,
            arenaCenter.y - safeArenaHalfExtents.y,
            arenaCenter.y + safeArenaHalfExtents.y
        );
        Vector3 resolvedFocusPosition = gungeonCamera.BeginCinematicFocusBlend(
            requestedFocusPosition,
            bossRevealFocusPanDuration,
            bossRevealFocusPanCurve
        );

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logSequence)
        {
            Debug.Log(
                $"[BossIntro] Boss focus pan: arrival={fixedBossArrivalDestination:F2}, " +
                $"requested={requestedFocusPosition:F2}, resolved={resolvedFocusPosition:F2}, " +
                $"start={gungeonCamera.transform.position:F2}, " +
                $"ortho={(Camera.main != null ? Camera.main.orthographicSize : 0f):F3}",
                this
            );
        }
#endif

        while (gungeonCamera != null && gungeonCamera.IsCinematicFocusBlendActive)
        {
            if (ShouldAbortIntro())
            {
                gungeonCamera.CancelCinematicFocusBlend();
                yield break;
            }

            yield return null;
        }
    }

    private void DisableBossForIntro(GameObject bossObject)
    {
        disabledBossComponents.Clear();

        if (bossObject == null)
        {
            return;
        }

        MonoBehaviour[] components = bossObject.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < components.Length; i++)
        {
            MonoBehaviour component = components[i];

            if (component == null || !component.enabled)
            {
                continue;
            }

            if (!ShouldDisableBossComponentForIntro(component))
            {
                continue;
            }

            if (component is BossPatternController bossPatternController)
            {
                bossPatternController.SetExternalIntroPresentationOwnership(true);
            }

            component.enabled = false;
            disabledBossComponents.Add(component);
        }

        Rigidbody2D rb = bossObject.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private bool ShouldDisableBossComponentForIntro(MonoBehaviour component)
    {
        string typeName = component.GetType().Name;

        return typeName == "EnemyBaseAI" ||
               typeName == "EnemyAttackController" ||
               typeName == "BossPatternController";
    }

    private void EnableBossForBattle()
    {
        for (int i = 0; i < disabledBossComponents.Count; i++)
        {
            MonoBehaviour component = disabledBossComponents[i];

            if (component != null)
            {
                component.enabled = true;

                if (component is BossPatternController bossPatternController)
                {
                    bossPatternController.SetExternalIntroPresentationOwnership(false);
                }
            }
        }

        disabledBossComponents.Clear();

        if (spawnedBoss != null)
        {
            Rigidbody2D rb = spawnedBoss.GetComponent<Rigidbody2D>();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void SetIntroPhase(IntroPhase phase)
    {
        if (currentPhase == phase)
        {
            return;
        }

        currentPhase = phase;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logSequence)
        {
            Debug.Log($"[BossIntro] Phase -> {currentPhase}", this);
        }
#endif
    }

    private void TrackBossDeathForCleanup(GameObject bossObject)
    {
        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        if (!cleanupArenaObjectsOnBossDeath || bossObject == null)
        {
            return;
        }

        trackedBossHealth = bossObject.GetComponent<EnemyHealth>();

        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died += HandleTrackedBossDied;
        }
    }

    private void HandleTrackedBossDied(EnemyHealth health)
    {
        if (trackedBossHealth != null)
        {
            trackedBossHealth.Died -= HandleTrackedBossDied;
            trackedBossHealth = null;
        }

        DestroySpawnedWalls();
        DestroySpawnedManagerShips();
        DestroySpawnedRaiderCarriers();
        ClearRaiderCoverReferences();
    }

    private void DestroySpawnedWalls()
    {
        for (int i = spawnedWallObjects.Count - 1; i >= 0; i--)
        {
            GameObject wallObject = spawnedWallObjects[i];

            if (wallObject == null)
            {
                continue;
            }

            BossArenaLaserWall wall = wallObject.GetComponent<BossArenaLaserWall>();

            if (wall != null)
            {
                wall.Deactivate();
            }
            else
            {
                Destroy(wallObject);
            }
        }

        spawnedWallObjects.Clear();
    }

    private void DestroySpawnedManagerShips()
    {
        for (int i = spawnedManagerShips.Count - 1; i >= 0; i--)
        {
            LaserGuardianDrone managerShip = spawnedManagerShips[i];

            if (managerShip != null)
            {
                Destroy(managerShip.gameObject);
            }
        }

        spawnedManagerShips.Clear();
    }

    private void DestroySpawnedRaiderCarriers()
    {
        for (int i = spawnedRaiderCarriers.Count - 1; i >= 0; i--)
        {
            RaiderBarricadeCarrier carrier = spawnedRaiderCarriers[i];
            if (carrier != null)
            {
                carrier.Retire();
            }
        }

        spawnedRaiderCarriers.Clear();
    }

    private void ShowWarning()
    {
        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(activationWarningMessage, warningDuration);
            return;
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowWarning(activationWarningMessage);
        }
    }

    private IEnumerator PlayCoreActivationNoticeRoutine()
    {
        bool titlePlayed = false;

        EventTitleDirector director = coreActivationTitleDirector != null
            ? coreActivationTitleDirector
            : EventTitleDirector.Instance;

        if (useMotionTitleForCoreActivation && director != null)
        {
            director.Show(
                coreActivationTitleType,
                coreActivationTitle,
                string.IsNullOrWhiteSpace(encounterSignalSubtitleOverride)
                    ? coreActivationSubtitle
                    : encounterSignalSubtitleOverride
            );

            titlePlayed = true;
            coreActivationTitlePresented = true;
        }
        else if (fallbackToWarningMessageIfTitleMissing)
        {
            ShowWarning();
        }

        float waitTime = titlePlayed
            ? coreActivationTitleWait
            : delayAfterWarning;

        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void CloseCoreActivationPresentation()
    {
        if (!coreActivationTitlePresented)
        {
            return;
        }

        coreActivationTitlePresented = false;
        EventTitleDirector director = coreActivationTitleDirector != null
            ? coreActivationTitleDirector
            : EventTitleDirector.Instance;

        if (director != null)
        {
            director.StopCurrentAndClear();
        }
    }

    private IEnumerator AnimateCameraZoomOnlyRoutine(
        float targetMultiplier,
        float duration,
        AnimationCurve curve)
    {
        if (cameraZoomController == null)
        {
            ResolveReferences();
        }

        if (cameraZoomController == null)
        {
            yield break;
        }

        float startMultiplier = cameraZoomController.CurrentZoomMultiplier;
        float endMultiplier = Mathf.Max(0.1f, targetMultiplier);
        float safeDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            float eased = curve != null && curve.length > 0
                ? Mathf.Clamp01(curve.Evaluate(normalized))
                : Mathf.SmoothStep(0f, 1f, normalized);
            cameraZoomController.SetZoomMultiplier(
                Mathf.LerpUnclamped(startMultiplier, endMultiplier, eased),
                true
            );
            yield return null;
        }

        cameraZoomController.SetZoomMultiplier(endMultiplier, true);
    }

    private IEnumerator AnimateCameraHandoffToPlayerRoutine(Transform playerTarget)
    {
        if (gungeonCamera == null || cameraZoomController == null)
        {
            ResolveReferences();
        }

        float duration = Mathf.Max(0.05f, zoomInDuration);
        Vector3 startCameraCenter = gungeonCamera != null
            ? gungeonCamera.transform.position
            : playerTarget != null ? playerTarget.position : transform.position;
        float startZoomMultiplier = cameraZoomController != null
            ? cameraZoomController.CurrentZoomMultiplier
            : 1f;
        float endZoomMultiplier = cameraZoomController != null
            ? cameraZoomController.GameplayFramingMultiplier
            : 1f;

        if (gungeonCamera != null)
        {
            gungeonCamera.SetCinematicFocus(startCameraCenter, true);
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = zoomInCurve != null && zoomInCurve.length > 0
                ? Mathf.Clamp01(zoomInCurve.Evaluate(normalized))
                : Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 playerPosition = playerTarget != null && gungeonCamera != null
                ? gungeonCamera.ResolveGameplayFramingCenter(playerTarget.position)
                : playerTarget != null
                    ? playerTarget.position
                : startCameraCenter;

            if (cameraZoomController != null &&
                (resetCameraZoomOnBattleStart || returnCameraZoomAfterIntro))
            {
                float zoomMultiplier = Mathf.LerpUnclamped(
                    startZoomMultiplier,
                    endZoomMultiplier,
                    eased
                );
                cameraZoomController.SetEffectiveZoomMultiplier(zoomMultiplier, true);
            }

            if (gungeonCamera != null)
            {
                Vector3 focusPosition = Vector3.LerpUnclamped(
                    startCameraCenter,
                    playerPosition,
                    eased
                );
                gungeonCamera.SetCinematicFocus(focusPosition, true);
            }

            SyncBackgroundZoomForPlayerHandoff(eased);
            yield return null;
        }

        if (cameraZoomController != null &&
            (resetCameraZoomOnBattleStart || returnCameraZoomAfterIntro))
        {
            cameraZoomController.SetEffectiveZoomMultiplier(endZoomMultiplier, true);
        }

        if (gungeonCamera != null)
        {
            Vector3 finalPlayerPosition = playerTarget != null
                ? gungeonCamera.ResolveGameplayFramingCenter(playerTarget.position)
                : startCameraCenter;
            gungeonCamera.SetCinematicFocus(finalPlayerPosition, true);
        }

        CompleteBackgroundZoomForPlayerHandoff();

        yield return new WaitForEndOfFrame();
        yield return WaitForActualGameplayFramingRoutine(playerTarget, true);

        if (ShouldAbortIntro())
        {
            yield break;
        }

        if (gungeonCamera != null)
        {
            gungeonCamera.ClearCinematicFocus(true);
        }

        yield return new WaitForEndOfFrame();
        yield return WaitForActualGameplayFramingRoutine(playerTarget, false);
    }

    private IEnumerator WaitForActualGameplayFramingRoutine(
        Transform playerTarget,
        bool requireCinematicFocusAtTarget)
    {
        if (playerTarget == null)
        {
            yield break;
        }

        float timeout = Mathf.Max(0.05f, gameplayFramingSettleTimeout);
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            bool cameraSettled = gungeonCamera == null ||
                gungeonCamera.IsAtGameplayFraming(
                    playerTarget,
                    gameplayFramingPositionTolerance,
                    requireCinematicFocusAtTarget
                );
            bool zoomSettled = cameraZoomController == null ||
                cameraZoomController.IsAtGameplayZoomWithin(gameplayFramingZoomTolerance);

            if (cameraSettled && zoomSettled)
            {
                yield break;
            }

            yield return new WaitForEndOfFrame();
            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
        }

        if (cameraZoomController != null &&
            (resetCameraZoomOnBattleStart || returnCameraZoomAfterIntro))
        {
            cameraZoomController.SetEffectiveZoomMultiplier(
                cameraZoomController.GameplayFramingMultiplier,
                true
            );
        }

        if (gungeonCamera != null)
        {
            if (requireCinematicFocusAtTarget)
            {
                gungeonCamera.SetCinematicFocus(
                    gungeonCamera.ResolveGameplayFramingCenter(playerTarget.position),
                    true
                );
            }
            else
            {
                gungeonCamera.SnapToPlayer();
            }
        }

        yield return new WaitForEndOfFrame();
    }

    private void SyncBackgroundZoomForPlayerHandoff(float normalizedProgress)
    {
        if (!syncStarfieldScaleWithCameraZoom || spaceBackgroundGenerator == null ||
            !spaceBackgroundGenerator.IsCameraZoomTransitionActive)
        {
            return;
        }

        spaceBackgroundGenerator.SetCameraZoomTransitionProgress(
            1f - Mathf.Clamp01(normalizedProgress)
        );
    }

    private void CompleteBackgroundZoomForPlayerHandoff()
    {
        if (spaceBackgroundGenerator == null ||
            !spaceBackgroundGenerator.IsCameraZoomTransitionActive)
        {
            return;
        }

        spaceBackgroundGenerator.SetCameraZoomTransitionProgress(0f);
        spaceBackgroundGenerator.EndCameraZoomTransition(true);
        spaceBackgroundGenerator.ForceSyncNow();
    }

    private Transform ResolvePlayerCameraTarget(GameObject interactor)
    {
        if (playerLockState.rb != null)
        {
            return playerLockState.rb.transform;
        }

        Rigidbody2D playerRigidbody = interactor != null
            ? interactor.GetComponentInParent<Rigidbody2D>()
            : null;
        return playerRigidbody != null
            ? playerRigidbody.transform
            : interactor != null ? interactor.transform : null;
    }

    private void PrepareRaiderBattleCameraProfile()
    {
        if (!useRaiderIntroVariant || spawnedBoss == null)
        {
            return;
        }

        PirateCommanderBossController commander =
            spawnedBoss.GetComponent<PirateCommanderBossController>();
        if (commander != null)
        {
            commander.PrepareBattleCameraProfile();
        }
    }

    private void ReleaseRaiderBattleCameraProfile(bool immediate)
    {
        if (spawnedBoss == null)
        {
            return;
        }

        PirateCommanderBossController commander =
            spawnedBoss.GetComponent<PirateCommanderBossController>();
        if (commander != null)
        {
            commander.ReleaseBattleCameraProfile(immediate);
        }
    }

    private bool ShouldAbortIntro()
    {
        return introCancellationRequested ||
               !isActiveAndEnabled ||
               (RunManager.Instance != null && RunManager.Instance.IsCompletingRun);
    }

    private void BindRunEnd()
    {
        RunManager currentRunManager = RunManager.Instance;
        if (observedRunManager == currentRunManager)
        {
            return;
        }

        UnbindRunEnd();
        observedRunManager = currentRunManager;
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnbindRunEnd()
    {
        if (observedRunManager == null)
        {
            return;
        }

        observedRunManager.RunEnded -= HandleRunEnded;
        observedRunManager = null;
    }

    private void HandleRunEnded(RunResultData _)
    {
        introCancellationRequested = true;
        StopAllCoroutines();
        if (IsSalvageDevourerIntroRuntimeOwned())
        {
            CleanupSalvageDevourerIntro(true);
        }
        CloseCoreActivationPresentation();
        ReleaseIntroWideZoomHold(false);
        ReleaseRaiderBattleCameraProfile(true);
        DestroySpawnedRaiderCarriers();
        DestroySpawnedWalls();
        DestroySpawnedManagerShips();
        ClearRaiderCoverReferences();
        ReleaseCameraInputOffsetLock();

        if (gungeonCamera != null)
        {
            gungeonCamera.CancelCinematicFocusBlend();
            gungeonCamera.ClearCinematicFocus(true);
        }
    }

    private void CacheBossPresentation(GameObject bossObject)
    {
        spawnedBossAnimator = null;
        spawnedBossVisualRoot = null;
        spawnedBossBaseScale = Vector3.one;

        if (bossObject == null)
        {
            return;
        }

        spawnedBossAnimator = bossObject.GetComponentInChildren<Animator>(true);
        SpriteRenderer bodyRenderer = bossObject.GetComponentInChildren<SpriteRenderer>(true);
        spawnedBossVisualRoot = bodyRenderer != null
            ? bodyRenderer.transform
            : bossObject.transform;
        spawnedBossBaseScale = spawnedBossVisualRoot.localScale;
    }

    private void TriggerBossAnimator(string triggerName)
    {
        if (!useBossAnimatorTriggers ||
            spawnedBossAnimator == null ||
            string.IsNullOrWhiteSpace(triggerName) ||
            !HasAnimatorParameter(spawnedBossAnimator, triggerName, AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        spawnedBossAnimator.ResetTrigger(triggerName);
        spawnedBossAnimator.SetTrigger(triggerName);
    }

    private static bool HasAnimatorParameter(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator PlayBossRevealScaleRoutine()
    {
        if (spawnedBoss == null)
        {
            yield break;
        }

        Transform bossTransform = spawnedBossVisualRoot != null
            ? spawnedBossVisualRoot
            : spawnedBoss.transform;
        Vector3 baseScale = spawnedBossBaseScale;

        if (!useFallbackBossRevealScale)
        {
            if (bossRevealShakeDuration > 0f && bossRevealShakeAmplitude > 0f)
            {
                GungeonStyleCamera2D.RequestShake(
                    bossRevealShakeAmplitude,
                    bossRevealShakeDuration
                );
            }

            yield break;
        }

        float duration = Mathf.Max(0.05f, bossRevealScaleDuration);
        float firstPhaseDuration = duration * 0.65f;
        float secondPhaseDuration = Mathf.Max(0.01f, duration - firstPhaseDuration);
        Vector3 startScale = baseScale * Mathf.Clamp(bossRevealStartScale, 0.05f, 1f);
        Vector3 overshootScale = baseScale * Mathf.Max(1f, bossRevealOvershootScale);

        bossTransform.localScale = startScale;

        float elapsed = 0f;

        while (elapsed < firstPhaseDuration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, firstPhaseDuration));
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            bossTransform.localScale = Vector3.LerpUnclamped(startScale, overshootScale, eased);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < secondPhaseDuration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / secondPhaseDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            bossTransform.localScale = Vector3.LerpUnclamped(overshootScale, baseScale, eased);
            yield return null;
        }

        bossTransform.localScale = baseScale;

        if (bossRevealShakeDuration > 0f && bossRevealShakeAmplitude > 0f)
        {
            GungeonStyleCamera2D.RequestShake(
                bossRevealShakeAmplitude,
                bossRevealShakeDuration
            );
        }
    }

    private void PrepareBackgroundForWideZoom()
    {
        if (!prepareBackgroundBeforeWideZoom && !syncStarfieldScaleWithCameraZoom)
        {
            return;
        }

        if (spaceBackgroundGenerator == null)
        {
            ResolveReferences();
        }

        if (spaceBackgroundGenerator != null)
        {
            spaceBackgroundGenerator.BeginCameraZoomTransition(ResolveIntroWideZoomMultiplier());
        }
    }

    private IEnumerator AnimateCameraAndBackgroundZoomRoutine(
        float targetMultiplier,
        float duration,
        AnimationCurve curve,
        bool zoomingOut)
    {
        if (cameraZoomController == null)
        {
            ResolveReferences();
        }

        if (cameraZoomController == null)
        {
            Debug.LogWarning(
                "CameraZoomController2D를 찾지 못했습니다. GameplayCameraRig에 CameraZoomController2D를 연결하세요.",
                this
            );

            if (spaceBackgroundGenerator != null &&
                spaceBackgroundGenerator.IsCameraZoomTransitionActive)
            {
                spaceBackgroundGenerator.SetCameraZoomTransitionProgress(zoomingOut ? 1f : 0f);

                if (!zoomingOut)
                {
                    spaceBackgroundGenerator.EndCameraZoomTransition(true);
                }
            }

            yield break;
        }

        float startMultiplier = cameraZoomController.CurrentZoomMultiplier;
        float endMultiplier = Mathf.Max(0.1f, targetMultiplier);
        float safeDuration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float normalized = Mathf.Clamp01(elapsed / safeDuration);
            float eased = curve != null && curve.length > 0
                ? Mathf.Clamp01(curve.Evaluate(normalized))
                : Mathf.SmoothStep(0f, 1f, normalized);
            float currentMultiplier = Mathf.LerpUnclamped(
                startMultiplier,
                endMultiplier,
                eased
            );
            cameraZoomController.SetZoomMultiplier(currentMultiplier, true);

            if (syncStarfieldScaleWithCameraZoom &&
                spaceBackgroundGenerator != null &&
                spaceBackgroundGenerator.IsCameraZoomTransitionActive)
            {
                float backgroundProgress = zoomingOut ? eased : 1f - eased;
                spaceBackgroundGenerator.SetCameraZoomTransitionProgress(backgroundProgress);
            }

            yield return null;
        }

        cameraZoomController.SetZoomMultiplier(endMultiplier, true);

        if (spaceBackgroundGenerator != null &&
            spaceBackgroundGenerator.IsCameraZoomTransitionActive)
        {
            if (zoomingOut)
            {
                spaceBackgroundGenerator.SetCameraZoomTransitionProgress(1f);
            }
            else
            {
                spaceBackgroundGenerator.SetCameraZoomTransitionProgress(0f);
                spaceBackgroundGenerator.EndCameraZoomTransition(true);
                spaceBackgroundGenerator.ForceSyncNow();
            }
        }
    }

    private void HoldIntroWideZoom()
    {
        if (cameraZoomController == null)
        {
            ResolveReferences();
        }

        if (cameraZoomController == null || introWideZoomHoldActive)
        {
            return;
        }

        cameraZoomController.BeginCinematicZoomHold(ResolveIntroWideZoomMultiplier());
        introWideZoomHoldActive = true;
    }

    private void ReleaseIntroWideZoomHold(bool keepCurrentZoom)
    {
        if (!introWideZoomHoldActive)
        {
            return;
        }

        if (cameraZoomController != null)
        {
            cameraZoomController.EndCinematicZoomHold(keepCurrentZoom);
        }

        introWideZoomHoldActive = false;
    }

    private void ResetCameraZoom()
    {
        if (cameraZoomController != null)
        {
            cameraZoomController.ClearCinematicZoomHold(false);
            introWideZoomHoldActive = false;
            cameraZoomController.CancelCinematicTransition(true);
            cameraZoomController.ResetZoom(true);
        }

        if (spaceBackgroundGenerator != null)
        {
            spaceBackgroundGenerator.EndCameraZoomTransition(true);
            spaceBackgroundGenerator.ForceSyncNow();
        }
    }

    private void LockPlayer(GameObject interactor)
    {
        if (interactor == null)
        {
            return;
        }

        playerLockState = new PlayerLockState
        {
            hasState = true
        };

        playerLockState.rb = interactor.GetComponent<Rigidbody2D>();

        if (playerLockState.rb != null)
        {
            playerLockState.rb.linearVelocity = Vector2.zero;
            playerLockState.rb.angularVelocity = 0f;
        }

        playerLockState.controller = interactor.GetComponent<PlayerController2D>();

        if (playerLockState.controller != null)
        {
            playerLockState.controllerControlWasEnabled = playerLockState.controller.ControlEnabled;
            playerLockState.controllerMovementWasLocked = playerLockState.controller.MovementLocked;

            playerLockState.controller.SetControlEnabled(false);
            playerLockState.controller.SetMovementLocked(true);
        }

        playerLockState.dash = interactor.GetComponent<PlayerDash>();

        if (playerLockState.dash != null)
        {
            playerLockState.dashWasEnabled = playerLockState.dash.enabled;
            playerLockState.dash.enabled = false;
        }

        playerLockState.weaponController = interactor.GetComponent<PlayerWeaponController>();

        if (playerLockState.weaponController != null)
        {
            playerLockState.weaponWasEnabled = playerLockState.weaponController.enabled;
            playerLockState.weaponExternalInputWasLocked = playerLockState.weaponController.ExternalInputLocked;
            playerLockState.weaponController.SetExternalInputLocked(true);
        }

        playerLockState.interactor = interactor.GetComponent<PlayerInteractor>();

        if (playerLockState.interactor != null)
        {
            playerLockState.interactorWasEnabled = playerLockState.interactor.enabled;
            playerLockState.interactor.enabled = false;
        }

        playerLockState.radarScanner = interactor.GetComponent("PlayerRadarScanner") as MonoBehaviour;

        if (playerLockState.radarScanner != null)
        {
            playerLockState.radarWasEnabled = playerLockState.radarScanner.enabled;
            playerLockState.radarScanner.enabled = false;
        }

        playerLockState.emergencyReturnController = interactor.GetComponent<EmergencyReturnController>();

        if (playerLockState.emergencyReturnController != null)
        {
            playerLockState.emergencyReturnWasEnabled = playerLockState.emergencyReturnController.enabled;
            playerLockState.emergencyReturnController.enabled = false;
        }

        extraDisabledPlayerComponents.Clear();

        if (extraPlayerComponentsToDisable == null)
        {
            return;
        }

        for (int i = 0; i < extraPlayerComponentsToDisable.Length; i++)
        {
            MonoBehaviour component = extraPlayerComponentsToDisable[i];

            if (component == null || component == this)
            {
                continue;
            }

            extraDisabledPlayerComponents.Add(new ComponentEnabledState
            {
                component = component,
                wasEnabled = component.enabled
            });

            component.enabled = false;
        }
    }

    private void RestorePlayer()
    {
        if (!playerLockState.hasState)
        {
            return;
        }

        if (playerLockState.rb != null)
        {
            playerLockState.rb.linearVelocity = Vector2.zero;
            playerLockState.rb.angularVelocity = 0f;
        }

        if (playerLockState.controller != null)
        {
            playerLockState.controller.SetControlEnabled(playerLockState.controllerControlWasEnabled);
            playerLockState.controller.SetMovementLocked(playerLockState.controllerMovementWasLocked);
        }

        if (playerLockState.dash != null)
        {
            playerLockState.dash.enabled = playerLockState.dashWasEnabled;
        }

        if (playerLockState.weaponController != null)
        {
            playerLockState.weaponController.enabled = playerLockState.weaponWasEnabled;
            playerLockState.weaponController.SetExternalInputLocked(playerLockState.weaponExternalInputWasLocked);
        }

        if (playerLockState.interactor != null)
        {
            playerLockState.interactor.enabled = playerLockState.interactorWasEnabled;
        }

        if (playerLockState.radarScanner != null)
        {
            playerLockState.radarScanner.enabled = playerLockState.radarWasEnabled;
        }

        if (playerLockState.emergencyReturnController != null)
        {
            playerLockState.emergencyReturnController.enabled = playerLockState.emergencyReturnWasEnabled;
        }

        for (int i = 0; i < extraDisabledPlayerComponents.Count; i++)
        {
            ComponentEnabledState state = extraDisabledPlayerComponents[i];

            if (state.component != null)
            {
                state.component.enabled = state.wasEnabled;
            }
        }

        extraDisabledPlayerComponents.Clear();
        playerLockState = new PlayerLockState();
    }

    private void ApplyIntroInvincibility(GameObject interactor)
    {
        if (!makePlayerInvincibleDuringIntro || interactor == null)
        {
            return;
        }

        PlayerHealth playerHealth = interactor.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            return;
        }

        float duration =
            warningDuration +
            delayAfterWarning +
            coreFocusZoomDuration +
            coreFocusSettleDuration +
            delayAfterCorePulse +
            coreActivationTitleWait +
            zoomOutDuration +
            managerShipMoveDuration +
            bossArrivalDuration +
            delayBeforeWallActivation +
            delayAfterWallActivation +
            bossRevealScaleDuration +
            bossHealthBarLeadTime +
            zoomInDuration +
            playerInvincibleExtraTime;

        playerHealth.AddInvincibleTime(duration);
    }

    private void FaceTransformToTarget(Transform target, Vector3 targetPosition, float rotationOffset)
    {
        if (target == null)
        {
            return;
        }

        Vector2 direction = targetPosition - target.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        target.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    private IEnumerator Wait(float duration)
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            if (ShouldAbortIntro())
            {
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + (Vector3)arenaCenterOffset;
        Vector2 half = GetEffectiveHalfExtents();

        Vector3 bottomLeft = center + new Vector3(-half.x, -half.y, 0f);
        Vector3 topLeft = center + new Vector3(-half.x, half.y, 0f);
        Vector3 topRight = center + new Vector3(half.x, half.y, 0f);
        Vector3 bottomRight = center + new Vector3(half.x, -half.y, 0f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(bottomLeft, 0.35f);
        Gizmos.DrawWireSphere(topLeft, 0.35f);
        Gizmos.DrawWireSphere(topRight, 0.35f);
        Gizmos.DrawWireSphere(bottomRight, 0.35f);
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        wideZoomMultiplier = Mathf.Max(1f, wideZoomMultiplier);
        raiderArenaSizeMultiplier = Mathf.Clamp(raiderArenaSizeMultiplier, 0.5f, 1f);
        zoomOutDuration = Mathf.Max(0.05f, zoomOutDuration);
        zoomInDuration = Mathf.Max(0.05f, zoomInDuration);
        coreFocusZoomDuration = Mathf.Max(0.05f, coreFocusZoomDuration);
        bossRevealFocusPanDuration = Mathf.Max(0.05f, bossRevealFocusPanDuration);
        bossRevealScaleDuration = Mathf.Max(0.05f, bossRevealScaleDuration);

        if (zoomOutCurve == null || zoomOutCurve.length == 0)
        {
            zoomOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (zoomInCurve == null || zoomInCurve.length == 0)
        {
            zoomInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (coreFocusZoomCurve == null || coreFocusZoomCurve.length == 0)
        {
            coreFocusZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (bossRevealFocusPanCurve == null || bossRevealFocusPanCurve.length == 0)
        {
            bossRevealFocusPanCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

    }
#endif

}
