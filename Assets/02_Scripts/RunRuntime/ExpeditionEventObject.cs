using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public enum ExpeditionEventType
{
    RescueSignal,
    UnknownDevice,
    UnstableReactor,
    BlackBoxRecovery
}

public enum ExpeditionEventState
{
    Idle,
    Arming,
    Active,
    Completed,
    Failed
}

[RequireComponent(typeof(Collider2D))]
public class ExpeditionEventObject : MonoBehaviour, IInteractable
{
    private enum CombatPhase
    {
        None,
        RescueWave1,
        RescueWave2,
        UnknownCombat,
        ReactorFailure
    }

    [Header("Event")]
    [SerializeField] private ExpeditionEventType eventType = ExpeditionEventType.RescueSignal;
    [SerializeField] private ExpeditionEventState state = ExpeditionEventState.Idle;
    [SerializeField] private bool resetOnEnable = true;

    [Header("High-Value Objective")]
    [SerializeField] private bool countsAsHighValueObjective = true;
    [SerializeField] private int objectiveSignalAmount = 1;
    [SerializeField] private string objectiveId;

    [Header("Interaction Text")]
    [SerializeField] private string rescueText = "구조 신호 조사";
    [SerializeField] private string unknownText = "미확인 장치 조사";
    [SerializeField] private string reactorText = "불안정 원자로 가동";
    [SerializeField] private string blackBoxText = "블랙박스 회수";
    [SerializeField] private string armingText = "분석 중";
    [SerializeField] private string activeText = "이벤트 진행 중";
    [SerializeField] private string completedText = "이벤트 완료";
    [SerializeField] private string failedText = "이벤트 실패";

    [Header("Rewards - RewardDropper로 바닥에 드랍")]
    [SerializeField] private RewardDropper rewardDropper;
    [SerializeField] private Transform rewardDropPoint;
    [SerializeField] private float rewardDropOffset = 0.75f;
    [SerializeField] private RewardDefinition rescueReward;
    [SerializeField] private RewardDefinition unknownSafeReward;
    [SerializeField] private RewardDefinition unknownCombatReward;
    [SerializeField] private RewardDefinition reactorReward;
    [SerializeField] private RewardDefinition blackBoxReward;

    [Header("RewardDropper Event Presets")]
    [Tooltip("켜두면 이벤트 보상을 드랍하기 직전에 RewardDropper의 조각 수/흩뿌림 값을 이벤트별로 자동 조정합니다.")]
    [SerializeField] private bool applyRewardDropperPresetBeforeDrop = true;
    [SerializeField] private RewardDropperDropPreset rescueDropPreset = RewardDropperDropPreset.EventRescueSignal;
    [SerializeField] private RewardDropperDropPreset unknownSafeDropPreset = RewardDropperDropPreset.EventUnknownSafe;
    [SerializeField] private RewardDropperDropPreset unknownCombatDropPreset = RewardDropperDropPreset.EventUnknownCombat;
    [SerializeField] private RewardDropperDropPreset reactorDropPreset = RewardDropperDropPreset.EventUnstableReactor;
    [SerializeField] private RewardDropperDropPreset blackBoxDropPreset = RewardDropperDropPreset.EventBlackBox;

    [Header("Enemy Definitions")]
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;
    [SerializeField] private EnemyDefinition chargingEnemyDefinition;

    [Header("Enemy Prefab Fallback")]
    [SerializeField] private GameObject basicEnemyPrefabFallback;
    [SerializeField] private GameObject shotgunEnemyPrefabFallback;
    [SerializeField] private GameObject chargingEnemyPrefabFallback;

    [Header("Rescue Signal")]
    [SerializeField] private int rescueWave1Basic = 2;
    [SerializeField] private int rescueWave1Shotgun = 1;
    [SerializeField] private int rescueWave2Basic = 2;
    [SerializeField] private int rescueWave2Shotgun;
    [SerializeField] private float rescueWave2Delay = 0.75f;

    [Header("Unknown Device")]
    [SerializeField] private float unknownChargeTime = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float unknownSafeChance = 0.5f;
    [SerializeField] private int unknownCombatBasic = 2;
    [SerializeField] private int unknownCombatShotgun = 1;

    [Header("Unstable Reactor")]
    [SerializeField] private ExpeditionEventDamageReceiver reactorDamageReceiver;
    [SerializeField] private float reactorChargeTime = 0.75f;
    [SerializeField] private float reactorMaxHp = 24f;
    [SerializeField] private float reactorTimeLimit = 12f;
    [SerializeField] private int reactorFailureBasic = 2;
    [SerializeField] private int reactorFailureCharging = 1;

    [Header("Black Box Recovery")]
    [SerializeField] private float blackBoxDuration = 18f;
    [SerializeField] private float blackBoxFirstWaveDelay = 1f;
    [SerializeField] private float blackBoxWaveInterval = 6f;
    [SerializeField] private int blackBoxMaxWaves = 3;
    [SerializeField] private int blackBoxBasicPerWave = 2;
    [SerializeField] private int blackBoxShotgunPerWave = 1;
    [SerializeField] private bool blackBoxSpawnAroundPlayer = true;

