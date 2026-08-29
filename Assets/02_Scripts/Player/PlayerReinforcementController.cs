using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public class PlayerReinforcementController : MonoBehaviour
{
    private readonly HashSet<object> externalInputLocks = new HashSet<object>();
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string useActionName = "UseReinforcement";

    [Header("Keyboard Fallback")]
    [SerializeField] private bool allowKeyboardFallback = true;
    [SerializeField] private Key fallbackKey = Key.R;

    [Header("Emergency Return")]
    [SerializeField] private EmergencyReturnController emergencyReturnController;

    [Header("Default Target Layers")]
    [SerializeField] private LayerMask defaultEnemyLayer;
    [SerializeField] private LayerMask defaultProjectileLayer;

    [Header("Fallback VFX")]
    [SerializeField] private GameObject useEffectPrefab;
    [SerializeField] private float useEffectLifetime = 0.35f;

    [Header("Drop / Exchange")]
    [SerializeField] private bool dropCurrentEquipmentOnReplace = true;
    [SerializeField] private ReinforcementPickup dropPickupPrefab;
    [SerializeField] private Transform dropOrigin;
    [SerializeField] private float dropDistance = 1.25f;
    [SerializeField] private float dropPickupBlockSeconds = 0.5f;
    [SerializeField] private Vector2 fallbackDropDirection = Vector2.down;

    [Header("Debug")]
    [SerializeField] private bool logUseResult;

    private PlayerHealth playerHealth;
    private PlayerArmor playerArmor;
    private PlayerController2D playerController;
    private PlayerDash playerDash;
    private PlayerWeaponModifiers weaponModifiers;
    private PlayerRuntimeBonusState runtimeBonusState;
    private PlayerStealthController playerStealthController;
    private PlayerRadarScanner playerRadarScanner;
    private PlayerReturnMarker activeReturnMarker;
    private GameObject activeReturnMarkerObject;
    private bool activeReturnMarkerPooled;
    private GameStateManager observedGameStateManager;

    private InputAction useAction;
    private ReinforcementDefinition equippedDefinition;
    private int currentCharges;
    private float rechargeTimer;

    private readonly Collider2D[] projectileBuffer = new Collider2D[128];
    private readonly Collider2D[] enemyBuffer = new Collider2D[96];
    private readonly HashSet<int> processedTargets = new HashSet<int>();
    private readonly List<ActiveTimedStatus> activeTimedStatuses = new List<ActiveTimedStatus>();
    private readonly List<ActiveTimedModifier> activeTimedModifiers = new List<ActiveTimedModifier>();
    private readonly List<ActiveSpawnedObject> activeSpawnedObjects = new List<ActiveSpawnedObject>();

    private sealed class ActiveTimedStatus
    {
        public ReinforcementDefinition definition;
        public float duration;
        public float endsAt;
    }

    private sealed class ActiveTimedModifier
    {
        public string definitionId;
        public ReinforcementEffectType effectType;
        public float multiplier;
        public float endsAt;
    }

    private sealed class ActiveSpawnedObject
    {
        public string definitionId;
        public GameObject instance;
        public bool pooled;
        public float expiresAt;
    }

    public ReinforcementDefinition EquippedDefinition => equippedDefinition;
    public string EquippedReinforcementId => equippedDefinition != null ? equippedDefinition.EquipmentId : string.Empty;
    public bool HasEquipment => equippedDefinition != null;
    public int CurrentCharges => currentCharges;
    public int MaxCharges => equippedDefinition != null ? equippedDefinition.MaxCharges : 0;
    public float RechargeTimer => rechargeTimer;
    public bool IsRecharging => equippedDefinition != null && equippedDefinition.UsesRecharge && currentCharges < equippedDefinition.MaxCharges;
    public float EffectiveRechargeSeconds => ResolveEffectiveRechargeSeconds();
    public float RechargeRatio => equippedDefinition == null || EffectiveRechargeSeconds <= 0f
        ? 0f
        : Mathf.Clamp01(rechargeTimer / EffectiveRechargeSeconds);
    public float RechargeRemainingSeconds => !IsRecharging || equippedDefinition == null
        ? 0f
        : Mathf.Max(0f, EffectiveRechargeSeconds - rechargeTimer);
    public int ActiveTimedStatusCount => activeTimedStatuses.Count;
    public bool HasPlacedReturnMarker => activeReturnMarker != null &&
                                         activeReturnMarkerObject != null &&
                                         activeReturnMarkerObject.activeInHierarchy;

    public event Action<ReinforcementDefinition, int, int> EquipmentChanged;
    public event Action<int, int, float> ChargesChanged;
    public event Action<ReinforcementDefinition> Used;
    public event Action ActiveTimedStatusesChanged;
    public event Action<bool> ReturnMarkerStateChanged;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindGameStateManager();
        BindInput();
    }

    private void OnDisable()
    {
        if (useAction != null)
        {
            useAction.Disable();
        }

        ClearActiveTimedModifiers();
        ClearActiveTimedStatuses();
        ReleaseAllSpawnedObjects();
        ReleaseReturnMarker();
        UnbindGameStateManager();
    }

    private void Update()
    {
        if (observedGameStateManager == null)
        {
            BindGameStateManager();
        }

        PruneExpiredTimedModifiers();
        PruneExpiredTimedStatuses();
        PruneReleasedSpawnedObjects();
        UpdateRecharge(Time.deltaTime);

        if (WasUsePressed())
        {
            TryUseCurrent();
        }

        if (WasUseReleased())
        {
            emergencyReturnController?.NotifyHoldReleased();
        }
    }

    public bool Equip(ReinforcementDefinition definition, int overrideCharges = -1, bool updateRunContext = true)
    {
        CacheReferences();

        if (definition == null)
        {
            return false;
        }

        WeaponTreeType selectedTree = ResolveSelectedWeaponTree();

        if (!definition.CanUseFor(selectedTree))
        {
            ShowWarning($"{definition.DisplayName}은 현재 기체 트리에서 사용할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        ClearTemporaryEffectsForDefinition(equippedDefinition);

        equippedDefinition = definition;
        rechargeTimer = 0f;

        if (overrideCharges >= 0)
        {
            currentCharges = Mathf.Clamp(overrideCharges, 0, definition.MaxCharges);
        }
        else
        {
            currentCharges = definition.StartWithFullCharges ? definition.MaxCharges : 0;
        }

        NotifyEquipmentChanged();
        AudioManager.Play(SoundEventIds.ReinforcementEquip);

        if (updateRunContext)
        {
            PushStateToRunContext();
        }

        ShowWarning(
            $"장비 장착: {definition.DisplayName}",
            ShipCommunicationSeverity.Confirmation
        );
        return true;
    }

    public bool EquipFromShop(ReinforcementDefinition definition)
    {
        return EquipFromShop(definition, null);
    }

    public bool EquipFromShop(ReinforcementDefinition definition, ShopActiveMaintenanceBay maintenanceBay)
    {
        if (maintenanceBay != null)
        {
            return EquipReplacingCurrentToMaintenance(definition, -1, true, maintenanceBay);
        }

        return EquipReplacingCurrent(definition, -1, true, ResolveDropPosition());
    }

    public bool EquipWithoutDropping(ReinforcementDefinition definition, int overrideCharges = -1, bool updateRunContext = true)
    {
        return Equip(definition, overrideCharges, updateRunContext);
    }

    public bool EquipFromPickup(ReinforcementDefinition definition, int charges, Vector2 pickupPosition)
    {
        return EquipReplacingCurrent(definition, charges, true, pickupPosition);
    }

    public bool RestoreFromRunContext(RunContext runContext, IReadOnlyList<ReinforcementDefinition> definitions)
    {
        if (runContext == null || !runContext.HasEquippedReinforcement)
        {
            ClearEquipment(false);
            return false;
        }

        ReinforcementDefinition definition = FindDefinition(definitions, runContext.EquippedReinforcementId);

        if (definition == null)
        {
            ClearEquipment(false);
            return false;
        }

        return Equip(definition, runContext.EquippedReinforcementCharges, false);
    }

    public void ClearEquipment(bool updateRunContext = true)
    {
        ClearTemporaryEffectsForDefinition(equippedDefinition);
        equippedDefinition = null;
        currentCharges = 0;
        rechargeTimer = 0f;

        NotifyEquipmentChanged();

        if (updateRunContext)
        {
            ShopRunBridge.ClearEquippedReinforcement();
        }
    }

    public bool CanUseCurrent()
    {
        if (GameplayPauseManager.IsPaused || externalInputLocks.Count > 0)
        {
            return false;
        }

        if (equippedDefinition == null)
        {
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        return currentCharges > 0;
    }

    public bool TryUseCurrent()
    {
        CacheReferences();

        if (equippedDefinition == null)
        {
            ShowWarning("장착한 Reinforcement가 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        if (GameplayPauseManager.IsPaused || externalInputLocks.Count > 0)
        {
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        if (currentCharges <= 0)
        {
            ShowWarning(equippedDefinition.UsesRecharge ? "장비 충전 중입니다." : "장비 사용 횟수가 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        IReadOnlyList<ReinforcementEffect> effects = equippedDefinition.Effects;

        if (effects == null || effects.Count == 0)
        {
            ShowWarning("장비 효과가 설정되지 않았습니다.");
            return false;
        }

        if (equippedDefinition.HasEffect(ReinforcementEffectType.EmergencyReturn))
        {
            bool started = TryStartEmergencyReturn();

            if (!started)
            {
                return false;
            }

            // 긴급복귀 앵커는 홀드 서비스다. 취소될 수 있으므로 시작 시 충전/사용 이벤트를 소비하지 않는다.
            return true;
        }

        ReinforcementEffect returnMarkerEffect = FindEffect(
            effects,
            ReinforcementEffectType.PlaceOrReturnToMarker
        );
        if (returnMarkerEffect != null)
        {
            return TryUseReturnMarker(returnMarkerEffect);
        }

        if (!TryValidateEconomyActivation(
                effects,
                out int creditCost,
                out float convertedHealAmount,
                out string failureMessage))
        {
            ShowWarning(failureMessage);
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        if (creditCost > 0 && !TryCommitCreditHealingConversion(creditCost, convertedHealAmount))
        {
            return false;
        }

        Vector2 center = transform.position;
        SpawnEffect(useEffectPrefab, center, useEffectLifetime);

        bool activeTimedStatusChanged = false;

        for (int i = 0; i < effects.Count; i++)
        {
            activeTimedStatusChanged |= ApplyEffect(effects[i], center);
        }

        if (activeTimedStatusChanged)
        {
            ActiveTimedStatusesChanged?.Invoke();
        }

        ConsumeChargeAfterSuccessfulUse();
        return true;
    }

    public void SetExternalInputLocked(object source, bool locked)
    {
        if (source == null)
        {
            return;
        }

        if (locked)
        {
            externalInputLocks.Add(source);
            emergencyReturnController?.NotifyHoldReleased();
        }
        else
        {
            externalInputLocks.Remove(source);
        }
    }

    public bool DropCurrentEquipment(Vector2 position)
    {
        if (equippedDefinition == null)
        {
            return false;
        }

        ReinforcementDefinition droppedDefinition = equippedDefinition;
        int droppedCharges = currentCharges;

        bool spawned = SpawnPickup(droppedDefinition, droppedCharges, position);

        if (spawned)
        {
            ClearEquipment(true);
        }

        return spawned;
    }

    private bool EquipReplacingCurrent(ReinforcementDefinition newDefinition, int overrideCharges, bool updateRunContext, Vector2 dropPosition)
    {
        if (newDefinition == null)
        {
            return false;
        }

        ReinforcementDefinition previousDefinition = equippedDefinition;
        int previousCharges = currentCharges;

        bool equipped = Equip(newDefinition, overrideCharges, updateRunContext);

        if (!equipped)
        {
            return false;
        }

        if (dropCurrentEquipmentOnReplace && previousDefinition != null && previousDefinition != newDefinition)
        {
            SpawnPickup(previousDefinition, previousCharges, dropPosition);
        }

        return true;
    }

    private bool EquipReplacingCurrentToMaintenance(
        ReinforcementDefinition newDefinition,
        int overrideCharges,
        bool updateRunContext,
        ShopActiveMaintenanceBay maintenanceBay)
    {
        if (newDefinition == null)
        {
            return false;
        }

        if (maintenanceBay == null)
        {
            return EquipReplacingCurrent(newDefinition, overrideCharges, updateRunContext, ResolveDropPosition());
        }

        ReinforcementDefinition previousDefinition = equippedDefinition;
        int previousCharges = currentCharges;
        bool shouldStorePrevious = dropCurrentEquipmentOnReplace && previousDefinition != null && previousDefinition != newDefinition;

        if (shouldStorePrevious && !maintenanceBay.CanStore(previousDefinition))
        {
            ShowWarning("정비소 보관 슬롯이 가득 찼습니다.");
            return false;
        }

        bool equipped = Equip(newDefinition, overrideCharges, updateRunContext);

        if (!equipped)
        {
            return false;
        }

        if (shouldStorePrevious && !maintenanceBay.TryStore(previousDefinition, previousCharges))
        {
            // 보관 실패 시 기존 장비로 롤백한다. 장비 유실 방지용.
            Equip(previousDefinition, previousCharges, updateRunContext);
            ShowWarning("정비소 보관 실패로 장비 교체를 취소했습니다.");
            return false;
        }

        return true;
    }

    private void ConsumeChargeAfterSuccessfulUse()
    {
        currentCharges = Mathf.Max(0, currentCharges - 1);
        rechargeTimer = 0f;

        Used?.Invoke(equippedDefinition);
        AudioManager.Play(SoundEventIds.ReinforcementUse);

        if (logUseResult && equippedDefinition != null)
        {
            Debug.Log($"Reinforcement 사용: {equippedDefinition.DisplayName}, Charges: {currentCharges}/{equippedDefinition.MaxCharges}", this);
        }

        NotifyChargesChanged();
        PushStateToRunContext();
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (playerArmor == null)
        {
            playerArmor = GetComponent<PlayerArmor>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (weaponModifiers == null)
        {
            weaponModifiers = GetComponent<PlayerWeaponModifiers>();
        }

        if (runtimeBonusState == null)
        {
            runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
        }

        if (playerStealthController == null)
        {
            playerStealthController = GetComponent<PlayerStealthController>();
        }

        if (playerRadarScanner == null)
        {
            playerRadarScanner = GetComponent<PlayerRadarScanner>();
        }

        if (emergencyReturnController == null)
        {
            emergencyReturnController = GetComponent<EmergencyReturnController>();
        }

        if (GetComponent<PlayerStealthController>() == null)
        {
            gameObject.AddComponent<PlayerStealthController>();
        }
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        useAction = InputBindingUtility.ResolveAction(
            inputActions,
            actionMapName,
            useActionName
        );
        useAction?.Enable();
    }

    private bool WasUsePressed()
    {
        if (useAction != null)
        {
            return useAction.WasPressedThisFrame();
        }

        if (!allowKeyboardFallback || Keyboard.current == null)
        {
            return false;
        }

        KeyControl keyControl = Keyboard.current[fallbackKey];
        return keyControl != null && keyControl.wasPressedThisFrame;
    }

    private bool WasUseReleased()
    {
        if (useAction != null)
        {
            return useAction.WasReleasedThisFrame();
        }

        if (!allowKeyboardFallback || Keyboard.current == null)
        {
            return false;
        }

        KeyControl keyControl = Keyboard.current[fallbackKey];
        return keyControl != null && keyControl.wasReleasedThisFrame;
    }

    private void UpdateRecharge(float deltaTime)
    {
        if (equippedDefinition == null)
        {
            return;
        }

        if (!equippedDefinition.UsesRecharge)
        {
            return;
        }

        int maxCharges = equippedDefinition.MaxCharges;

        if (currentCharges >= maxCharges)
        {
            rechargeTimer = 0f;
            return;
        }

        float rechargeSeconds = ResolveEffectiveRechargeSeconds();

        if (rechargeSeconds <= 0f)
        {
            currentCharges = maxCharges;
            rechargeTimer = 0f;
            NotifyChargesChanged();
            PushStateToRunContext();
            return;
        }

        rechargeTimer += deltaTime;

        bool changed = false;

        while (rechargeTimer >= rechargeSeconds && currentCharges < maxCharges)
        {
            rechargeTimer -= rechargeSeconds;
            currentCharges++;
            changed = true;
        }

        if (currentCharges >= maxCharges)
        {
            rechargeTimer = 0f;
        }

        if (changed)
        {
            NotifyChargesChanged();
            PushStateToRunContext();
        }
    }

    private bool ApplyEffect(ReinforcementEffect effect, Vector2 center)
    {
        if (effect == null)
        {
            return false;
        }

        float activeStatusDuration = 0f;

        switch (effect.EffectType)
        {
            case ReinforcementEffectType.HealFlat:
                if (playerHealth != null)
                {
                    float healAmount = runtimeBonusState != null
                        ? runtimeBonusState.ApplyHealAmount(effect.Value)
                        : effect.Value;
                    playerHealth.Heal(healAmount);
                }
                break;

            case ReinforcementEffectType.AddArmor:
                ApplyArmor(effect.Value);
                break;

            case ReinforcementEffectType.AddInvincibleTime:
                if (playerHealth != null)
                {
                    playerHealth.AddInvincibleTime(effect.Value);
                    activeStatusDuration = Mathf.Max(0f, effect.Value);
                }
                break;

            case ReinforcementEffectType.ClearEnemyProjectiles:
                ClearEnemyProjectiles(center, effect.Radius, ResolveLayer(effect.TargetLayer, defaultProjectileLayer));
                break;

            case ReinforcementEffectType.DamageNearbyEnemies:
                DamageNearbyEnemies(center, effect.Radius, effect.Value, ResolveLayer(effect.TargetLayer, defaultEnemyLayer));
                break;

            case ReinforcementEffectType.KnockbackNearbyEnemies:
                KnockbackNearbyEnemies(center, effect.Radius, effect.Value, ResolveLayer(effect.TargetLayer, defaultEnemyLayer));
                break;

            case ReinforcementEffectType.TemporaryDamagePercent:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.TemporaryFireRatePercent:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.TemporaryMoveSpeedPercent:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.TemporaryDashCooldownReductionPercent:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.TemporaryScrapGainPercent:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.TemporaryHomingAngleBonus:
            case ReinforcementEffectType.TemporaryHomingRangeBonus:
            case ReinforcementEffectType.TemporaryPierceCountBonus:
            case ReinforcementEffectType.TemporaryRemovePierceDamageFalloff:
                if (ApplyOrRefreshTimedModifier(effect))
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.ClearEnemyProjectilesInCone:
            {
                Vector2 aimDirection = ResolveAimDirection();
                ClearEnemyProjectilesInCone(
                    center,
                    aimDirection,
                    effect.Radius,
                    effect.ConeAngle,
                    ResolveLayer(effect.TargetLayer, defaultProjectileLayer)
                );
                ExpandingPulseRing2D.SpawnCone(
                    center,
                    aimDirection,
                    new Color(1f, 0.42f, 0.08f, 0.88f),
                    0.28f,
                    0.08f,
                    effect.Radius,
                    effect.ConeAngle,
                    0.05f,
                    0.025f,
                    24,
                    "Default",
                    70
                );
                break;
            }

            case ReinforcementEffectType.DamageEnemiesInCone:
                DamageEnemiesInCone(
                    center,
                    ResolveAimDirection(),
                    effect.Radius,
                    effect.ConeAngle,
                    effect.Value,
                    ResolveLayer(effect.TargetLayer, defaultEnemyLayer)
                );
                break;

            // The run-wallet transaction is preflighted and committed atomically before
            // presentation or any non-transactional effects are applied.
            case ReinforcementEffectType.ConvertCreditsToHealing:
                break;

            case ReinforcementEffectType.SpawnPrefabAtPlayer:
                if (SpawnEffect(effect, center) != null)
                {
                    activeStatusDuration = effect.PrefabLifetime;
                }
                break;

            case ReinforcementEffectType.EmergencyReturn:
            case ReinforcementEffectType.PlaceOrReturnToMarker:
                break;

            // PlayerStealthController가 Used 이벤트를 받아 지속시간 효과를 처리한다.
            case ReinforcementEffectType.TemporaryEnemyRadarJamming:
            case ReinforcementEffectType.RevealEnemyVisionAndState:
                if (playerStealthController != null && playerStealthController.isActiveAndEnabled)
                {
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.RevealRadarTargets:
                if (playerRadarScanner != null)
                {
                    playerRadarScanner.RevealTargetsTemporarily(
                        this,
                        effect.Radius,
                        effect.Duration
                    );
                    activeStatusDuration = effect.Duration;
                }
                break;

            case ReinforcementEffectType.DisruptEnemyTracking:
                if (DisruptNearbyEnemyTracking(
                    center,
                    effect.Radius,
                    effect.Duration,
                    ResolveLayer(effect.TargetLayer, defaultEnemyLayer)))
                {
                    activeStatusDuration = effect.Duration;
                }

                playerRadarScanner?.PlayTacticalPulse(effect.Radius);
                break;
        }

        return RegisterActiveTimedStatus(equippedDefinition, activeStatusDuration);
    }

    public bool TryGetActiveTimedStatus(
        int index,
        out string statusId,
        out ReinforcementDefinition definition,
        out float remainingSeconds,
        out float durationSeconds)
    {
        if (index < 0 || index >= activeTimedStatuses.Count)
        {
            statusId = string.Empty;
            definition = null;
            remainingSeconds = 0f;
            durationSeconds = 0f;
            return false;
        }

        ActiveTimedStatus status = activeTimedStatuses[index];
        statusId = status.definition != null ? status.definition.EquipmentId : string.Empty;
        definition = status.definition;
        remainingSeconds = Mathf.Max(0f, status.endsAt - Time.time);
        durationSeconds = Mathf.Max(0.01f, status.duration);
        return definition != null && remainingSeconds > 0f;
    }

    public bool TryGetActiveTimedStatus(
        string statusId,
        out ReinforcementDefinition definition,
        out float remainingSeconds,
        out float durationSeconds)
    {
        if (!string.IsNullOrWhiteSpace(statusId))
        {
            for (int i = 0; i < activeTimedStatuses.Count; i++)
            {
                ActiveTimedStatus status = activeTimedStatuses[i];

                if (status.definition != null && status.definition.EquipmentId == statusId)
                {
                    definition = status.definition;
                    remainingSeconds = Mathf.Max(0f, status.endsAt - Time.time);
                    durationSeconds = Mathf.Max(0.01f, status.duration);
                    return remainingSeconds > 0f;
                }
            }
        }

        definition = null;
        remainingSeconds = 0f;
        durationSeconds = 0f;
        return false;
    }

    private bool RegisterActiveTimedStatus(ReinforcementDefinition definition, float duration)
    {
        if (definition == null || duration <= 0f)
        {
            return false;
        }

        float now = Time.time;
        float requestedEnd = now + duration;

        for (int i = 0; i < activeTimedStatuses.Count; i++)
        {
            ActiveTimedStatus status = activeTimedStatuses[i];

            if (status.definition == null || status.definition.EquipmentId != definition.EquipmentId)
            {
                continue;
            }

            status.endsAt = Mathf.Max(status.endsAt, requestedEnd);
            status.duration = Mathf.Max(0.01f, status.endsAt - now);
            return true;
        }

        activeTimedStatuses.Add(new ActiveTimedStatus
        {
            definition = definition,
            duration = duration,
            endsAt = requestedEnd
        });
        return true;
    }

    private void PruneExpiredTimedStatuses()
    {
        bool changed = false;

        for (int i = activeTimedStatuses.Count - 1; i >= 0; i--)
        {
            ActiveTimedStatus status = activeTimedStatuses[i];

            if (status.definition != null && Time.time < status.endsAt)
            {
                continue;
            }

            activeTimedStatuses.RemoveAt(i);
            changed = true;
        }

        if (changed)
        {
            ActiveTimedStatusesChanged?.Invoke();
        }
    }

    private bool ApplyOrRefreshTimedModifier(ReinforcementEffect effect)
    {
        if (effect == null || effect.Duration <= 0f || equippedDefinition == null)
        {
            return false;
        }

        float multiplier = ResolveTimedModifierMultiplier(effect);

        if (multiplier <= 0f)
        {
            return false;
        }

        string definitionId = equippedDefinition.EquipmentId;
        ActiveTimedModifier modifier = FindTimedModifier(definitionId, effect.EffectType);

        if (modifier != null)
        {
            RemoveTimedModifierEffect(modifier);
        }
        else
        {
            modifier = new ActiveTimedModifier
            {
                definitionId = definitionId,
                effectType = effect.EffectType
            };
            activeTimedModifiers.Add(modifier);
        }

        modifier.multiplier = multiplier;
        modifier.endsAt = Time.time + effect.Duration;
        ApplyTimedModifierEffect(modifier);
        return true;
    }

    private ActiveTimedModifier FindTimedModifier(string definitionId, ReinforcementEffectType effectType)
    {
        for (int i = 0; i < activeTimedModifiers.Count; i++)
        {
            ActiveTimedModifier modifier = activeTimedModifiers[i];

            if (modifier.definitionId == definitionId && modifier.effectType == effectType)
            {
                return modifier;
            }
        }

        return null;
    }

    private float ResolveTimedModifierMultiplier(ReinforcementEffect effect)
    {
        switch (effect.EffectType)
        {
            case ReinforcementEffectType.TemporaryDamagePercent:
                return weaponModifiers != null ? PercentToMultiplier(effect.Value) : 0f;

            case ReinforcementEffectType.TemporaryFireRatePercent:
                return weaponModifiers != null ? PercentToMultiplier(effect.Value) : 0f;

            case ReinforcementEffectType.TemporaryMoveSpeedPercent:
                return playerController != null ? PercentToMultiplier(effect.Value) : 0f;

            case ReinforcementEffectType.TemporaryDashCooldownReductionPercent:
                if (playerDash == null)
                {
                    return 0f;
                }

                float reduction = Mathf.Clamp01(Mathf.Abs(effect.Value) * 0.01f);
                return Mathf.Clamp(1f - reduction, 0.05f, 1f);

            case ReinforcementEffectType.TemporaryScrapGainPercent:
                return runtimeBonusState != null ? PercentToMultiplier(effect.Value) : 0f;

            case ReinforcementEffectType.TemporaryHomingAngleBonus:
            case ReinforcementEffectType.TemporaryHomingRangeBonus:
            case ReinforcementEffectType.TemporaryPierceCountBonus:
                return weaponModifiers != null && effect.Value > 0f ? effect.Value : 0f;

            case ReinforcementEffectType.TemporaryRemovePierceDamageFalloff:
                return weaponModifiers != null ? Mathf.Max(1f, effect.Value) : 0f;

            default:
                return 0f;
        }
    }

    private void ApplyTimedModifierEffect(ActiveTimedModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        switch (modifier.effectType)
        {
            case ReinforcementEffectType.TemporaryDamagePercent:
                weaponModifiers?.MultiplyDamage(modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryFireRatePercent:
                weaponModifiers?.MultiplyFireRate(modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryMoveSpeedPercent:
                if (playerController != null)
                {
                    playerController.SetMoveSpeed(playerController.MoveSpeed * modifier.multiplier);
                }
                break;

            case ReinforcementEffectType.TemporaryDashCooldownReductionPercent:
                if (playerDash != null)
                {
                    playerDash.SetDashCooldown(playerDash.DashCooldown * modifier.multiplier);
                }
                break;

            case ReinforcementEffectType.TemporaryScrapGainPercent:
                runtimeBonusState?.SetExternalScrapGainMultiplier(modifier, modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryHomingAngleBonus:
                weaponModifiers?.AddHomingAngle(modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryHomingRangeBonus:
                weaponModifiers?.AddHomingRange(modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryPierceCountBonus:
                weaponModifiers?.AddPierceCount(Mathf.RoundToInt(modifier.multiplier));
                break;

            case ReinforcementEffectType.TemporaryRemovePierceDamageFalloff:
                weaponModifiers?.AddPierceDamageFalloffRemoval(Mathf.Max(1, Mathf.RoundToInt(modifier.multiplier)));
                break;
        }
    }

    private void RemoveTimedModifierEffect(ActiveTimedModifier modifier)
    {
        if (modifier == null)
        {
            return;
        }

        float inverse = SafeInverse(modifier.multiplier);

        switch (modifier.effectType)
        {
            case ReinforcementEffectType.TemporaryDamagePercent:
                weaponModifiers?.MultiplyDamage(inverse);
                break;

            case ReinforcementEffectType.TemporaryFireRatePercent:
                weaponModifiers?.MultiplyFireRate(inverse);
                break;

            case ReinforcementEffectType.TemporaryMoveSpeedPercent:
                if (playerController != null)
                {
                    playerController.SetMoveSpeed(playerController.MoveSpeed * inverse);
                }
                break;

            case ReinforcementEffectType.TemporaryDashCooldownReductionPercent:
                if (playerDash != null)
                {
                    playerDash.SetDashCooldown(playerDash.DashCooldown * inverse);
                }
                break;

            case ReinforcementEffectType.TemporaryScrapGainPercent:
                runtimeBonusState?.ClearExternalScrapGainMultiplier(modifier);
                break;

            case ReinforcementEffectType.TemporaryHomingAngleBonus:
                weaponModifiers?.AddHomingAngle(-modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryHomingRangeBonus:
                weaponModifiers?.AddHomingRange(-modifier.multiplier);
                break;

            case ReinforcementEffectType.TemporaryPierceCountBonus:
                weaponModifiers?.AddPierceCount(-Mathf.RoundToInt(modifier.multiplier));
                break;

            case ReinforcementEffectType.TemporaryRemovePierceDamageFalloff:
                weaponModifiers?.AddPierceDamageFalloffRemoval(-Mathf.Max(1, Mathf.RoundToInt(modifier.multiplier)));
                break;
        }
    }

    private bool TryValidateEconomyActivation(
        IReadOnlyList<ReinforcementEffect> effects,
        out int creditCost,
        out float convertedHealAmount,
        out string failureMessage)
    {
        creditCost = 0;
        convertedHealAmount = 0f;
        failureMessage = string.Empty;
        bool hasCreditConversion = false;

        for (int i = 0; i < effects.Count; i++)
        {
            ReinforcementEffect effect = effects[i];

            if (effect == null)
            {
                continue;
            }

            switch (effect.EffectType)
            {
                case ReinforcementEffectType.TemporaryScrapGainPercent:
                    if (runtimeBonusState == null || effect.Value <= 0f || effect.Duration <= 0f)
                    {
                        failureMessage = "스크랩 압축기 설정이 올바르지 않습니다.";
                        return false;
                    }
                    break;

                case ReinforcementEffectType.ConvertCreditsToHealing:
                    if (effect.ResourceCost <= 0 || effect.Value <= 0f)
                    {
                        failureMessage = "크레딧 변환기 설정이 올바르지 않습니다.";
                        return false;
                    }

                    if (creditCost > int.MaxValue - effect.ResourceCost)
                    {
                        failureMessage = "크레딧 변환 비용이 유효 범위를 벗어났습니다.";
                        return false;
                    }

                    hasCreditConversion = true;
                    creditCost += effect.ResourceCost;
                    convertedHealAmount += runtimeBonusState != null
                        ? runtimeBonusState.ApplyHealAmount(effect.Value)
                        : effect.Value;
                    break;
            }
        }

        if (!hasCreditConversion)
        {
            return true;
        }

        if (playerHealth == null || playerHealth.IsDead)
        {
            failureMessage = "회복할 기체 상태를 확인할 수 없습니다.";
            return false;
        }

        if (playerHealth.CurrentHp >= playerHealth.MaxHp - 0.001f)
        {
            failureMessage = "체력이 이미 최대입니다.";
            return false;
        }

        if (convertedHealAmount <= 0f)
        {
            failureMessage = "적용할 회복량이 없습니다.";
            return false;
        }

        RunManager runManager = RunManager.Instance;

        if (runManager == null || !runManager.HasActiveRun || runManager.CurrentRun?.Wallet == null)
        {
            failureMessage = "활성 탐사 크레딧 지갑이 없습니다.";
            return false;
        }

        if (!runManager.CurrentRun.Wallet.CanSpend(CurrencyType.Credits, creditCost))
        {
            failureMessage = $"크레딧 부족 · 필요 {creditCost}";
            return false;
        }

        return true;
    }

    private bool TryCommitCreditHealingConversion(int creditCost, float healAmount)
    {
        RunManager runManager = RunManager.Instance;

        if (creditCost <= 0 || healAmount <= 0f || playerHealth == null ||
            runManager == null || !runManager.TrySpendCredits(creditCost))
        {
            ShowWarning("크레딧 변환에 실패했습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        float actualHealAmount = Mathf.Min(
            healAmount,
            Mathf.Max(0f, playerHealth.MaxHp - playerHealth.CurrentHp)
        );

        playerHealth.Heal(healAmount);
        ShowWarning(
            $"크레딧 {creditCost} 소모 · HP {actualHealAmount:0.#} 회복",
            ShipCommunicationSeverity.Confirmation
        );
        return true;
    }

    private void ClearTemporaryEffectsForDefinition(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        string definitionId = definition.EquipmentId;

        if (definition.HasEffect(ReinforcementEffectType.PlaceOrReturnToMarker))
        {
            ReleaseReturnMarker();
        }

        for (int i = activeTimedModifiers.Count - 1; i >= 0; i--)
        {
            ActiveTimedModifier modifier = activeTimedModifiers[i];

            if (modifier.definitionId != definitionId)
            {
                continue;
            }

            RemoveTimedModifierEffect(modifier);
            activeTimedModifiers.RemoveAt(i);
        }

        bool statusChanged = false;

        for (int i = activeTimedStatuses.Count - 1; i >= 0; i--)
        {
            ActiveTimedStatus status = activeTimedStatuses[i];

            if (status.definition == null || status.definition.EquipmentId != definitionId)
            {
                continue;
            }

            activeTimedStatuses.RemoveAt(i);
            statusChanged = true;
        }

        if (statusChanged)
        {
            ActiveTimedStatusesChanged?.Invoke();
        }
    }

    private void PruneExpiredTimedModifiers()
    {
        for (int i = activeTimedModifiers.Count - 1; i >= 0; i--)
        {
            ActiveTimedModifier modifier = activeTimedModifiers[i];

            if (Time.time < modifier.endsAt)
            {
                continue;
            }

            RemoveTimedModifierEffect(modifier);
            activeTimedModifiers.RemoveAt(i);
        }
    }

    private void ClearActiveTimedModifiers()
    {
        for (int i = activeTimedModifiers.Count - 1; i >= 0; i--)
        {
            RemoveTimedModifierEffect(activeTimedModifiers[i]);
        }

        activeTimedModifiers.Clear();
    }

    private void ClearActiveTimedStatuses()
    {
        if (activeTimedStatuses.Count == 0)
        {
            return;
        }

        activeTimedStatuses.Clear();
        ActiveTimedStatusesChanged?.Invoke();
    }

    private bool TryUseReturnMarker(ReinforcementEffect effect)
    {
        if (!CanUseReturnMarker(out string failureMessage))
        {
            ShowWarning(failureMessage);
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        if (activeReturnMarker == null ||
            activeReturnMarkerObject == null ||
            !activeReturnMarkerObject.activeInHierarchy)
        {
            ReleaseReturnMarker();
            return TryPlaceReturnMarker(effect);
        }

        return TryReturnToMarker();
    }

    private bool TryPlaceReturnMarker(ReinforcementEffect effect)
    {
        if (effect == null || effect.Prefab == null || playerController == null)
        {
            ShowWarning("귀환 표식 프리팹 또는 플레이어 이동 컨트롤러가 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        Vector2 markerPosition = transform.position;
        if (!playerController.IsRepositionDestinationValid(markerPosition))
        {
            ShowWarning("현재 위치에는 귀환 표식을 설치할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        bool usePool = PoolManager.Instance != null;
        GameObject instance = usePool
            ? PoolManager.Instance.Get(effect.Prefab, markerPosition, Quaternion.identity)
            : Instantiate(effect.Prefab, markerPosition, Quaternion.identity);

        if (instance == null)
        {
            ShowWarning("귀환 표식을 생성하지 못했습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        PlayerReturnMarker marker = instance.GetComponent<PlayerReturnMarker>();
        if (marker == null || !marker.Configure(gameObject))
        {
            if (usePool && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(instance);
            }
            else
            {
                Destroy(instance);
            }

            ShowWarning("귀환 표식 구성이 올바르지 않습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        activeReturnMarker = marker;
        activeReturnMarkerObject = instance;
        activeReturnMarkerPooled = usePool;
        ReturnMarkerStateChanged?.Invoke(true);
        AudioManager.Play(SoundEventIds.ReinforcementUse);
        ShowWarning(
            "귀환 표식을 설치했습니다. 다시 사용하면 이 위치로 복귀합니다.",
            ShipCommunicationSeverity.Confirmation,
            ShipCommunicationChannel.Navigation
        );
        return true;
    }

    private bool TryReturnToMarker()
    {
        if (activeReturnMarker == null || activeReturnMarkerObject == null || playerController == null)
        {
            ShowWarning("사용 가능한 귀환 표식이 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        Vector2 destination = activeReturnMarker.ReturnPosition;
        if (!playerController.IsRepositionDestinationValid(destination))
        {
            ShowWarning("귀환 표식 위치가 막혀 있어 복귀할 수 없습니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        Vector2 departure = transform.position;
        if (!playerController.TryRepositionTo(destination))
        {
            ShowWarning("귀환에 실패했습니다. 표식은 유지됩니다.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return false;
        }

        SpawnReturnPulse(departure, 0.18f, 0.75f);
        SpawnReturnPulse(destination, 0.08f, 1.05f);
        ReleaseReturnMarker();
        ShowWarning(
            "귀환 표식 위치로 복귀했습니다.",
            ShipCommunicationSeverity.Confirmation,
            ShipCommunicationChannel.Navigation
        );
        ConsumeChargeAfterSuccessfulUse();
        return true;
    }

    private bool CanUseReturnMarker(out string failureMessage)
    {
        failureMessage = string.Empty;
        GameStateManager stateManager = GameStateManager.Instance;

        if (stateManager == null)
        {
            failureMessage = "게임 상태를 확인할 수 없어 귀환 표식을 사용할 수 없습니다.";
            return false;
        }

        GameState state = stateManager.CurrentState;
        if (state == GameState.BossBattle || state == GameState.FinalBossBattle)
        {
            failureMessage = "보스 전투 중에는 귀환 표식을 사용할 수 없습니다.";
            return false;
        }

        if (state != GameState.Expedition && state != GameState.Tutorial)
        {
            failureMessage = "현재 상태에서는 귀환 표식을 사용할 수 없습니다.";
            return false;
        }

        return true;
    }

    private void ReleaseReturnMarker()
    {
        GameObject instance = activeReturnMarkerObject;
        bool wasPlaced = activeReturnMarker != null || instance != null;

        activeReturnMarker?.PrepareForRelease();
        activeReturnMarker = null;
        activeReturnMarkerObject = null;
        bool usePool = activeReturnMarkerPooled;
        activeReturnMarkerPooled = false;

        if (instance != null && instance.activeSelf)
        {
            if (usePool && PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        if (wasPlaced)
        {
            ReturnMarkerStateChanged?.Invoke(false);
        }
    }

    private void BindGameStateManager()
    {
        GameStateManager manager = GameStateManager.Instance;
        if (observedGameStateManager == manager)
        {
            return;
        }

        UnbindGameStateManager();
        observedGameStateManager = manager;

        if (observedGameStateManager != null)
        {
            observedGameStateManager.StateChanged += HandleGameStateChanged;
        }
    }

    private void UnbindGameStateManager()
    {
        if (observedGameStateManager != null)
        {
            observedGameStateManager.StateChanged -= HandleGameStateChanged;
            observedGameStateManager = null;
        }
    }

    private void HandleGameStateChanged(GameState previous, GameState next)
    {
        if (next != GameState.Expedition && next != GameState.Tutorial)
        {
            ReleaseReturnMarker();
        }
    }

    private static ReinforcementEffect FindEffect(
        IReadOnlyList<ReinforcementEffect> effects,
        ReinforcementEffectType effectType)
    {
        if (effects == null)
        {
            return null;
        }

        for (int i = 0; i < effects.Count; i++)
        {
            ReinforcementEffect effect = effects[i];
            if (effect != null && effect.EffectType == effectType)
            {
                return effect;
            }
        }

        return null;
    }

    private static void SpawnReturnPulse(Vector2 position, float startRadius, float endRadius)
    {
        ExpandingPulseRing2D.Spawn(
            position,
            new Color(0.48f, 1f, 1f, 0.9f),
            0.24f,
            startRadius,
            endRadius,
            0.055f,
            0.015f,
            36,
            "Default",
            72
        );
    }

    private bool TryStartEmergencyReturn()
    {
        if (emergencyReturnController == null)
        {
            emergencyReturnController = GetComponent<EmergencyReturnController>();
        }

        if (emergencyReturnController == null)
        {
            ShowWarning("긴급복귀 컨트롤러가 없습니다.");
            return false;
        }

        return emergencyReturnController.TryStartByHoldKey();
    }

    private void ApplyArmor(float amount)
    {
        if (playerArmor == null || amount <= 0f)
        {
            return;
        }

        float requiredMax = Mathf.Max(playerArmor.MaxArmor, playerArmor.CurrentArmor + amount);

        if (requiredMax > playerArmor.MaxArmor)
        {
            playerArmor.SetMaxArmor(requiredMax, false);
        }

        playerArmor.AddArmor(amount);
    }

    private void ClearEnemyProjectiles(Vector2 center, float radius, LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(center, radius, projectileBuffer, layer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = projectileBuffer[i];

            if (hit == null)
            {
                continue;
            }

            Bullet bullet = hit.GetComponentInParent<Bullet>();
            if (bullet == null || bullet.Owner != ProjectileOwner.Enemy)
            {
                continue;
            }

            ReleaseObject(bullet.gameObject);
        }
    }

    private void ClearEnemyProjectilesInCone(
        Vector2 center,
        Vector2 direction,
        float radius,
        float coneAngle,
        LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f)
        {
            return;
        }

        processedTargets.Clear();
        Vector2 normalizedDirection = NormalizeDirection(direction);
        float minimumDot = Mathf.Cos(Mathf.Clamp(coneAngle, 1f, 180f) * 0.5f * Mathf.Deg2Rad);
        ContactFilter2D contactFilter = CreateLayerContactFilter(layer);
        int count = Physics2D.OverlapCircle(center, radius, contactFilter, projectileBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = projectileBuffer[i];

            if (hit == null)
            {
                continue;
            }

            Bullet bullet = hit.GetComponentInParent<Bullet>();

            if (bullet == null || bullet.Owner != ProjectileOwner.Enemy ||
                !IsPointInsideCone(center, normalizedDirection, bullet.transform.position, radius, minimumDot))
            {
                continue;
            }

            int id = bullet.GetInstanceID();
            if (!processedTargets.Add(id))
            {
                continue;
            }

            bullet.ForceRelease(false);
        }
    }

    private void DamageEnemiesInCone(
        Vector2 center,
        Vector2 direction,
        float radius,
        float coneAngle,
        float damage,
        LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f || damage <= 0f)
        {
            return;
        }

        processedTargets.Clear();
        Vector2 normalizedDirection = NormalizeDirection(direction);
        float minimumDot = Mathf.Cos(Mathf.Clamp(coneAngle, 1f, 180f) * 0.5f * Mathf.Deg2Rad);
        ContactFilter2D contactFilter = CreateLayerContactFilter(layer);
        int count = Physics2D.OverlapCircle(center, radius, contactFilter, enemyBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null || hit.GetComponentInParent<IPlayerOwnedAlly>() != null)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null || enemyHealth.IsDead ||
                !IsPointInsideCone(center, normalizedDirection, hit.bounds.center, radius, minimumDot))
            {
                continue;
            }

            BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();
            if (turret != null &&
                (turret.IsPlayerAllied || (turret.ShopOwner != null && !turret.ShopOwner.IsHostile)))
            {
                continue;
            }

            EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();

            if (enemyAI != null && enemyAI.IsShopSecurityUnit && !ShopRunBridge.IsShopHostileThisRun())
            {
                continue;
            }

            int id = enemyHealth.GetInstanceID();
            if (!processedTargets.Add(id))
            {
                continue;
            }

            FrigateBossPart frigatePart = hit.GetComponentInParent<FrigateBossPart>();
            if (frigatePart != null)
            {
                Vector2 hitPoint = hit.bounds.center;
                Vector2 incomingDirection = hitPoint - center;
                frigatePart.TryTakeDamage(damage, hitPoint, incomingDirection.normalized);
            }
            else
            {
                enemyHealth.TakeDamage(damage);
            }

            enemyAI?.NotifyDamagedByPlayer();
        }
    }

    private static bool IsPointInsideCone(
        Vector2 center,
        Vector2 direction,
        Vector2 point,
        float radius,
        float minimumDot)
    {
        Vector2 offset = point - center;
        float sqrDistance = offset.sqrMagnitude;

        if (sqrDistance > radius * radius)
        {
            return false;
        }

        if (sqrDistance <= 0.0001f)
        {
            return true;
        }

        return Vector2.Dot(direction, offset / Mathf.Sqrt(sqrDistance)) >= minimumDot;
    }

    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
    }

    private static ContactFilter2D CreateLayerContactFilter(LayerMask layer)
    {
        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(layer);
        contactFilter.useTriggers = true;
        return contactFilter;
    }

    private void DamageNearbyEnemies(Vector2 center, float radius, float damage, LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f || damage <= 0f)
        {
            return;
        }

        processedTargets.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, enemyBuffer, layer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || enemyHealth.IsDead)
            {
                continue;
            }

            BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();
            if (turret != null && turret.IsPlayerAllied)
            {
                continue;
            }

            int id = enemyHealth.GetInstanceID();
            if (processedTargets.Contains(id))
            {
                continue;
            }

            processedTargets.Add(id);

            FrigateBossPart frigatePart = hit.GetComponentInParent<FrigateBossPart>();
            if (frigatePart != null)
            {
                Vector2 hitPoint = hit.bounds.center;
                Vector2 incomingDirection = hitPoint - center;
                frigatePart.TryTakeDamage(damage, hitPoint, incomingDirection.normalized);
            }
            else
            {
                enemyHealth.TakeDamage(damage);
            }

            EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
            if (enemyAI != null)
            {
                enemyAI.NotifyDamagedByPlayer();
            }
        }
    }

    private void KnockbackNearbyEnemies(Vector2 center, float radius, float distance, LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f || distance <= 0f)
        {
            return;
        }

        processedTargets.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, enemyBuffer, layer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null)
            {
                continue;
            }

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();
            if (receiver == null)
            {
                continue;
            }

            BaseTurretController turret = hit.GetComponentInParent<BaseTurretController>();
            if (turret != null && turret.IsPlayerAllied)
            {
                continue;
            }

            Component receiverComponent = receiver as Component;
            int id = receiverComponent != null ? receiverComponent.GetInstanceID() : receiver.GetHashCode();

            if (processedTargets.Contains(id))
            {
                continue;
            }

            processedTargets.Add(id);
            receiver.ApplyKnockback(center, distance);
        }
    }

    private bool DisruptNearbyEnemyTracking(
        Vector2 center,
        float radius,
        float duration,
        LayerMask layer)
    {
        if (layer.value == 0 || radius <= 0f || duration <= 0f)
        {
            return false;
        }

        bool disruptedAny = false;
        processedTargets.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(center, radius, enemyBuffer, layer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null || hit.GetComponentInParent<IPlayerOwnedAlly>() != null)
            {
                continue;
            }

            EnemyBaseAI enemyAI = hit.GetComponentInParent<EnemyBaseAI>();

            if (enemyAI == null || !enemyAI.CanReceiveTrackingDisruption)
            {
                continue;
            }

            int id = enemyAI.GetInstanceID();

            if (!processedTargets.Add(id))
            {
                continue;
            }

            disruptedAny |= enemyAI.ApplyTrackingDisruption(this, duration);
        }

        return disruptedAny;
    }

    private GameObject SpawnEffect(ReinforcementEffect effect, Vector2 position)
    {
        return effect != null
            ? SpawnEffect(effect.Prefab, position, effect.PrefabLifetime, effect)
            : null;
    }

    private GameObject SpawnEffect(GameObject prefab, Vector2 position, float lifetime)
    {
        return SpawnEffect(prefab, position, lifetime, null);
    }

    private GameObject SpawnEffect(
        GameObject prefab,
        Vector2 position,
        float lifetime,
        ReinforcementEffect deploymentEffect)
    {

        if (prefab == null || equippedDefinition == null)
        {
            return null;
        }

        ReleaseSpawnedObjectsForDefinition(equippedDefinition.EquipmentId);

        GameObject instance;

        bool usePool = PoolManager.Instance != null;

        if (usePool)
        {
            instance = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
        }
        else
        {
            instance = Instantiate(prefab, position, Quaternion.identity);
        }

        if (instance == null)
        {
            return null;
        }

        BaseTurretController alliedTurret = instance.GetComponent<BaseTurretController>();
        if (alliedTurret != null)
        {
            alliedTurret.ConfigurePlayerAlly(
                gameObject,
                deploymentEffect != null
                    ? deploymentEffect.SpawnedTurretAttackIntervalMultiplier
                    : 1f,
                deploymentEffect != null
                    ? deploymentEffect.SpawnedTurretDamageMultiplier
                    : 1f,
                deploymentEffect != null
                    ? deploymentEffect.SpawnedTurretTauntDuration
                    : 0f,
                deploymentEffect != null
                    ? deploymentEffect.SpawnedTurretMaxTauntTargets
                    : 0
            );
        }

        PlayerSupportDroneController supportDrone = instance.GetComponent<PlayerSupportDroneController>();
        supportDrone?.Configure(
            gameObject,
            equippedDefinition,
            equippedDefinition.HasEffect(ReinforcementEffectType.ClearEnemyProjectiles)
        );

        PlayerAttractionBeacon attractionBeacon = instance.GetComponent<PlayerAttractionBeacon>();
        attractionBeacon?.Configure(
            gameObject,
            equippedDefinition,
            deploymentEffect != null ? deploymentEffect.Radius : 0f,
            deploymentEffect != null ? deploymentEffect.Duration : 0f,
            lifetime,
            deploymentEffect != null
                ? ResolveLayer(deploymentEffect.TargetLayer, defaultEnemyLayer)
                : defaultEnemyLayer
        );

        PlayerAreaControlField areaControlField = instance.GetComponent<PlayerAreaControlField>();
        areaControlField?.Configure(
            gameObject,
            equippedDefinition,
            deploymentEffect != null ? deploymentEffect.Radius : 0f,
            deploymentEffect != null ? deploymentEffect.Value : 1f,
            lifetime
        );

        float safeLifetime = Mathf.Max(0.01f, lifetime);
        activeSpawnedObjects.Add(new ActiveSpawnedObject
        {
            definitionId = equippedDefinition.EquipmentId,
            instance = instance,
            pooled = usePool,
            expiresAt = Time.time + safeLifetime
        });

        return instance;
    }

    private void PruneReleasedSpawnedObjects()
    {
        for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
        {
            ActiveSpawnedObject spawned = activeSpawnedObjects[i];
            GameObject instance = spawned.instance;

            if (instance == null || !instance.activeSelf)
            {
                activeSpawnedObjects.RemoveAt(i);
                continue;
            }

            if (Time.time >= spawned.expiresAt)
            {
                ReleaseSpawnedObject(spawned);
                activeSpawnedObjects.RemoveAt(i);
            }
        }
    }

    private void ReleaseSpawnedObjectsForDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return;
        }

        for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
        {
            ActiveSpawnedObject spawned = activeSpawnedObjects[i];

            if (spawned.definitionId != definitionId)
            {
                continue;
            }

            ReleaseSpawnedObject(spawned);
            activeSpawnedObjects.RemoveAt(i);
        }
    }

    private void ReleaseAllSpawnedObjects()
    {
        for (int i = activeSpawnedObjects.Count - 1; i >= 0; i--)
        {
            ReleaseSpawnedObject(activeSpawnedObjects[i]);
        }

        activeSpawnedObjects.Clear();
    }

    private static void ReleaseSpawnedObject(ActiveSpawnedObject spawned)
    {
        GameObject instance = spawned != null ? spawned.instance : null;

        if (instance == null || !instance.activeSelf)
        {
            return;
        }

        if (spawned.pooled && PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(instance);
        }
        else
        {
            Destroy(instance);
        }
    }

    private bool SpawnPickup(ReinforcementDefinition definition, int charges, Vector2 position)
    {
        if (definition == null)
        {
            return false;
        }

        ReinforcementPickup pickup = null;

        if (dropPickupPrefab != null)
        {
            if (PoolManager.Instance != null)
            {
                GameObject pooledObject = PoolManager.Instance.Get(dropPickupPrefab.gameObject, position, Quaternion.identity);
                pickup = pooledObject != null ? pooledObject.GetComponent<ReinforcementPickup>() : null;
            }
            else
            {
                pickup = Instantiate(dropPickupPrefab, position, Quaternion.identity);
            }
        }
        else
        {
            GameObject pickupObject = new GameObject($"ReinforcementPickup_{definition.EquipmentId}");
            pickupObject.transform.position = position;
            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            Rigidbody2D rb = pickupObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;

            pickupObject.AddComponent<SpriteRenderer>();
            pickup = pickupObject.AddComponent<ReinforcementPickup>();
        }

        if (pickup == null)
        {
            return false;
        }

        pickup.Initialize(definition, charges, dropPickupBlockSeconds);
        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, position);
        return true;
    }

    private Vector2 ResolveDropPosition()
    {
        Vector2 origin = dropOrigin != null ? dropOrigin.position : transform.position;
        Vector2 direction = fallbackDropDirection;

        if (playerController != null && playerController.AimDirection.sqrMagnitude > 0.001f)
        {
            direction = -playerController.AimDirection;
        }
        else if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        return origin + direction.normalized * Mathf.Max(0.1f, dropDistance);
    }

    private Vector2 ResolveAimDirection()
    {
        if (playerController != null && playerController.AimDirection.sqrMagnitude > 0.001f)
        {
            return playerController.AimDirection.normalized;
        }

        Vector2 fallback = transform.up;
        return NormalizeDirection(fallback);
    }

    private void ReleaseObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(target);
        }
        else
        {
            Destroy(target);
        }
    }

    private LayerMask ResolveLayer(LayerMask preferred, LayerMask fallback)
    {
        return preferred.value != 0 ? preferred : fallback;
    }

    private float ResolveEffectiveRechargeSeconds()
    {
        if (equippedDefinition == null)
        {
            return 0f;
        }

        float seconds = equippedDefinition.RechargeSeconds;

        if (runtimeBonusState != null)
        {
            seconds *= runtimeBonusState.ActiveCooldownMultiplier;
        }

        return Mathf.Max(0f, seconds);
    }

    private ReinforcementDefinition FindDefinition(IReadOnlyList<ReinforcementDefinition> definitions, string equipmentId)
    {
        if (definitions == null || string.IsNullOrWhiteSpace(equipmentId))
        {
            return null;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            ReinforcementDefinition definition = definitions[i];

            if (definition != null && definition.EquipmentId == equipmentId)
            {
                return definition;
            }
        }

        return null;
    }

    private WeaponTreeType ResolveSelectedWeaponTree()
    {
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

    private void PushStateToRunContext()
    {
        if (equippedDefinition == null)
        {
            ShopRunBridge.ClearEquippedReinforcement();
            return;
        }

        ShopRunBridge.SetEquippedReinforcement(equippedDefinition.EquipmentId, currentCharges);
    }

    private void NotifyEquipmentChanged()
    {
        EquipmentChanged?.Invoke(equippedDefinition, currentCharges, MaxCharges);
        NotifyChargesChanged();
    }

    private void NotifyChargesChanged()
    {
        ChargesChanged?.Invoke(currentCharges, MaxCharges, RechargeRatio);
    }

    private void ShowWarning(
        string message,
        ShipCommunicationSeverity severity = ShipCommunicationSeverity.Warning,
        ShipCommunicationChannel channel = ShipCommunicationChannel.Equipment)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowCommunication(
                channel,
                message,
                severity
            );
        }
    }

    private float PercentToMultiplier(float percent)
    {
        return Mathf.Max(0.05f, 1f + percent * 0.01f);
    }

    private float SafeInverse(float value)
    {
        if (Mathf.Abs(value) <= 0.0001f || float.IsNaN(value) || float.IsInfinity(value))
        {
            return 1f;
        }

        return 1f / value;
    }
}
