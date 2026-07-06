using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public class PlayerReinforcementController : MonoBehaviour
{
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

    private InputAction useAction;
    private ReinforcementDefinition equippedDefinition;
    private int currentCharges;
    private float rechargeTimer;

    private readonly Collider2D[] projectileBuffer = new Collider2D[128];
    private readonly Collider2D[] enemyBuffer = new Collider2D[96];
    private readonly HashSet<int> processedTargets = new HashSet<int>();

    public ReinforcementDefinition EquippedDefinition => equippedDefinition;
    public string EquippedReinforcementId => equippedDefinition != null ? equippedDefinition.EquipmentId : string.Empty;
    public bool HasEquipment => equippedDefinition != null;
    public int CurrentCharges => currentCharges;
    public int MaxCharges => equippedDefinition != null ? equippedDefinition.MaxCharges : 0;
    public float RechargeTimer => rechargeTimer;
    public bool IsRecharging => equippedDefinition != null && equippedDefinition.UsesRecharge && currentCharges < equippedDefinition.MaxCharges;
    public float RechargeRatio => equippedDefinition == null || equippedDefinition.RechargeSeconds <= 0f
        ? 0f
        : Mathf.Clamp01(rechargeTimer / equippedDefinition.RechargeSeconds);
    public float RechargeRemainingSeconds => !IsRecharging || equippedDefinition == null
        ? 0f
        : Mathf.Max(0f, equippedDefinition.RechargeSeconds - rechargeTimer);

    public event Action<ReinforcementDefinition, int, int> EquipmentChanged;
    public event Action<int, int, float> ChargesChanged;
    public event Action<ReinforcementDefinition> Used;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindInput();
    }

    private void OnDisable()
    {
        if (useAction != null)
        {
            useAction.Disable();
        }
    }

    private void Update()
    {
        UpdateRecharge(Time.deltaTime);

        if (WasUsePressed())
        {
            TryUseCurrent();
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

        ShowWarning($"장비 장착: {definition.DisplayName}");
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
        if (GameplayPauseManager.IsPaused)
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

        if (GameplayPauseManager.IsPaused)
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

            ConsumeChargeAfterSuccessfulUse();
            return true;
        }

        Vector2 center = transform.position;
        SpawnEffect(useEffectPrefab, center, useEffectLifetime);

        for (int i = 0; i < effects.Count; i++)
        {
            ApplyEffect(effects[i], center);
        }

        ConsumeChargeAfterSuccessfulUse();
        return true;
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
        AudioManager.Play(SoundEventIds.ReinforcementEquip);

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

        if (emergencyReturnController == null)
        {
            emergencyReturnController = GetComponent<EmergencyReturnController>();
        }
    }

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            return;
        }

        useAction = actionMap.FindAction(useActionName, false);
        if (useAction != null)
        {
            useAction.Enable();
        }
    }

    private bool WasUsePressed()
    {
        if (useAction != null && useAction.WasPressedThisFrame())
        {
            return true;
        }

        if (!allowKeyboardFallback || Keyboard.current == null)
        {
            return false;
        }

        KeyControl keyControl = Keyboard.current[fallbackKey];
        return keyControl != null && keyControl.wasPressedThisFrame;
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

        float rechargeSeconds = equippedDefinition.RechargeSeconds;

        if (runtimeBonusState != null)
        {
            rechargeSeconds *= runtimeBonusState.ActiveCooldownMultiplier;
        }

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

    private void ApplyEffect(ReinforcementEffect effect, Vector2 center)
    {
        if (effect == null)
        {
            return;
        }

        switch (effect.EffectType)
        {
            case ReinforcementEffectType.HealFlat:
                if (playerHealth != null)
                {
                    playerHealth.Heal(effect.Value);
                }
                break;

            case ReinforcementEffectType.AddArmor:
                ApplyArmor(effect.Value);
                break;

            case ReinforcementEffectType.AddInvincibleTime:
                if (playerHealth != null)
                {
                    playerHealth.AddInvincibleTime(effect.Value);
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
                StartCoroutine(TemporaryDamageRoutine(effect.Value, effect.Duration));
                break;

            case ReinforcementEffectType.TemporaryFireRatePercent:
                StartCoroutine(TemporaryFireRateRoutine(effect.Value, effect.Duration));
                break;

            case ReinforcementEffectType.TemporaryMoveSpeedPercent:
                StartCoroutine(TemporaryMoveSpeedRoutine(effect.Value, effect.Duration));
                break;

            case ReinforcementEffectType.TemporaryDashCooldownReductionPercent:
                StartCoroutine(TemporaryDashCooldownRoutine(effect.Value, effect.Duration));
                break;

            case ReinforcementEffectType.SpawnPrefabAtPlayer:
                SpawnEffect(effect.Prefab, center, effect.PrefabLifetime);
                break;

            case ReinforcementEffectType.EmergencyReturn:
                break;
        }
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

        return emergencyReturnController.TryStartByExternalHoldKey(fallbackKey);
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

            ShopProjectile shopProjectile = hit.GetComponentInParent<ShopProjectile>();
            if (shopProjectile != null)
            {
                shopProjectile.ReleaseSelf();
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

            int id = enemyHealth.GetInstanceID();
            if (processedTargets.Contains(id))
            {
                continue;
            }

            processedTargets.Add(id);
            enemyHealth.TakeDamage(damage);

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

    private IEnumerator TemporaryDamageRoutine(float percent, float duration)
    {
        if (weaponModifiers == null || duration <= 0f)
        {
            yield break;
        }

        float multiplier = PercentToMultiplier(percent);
        weaponModifiers.MultiplyDamage(multiplier);
        yield return new WaitForSeconds(duration);

        if (weaponModifiers != null)
        {
            weaponModifiers.MultiplyDamage(SafeInverse(multiplier));
        }
    }

    private IEnumerator TemporaryFireRateRoutine(float percent, float duration)
    {
        if (weaponModifiers == null || duration <= 0f)
        {
            yield break;
        }

        float multiplier = PercentToMultiplier(percent);
        weaponModifiers.MultiplyFireRate(multiplier);
        yield return new WaitForSeconds(duration);

        if (weaponModifiers != null)
        {
            weaponModifiers.MultiplyFireRate(SafeInverse(multiplier));
        }
    }

    private IEnumerator TemporaryMoveSpeedRoutine(float percent, float duration)
    {
        if (playerController == null || duration <= 0f)
        {
            yield break;
        }

        float multiplier = PercentToMultiplier(percent);
        playerController.SetMoveSpeed(playerController.MoveSpeed * multiplier);
        yield return new WaitForSeconds(duration);

        if (playerController != null)
        {
            playerController.SetMoveSpeed(playerController.MoveSpeed * SafeInverse(multiplier));
        }
    }

    private IEnumerator TemporaryDashCooldownRoutine(float reductionPercent, float duration)
    {
        if (playerDash == null || duration <= 0f)
        {
            yield break;
        }

        float reduction = Mathf.Clamp01(Mathf.Abs(reductionPercent) * 0.01f);
        float multiplier = Mathf.Clamp(1f - reduction, 0.05f, 1f);

        playerDash.SetDashCooldown(playerDash.DashCooldown * multiplier);
        yield return new WaitForSeconds(duration);

        if (playerDash != null)
        {
            playerDash.SetDashCooldown(playerDash.DashCooldown * SafeInverse(multiplier));
        }
    }

    private void SpawnEffect(GameObject prefab, Vector2 position, float lifetime)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance;

        if (PoolManager.Instance != null)
        {
            instance = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            PoolManager.Instance.ReleaseAfter(instance, Mathf.Max(0.01f, lifetime));
        }
        else
        {
            instance = Instantiate(prefab, position, Quaternion.identity);
            Destroy(instance, Mathf.Max(0.01f, lifetime));
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

    private void ShowWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowWarning(message);
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
