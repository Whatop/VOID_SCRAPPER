using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStealthController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerRadarScanner radarScanner;
    [SerializeField] private PlayerRuntimeBonusState runtimeBonusState;

    [Header("Sniper Infiltration Trait")]
    [Tooltip("기존 스나이퍼 40번 Trait ID를 그대로 사용합니다.")]
    [SerializeField] private string infiltrationTraitId = "sn_stealth_scan";

    [Min(1)]
    [SerializeField] private int radarCloakUnlockLevel = 1;

    [Min(1)]
    [SerializeField] private int awarenessReadoutUnlockLevel = 1;

    [Min(1)]
    [SerializeField] private int visionConeUnlockLevel = 2;

    [Min(1)]
    [SerializeField] private int visualDetectionDelayUnlockLevel = 3;

    [Tooltip("1.35면 적의 시각 탐지 완료 시간이 35% 길어집니다.")]
    [Min(1f)]
    [SerializeField] private float visualDetectionTimeMultiplierAtMaxLevel = 1.35f;

    [Tooltip("PlayerRadarScanner를 찾지 못했을 때 사용할 기본 정보 표시 시간입니다.")]
    [Min(0.1f)]
    [SerializeField] private float fallbackIntelRevealDuration = 6f;

    [Min(0.05f)]
    [SerializeField] private float passiveStateRefreshInterval = 0.2f;

    [Header("Runtime Reinforcement Effects")]
    [SerializeField] private float radarJammingUntil;
    [SerializeField] private float tacticalIntelUntil;

    [Header("Messages")]
    [SerializeField] private bool showActivationMessage = true;
    [SerializeField] private string activationMessage = "저피탐 침투 모듈 활성화 · 적 레이더 차단 / 레이더 패널 전술 분석";

    [Header("Debug")]
    [SerializeField] private int currentInfiltrationTraitLevel;
    [SerializeField] private WeaponTreeType currentWeaponTree = WeaponTreeType.MachineGun;

    private bool lastRadarJammingState;
    private bool lastTacticalIntelState;
    private float nextPassiveStateRefreshTime;

    public int InfiltrationTraitLevel => currentInfiltrationTraitLevel;
    public WeaponTreeType CurrentWeaponTree => currentWeaponTree;

    public bool IsSniperInfiltrationActive =>
        currentWeaponTree == WeaponTreeType.Sniper &&
        currentInfiltrationTraitLevel > 0;

    public bool IsPassiveRadarCloakActive =>
        IsSniperInfiltrationActive &&
        currentInfiltrationTraitLevel >= radarCloakUnlockLevel;

    public bool IsHiddenFromEnemyRadar =>
        Time.time < radarJammingUntil ||
        IsPassiveRadarCloakActive;

    // Reinforcement로 직접 켜는 전역 전술 정보 효과입니다.
    public bool IsTacticalIntelActive => Time.time < tacticalIntelUntil;

    // PlayerRadarScanner가 실제로 레이더 패널을 열어 둔 동안만 침투 프로토콜의 전술 오버레이를 허용합니다.
    public bool IsRadarPanelActive =>
        radarScanner != null &&
        radarScanner.IsRadarPanelOpen;

    public float RadarJammingRemaining => Mathf.Max(0f, radarJammingUntil - Time.time);
    public float TacticalIntelRemaining => Mathf.Max(0f, tacticalIntelUntil - Time.time);

    public float EnemyVisualDetectionTimeMultiplier =>
        IsSniperInfiltrationActive &&
        currentInfiltrationTraitLevel >= visualDetectionDelayUnlockLevel
            ? Mathf.Max(1f, visualDetectionTimeMultiplierAtMaxLevel)
            : 1f;

    public float EnemyIntelRevealDuration
    {
        get
        {
            if (radarScanner != null)
            {
                // PlayerRadarScanner.SniperLingerTime already includes the
                // runtime Trait bonus. Adding it again here doubled the effect.
                return Mathf.Max(0.1f, radarScanner.SniperLingerTime);
            }

            float bonus = runtimeBonusState != null
                ? runtimeBonusState.RadarStealthDurationBonus
                : 0f;

            return Mathf.Max(0.1f, fallbackIntelRevealDuration + bonus);
        }
    }

    public event Action<bool> RadarJammingChanged;
    public event Action<bool> TacticalIntelChanged;
    public event Action PassiveInfiltrationChanged;

    private void Reset()
    {
        reinforcementController = GetComponent<PlayerReinforcementController>();
        weaponController = GetComponent<PlayerWeaponController>();
        radarScanner = GetComponent<PlayerRadarScanner>();
        runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureGunshotNoiseEmitter();
        RefreshPassiveState(true);
        CacheRuntimeStates();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (reinforcementController != null)
        {
            reinforcementController.Used += HandleReinforcementUsed;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
        }

        RefreshPassiveState(true);
        CacheRuntimeStates();
    }

    private void OnDisable()
    {
        if (reinforcementController != null)
        {
            reinforcementController.Used -= HandleReinforcementUsed;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextPassiveStateRefreshTime)
        {
            nextPassiveStateRefreshTime =
                Time.unscaledTime + Mathf.Max(0.05f, passiveStateRefreshInterval);

            RefreshPassiveState(false);
        }

        bool radarState = IsHiddenFromEnemyRadar;
        bool intelState = IsTacticalIntelActive;

        if (radarState != lastRadarJammingState)
        {
            lastRadarJammingState = radarState;
            RadarJammingChanged?.Invoke(radarState);
        }

        if (intelState != lastTacticalIntelState)
        {
            lastTacticalIntelState = intelState;
            TacticalIntelChanged?.Invoke(intelState);
        }
    }

    public bool CanRevealEnemyState(RadarTarget radarTarget)
    {
        if (IsTacticalIntelActive)
        {
            return true;
        }

        // 최근 스캔 대상 여부와 무관하게, 레이더 패널이 열려 있는 동안 모든 적 상태를 표시합니다.
        return IsRadarPanelActive &&
               IsSniperInfiltrationActive &&
               currentInfiltrationTraitLevel >= awarenessReadoutUnlockLevel;
    }

    public bool CanRevealEnemyVision(RadarTarget radarTarget)
    {
        if (IsTacticalIntelActive)
        {
            return true;
        }

        // Lv2 부채꼴 시야도 레이더 패널이 열려 있는 동안 모든 적에게 표시합니다.
        return IsRadarPanelActive &&
               IsSniperInfiltrationActive &&
               currentInfiltrationTraitLevel >= visionConeUnlockLevel;
    }

    public void ActivateRadarJamming(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        radarJammingUntil = Mathf.Max(radarJammingUntil, Time.time + duration);
    }

    public void ActivateTacticalIntel(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        tacticalIntelUntil = Mathf.Max(tacticalIntelUntil, Time.time + duration);
    }

    public void ActivateInfiltrationMode(float duration)
    {
        ActivateRadarJamming(duration);
        ActivateTacticalIntel(duration);
    }

    public void ClearRuntimeEffects()
    {
        radarJammingUntil = 0f;
        tacticalIntelUntil = 0f;
    }

    public void RefreshTraitStateNow()
    {
        RefreshPassiveState(true);
    }

    private void ResolveReferences()
    {
        if (reinforcementController == null)
        {
            reinforcementController = GetComponent<PlayerReinforcementController>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (radarScanner == null)
        {
            radarScanner = GetComponent<PlayerRadarScanner>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
        }
    }

    private void EnsureGunshotNoiseEmitter()
    {
        if (GetComponent<PlayerGunshotNoiseEmitter>() == null)
        {
            gameObject.AddComponent<PlayerGunshotNoiseEmitter>();
        }
    }

    private void HandleWeaponEquipped(
        WeaponTreeType weaponTreeType,
        PlayerWeaponBase weapon)
    {
        RefreshPassiveState(true);
    }

    private void HandleReinforcementUsed(ReinforcementDefinition definition)
    {
        if (definition == null || definition.Effects == null)
        {
            return;
        }

        bool activated = false;

        for (int i = 0; i < definition.Effects.Count; i++)
        {
            ReinforcementEffect effect = definition.Effects[i];

            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case ReinforcementEffectType.TemporaryEnemyRadarJamming:
                    ActivateRadarJamming(effect.Duration);
                    activated = true;
                    break;

                case ReinforcementEffectType.RevealEnemyVisionAndState:
                    ActivateTacticalIntel(effect.Duration);
                    activated = true;
                    break;
            }
        }

        if (activated && showActivationMessage)
        {
            ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

            if (hud != null)
            {
                hud.ShowWarning(activationMessage);
            }
        }
    }

    private void RefreshPassiveState(bool forceNotify)
    {
        ResolveReferences();

        WeaponTreeType nextWeaponTree = ResolveCurrentWeaponTree();
        int nextTraitLevel = ResolveTraitLevel();

        bool changed =
            nextWeaponTree != currentWeaponTree ||
            nextTraitLevel != currentInfiltrationTraitLevel;

        currentWeaponTree = nextWeaponTree;
        currentInfiltrationTraitLevel = nextTraitLevel;

        if (changed || forceNotify)
        {
            PassiveInfiltrationChanged?.Invoke();
        }
    }

    private int ResolveTraitLevel()
    {
        if (string.IsNullOrWhiteSpace(infiltrationTraitId))
        {
            return 0;
        }

        int runtimeLevel = 0;
        RunRuntimeTraitStore runtimeStore = RunRuntimeTraitStore.Instance;

        if (runtimeStore != null)
        {
            runtimeLevel = runtimeStore.GetLevel(infiltrationTraitId);
        }

        int permanentLevel = 0;
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress != null && progress.IsTraitActive(infiltrationTraitId))
        {
            permanentLevel = progress.GetTraitLevel(infiltrationTraitId);
        }

        return Mathf.Max(0, Mathf.Max(runtimeLevel, permanentLevel));
    }

    private WeaponTreeType ResolveCurrentWeaponTree()
    {
        if (weaponController != null)
        {
            return weaponController.CurrentWeaponTree;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return WeaponTreeType.MachineGun;
    }

    private void CacheRuntimeStates()
    {
        lastRadarJammingState = IsHiddenFromEnemyRadar;
        lastTacticalIntelState = IsTacticalIntelActive;
    }
}