    [Header("Spawn")]
    [SerializeField] private float spawnRadiusMin = 2.5f;
    [SerializeField] private float spawnRadiusMax = 4.5f;
    [SerializeField] private bool alertSpawnedEnemies = true;
    [SerializeField] private bool suppressSpawnedEnemyNormalRewards = true;
    [SerializeField] private float deepZoneEnemyHpMultiplier = 1.2f;

    [Header("Enemy Reinforcement Arrival")]
    [Tooltip("켜두면 이벤트 증원 적이 도착 지점에 즉시 생성되지 않고, 화면 밖에서 빠르게 진입합니다.")]
    [SerializeField] private bool useArrivalSpawn = true;

    [Tooltip("기존 프리팹에 낮은 Spawn Radius 값이 들어 있어도 이벤트별 최소 도착 반경을 보장합니다.")]
    [SerializeField] private bool useEventDefaultArrivalRadiusFloor = true;

    [Tooltip("도착 지점에 표시할 경고 원 프리팹. 비워두면 런타임 LineRenderer 빨간 원을 자동 생성합니다.")]
    [SerializeField] private GameObject arrivalMarkerPrefab;

    [Tooltip("적이 도착했을 때 재생할 착지 이펙트 프리팹. 비워두면 짧은 빨간 충격파 원을 자동 생성합니다.")]
    [SerializeField] private GameObject arrivalImpactPrefab;

    [SerializeField] private bool createFallbackArrivalMarker = true;
    [SerializeField] private bool createFallbackLandingBurst = true;
    [SerializeField] private float arrivalWarningTime = 0.75f;
    [SerializeField] private float arrivalTravelTime = 0.35f;
    [SerializeField] private float arrivalReadyDelay = 0.25f;
    [SerializeField] private float offscreenEntryDistance = 10.5f;
    [SerializeField] private bool preferOffscreenEntry = true;
    [SerializeField] private float arrivalMarkerRadius = 0.85f;
    [SerializeField] private Color arrivalMarkerColor = new Color(1f, 0.05f, 0.03f, 0.9f);

    [Header("Enemy Reinforcement Afterimage")]
    [SerializeField] private bool useEnemyArrivalAfterimages = true;
    [SerializeField] private float enemyAfterimageInterval = 0.055f;
    [SerializeField] private float enemyAfterimageLifetime = 0.22f;
    [SerializeField] private Color enemyAfterimageColor = new Color(1f, 0.18f, 0.12f, 0.35f);
    [SerializeField] private int enemyAfterimageSortingOrderOffset = -1;
    [SerializeField] private bool disableEnemyCollidersDuringArrival = true;

    [Header("Visual / Radar")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Transform animatedRoot;
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private Transform ringRoot;
    [SerializeField] private SpriteRenderer ringRenderer;
    [SerializeField] private Transform progressRoot;
    [SerializeField] private SpriteRenderer progressRenderer;
    [SerializeField] private bool hideVisualOnFinished = true;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private bool autoConfigureRadarTarget = true;

    [Header("Visual Root Safety")]
    [Tooltip("이벤트의 Collider/상호작용 Root가 부유하지 않도록 Animated Root가 자기 Transform을 사용하지 못하게 합니다.")]
    [SerializeField] private bool preventAnimatedRootFromUsingEventRoot = true;
    [SerializeField] private string visualRootChildName = "VisualRoot";

    [Header("DOTween")]
    [SerializeField] private bool useTween = true;
    [SerializeField] private float idleFloatY = 0.1f;
    [SerializeField] private float idleFloatDuration = 1.2f;
    [SerializeField] private float idlePulseScale = 1.05f;
    [SerializeField] private float ringStartScale = 0.3f;
    [SerializeField] private float ringEndScale = 2.2f;
    [SerializeField] private float ringDuration = 0.6f;
    [SerializeField] private float finishShrinkDuration = 0.22f;

    [Header("Colors")]
    [SerializeField] private Color infoColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.15f, 1f);
    [SerializeField] private Color successColor = new Color(0.4f, 1f, 0.45f, 1f);
    [SerializeField] private Color failColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color hitFlashColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool logFlow;

    private readonly List<EnemyHealth> trackedEnemies = new List<EnemyHealth>();

    private Collider2D eventCollider;
    private ExpeditionHUD hud;
    private Transform player;
    private Coroutine routine;
    private Coroutine phaseClearRoutine;
    private CombatPhase phase = CombatPhase.None;

    private float reactorHp;
    private float reactorTimer;
    private float blackBoxTimer;
    private float blackBoxWaveTimer;
    private int blackBoxWaveCount;
    private bool objectiveRegistered;

    private Vector3 baseLocalPos;
    private Vector3 baseLocalScale = Vector3.one;
    private Color baseCoreColor = Color.white;
    private Tween idleMoveTween;
    private Tween idleScaleTween;
    private Sequence ringSequence;
    private Sequence progressSequence;

    public ExpeditionEventType EventType => eventType;
    public ExpeditionEventState State => state;
    public bool CanReceiveEventDamage => eventType == ExpeditionEventType.UnstableReactor && state == ExpeditionEventState.Active;

