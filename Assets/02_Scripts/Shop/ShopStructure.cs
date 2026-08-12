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
    [SerializeField] private ShopDefenseController2D defenseController;
    [SerializeField] private ShopNeutralZone2D neutralZone;

    [Header("Field Base Portal")]
    [Tooltip("적 기지 포탈에서 이동했을 때 플레이어가 도착할 안전한 위치입니다. 상점 BodyCollider 밖에 두세요.")]
    [SerializeField] private Transform portalArrivalPoint;

    [Header("Health")]
    [SerializeField] private float maxShield = 24f;
    [SerializeField] private float maxBodyHp = 42f;

    [Header("Hit Feedback")]
    [SerializeField] private bool useProceduralHitEffect = true;
    [SerializeField] private float shieldHitEffectIntensity = 1.05f;
    [SerializeField] private float bodyHitEffectIntensity = 1f;
    [SerializeField] private float hitShakeAmplitude = 0.035f;
    [SerializeField] private float hitShakeDuration = 0.06f;
    [SerializeField] private float shieldBreakShakeAmplitude = 0.2f;
    [SerializeField] private float shieldBreakShakeDuration = 0.2f;
    [SerializeField] private float deathShakeAmplitude = 0.28f;
    [SerializeField] private float deathShakeDuration = 0.3f;

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

    [Header("Physics Safety")]
    [Tooltip("상점은 거래/전투 중심점이므로 기본적으로 외부 넉백을 받지 않습니다.")]
    [SerializeField] private bool allowExternalKnockback;
    [Tooltip("Rigidbody2D가 실수로 붙어 있어도 움직이지 않도록 Kinematic으로 고정합니다.")]
    [SerializeField] private bool enforceImmovableRigidbody = true;

    [Header("Trade - Repair")]
    [SerializeField] private int repairCost = 25;
    [SerializeField] private float repairAmount = 8f;

    [Header("Hostile Detection")]
    [SerializeField] private float hostileDetectRange = 18f;
    [SerializeField] private string playerTag = "Player";

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

    private Rigidbody2D structureBody;
    private float currentShield;
    private float currentBodyHp;
    private int spentCredits;
    private bool combatStarted;
    private Transform targetPlayer;
    private Coroutine warningRoutine;

    private readonly Collider2D[] projectileBuffer = new Collider2D[128];
    private readonly Collider2D[] knockbackBuffer = new Collider2D[96];

    [Header("Maintenance Field Drop")]
    [SerializeField] private ReinforcementPickup reinforcementPickupPrefab;
    [SerializeField] private Transform reinforcementDropPoint;
    [Min(0.1f)]
    [SerializeField] private float fallbackReinforcementDropDistance = 1.25f;
    [SerializeField] private bool createVisibleFallbackPickupWhenPrefabMissing = true;
    [SerializeField] private string fallbackPickupLayerName = "Interactable";
    [SerializeField] private string fallbackPickupSortingLayerName = "Default";
    [SerializeField] private int fallbackPickupSortingOrder = 20;

 
    public string DisplayName => displayName;
    public Transform PortalArrivalPoint => portalArrivalPoint;
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
    public bool IsHostile => globalHostile && CurrentState != ShopStructureState.Dead;
    public bool IsNeutralSafeZoneActive =>
        !globalHostile &&
        (CurrentState == ShopStructureState.Neutral || CurrentState == ShopStructureState.Warning);
    public float HostileDetectRange => Mathf.Max(0.1f, hostileDetectRange);

    public event Action<ShopStructure> StateChanged;
    public event Action<ShopStructure, float, float> ShieldChanged;
    public event Action<ShopStructure, float, float> BodyHpChanged;
    public event Action<ShopStructure> Died;

    private void Reset()
    {
        structureBody = GetComponent<Rigidbody2D>();
        radarTarget = GetComponent<RadarTarget>();
        rewardDropper = GetComponent<RewardDropper>();
        activeMaintenanceBay = GetComponentInChildren<ShopActiveMaintenanceBay>(true);
        defenseController = GetComponent<ShopDefenseController2D>();
        neutralZone = GetComponentInChildren<ShopNeutralZone2D>(true);

        if (portalArrivalPoint == null)
        {
            portalArrivalPoint = transform.Find("PortalArrivalPoint");
        }

        collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>(true);
        renderersToDisableOnDeath = GetComponentsInChildren<Renderer>(true);
    }

    private void Awake()
    {
        structureBody = GetComponent<Rigidbody2D>();
        EnforceImmovableBody();

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        EnsureActiveMaintenanceBay();

        if (defenseController == null)
        {
            defenseController = GetComponent<ShopDefenseController2D>();
        }

        if (neutralZone == null)
        {
            neutralZone = GetComponentInChildren<ShopNeutralZone2D>(true);
        }

        if (portalArrivalPoint == null)
        {
            portalArrivalPoint = transform.Find("PortalArrivalPoint");
        }

        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
        {
            collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>(true);
        }

        if (renderersToDisableOnDeath == null || renderersToDisableOnDeath.Length == 0)
        {
            renderersToDisableOnDeath = GetComponentsInChildren<Renderer>(true);
        }
    }

    private void OnEnable()
    {
        EnforceImmovableBody();

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

        defenseController?.SetCombatActive(false, null);
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

        Vector3 dropPosition = ResolveReinforcementDropPosition();
        ReinforcementPickup pickup = reinforcementPickupPrefab != null
            ? Instantiate(reinforcementPickupPrefab, dropPosition, Quaternion.identity)
            : CreateFallbackReinforcementPickup(definition, dropPosition);

        if (pickup == null)
        {
            Debug.LogWarning(
                $"[{name}] ReinforcementPickup 프리팹이 없고 fallback 생성도 비활성화되어 필드 드랍에 실패했습니다.",
                this
            );
            return false;
        }

        GameObject pickupObject = pickup.gameObject;
        pickupObject.transform.SetPositionAndRotation(dropPosition, Quaternion.identity);

        if (!pickupObject.activeSelf)
        {
            pickupObject.SetActive(true);
        }

        pickup.Initialize(definition, charges, 0.35f);
        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, dropPosition);
        return true;
    }

    private Vector3 ResolveReinforcementDropPosition()
    {
        if (reinforcementDropPoint != null)
        {
            return reinforcementDropPoint.position;
        }

        GameObject playerObject = FindPlayerObject();

        if (playerObject == null)
        {
            return transform.position + Vector3.down * Mathf.Max(0.1f, fallbackReinforcementDropDistance);
        }

        Vector2 direction = (Vector2)playerObject.transform.position - (Vector2)transform.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        return playerObject.transform.position +
               (Vector3)(direction.normalized * Mathf.Max(0.1f, fallbackReinforcementDropDistance));
    }

    private ReinforcementPickup CreateFallbackReinforcementPickup(
        ReinforcementDefinition definition,
        Vector3 position)
    {
        if (!createVisibleFallbackPickupWhenPrefabMissing)
        {
            return null;
        }

        GameObject pickupObject = new GameObject($"ReinforcementPickup_{definition.EquipmentId}");
        pickupObject.transform.position = position;

        int interactableLayer = LayerMask.NameToLayer(fallbackPickupLayerName);
        if (interactableLayer >= 0)
        {
            pickupObject.layer = interactableLayer;
        }

        CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;

        Rigidbody2D rigidbody2D = pickupObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.simulated = true;

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(pickupObject.transform, false);
        iconObject.layer = pickupObject.layer;

        SpriteRenderer iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = definition.Icon;
        iconRenderer.color = Color.white;
        iconRenderer.sortingLayerName = fallbackPickupSortingLayerName;
        iconRenderer.sortingOrder = fallbackPickupSortingOrder;

        ReinforcementPickup pickup = pickupObject.AddComponent<ReinforcementPickup>();

        if (definition.Icon == null)
        {
            Debug.LogWarning(
                $"[{name}] {definition.DisplayName} 아이콘이 비어 있어 fallback 필드 드랍이 보이지 않을 수 있습니다.",
                definition
            );
        }

        return pickup;
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
        currentShield = maxShield;
        currentBodyHp = maxBodyHp;
        spentCredits = 0;
        combatStarted = false;
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
        TakeDamage(damage, transform.position, Vector2.zero);
    }

    public void TakeDamage(float damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        if (damage <= 0f || CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (currentShield > 0f)
        {
            DamageShield(damage, hitPoint, incomingDirection);
            return;
        }

        DamageBody(damage, hitPoint, incomingDirection);
    }

    private void DamageShield(float damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        currentShield = Mathf.Max(0f, currentShield - damage);

        CombatFeedbackManager.PlayHit(
            hitPoint,
            incomingDirection,
            CombatFeedbackKind.Shield,
            shieldHitEffectIntensity,
            hitShakeAmplitude,
            hitShakeDuration,
            useProceduralHitEffect
        );

        AudioManager.PlayAt(SoundEventIds.ShopShieldHit, hitPoint, 0.7f);
        ShieldChanged?.Invoke(this, currentShield, maxShield);

        if (currentShield <= 0f)
        {
            BreakShieldAndBecomeHostile();
            return;
        }

        EnterWarningState();
    }

    private void DamageBody(float damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        currentBodyHp = Mathf.Max(0f, currentBodyHp - damage);

        CombatFeedbackManager.PlayHit(
            hitPoint,
            incomingDirection,
            CombatFeedbackKind.Structure,
            bodyHitEffectIntensity,
            hitShakeAmplitude,
            hitShakeDuration,
            useProceduralHitEffect
        );

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

        AudioManager.PlayAt(SoundEventIds.ShopWarning, transform.position);

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
        AudioManager.PlayAt(SoundEventIds.ShopShieldBreak, transform.position);
        AudioManager.PlayAt(SoundEventIds.ShopHostile, transform.position);
        SetGlobalHostile();
    }

    private void HandleGlobalHostilityChanged()
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        SetWarningVisual(false);
        CurrentState = globalHostile
            ? ShopStructureState.HostileIdle
            : ShopStructureState.Neutral;

        if (!globalHostile && currentShield <= 0f)
        {
            currentShield = maxShield;
        }

        ShieldChanged?.Invoke(this, currentShield, maxShield);
        StateChanged?.Invoke(this);

        if (!globalHostile)
        {
            defenseController?.SetCombatActive(false, null);
        }
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
            SetHostileRuntimeState(ShopStructureState.HostileIdle);
            defenseController?.SetCombatActive(false, null);
            return;
        }

        float distance = Vector2.Distance(transform.position, targetPlayer.position);

        if (distance > hostileDetectRange)
        {
            combatStarted = false;
            SetHostileRuntimeState(ShopStructureState.HostileIdle);
            defenseController?.SetCombatActive(false, targetPlayer);
            return;
        }

        combatStarted = true;
        SetHostileRuntimeState(ShopStructureState.Combat);
        defenseController?.SetCombatActive(true, targetPlayer);
    }

    private void SetHostileRuntimeState(ShopStructureState nextState)
    {
        if (CurrentState == nextState || CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        CurrentState = nextState;
        StateChanged?.Invoke(this);
    }

    public void ForceHostileFromSecuritySabotage(FieldBaseSecurityNode disabledNode)
    {
        string sourceName = disabledNode != null ? disabledNode.NodeName : "전력 장치";
        ForceHostileFromDefenseSabotage(sourceName);
    }

    public void ForceHostileFromDefenseSabotage(string sourceName)
    {
        if (CurrentState == ShopStructureState.Dead)
        {
            return;
        }

        if (!globalHostile)
        {
            string finalSourceName = string.IsNullOrWhiteSpace(sourceName)
                ? "방어 설비"
                : sourceName;
            ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
            hud?.ShowWarning($"{finalSourceName} 공격 감지. 상점 보안 체계가 적대화됩니다.");
            AudioManager.PlayAt(SoundEventIds.ShopHostile, transform.position, 0.9f);
        }

        SetGlobalHostile();
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
        if (CurrentState == ShopStructureState.Dead || !allowExternalKnockback || distance <= 0f)
        {
            return;
        }

        if (structureBody == null)
        {
            structureBody = GetComponent<Rigidbody2D>();
        }

        if (structureBody == null || structureBody.bodyType != RigidbodyType2D.Dynamic)
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

        structureBody.AddForce(direction.normalized * distance, ForceMode2D.Impulse);
    }

    private void EnforceImmovableBody()
    {
        if (!enforceImmovableRigidbody || allowExternalKnockback)
        {
            return;
        }

        if (structureBody == null)
        {
            structureBody = GetComponent<Rigidbody2D>();
        }

        if (structureBody == null)
        {
            return;
        }

        structureBody.simulated = true;
        structureBody.linearVelocity = Vector2.zero;
        structureBody.angularVelocity = 0f;
        structureBody.gravityScale = 0f;
        structureBody.bodyType = RigidbodyType2D.Kinematic;
        structureBody.constraints = RigidbodyConstraints2D.FreezeAll;
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
        CombatFeedbackManager.PlayBreak(
            center,
            CombatFeedbackKind.Shield,
            1.45f,
            shieldBreakShakeAmplitude,
            shieldBreakShakeDuration,
            shieldBreakEffectPrefab == null
        );
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

            ShopStructure otherShop = hit.GetComponentInParent<ShopStructure>();
            if (otherShop != null)
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
        defenseController?.SetCombatActive(false, null);

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
        CombatFeedbackManager.PlayBreak(
            transform.position,
            CombatFeedbackKind.Structure,
            1.8f,
            deathShakeAmplitude,
            deathShakeDuration,
            deathEffectPrefab == null
        );

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
        PlayerRuntimeBonusState bonusState = playerObject != null
            ? playerObject.GetComponent<PlayerRuntimeBonusState>()
            : null;

        DropWorldCurrencyOrGrantDirect(
            CurrencyType.Credits,
            rewardCredits + refundCredits,
            bonusState
        );
        DropWorldCurrencyOrGrantDirect(
            CurrencyType.ScrapParts,
            rewardScrap,
            bonusState
        );

        if (UnityEngine.Random.value <= componentShieldDropChance)
        {
            GiveComponentShieldReward(playerObject);
        }
    }

    private void DropWorldCurrencyOrGrantDirect(
        CurrencyType currencyType,
        int baseAmount,
        PlayerRuntimeBonusState bonusState)
    {
        baseAmount = Mathf.Max(0, baseAmount);

        if (baseAmount <= 0)
        {
            return;
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (rewardDropper != null &&
            rewardDropper.TryDropCurrencyRewardAt(transform.position, currencyType, baseAmount))
        {
            // 획득 시 RewardPickup이 수확량 보너스를 적용합니다.
            return;
        }

        int finalAmount = bonusState != null
            ? bonusState.ApplyCurrencyGain(currencyType, baseAmount)
            : baseAmount;

        ShopRunBridge.AddCurrency(currencyType, finalAmount);
    }

    private void GiveComponentShieldReward(GameObject playerObject)
    {
        if (playerObject == null)
        {
            DropWorldCurrencyOrGrantDirect(
                CurrencyType.ScrapParts,
                duplicateComponentShieldScrap,
                null
            );
            return;
        }

        ComponentShieldPassive existingShield = playerObject.GetComponent<ComponentShieldPassive>();

        if (existingShield != null && existingShield.enabled)
        {
            PlayerRuntimeBonusState bonusState = playerObject.GetComponent<PlayerRuntimeBonusState>();
            DropWorldCurrencyOrGrantDirect(
                CurrencyType.ScrapParts,
                duplicateComponentShieldScrap,
                bonusState
            );
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