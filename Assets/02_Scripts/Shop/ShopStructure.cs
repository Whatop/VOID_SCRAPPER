using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ShopStructureState
{
    Neutral,
    Warning,
    HostileIdle,
    Combat,
    Dead
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ShopStructure : MonoBehaviour, IDamageable, IInteractable, IKnockbackReceiver
{
    private static readonly List<ShopStructure> ActiveShops = new List<ShopStructure>();
    private static bool globalHostile;

    public static event Action GlobalHostilityChanged;

    [Header("Identity")]
    [SerializeField] private string displayName = "상점 구조선";
    [SerializeField] private string neutralInteractionText = "상점 이용";
    [SerializeField] private string hostileInteractionText = "적대화된 상점";

    [Header("References")]
    [SerializeField] private ShopTradeUI tradeUI;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private RewardDropper rewardDropper;
    [SerializeField] private ShopActiveMaintenanceBay activeMaintenanceBay;

    [Header("Health")]
    [SerializeField] private float maxShield = 24f;
    [SerializeField] private float maxBodyHp = 42f;

    [Header("Warning")]
    [SerializeField] private float warningDuration = 3f;
    [SerializeField] private float shieldRecoverDuration = 3f;
    [SerializeField] private GameObject warningRingObject;
    [SerializeField] private AudioSource warningAudioSource;

    [Header("Shield Break Burst")]
    [SerializeField] private LayerMask projectileClearLayer;
    [SerializeField] private float shieldBreakProjectileClearRadius = 4.5f;
    [SerializeField] private LayerMask knockbackLayer;
    [SerializeField] private float shieldBreakKnockbackRadius = 4f;
    [SerializeField] private float shieldBreakKnockbackDistance = 3f;
    [SerializeField] private GameObject shieldBreakEffectPrefab;
    [SerializeField] private float shieldBreakEffectLifetime = 0.6f;

    [Header("Trade - Repair")]
    [SerializeField] private int repairCost = 25;
    [SerializeField] private float repairAmount = 8f;

    [Header("Hostile Detection")]
    [SerializeField] private float hostileDetectRange = 18f;
    [SerializeField] private string playerTag = "Player";

    [Header("Hostile Attack")]
    [SerializeField] private GameObject shopProjectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private int pelletCount = 5;
    [SerializeField] private float spreadAngle = 70f;
    [SerializeField] private float projectileSpeed = 9f;
    [SerializeField] private float projectileDamage = 2f;
    [SerializeField] private float projectileLifetime = 4f;

    [Header("Security Drone")]
    [SerializeField] private GameObject[] securityDronePrefabs;
    [SerializeField] private int dronesOnCombatStart = 2;
    [SerializeField] private float droneSummonInterval = 8f;
    [SerializeField] private int maxActiveDrones = 4;
    [SerializeField] private float droneSpawnRadius = 2.5f;

    [Header("Death Reward")]
    [SerializeField] private int minRewardCredits = 10;
    [SerializeField] private int maxRewardCredits = 25;
    [SerializeField] private int minRewardScrap = 4;
    [SerializeField] private int maxRewardScrap = 6;
    [Range(0f, 1f)]
    [SerializeField] private float componentShieldDropChance = 0.25f;
    [SerializeField] private int duplicateComponentShieldScrap = 3;

    [Header("Death")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider2D[] collidersToDisableOnDeath;
    [SerializeField] private bool disableRenderersOnDeath = true;
    [SerializeField] private Renderer[] renderersToDisableOnDeath;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float deathEffectLifetime = 0.8f;
    [SerializeField] private bool releaseOnDeath = false;
    [SerializeField] private float releaseDelay = 0.1f;

    private float currentShield;
    private float currentBodyHp;
    private int spentCredits;
    private bool combatStarted;
    private float attackTimer;
    private float droneSummonTimer;
    private Transform targetPlayer;
    private Coroutine warningRoutine;

    private readonly Collider2D[] projectileBuffer = new Collider2D[128];
    private readonly Collider2D[] knockbackBuffer = new Collider2D[96];
    private readonly List<GameObject> activeDrones = new List<GameObject>();

    [SerializeField] private ReinforcementPickup reinforcementPickupPrefab;
    [SerializeField] private Transform reinforcementDropPoint;

 
    public string DisplayName => displayName;
    public ShopStructureState CurrentState { get; private set; } = ShopStructureState.Neutral;
    public string InteractionText => CanTrade ? neutralInteractionText : hostileInteractionText;
    public ShopActiveMaintenanceBay ActiveMaintenanceBay => EnsureActiveMaintenanceBay();

    public bool IsDead => CurrentState == ShopStructureState.Dead;
    public float CurrentShield => currentShield;
    public float MaxShield => maxShield;
    public float CurrentBodyHp => currentBodyHp;
    public float MaxBodyHp => maxBodyHp;
    public int RepairCost => repairCost;
    public float RepairAmount => repairAmount;
    public int SpentCredits => spentCredits;
    public bool CanTrade => !globalHostile && CurrentState != ShopStructureState.Dead;

    public event Action<ShopStructure> StateChanged;
    public event Action<ShopStructure, float, float> ShieldChanged;
    public event Action<ShopStructure, float, float> BodyHpChanged;
    public event Action<ShopStructure> Died;

    private void Reset()
    {
        radarTarget = GetComponent<RadarTarget>();
        rewardDropper = GetComponent<RewardDropper>();
        activeMaintenanceBay = GetComponentInChildren<ShopActiveMaintenanceBay>(true);
        collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>(true);
        renderersToDisableOnDeath = GetComponentsInChildren<Renderer>(true);

        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    private void Awake()
    {
        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        EnsureActiveMaintenanceBay();

        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
        {
            collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>(true);
        }

        if (renderersToDisableOnDeath == null || renderersToDisableOnDeath.Length == 0)
        {
            renderersToDisableOnDeath = GetComponentsInChildren<Renderer>(true);
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    private void OnEnable()
    {
        if (!ActiveShops.Contains(this))
        {
            ActiveShops.Add(this);
        }

        SyncGlobalHostilityFromRun();
        GlobalHostilityChanged += HandleGlobalHostilityChanged;
        ResetShop();
    }

    private void OnDisable()
    {
        ActiveShops.Remove(this);
        GlobalHostilityChanged -= HandleGlobalHostilityChanged;

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }
    }

    private void Update()
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (!globalHostile)
        {
            return;
        }

        UpdateHostileState();
    }
    public bool TryDropReinforcementToField(ReinforcementDefinition definition)
    {
        return TryDropReinforcementToField(definition, -1);
    }

    public bool TryDropReinforcementToField(ReinforcementDefinition definition, int charges)
    {
        if (definition == null)
        {
            return false;
        }

        Vector3 dropPosition = reinforcementDropPoint != null
            ? reinforcementDropPoint.position
            : transform.position + Vector3.down;

        ReinforcementPickup pickup = null;

        if (reinforcementPickupPrefab != null)
        {
            pickup = Instantiate(reinforcementPickupPrefab, dropPosition, Quaternion.identity);
        }
        else
        {
            GameObject pickupObject = new GameObject($"ReinforcementPickup_{definition.EquipmentId}");
            pickupObject.transform.position = dropPosition;

            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.45f;

            Rigidbody2D rigidbody2D = pickupObject.AddComponent<Rigidbody2D>();
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;

            pickupObject.AddComponent<SpriteRenderer>();
            pickup = pickupObject.AddComponent<ReinforcementPickup>();
        }

        if (pickup == null)
        {
            return false;
        }

        pickup.Initialize(definition, charges, 0.5f);
        return true;
    }
    public static void SyncGlobalHostilityFromRun()
    {
        bool hostileFromRun = ShopRunBridge.IsShopHostileThisRun();

        if (globalHostile == hostileFromRun)
        {
            return;
        }

        globalHostile = hostileFromRun;
        GlobalHostilityChanged?.Invoke();
    }

    public static void ResetGlobalHostility(bool writeRunFlag)
    {
        globalHostile = false;

        if (writeRunFlag)
        {
            ShopRunBridge.SetShopHostileThisRun(false);
        }

        GlobalHostilityChanged?.Invoke();
    }

    public static void SetGlobalHostile()
    {
        if (globalHostile)
        {
            return;
        }

        globalHostile = true;
        ShopRunBridge.SetShopHostileThisRun(true);
        GlobalHostilityChanged?.Invoke();
    }

    private ShopActiveMaintenanceBay EnsureActiveMaintenanceBay()
    {
        if (activeMaintenanceBay != null)
        {
            return activeMaintenanceBay;
        }

        activeMaintenanceBay = GetComponentInChildren<ShopActiveMaintenanceBay>(true);

        if (activeMaintenanceBay == null)
        {
            activeMaintenanceBay = GetComponent<ShopActiveMaintenanceBay>();
        }

        if (activeMaintenanceBay == null)
        {
            activeMaintenanceBay = gameObject.AddComponent<ShopActiveMaintenanceBay>();
        }

        return activeMaintenanceBay;
    }

    public void ResetShop()
    {
        EnsureActiveMaintenanceBay();
        currentShield = globalHostile ? 0f : maxShield;
        currentBodyHp = maxBodyHp;
        spentCredits = 0;
        combatStarted = false;
        attackTimer = 0f;
        droneSummonTimer = 0f;
        targetPlayer = null;

        SetWarningVisual(false);

        CurrentState = globalHostile
            ? ShopStructureState.HostileIdle
            : ShopStructureState.Neutral;

        SetCollidersEnabled(true);
        SetRenderersEnabled(true);

        ShieldChanged?.Invoke(this, currentShield, maxShield);
        BodyHpChanged?.Invoke(this, currentBodyHp, maxBodyHp);
        StateChanged?.Invoke(this);
    }

    public bool CanInteract(GameObject interactor)
    {
        return CanTrade;
    }

    public void Interact(GameObject interactor)
    {
        OpenTradeUI(interactor != null ? interactor : FindPlayerObject());
    }

    private void OpenTradeUI(GameObject playerObject)
    {
        if (!CanTrade)
        {
            return;
        }

        if (tradeUI == null)
        {
            tradeUI = FindFirstObjectByType<ShopTradeUI>(FindObjectsInactive.Include);
        }

        if (tradeUI == null)
        {
            Debug.LogWarning("ShopTradeUI가 씬에 없습니다.", this);
            return;
        }

        tradeUI.Open(this, playerObject);
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (!globalHostile)
        {
            DamageShield(damage);
            return;
        }

        DamageBody(damage);
    }

    private void DamageShield(float damage)
    {
        currentShield = Mathf.Max(0f, currentShield - damage);
        ShieldChanged?.Invoke(this, currentShield, maxShield);

        if (currentShield <= 0f)
        {
            BreakShieldAndBecomeHostile();
            return;
        }

        EnterWarningState();
    }

    private void DamageBody(float damage)
    {
        currentBodyHp = Mathf.Max(0f, currentBodyHp - damage);
        BodyHpChanged?.Invoke(this, currentBodyHp, maxBodyHp);

        if (currentBodyHp <= 0f)
        {
            Die();
        }
    }

    private void EnterWarningState()
    {
        if (CurrentState == ShopStructureState.Dead || globalHostile)
        {
            return;
        }

        CurrentState = ShopStructureState.Warning;
        SetWarningVisual(true);
        StateChanged?.Invoke(this);

        if (warningAudioSource != null)
        {
            warningAudioSource.Play();
        }

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
        }

        warningRoutine = StartCoroutine(WarningRecoverRoutine());
    }

    private IEnumerator WarningRecoverRoutine()
    {
        yield return new WaitForSeconds(warningDuration);

        float startShield = currentShield;
        float timer = 0f;
        float recoverDuration = Mathf.Max(0.01f, shieldRecoverDuration);

        while (timer < recoverDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / recoverDuration);
            currentShield = Mathf.Lerp(startShield, maxShield, t);
            ShieldChanged?.Invoke(this, currentShield, maxShield);
            yield return null;
        }

        currentShield = maxShield;
        ShieldChanged?.Invoke(this, currentShield, maxShield);
        SetWarningVisual(false);

        if (!globalHostile && CurrentState != ShopStructureState.Dead)
        {
            CurrentState = ShopStructureState.Neutral;
            StateChanged?.Invoke(this);
        }

        warningRoutine = null;
    }

    private void BreakShieldAndBecomeHostile()
    {
        currentShield = 0f;
        ShieldChanged?.Invoke(this, currentShield, maxShield);

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        SetWarningVisual(false);
        TriggerShieldBreakBurst();
        SetGlobalHostile();
    }

    private void HandleGlobalHostilityChanged()
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (globalHostile)
        {
            currentShield = 0f;
            CurrentState = ShopStructureState.HostileIdle;
        }
        else
        {
            currentShield = maxShield;
            CurrentState = ShopStructureState.Neutral;
        }

        ShieldChanged?.Invoke(this, currentShield, maxShield);
        StateChanged?.Invoke(this);
    }

    private void UpdateHostileState()
    {
        if (targetPlayer == null)
        {
            GameObject playerObject = FindPlayerObject();
            targetPlayer = playerObject != null ? playerObject.transform : null;
        }

        if (targetPlayer == null)
        {
            CurrentState = ShopStructureState.HostileIdle;
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        if (distance > hostileDetectRange)
        {
            CurrentState = ShopStructureState.HostileIdle;
            combatStarted = false;
            return;
        }

        if (!combatStarted)
        {
            StartCombat();
        }

        CurrentState = ShopStructureState.Combat;
        attackTimer -= Time.deltaTime;
        droneSummonTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            FireShotgunPattern();
        }

        CleanupDroneList();

        if (droneSummonTimer <= 0f)
        {
            droneSummonTimer = droneSummonInterval;
            TrySummonSecurityDrones(1);
        }
    }

    private void StartCombat()
    {
        combatStarted = true;
        attackTimer = 0.2f;
        droneSummonTimer = droneSummonInterval;
        TrySummonSecurityDrones(dronesOnCombatStart);
    }

    private void FireShotgunPattern()
    {
        if (shopProjectilePrefab == null || targetPlayer == null)
        {
            return;
        }

        Vector2 origin = firePoint != null ? firePoint.position : transform.position;
        Vector2 baseDirection = ((Vector2)targetPlayer.position - origin).normalized;

        if (baseDirection.sqrMagnitude <= 0.001f)
        {
            baseDirection = transform.up;
        }

        int count = Mathf.Max(1, pelletCount);
        float totalAngle = Mathf.Max(0f, spreadAngle);
        float step = count <= 1 ? 0f : totalAngle / (count - 1);
        float startAngle = -totalAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + (step * i);
            Vector2 direction = Rotate(baseDirection, angle);
            SpawnProjectile(origin, direction);
        }
    }

    private void SpawnProjectile(Vector2 position, Vector2 direction)
    {
        GameObject projectile;

        if (PoolManager.Instance != null)
        {
            projectile = PoolManager.Instance.Get(shopProjectilePrefab, position, Quaternion.identity);
        }
        else
        {
            projectile = Instantiate(shopProjectilePrefab, position, Quaternion.identity);
        }

        if (projectile == null)
        {
            return;
        }

        ShopProjectile shopProjectile = projectile.GetComponent<ShopProjectile>();

        if (shopProjectile != null)
        {
            shopProjectile.Initialize(direction, projectileSpeed, projectileDamage, projectileLifetime);
        }
    }

    private void TrySummonSecurityDrones(int count)
    {
        if (securityDronePrefabs == null || securityDronePrefabs.Length == 0)
        {
            return;
        }

        CleanupDroneList();

        int summonCount = Mathf.Max(0, count);

        for (int i = 0; i < summonCount; i++)
        {
            if (activeDrones.Count >= maxActiveDrones)
            {
                return;
            }

            GameObject prefab = GetRandomDronePrefab();

            if (prefab == null)
            {
                continue;
            }

            Vector2 direction = Rotate(Vector2.up, UnityEngine.Random.Range(0f, 360f));
            Vector3 position = transform.position + (Vector3)(direction * droneSpawnRadius);

            GameObject drone;

            if (PoolManager.Instance != null)
            {
                drone = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            }
            else
            {
                drone = Instantiate(prefab, position, Quaternion.identity);
            }

            if (drone == null)
            {
                continue;
            }

            activeDrones.Add(drone);
            TrySetDroneTarget(drone);
        }
    }

    private GameObject GetRandomDronePrefab()
    {
        if (securityDronePrefabs == null || securityDronePrefabs.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < 16; i++)
        {
            GameObject prefab = securityDronePrefabs[UnityEngine.Random.Range(0, securityDronePrefabs.Length)];

            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private void TrySetDroneTarget(GameObject drone)
    {
        if (drone == null || targetPlayer == null)
        {
            return;
        }

        EnemyBaseAI enemyAI = drone.GetComponentInChildren<EnemyBaseAI>(true);

        if (enemyAI != null)
        {
            enemyAI.SetTarget(targetPlayer);
            enemyAI.ApplyRadarAlert(transform.position);
        }
    }

    private void CleanupDroneList()
    {
        for (int i = activeDrones.Count - 1; i >= 0; i--)
        {
            if (activeDrones[i] == null || !activeDrones[i].activeInHierarchy)
            {
                activeDrones.RemoveAt(i);
            }
        }
    }

    public bool CanBuyRepair(GameObject playerObject)
    {
        if (!CanTrade || !ShopRunBridge.CanSpendCredits(repairCost))
        {
            return false;
        }

        PlayerHealth health = ResolvePlayerHealth(playerObject);
        return health != null && !health.IsDead && health.CurrentHp < health.MaxHp;
    }

    public bool TryBuyRepair(GameObject playerObject)
    {
        if (!CanBuyRepair(playerObject))
        {
            return false;
        }

        if (!ShopRunBridge.TrySpendCredits(repairCost))
        {
            return false;
        }

        PlayerHealth health = ResolvePlayerHealth(playerObject);

        if (health == null)
        {
            ShopRunBridge.AddCredits(repairCost);
            return false;
        }

        float finalRepairAmount = repairAmount;
        PlayerRuntimeBonusState bonusState = health.GetComponent<PlayerRuntimeBonusState>();

        if (bonusState != null)
        {
            finalRepairAmount = bonusState.ApplyRepairAmount(repairAmount);
        }

        spentCredits += repairCost;
        health.Heal(finalRepairAmount);
        return true;
    }

    public void RegisterSpentCredits(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        spentCredits += amount;
    }

    public void ApplyKnockback(Vector2 origin, float distance)
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb == null || distance <= 0f)
        {
            return;
        }

        Vector2 direction = (Vector2)transform.position - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = UnityEngine.Random.insideUnitCircle;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        rb.position += direction.normalized * distance;
    }

    private PlayerHealth ResolvePlayerHealth(GameObject playerObject)
    {
        if (playerObject != null)
        {
            PlayerHealth health = playerObject.GetComponentInChildren<PlayerHealth>(true);
            if (health != null)
            {
                return health;
            }
        }

        return FindFirstObjectByType<PlayerHealth>();
    }

    private void TriggerShieldBreakBurst()
    {
        Vector2 center = transform.position;

        SpawnEffect(shieldBreakEffectPrefab, center, shieldBreakEffectLifetime);
        ClearProjectiles(center, shieldBreakProjectileClearRadius);
        PushNearbyObjects(center, shieldBreakKnockbackRadius, shieldBreakKnockbackDistance);
    }

    private void ClearProjectiles(Vector2 center, float radius)
    {
        if (projectileClearLayer.value == 0 || radius <= 0f)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            radius,
            projectileBuffer,
            projectileClearLayer
        );

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

            if (bullet != null)
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(bullet.gameObject);
                }
                else
                {
                    Destroy(bullet.gameObject);
                }
            }
        }
    }

    private void PushNearbyObjects(Vector2 center, float radius, float distance)
    {
        if (knockbackLayer.value == 0 || radius <= 0f || distance <= 0f)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            radius,
            knockbackBuffer,
            knockbackLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = knockbackBuffer[i];

            if (hit == null)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();

            if (receiver != null)
            {
                receiver.ApplyKnockback(center, distance);
                continue;
            }

            Rigidbody2D targetRb = hit.attachedRigidbody;

            if (targetRb == null)
            {
                continue;
            }

            Vector2 direction = targetRb.position - center;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = UnityEngine.Random.insideUnitCircle;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            targetRb.position += direction.normalized * distance;
        }
    }

    private void Die()
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        CurrentState = ShopStructureState.Dead;
        StateChanged?.Invoke(this);

        SetWarningVisual(false);

        if (disableCollidersOnDeath)
        {
            SetCollidersEnabled(false);
        }

        if (disableRenderersOnDeath)
        {
            SetRenderersEnabled(false);
        }

        SpawnEffect(deathEffectPrefab, transform.position, deathEffectLifetime);

        if (rewardDropper != null)
        {
            rewardDropper.DropAt(transform.position);
        }

        GiveDeathRewards();
        Died?.Invoke(this);

        if (releaseOnDeath)
        {
            StartCoroutine(ReleaseAfterDeathRoutine());
        }
    }

    private void GiveDeathRewards()
    {
        int rewardCredits = UnityEngine.Random.Range(minRewardCredits, maxRewardCredits + 1);
        int rewardScrap = UnityEngine.Random.Range(minRewardScrap, maxRewardScrap + 1);
        int refundCredits = Mathf.FloorToInt(spentCredits * 0.5f);

        GameObject playerObject = FindPlayerObject();
        PlayerRuntimeBonusState bonusState = playerObject != null ? playerObject.GetComponent<PlayerRuntimeBonusState>() : null;

        if (bonusState != null)
        {
            rewardCredits = bonusState.ApplyCurrencyGain(CurrencyType.Credits, rewardCredits);
            rewardScrap = bonusState.ApplyCurrencyGain(CurrencyType.ScrapParts, rewardScrap);
        }

        ShopRunBridge.AddCredits(rewardCredits + refundCredits);
        ShopRunBridge.AddScrapParts(rewardScrap);

        if (UnityEngine.Random.value <= componentShieldDropChance)
        {
            GiveComponentShieldReward(playerObject);
        }
    }

    private void GiveComponentShieldReward(GameObject playerObject)
    {
        if (playerObject == null)
        {
            ShopRunBridge.AddScrapParts(duplicateComponentShieldScrap);
            return;
        }

        ComponentShieldPassive existingShield = playerObject.GetComponent<ComponentShieldPassive>();

        if (existingShield != null && existingShield.enabled)
        {
            ShopRunBridge.AddScrapParts(duplicateComponentShieldScrap);
            return;
        }

        if (existingShield == null)
        {
            existingShield = playerObject.AddComponent<ComponentShieldPassive>();
        }

        existingShield.enabled = true;
        existingShield.RechargeNow();
    }

    private IEnumerator ReleaseAfterDeathRoutine()
    {
        yield return new WaitForSeconds(releaseDelay);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetCollidersEnabled(bool value)
    {
        if (collidersToDisableOnDeath == null)
        {
            return;
        }

        foreach (Collider2D col in collidersToDisableOnDeath)
        {
            if (col != null)
            {
                col.enabled = value;
            }
        }
    }

    private void SetRenderersEnabled(bool value)
    {
        if (renderersToDisableOnDeath == null)
        {
            return;
        }

        foreach (Renderer targetRenderer in renderersToDisableOnDeath)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = value;
            }
        }
    }

    private void SetWarningVisual(bool value)
    {
        if (warningRingObject != null)
        {
            warningRingObject.SetActive(value);
        }
    }

    private void SpawnEffect(GameObject prefab, Vector2 position, float lifetime)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject effect;

        if (PoolManager.Instance != null)
        {
            effect = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            PoolManager.Instance.ReleaseAfter(effect, Mathf.Max(0.01f, lifetime));
        }
        else
        {
            effect = Instantiate(prefab, position, Quaternion.identity);
            Destroy(effect, Mathf.Max(0.01f, lifetime));
        }
    }

    private GameObject FindPlayerObject()
    {
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);

            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        return playerHealth != null ? playerHealth.gameObject : null;
    }

    private static Vector2 Rotate(Vector2 vector, float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, hostileDetectRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shieldBreakProjectileClearRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shieldBreakKnockbackRadius);
    }
}