    public string InteractionText
    {
        get
        {
            if (state == ExpeditionEventState.Completed)
            {
                return completedText;
            }

            if (state == ExpeditionEventState.Failed)
            {
                return failedText;
            }

            if (state == ExpeditionEventState.Arming)
            {
                return armingText;
            }

            if (state == ExpeditionEventState.Active)
            {
                return activeText;
            }

            return GetIdleText();
        }
    }

    private void Reset()
    {
        eventCollider = GetComponent<Collider2D>();
        rewardDropper = GetComponent<RewardDropper>();
        radarTarget = GetComponent<RadarTarget>();
        reactorDamageReceiver = GetComponentInChildren<ExpeditionEventDamageReceiver>(true);

        Transform childVisual = transform.Find(visualRootChildName);
        if (childVisual != null)
        {
            visualRoot = childVisual.gameObject;
            animatedRoot = childVisual;
        }
        else
        {
            visualRoot = gameObject;
            animatedRoot = preventAnimatedRootFromUsingEventRoot ? null : transform;
        }

        if (eventCollider != null)
        {
            eventCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        eventCollider = GetComponent<Collider2D>();

        if (eventCollider != null)
        {
            eventCollider.isTrigger = true;
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (reactorDamageReceiver == null)
        {
            reactorDamageReceiver = GetComponentInChildren<ExpeditionEventDamageReceiver>(true);
        }

        ResolveVisualRoots();

        if (reactorDamageReceiver != null)
        {
            reactorDamageReceiver.Bind(this);
            reactorDamageReceiver.SetDamageEnabled(false);
        }

        CaptureBaseVisual();

        if (autoConfigureRadarTarget)
        {
            ConfigureRadarTarget();
        }
    }

    private void OnEnable()
    {
        if (resetOnEnable)
        {
            ResetRuntime();
        }
    }

    private void Start()
    {
        ResolvePlayer();
        hud = FindFirstObjectByType<ExpeditionHUD>();
    }

    private void Update()
    {
        if (state != ExpeditionEventState.Active)
        {
            return;
        }

        if (phase != CombatPhase.None)
        {
            CleanupTrackedEnemies();

            if (trackedEnemies.Count <= 0)
            {
                TryClearCombatPhase();
            }
        }

        if (eventType == ExpeditionEventType.UnstableReactor && CanReceiveEventDamage)
        {
            UpdateReactorTimer();
        }

        if (eventType == ExpeditionEventType.BlackBoxRecovery)
        {
            UpdateBlackBox();
        }
    }

    private void OnDisable()
    {
        StopAllEventCoroutines();
        UntrackAllEnemies();
        KillTweens(false);
    }

    private void OnDestroy()
    {
        KillTweens(false);
    }

    public bool CanInteract(GameObject interactor)
    {
        return state == ExpeditionEventState.Idle &&
               RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        if (interactor != null)
        {
            player = interactor.transform;
        }

        switch (eventType)
        {
            case ExpeditionEventType.RescueSignal:
                StartRescue();
                break;

            case ExpeditionEventType.UnknownDevice:
                StartRoutine(UnknownRoutine());
                break;

            case ExpeditionEventType.UnstableReactor:
                StartRoutine(ReactorRoutine());
                break;

            case ExpeditionEventType.BlackBoxRecovery:
                StartBlackBox();
                break;
        }
    }

    public void ReceiveEventDamage(float damage)
    {
        if (!CanReceiveEventDamage || damage <= 0f)
        {
            return;
        }

        reactorHp = Mathf.Max(0f, reactorHp - damage);
        SetProgress(Mathf.Min(GetReactorHpRatio(), GetReactorTimeRatio()), warningColor);
        FlashCore();

        if (reactorHp <= 0f)
        {
            CompleteReactor();
        }
    }

    private void StartRescue()
    {
        BeginEvent("구조 신호", "적을 소탕하고 보급 캡슐을 회수한다.", infoColor);

        state = ExpeditionEventState.Active;
        phase = CombatPhase.RescueWave1;

        Message("구조 신호 확인. 1차 적 접근 중.");

        SpawnBatch(basicEnemyDefinition, basicEnemyPrefabFallback, rescueWave1Basic, true, false);
        SpawnBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, rescueWave1Shotgun, true, false);

        TryCompleteIfNoEnemies();
    }

    private IEnumerator UnknownRoutine()
    {
        BeginEvent("미확인 장치", "장치 반응을 분석한다.", infoColor);

        Message("미확인 장치 분석 중...");
        PlayProgressLoop(infoColor);

        yield return new WaitForSeconds(Mathf.Max(0.05f, unknownChargeTime));

        HideProgress();

        if (Random.value <= unknownSafeChance)
        {
            Message("장치 안정화 성공. 보상을 회수해라.");
            DropReward(unknownSafeReward);
            Finish(false);
            yield break;
        }

        state = ExpeditionEventState.Active;
        phase = CombatPhase.UnknownCombat;

        Pulse(warningColor);
        Message("불안정 반응. 적 증원 발생.");
        AudioManager.PlayAt(SoundEventIds.EnemyAlert, transform.position);

        SpawnBatch(basicEnemyDefinition, basicEnemyPrefabFallback, unknownCombatBasic, true, false);
        SpawnBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, unknownCombatShotgun, true, false);

        TryCompleteIfNoEnemies();
    }

    private IEnumerator ReactorRoutine()
    {
        BeginEvent("불안정 원자로", "제한 시간 안에 파괴한다.", warningColor);

        Message("원자로 기동 중...");
        PlayProgressLoop(warningColor);

        yield return new WaitForSeconds(Mathf.Max(0.05f, reactorChargeTime));

        state = ExpeditionEventState.Active;
        phase = CombatPhase.None;

        reactorHp = Mathf.Max(1f, reactorMaxHp);
        reactorTimer = Mathf.Max(1f, reactorTimeLimit);

        SetDamageEnabled(true);
        SetProgress(1f, warningColor);

        Message("원자로 폭주 시작. 공격해서 안정화해라.");
    }

    private void StartBlackBox()
    {
        BeginEvent("블랙박스 회수", "복구가 끝날 때까지 추적을 버틴다.", infoColor);

        state = ExpeditionEventState.Active;
        phase = CombatPhase.None;

        blackBoxTimer = Mathf.Max(1f, blackBoxDuration);
        blackBoxWaveTimer = Mathf.Max(0f, blackBoxFirstWaveDelay);
        blackBoxWaveCount = 0;

        PlayProgressLoop(infoColor);
        SetProgress(0f, infoColor);
        Message("블랙박스 회수 시작. 추적 신호 감지.");
    }

    private void UpdateReactorTimer()
    {
        reactorTimer -= Time.deltaTime;
        SetProgress(Mathf.Min(GetReactorHpRatio(), GetReactorTimeRatio()), warningColor);

        if (reactorTimer <= 0f)
        {
            FailReactor();
        }
    }

    private float GetReactorHpRatio()
    {
        return reactorMaxHp <= 0f ? 0f : Mathf.Clamp01(reactorHp / reactorMaxHp);
    }

    private float GetReactorTimeRatio()
    {
        return reactorTimeLimit <= 0f ? 0f : Mathf.Clamp01(reactorTimer / reactorTimeLimit);
    }

    private void UpdateBlackBox()
    {
        blackBoxTimer -= Time.deltaTime;
        blackBoxWaveTimer -= Time.deltaTime;

        float progress01 = blackBoxDuration <= 0f ? 1f : 1f - blackBoxTimer / blackBoxDuration;
        SetProgress(progress01, infoColor);

        if (blackBoxWaveCount < blackBoxMaxWaves && blackBoxWaveTimer <= 0f)
        {
            blackBoxWaveCount++;

            Message($"추적 웨이브 {blackBoxWaveCount}/{Mathf.Max(1, blackBoxMaxWaves)}");
            AudioManager.PlayAt(SoundEventIds.EnemyAlert, GetSpawnCenter(blackBoxSpawnAroundPlayer));

            SpawnBatch(basicEnemyDefinition, basicEnemyPrefabFallback, blackBoxBasicPerWave, false, blackBoxSpawnAroundPlayer);
            SpawnBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, blackBoxShotgunPerWave, false, blackBoxSpawnAroundPlayer);

            blackBoxWaveTimer = Mathf.Max(0.1f, blackBoxWaveInterval);
        }

        if (blackBoxTimer <= 0f)
        {
            Message("블랙박스 복구 완료. 보상을 회수해라.");
            DropReward(blackBoxReward);
            Finish(false);
        }
    }

    private void CompleteReactor()
    {
        if (state != ExpeditionEventState.Active)
        {
            return;
        }

        SetDamageEnabled(false);
        HideProgress();
        Pulse(successColor);

        Message("원자로 안정화 성공. 고가치 보상을 회수해라.");
        DropReward(reactorReward);
        Finish(false);
    }

    private void FailReactor()
    {
        if (state != ExpeditionEventState.Active)
        {
            return;
        }

        SetDamageEnabled(false);
        HideProgress();
        Pulse(failColor);

        Message("원자로 폭주. 보상 손실. 적 증원 발생.");
        AudioManager.PlayAt(SoundEventIds.EnemyAlert, transform.position);

        phase = CombatPhase.ReactorFailure;

        SpawnBatch(basicEnemyDefinition, basicEnemyPrefabFallback, reactorFailureBasic, true, false);
        SpawnBatch(chargingEnemyDefinition, chargingEnemyPrefabFallback, reactorFailureCharging, true, false);

        TryCompleteIfNoEnemies();
    }

    private void BeginEvent(string title, string subtitle, Color color)
    {
        UntrackAllEnemies();

        state = ExpeditionEventState.Arming;
        phase = CombatPhase.None;

        Pulse(color);
        AudioManager.PlayAt(SoundEventIds.EventStart, transform.position);
        Log($"Start {eventType}");
    }

    private void Finish(bool failed)
    {
        if (state == ExpeditionEventState.Completed || state == ExpeditionEventState.Failed)
        {
            return;
        }

        if (!failed)
        {
            RegisterHighValueObjective();
        }

        state = failed ? ExpeditionEventState.Failed : ExpeditionEventState.Completed;
        phase = CombatPhase.None;

        StopAllEventCoroutines();
        UntrackAllEnemies();
        SetDamageEnabled(false);
        HideProgress();

        if (eventCollider != null)
        {
            eventCollider.enabled = false;
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        Pulse(failed ? failColor : successColor);
        AudioManager.PlayAt(failed ? SoundEventIds.WarningMessage : SoundEventIds.EventComplete, transform.position);

        if (useTween && animatedRoot != null)
        {
            animatedRoot.DOKill(false);
            animatedRoot.DOScale(baseLocalScale * 0.05f, Mathf.Max(0.05f, finishShrinkDuration)).SetEase(Ease.InBack);
        }

        if (hideVisualOnFinished && visualRoot != null)
        {
            StartCoroutine(HideAfterFinish());
        }
    }

    private IEnumerator HideAfterFinish()
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, finishShrinkDuration));

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }
    }

    private void TryCompleteIfNoEnemies()
    {
        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0)
        {
            TryClearCombatPhase();
        }
    }

    private void TryClearCombatPhase()
    {
        if (phase == CombatPhase.None || phaseClearRoutine != null)
        {
            return;
        }

        phaseClearRoutine = StartCoroutine(ClearCombatPhaseRoutine());
    }

    private IEnumerator ClearCombatPhaseRoutine()
    {
        yield return null;

        phaseClearRoutine = null;

        CleanupTrackedEnemies();

        if (trackedEnemies.Count > 0)
        {
            yield break;
        }

        switch (phase)
        {
            case CombatPhase.RescueWave1:
                phase = CombatPhase.RescueWave2;
                Message("구조 신호 2차 접근 감지.");

                yield return new WaitForSeconds(Mathf.Max(0f, rescueWave2Delay));

                SpawnBatch(basicEnemyDefinition, basicEnemyPrefabFallback, rescueWave2Basic, true, false);
                SpawnBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, rescueWave2Shotgun, true, false);

                TryCompleteIfNoEnemies();
                break;

            case CombatPhase.RescueWave2:
                Message("구조 신호 클리어. 보급 캡슐을 회수해라.");
                DropReward(rescueReward);
                Finish(false);
                break;

            case CombatPhase.UnknownCombat:
                Message("증원 격파. 보상을 회수해라.");
                DropReward(unknownCombatReward);
                Finish(false);
                break;

            case CombatPhase.ReactorFailure:
                Message("폭주 잔존 위협 제거. 보상 없음.");
                Finish(true);
                break;
        }
    }

    private void SpawnBatch(EnemyDefinition definition, GameObject fallback, int count, bool track, bool aroundPlayer)
    {
        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            SpawnEnemy(definition, fallback, track, aroundPlayer);
        }
    }

    private void SpawnEnemy(EnemyDefinition definition, GameObject fallback, bool track, bool aroundPlayer)
    {
        GameObject prefab = definition != null && definition.EnemyPrefab != null
            ? definition.EnemyPrefab
            : fallback;

        if (prefab == null)
        {
            Debug.LogWarning("이벤트 적 프리팹이 비어 있습니다.", this);
            return;
        }

        Vector2 arrivalPosition = GetSpawnPosition(aroundPlayer);
        GameObject enemyObject = Instantiate(prefab, arrivalPosition, Quaternion.identity);

        EnemyBaseAI ai = enemyObject.GetComponentInChildren<EnemyBaseAI>(true);
        EnemyHealth health = enemyObject.GetComponentInChildren<EnemyHealth>(true);

        if (ai != null)
        {
            if (definition != null)
            {
                ai.ApplyDefinition(definition);
            }

            ResolvePlayer();

            if (player != null)
            {
                ai.SetTarget(player);
            }
        }
        else if (health != null && definition != null)
        {
            health.ApplyDefinition(definition);
        }

        ApplyDeepZoneModifier(health);
        SuppressNormalReward(enemyObject);

        if (track)
        {
            TrackEnemy(health);
        }

        EnemyArrivalSpawnSettings arrivalSettings = BuildArrivalSettings();
        bool arrivalStarted = EnemyArrivalSpawnUtility.BeginArrival(
            enemyObject,
            arrivalPosition,
            player,
            alertSpawnedEnemies,
            GetSpawnCenter(aroundPlayer),
            arrivalSettings
        );

        if (!arrivalStarted && ai != null && player != null && alertSpawnedEnemies)
        {
            ai.AlertTo(player.position);
        }
    }

    private Vector2 GetSpawnPosition(bool aroundPlayer)
    {
        Vector2 center = GetSpawnCenter(aroundPlayer);
        Vector2 direction = Random.insideUnitCircle;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        GetEffectiveArrivalRadius(aroundPlayer, out float min, out float max);
        return center + direction * Random.Range(min, max);
    }

    private void GetEffectiveArrivalRadius(bool aroundPlayer, out float min, out float max)
    {
        min = Mathf.Max(0.1f, spawnRadiusMin);
        max = Mathf.Max(min + 0.1f, spawnRadiusMax);

        if (!useEventDefaultArrivalRadiusFloor)
        {
            return;
        }

        float defaultMin = 5f;
        float defaultMax = 7f;

        switch (eventType)
        {
            case ExpeditionEventType.UnknownDevice:
                defaultMin = 4.5f;
                defaultMax = 6.5f;
                break;

            case ExpeditionEventType.UnstableReactor:
                defaultMin = 5.5f;
                defaultMax = 7.5f;
                break;

            case ExpeditionEventType.BlackBoxRecovery:
                defaultMin = aroundPlayer ? 8f : 6f;
                defaultMax = aroundPlayer ? 11f : 8f;
                break;

            case ExpeditionEventType.RescueSignal:
            default:
                defaultMin = 5f;
                defaultMax = 7f;
                break;
        }

        min = Mathf.Max(min, defaultMin);
        max = Mathf.Max(max, defaultMax, min + 0.1f);
    }

    private EnemyArrivalSpawnSettings BuildArrivalSettings()
    {
        return new EnemyArrivalSpawnSettings
        {
            Enabled = useArrivalSpawn,
            MarkerPrefab = arrivalMarkerPrefab,
            ImpactPrefab = arrivalImpactPrefab,
            CreateFallbackMarker = createFallbackArrivalMarker,
            CreateFallbackLandingBurst = createFallbackLandingBurst,
            WarningTime = arrivalWarningTime,
            TravelTime = arrivalTravelTime,
            ReadyDelay = arrivalReadyDelay,
            OffscreenEntryDistance = offscreenEntryDistance,
            PreferOffscreenEntry = preferOffscreenEntry,
            MarkerRadius = arrivalMarkerRadius,
            MarkerColor = arrivalMarkerColor,
            UseAfterimages = useEnemyArrivalAfterimages,
            AfterimageInterval = enemyAfterimageInterval,
            AfterimageLifetime = enemyAfterimageLifetime,
            AfterimageColor = enemyAfterimageColor,
            AfterimageSortingOrderOffset = enemyAfterimageSortingOrderOffset,
            DisableCollidersDuringArrival = disableEnemyCollidersDuringArrival
        };
    }

    private Vector2 GetSpawnCenter(bool aroundPlayer)
    {
        if (aroundPlayer)
        {
            ResolvePlayer();

            if (player != null)
            {
                return player.position;
            }
        }

        return transform.position;
    }

    private void TrackEnemy(EnemyHealth health)
    {
        if (health == null || trackedEnemies.Contains(health))
        {
            return;
        }

        trackedEnemies.Add(health);
        health.Died += OnTrackedEnemyDied;
    }

    private void OnTrackedEnemyDied(EnemyHealth health)
    {
        if (health != null)
        {
            health.Died -= OnTrackedEnemyDied;
        }

        trackedEnemies.Remove(health);
        CleanupTrackedEnemies();

        if (state == ExpeditionEventState.Active && trackedEnemies.Count <= 0)
        {
            TryClearCombatPhase();
        }
    }

    private void CleanupTrackedEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth health = trackedEnemies[i];

            if (health == null || health.IsDead)
            {
                if (health != null)
                {
                    health.Died -= OnTrackedEnemyDied;
                }

                trackedEnemies.RemoveAt(i);
            }
        }
    }

    private void UntrackAllEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            if (trackedEnemies[i] != null)
            {
                trackedEnemies[i].Died -= OnTrackedEnemyDied;
            }
        }

        trackedEnemies.Clear();
    }

    private void ApplyDeepZoneModifier(EnemyHealth health)
    {
        if (health == null || RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        ExpeditionDepth depth = RunManager.Instance.CurrentRun.ExpeditionDepth;
        float multiplier = depth == ExpeditionDepth.DeepZone1
            ? Mathf.Max(0.01f, deepZoneEnemyHpMultiplier)
            : CampaignProgressionCatalog.GetEnemyHpMultiplier(depth);

        if (Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        health.SetMaxHp(health.MaxHp * multiplier, true);
    }

    private void SuppressNormalReward(GameObject enemyObject)
    {
        if (!suppressSpawnedEnemyNormalRewards || enemyObject == null)
        {
            return;
        }

        RewardDropper dropper = enemyObject.GetComponent<RewardDropper>();

        if (dropper != null)
        {
            dropper.SetRewardDefinition(null);
        }
    }

    private void DropReward(RewardDefinition reward)
    {
        if (reward == null)
        {
            Debug.LogWarning($"[{name}] 이벤트 RewardDefinition이 비어 있습니다.", this);
            return;
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (rewardDropper == null)
        {
            Debug.LogWarning($"[{name}] RewardDropper가 없어 보상을 드랍할 수 없습니다.", this);
            return;
        }

        if (applyRewardDropperPresetBeforeDrop)
        {
            rewardDropper.ApplyPreset(GetDropPresetForReward(reward));
        }

        rewardDropper.SetRewardDefinition(reward);
        rewardDropper.DropAt(GetRewardDropPosition());
    }

    private RewardDropperDropPreset GetDropPresetForReward(RewardDefinition reward)
    {
        if (reward == rescueReward)
        {
            return rescueDropPreset;
        }

        if (reward == unknownSafeReward)
        {
            return unknownSafeDropPreset;
        }

        if (reward == unknownCombatReward)
        {
            return unknownCombatDropPreset;
        }

        if (reward == reactorReward)
        {
            return reactorDropPreset;
        }

        if (reward == blackBoxReward)
        {
            return blackBoxDropPreset;
        }

        return RewardDropperDropPreset.Default;
    }

    private Vector3 GetRewardDropPosition()
    {
        if (rewardDropPoint != null)
        {
            return rewardDropPoint.position;
        }

        ResolvePlayer();

        Vector2 dir = player != null
            ? ((Vector2)player.position - (Vector2)transform.position).normalized
            : Vector2.up;

        if (dir.sqrMagnitude <= 0.001f)
        {
            dir = Vector2.up;
        }

        return transform.position + (Vector3)(dir * Mathf.Max(0f, rewardDropOffset));
    }

    private void RegisterHighValueObjective()
    {
        if (objectiveRegistered || !countsAsHighValueObjective)
        {
            return;
        }

        objectiveRegistered = true;
        string id = !string.IsNullOrWhiteSpace(objectiveId)
            ? objectiveId
            : $"event_{eventType}_{GetInstanceID()}";

        ExpeditionObjectiveDirector.Instance?.RegisterObjective(
            id,
            HighValueObjectiveSource.ExpeditionEvent,
            Mathf.Max(1, objectiveSignalAmount)
        );
    }

    private void SetDamageEnabled(bool value)
    {
        if (reactorDamageReceiver != null)
        {
            reactorDamageReceiver.SetDamageEnabled(value);
        }
    }

    private void ResetRuntime()
    {
        StopAllEventCoroutines();
        UntrackAllEnemies();
        KillTweens(false);

        state = ExpeditionEventState.Idle;
        phase = CombatPhase.None;
        objectiveRegistered = false;

        reactorHp = Mathf.Max(1f, reactorMaxHp);
        reactorTimer = Mathf.Max(1f, reactorTimeLimit);

        blackBoxTimer = 0f;
        blackBoxWaveTimer = 0f;
        blackBoxWaveCount = 0;

        if (eventCollider == null)
        {
            eventCollider = GetComponent<Collider2D>();
        }

        if (eventCollider != null)
        {
            eventCollider.enabled = true;
            eventCollider.isTrigger = true;
        }

        if (visualRoot != null && !visualRoot.activeSelf)
        {
            visualRoot.SetActive(true);
        }

        if (radarTarget != null)
        {
            radarTarget.SetVisible(true);
        }

        SetDamageEnabled(false);
        HideProgress();
        RestoreVisual();
        StartIdleTween();
    }

    private void StartRoutine(IEnumerator targetRoutine)
    {
        StopRoutine();
        routine = StartCoroutine(targetRoutine);
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void StopAllEventCoroutines()
    {
        StopRoutine();

        if (phaseClearRoutine != null)
        {
            StopCoroutine(phaseClearRoutine);
            phaseClearRoutine = null;
        }
    }

    private string GetIdleText()
    {
        switch (eventType)
        {
            case ExpeditionEventType.RescueSignal:
                return rescueText;

            case ExpeditionEventType.UnknownDevice:
                return unknownText;

            case ExpeditionEventType.UnstableReactor:
                return reactorText;

            case ExpeditionEventType.BlackBoxRecovery:
                return blackBoxText;

            default:
                return "이벤트 조사";
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void ConfigureRadarTarget()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (radarTarget == null)
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
        }

        radarTarget.SetMarkerType(RadarMarkerType.Event);
        radarTarget.SetVisible(state != ExpeditionEventState.Completed && state != ExpeditionEventState.Failed);
    }

    private void Message(string message)
    {
        if (hud == null)
        {
            hud = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (hud != null)
        {
            hud.ShowWarning(message);
        }
    }

    private void ResolveVisualRoots()
    {
        Transform childVisual = null;

        if (!string.IsNullOrWhiteSpace(visualRootChildName))
        {
            childVisual = transform.Find(visualRootChildName);
        }

        if ((visualRoot == null || visualRoot == gameObject) && childVisual != null)
        {
            visualRoot = childVisual.gameObject;
        }

        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (animatedRoot == null || (preventAnimatedRootFromUsingEventRoot && animatedRoot == transform))
        {
            if (visualRoot != null && visualRoot.transform != transform)
            {
                animatedRoot = visualRoot.transform;
            }
            else if (childVisual != null)
            {
                animatedRoot = childVisual;
            }
            else
            {
                animatedRoot = preventAnimatedRootFromUsingEventRoot ? null : transform;
            }
        }
    }

    private void CaptureBaseVisual()
    {
        if (animatedRoot != null)
        {
            baseLocalPos = animatedRoot.localPosition;
            baseLocalScale = animatedRoot.localScale;
        }

        if (coreRenderer != null)
        {
            baseCoreColor = coreRenderer.color;
        }
    }

    private void RestoreVisual()
    {
        if (animatedRoot != null)
        {
            animatedRoot.localPosition = baseLocalPos;
            animatedRoot.localScale = baseLocalScale;
        }

        if (coreRenderer != null)
        {
            coreRenderer.color = baseCoreColor;
        }

        SetSpriteAlpha(ringRenderer, 0f, false);
        SetSpriteAlpha(progressRenderer, 0f, false);
    }

    private void StartIdleTween()
    {
        if (!useTween || animatedRoot == null)
        {
            return;
        }

        idleMoveTween = animatedRoot
            .DOLocalMoveY(baseLocalPos.y + idleFloatY, Mathf.Max(0.05f, idleFloatDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);

        idleScaleTween = animatedRoot
            .DOScale(baseLocalScale * Mathf.Max(0.01f, idlePulseScale), Mathf.Max(0.05f, idleFloatDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void Pulse(Color color)
    {
        if (!useTween)
        {
            return;
        }

        if (animatedRoot != null)
        {
            animatedRoot.DOKill(false);
            animatedRoot.localScale = baseLocalScale;
            animatedRoot.DOPunchScale(Vector3.one * 0.18f, 0.25f, 8, 0.8f);
        }

        if (coreRenderer != null)
        {
            coreRenderer.DOKill(false);
            coreRenderer.DOColor(color, 0.08f).SetLoops(2, LoopType.Yoyo);
        }

        if (ringRenderer == null || ringRoot == null)
        {
            return;
        }

        ringSequence?.Kill(false);

        ringRoot.localScale = Vector3.one * Mathf.Max(0.01f, ringStartScale);
        ringRenderer.gameObject.SetActive(true);
        ringRenderer.color = WithAlpha(color, 0.9f);

        ringSequence = DOTween.Sequence();
        ringSequence.Join(
            ringRoot
                .DOScale(Vector3.one * Mathf.Max(ringEndScale, ringStartScale), Mathf.Max(0.05f, ringDuration))
                .SetEase(Ease.OutQuad)
        );
        ringSequence.Join(
            ringRenderer
                .DOFade(0f, Mathf.Max(0.05f, ringDuration))
                .SetEase(Ease.OutQuad)
        );
        ringSequence.OnComplete(() => SetSpriteAlpha(ringRenderer, 0f, false));
    }

    private void PlayProgressLoop(Color color)
    {
        if (progressRenderer == null)
        {
            return;
        }

        progressRenderer.gameObject.SetActive(true);
        progressRenderer.color = WithAlpha(color, 0.85f);

        if (progressRoot != null)
        {
            progressRoot.localScale = Vector3.one * 0.75f;
        }

        if (!useTween)
        {
            return;
        }

        progressSequence?.Kill(false);

        progressSequence = DOTween.Sequence();
        progressSequence.Append(progressRenderer.DOFade(0.35f, 0.35f));
        progressSequence.Append(progressRenderer.DOFade(0.9f, 0.35f));
        progressSequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void SetProgress(float normalized, Color color)
    {
        if (progressRenderer == null)
        {
            return;
        }

        normalized = Mathf.Clamp01(normalized);

        progressRenderer.gameObject.SetActive(true);
        progressRenderer.color = WithAlpha(color, Mathf.Max(0.35f, progressRenderer.color.a));

        if (progressRoot != null)
        {
            progressRoot.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.45f, normalized);
        }
    }

    private void HideProgress()
    {
        progressSequence?.Kill(false);
        progressSequence = null;

        SetSpriteAlpha(progressRenderer, 0f, false);
    }

    private void FlashCore()
    {
        if (!useTween || coreRenderer == null)
        {
            return;
        }

        coreRenderer.DOKill(false);
        coreRenderer.DOColor(hitFlashColor, 0.04f).SetLoops(2, LoopType.Yoyo);
    }

    private void KillTweens(bool complete)
    {
        idleMoveTween?.Kill(complete);
        idleScaleTween?.Kill(complete);
        ringSequence?.Kill(complete);
        progressSequence?.Kill(complete);

        idleMoveTween = null;
        idleScaleTween = null;
        ringSequence = null;
        progressSequence = null;

        if (animatedRoot != null)
        {
            animatedRoot.DOKill(complete);
        }

        if (coreRenderer != null)
        {
            coreRenderer.DOKill(complete);
        }

        if (ringRenderer != null)
        {
            ringRenderer.DOKill(complete);
        }

        if (ringRoot != null)
        {
            ringRoot.DOKill(complete);
        }

        if (progressRenderer != null)
        {
            progressRenderer.DOKill(complete);
        }

        if (progressRoot != null)
        {
            progressRoot.DOKill(complete);
        }
    }

    private void SetSpriteAlpha(SpriteRenderer sprite, float alpha, bool active)
    {
        if (sprite == null)
        {
            return;
        }

        Color c = sprite.color;
        c.a = Mathf.Clamp01(alpha);
        sprite.color = c;
        sprite.gameObject.SetActive(active);
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void Log(string message)
    {
        if (logFlow)
        {
            Debug.Log($"[{name}] {message}", this);
        }
    }
}
