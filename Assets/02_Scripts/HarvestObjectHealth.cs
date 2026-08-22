using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum HarvestObjectKind
{
    SupplyContainer,
    HighValueWreck,
    DestroyedHull
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class HarvestObjectHealth : MonoBehaviour, IDamageable, IKnockbackReceiver
{
    private static readonly HashSet<HarvestObjectHealth> ActiveRegistry = new HashSet<HarvestObjectHealth>();

    public static IEnumerable<HarvestObjectHealth> ActiveObjects => ActiveRegistry;
    [Header("Harvest Object")]
    [SerializeField] private HarvestObjectKind objectKind = HarvestObjectKind.SupplyContainer;

    [Header("Health")]
    [SerializeField] private float maxHp = 6f;
    [SerializeField] private bool resetHealthOnEnable = true;

    [Header("Reward")]
    [SerializeField] private RewardDropper rewardDropper;
    [SerializeField] private bool dropRewardOnDeath = true;

    [Header("Radar")]
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private bool hideRadarTargetOnDeath = true;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.12f;
    [SerializeField] private bool useProceduralHitEffectWhenPrefabMissing = true;
    [SerializeField] private float hitEffectIntensity = 0.9f;

    [Header("Camera Shake")]
    [SerializeField] private float hitShakeAmplitude = 0.025f;
    [SerializeField] private float hitShakeDuration = 0.05f;
    [SerializeField] private float breakShakeAmplitude = 0.09f;
    [SerializeField] private float breakShakeDuration = 0.11f;

    [Header("Death Effect")]
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private float deathEffectDuration = 0.6f;

    [Header("Death State")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider2D[] collidersToDisableOnDeath;
    [SerializeField] private bool disableRenderersOnDeath = true;
    [SerializeField] private Renderer[] renderersToDisableOnDeath;
    [SerializeField] private MonoBehaviour[] componentsToDisableOnDeath;
    [SerializeField] private bool releaseOnDeath = true;
    [SerializeField] private float releaseDelayOnDeath = 0.1f;

    [Header("Destroyed Hull Reinforcement Optional")]
    [Range(0f, 1f)]
    [SerializeField] private float reinforcementSpawnChance;
    [SerializeField] private GameObject[] reinforcementPrefabs;
    [SerializeField] private int minReinforcementCount = 2;
    [SerializeField] private int maxReinforcementCount = 2;
    [SerializeField] private float reinforcementSpawnRadius = 1.75f;
    [SerializeField] private bool setPlayerAsEnemyTarget = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Reinforcement Arrival Presentation")]
    [SerializeField] private bool useReinforcementArrival = true;
    [Min(0f)]
    [SerializeField] private float reinforcementWarningTime = 0.8f;
    [Min(0.01f)]
    [SerializeField] private float reinforcementTravelTime = 0.32f;
    [Min(0f)]
    [SerializeField] private float reinforcementReadyDelay = 0.25f;
    [Min(0.1f)]
    [SerializeField] private float reinforcementOffscreenEntryDistance = 10.5f;
    [Min(0.05f)]
    [SerializeField] private float reinforcementArrivalMarkerRadius = 0.85f;
    [Min(0.05f)]
    [SerializeField] private float reinforcementSpawnClearance = 0.55f;
    [SerializeField] private LayerMask reinforcementDestinationBlockingLayers;

    [Header("Projectile Interaction")]
    [SerializeField] private bool takeDamageFromPlayerProjectiles = true;
    [SerializeField] private bool takeDamageFromEnemyProjectiles;
    [SerializeField] private bool blockProjectileWhenDamageIgnored = true;

    [Header("Knockback Optional")]
    [SerializeField] private bool receiveDashKnockback;
    [SerializeField] private float knockbackMultiplier = 1f;
    [Tooltip("고정 오브젝트가 Transform 순간이동으로 밀리는 것을 막습니다. 켜두면 Dynamic Rigidbody2D만 넉백됩니다.")]
    [SerializeField] private bool requireDynamicRigidbodyForKnockback = true;
    [SerializeField] private bool useImpulseKnockback = true;
    [SerializeField] private float maxKnockbackSpeed = 2f;

    private Rigidbody2D rb;
    private float currentHp;
    private bool isDead;
    private Coroutine releaseRoutine;
    private readonly Collider2D[] reinforcementDestinationBuffer = new Collider2D[24];

    public HarvestObjectKind ObjectKind => objectKind;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;
    public bool BlocksProjectileWhenDamageIgnored => blockProjectileWhenDamageIgnored;
    public bool TakesDamageFromPlayerProjectiles => takeDamageFromPlayerProjectiles;
    public bool TakesDamageFromEnemyProjectiles => takeDamageFromEnemyProjectiles;
    public RadarTarget RadarTarget => radarTarget;

    public event Action<HarvestObjectHealth, float, float> HealthChanged;
    public event Action<HarvestObjectHealth> Damaged;
    public event Action<HarvestObjectHealth> Died;

    public void SetReinforcementSpawnChance(float chance)
    {
        reinforcementSpawnChance = Mathf.Clamp01(chance);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRegistry()
    {
        ActiveRegistry.Clear();
    }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        rewardDropper = GetComponent<RewardDropper>();
        radarTarget = GetComponent<RadarTarget>();
        collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>(true);
        renderersToDisableOnDeath = GetComponentsInChildren<Renderer>(true);

        if (radarTarget != null)
        {
            radarTarget.SetMarkerType(RadarMarkerType.RewardObject);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
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
        ActiveRegistry.Add(this);

        if (resetHealthOnEnable)
        {
            ResetHealth();
        }
    }

    private void OnDisable()
    {
        ActiveRegistry.Remove(this);

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        ActiveRegistry.Remove(this);
    }

    public void SetRewardDefinition(RewardDefinition definition)
    {
        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (rewardDropper != null)
        {
            rewardDropper.SetRewardDefinition(definition);
        }
    }

    public void SetMaxHp(float newMaxHp, bool refill = true)
    {
        maxHp = Mathf.Max(1f, newMaxHp);

        if (refill)
        {
            currentHp = maxHp;
        }
        else
        {
            currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
        }

        HealthChanged?.Invoke(this, currentHp, maxHp);
    }

    public void ResetHealth()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        currentHp = maxHp;
        isDead = false;

        SetCollidersEnabled(true);
        SetRenderersEnabled(true);
        SetDeathComponentsEnabled(true);

        if (radarTarget != null)
        {
            radarTarget.SetVisible(true);
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        HealthChanged?.Invoke(this, currentHp, maxHp);
    }


    public bool CanReceiveProjectileDamage(ProjectileOwner projectileOwner)
    {
        return projectileOwner == ProjectileOwner.Player
            ? takeDamageFromPlayerProjectiles
            : takeDamageFromEnemyProjectiles;
    }

    public void SetPlayerProjectileDamageEnabled(bool enabled, bool blockProjectileWhenDisabled = true)
    {
        takeDamageFromPlayerProjectiles = enabled;

        if (!enabled)
        {
            blockProjectileWhenDamageIgnored = blockProjectileWhenDisabled;
        }
    }

    public void SetEnemyProjectileDamageEnabled(bool enabled, bool blockProjectileWhenDisabled = true)
    {
        takeDamageFromEnemyProjectiles = enabled;

        if (!enabled)
        {
            blockProjectileWhenDamageIgnored = blockProjectileWhenDisabled;
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position, Vector2.zero);
    }

    public void TakeDamage(float damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        if (isDead)
        {
            return;
        }

        if (damage <= 0f)
        {
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - damage);

        bool customEffectSpawned = hitEffectPrefab != null;
        SpawnEffect(hitEffectPrefab, hitEffectDuration, hitPoint, Quaternion.identity);

        float damageScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f, damage) / 2f), 0.7f, 1.65f);
        CombatFeedbackManager.PlayHit(
            hitPoint,
            incomingDirection,
            CombatFeedbackKind.Harvest,
            hitEffectIntensity * damageScale,
            hitShakeAmplitude * damageScale,
            hitShakeDuration,
            useProceduralHitEffectWhenPrefabMissing && !customEffectSpawned
        );

        AudioManager.PlayAt(ResolveHitSoundEventId(), hitPoint, 0.65f);
        HealthChanged?.Invoke(this, currentHp, maxHp);
        Damaged?.Invoke(this);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage, transform.position, Vector2.zero);
    }

    public void ApplyKnockback(Vector2 origin, float distance)
    {
        if (isDead || !receiveDashKnockback)
        {
            return;
        }

        if (distance <= 0f)
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

        direction.Normalize();

        float finalDistance = distance * Mathf.Max(0f, knockbackMultiplier);

        if (rb == null)
        {
            // 수확 오브젝트 Root를 Transform으로 순간 이동시키지 않습니다.
            return;
        }

        if (requireDynamicRigidbodyForKnockback && rb.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        if (useImpulseKnockback && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.AddForce(direction * finalDistance, ForceMode2D.Impulse);

            float speedLimit = Mathf.Max(0.01f, maxKnockbackSpeed);
            if (rb.linearVelocity.sqrMagnitude > speedLimit * speedLimit)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * speedLimit;
            }
        }
        else
        {
            rb.MovePosition(rb.position + direction * finalDistance);
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHp = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (hideRadarTargetOnDeath && radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        if (disableCollidersOnDeath)
        {
            SetCollidersEnabled(false);
        }

        if (disableRenderersOnDeath)
        {
            SetRenderersEnabled(false);
        }

        SpawnEffect(deathEffectPrefab, deathEffectDuration, transform.position, Quaternion.identity);
        CombatFeedbackManager.PlayBreak(
            transform.position,
            CombatFeedbackKind.Harvest,
            1.25f,
            breakShakeAmplitude,
            breakShakeDuration,
            deathEffectPrefab == null
        );
        AudioManager.PlayAt(ResolveBreakSoundEventId(), transform.position);

        if (dropRewardOnDeath && rewardDropper != null)
        {
            rewardDropper.DropAt(transform.position);
        }

        TrySpawnReinforcements();
        Died?.Invoke(this);
        SetDeathComponentsEnabled(false);

        if (releaseOnDeath)
        {
            if (releaseDelayOnDeath <= 0f)
            {
                ReleaseSelf();
            }
            else
            {
                releaseRoutine = StartCoroutine(ReleaseAfterDeathRoutine());
            }
        }
    }

    private string ResolveHitSoundEventId()
    {
        switch (objectKind)
        {
            case HarvestObjectKind.HighValueWreck:
            case HarvestObjectKind.DestroyedHull:
                return SoundEventIds.ObjectDebrisHit;

            case HarvestObjectKind.SupplyContainer:
            default:
                return SoundEventIds.ObjectContainerHit;
        }
    }

    private string ResolveBreakSoundEventId()
    {
        switch (objectKind)
        {
            case HarvestObjectKind.HighValueWreck:
                return SoundEventIds.ObjectDebrisBreak;

            case HarvestObjectKind.DestroyedHull:
                return SoundEventIds.ObjectShipwreckBreak;

            case HarvestObjectKind.SupplyContainer:
            default:
                return SoundEventIds.ObjectContainerBreak;
        }
    }

    private void TrySpawnReinforcements()
    {
        if (reinforcementSpawnChance <= 0f)
        {
            return;
        }

        if (reinforcementPrefabs == null || reinforcementPrefabs.Length == 0)
        {
            return;
        }

        if (UnityEngine.Random.value > reinforcementSpawnChance)
        {
            return;
        }

        int minCount = Mathf.Max(0, Mathf.Min(minReinforcementCount, maxReinforcementCount));
        int maxCount = Mathf.Max(minReinforcementCount, maxReinforcementCount);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);

        Transform player = FindPlayerTransform();
        ExpeditionMapGenerator mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>();
        Bounds mapBounds = mapGenerator != null ? mapGenerator.MapBounds : default;
        bool hasMapBounds = mapBounds.size.x > 0.01f && mapBounds.size.y > 0.01f;
        EnemyArrivalSpawnSettings arrivalSettings = BuildReinforcementArrivalSettings();

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = GetRandomReinforcementPrefab();
            if (prefab == null)
            {
                continue;
            }

            if (!TryResolveReinforcementDestination(
                    i,
                    count,
                    mapBounds,
                    hasMapBounds,
                    out Vector2 position))
            {
                continue;
            }

            GameObject spawned = SpawnObject(prefab, position, Quaternion.identity);

            if (spawned == null)
            {
                continue;
            }

            EnemyBaseAI enemyAI = spawned.GetComponent<EnemyBaseAI>();
            if (enemyAI != null)
            {
                if (setPlayerAsEnemyTarget && player != null)
                {
                    enemyAI.SetTarget(player);
                }

                bool arrivalStarted = EnemyArrivalSpawnUtility.BeginArrival(
                    spawned,
                    position,
                    player,
                    setPlayerAsEnemyTarget,
                    transform.position,
                    arrivalSettings
                );

                if (!arrivalStarted)
                {
                    enemyAI.ApplyRadarAlert(transform.position);
                }
            }
        }
    }

    private EnemyArrivalSpawnSettings BuildReinforcementArrivalSettings()
    {
        return new EnemyArrivalSpawnSettings
        {
            Enabled = useReinforcementArrival,
            CreateFallbackMarker = true,
            CreateFallbackLandingBurst = true,
            WarningTime = reinforcementWarningTime,
            TravelTime = reinforcementTravelTime,
            ReadyDelay = reinforcementReadyDelay,
            OffscreenEntryDistance = reinforcementOffscreenEntryDistance,
            PreferOffscreenEntry = true,
            MarkerRadius = reinforcementArrivalMarkerRadius,
            MarkerColor = new Color(1f, 0.05f, 0.03f, 0.9f),
            UseAfterimages = true,
            AfterimageInterval = 0.055f,
            AfterimageLifetime = 0.22f,
            AfterimageColor = new Color(1f, 0.18f, 0.12f, 0.35f),
            AfterimageSortingOrderOffset = -1,
            DisableCollidersDuringArrival = true
        };
    }

    private bool TryResolveReinforcementDestination(
        int index,
        int totalCount,
        Bounds mapBounds,
        bool hasMapBounds,
        out Vector2 destination)
    {
        const int maxAttempts = 16;
        float baseAngle = totalCount <= 1
            ? UnityEngine.Random.Range(0f, 360f)
            : (360f / totalCount) * index + UnityEngine.Random.Range(-18f, 18f);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float angle = baseAngle + attempt * (360f / maxAttempts);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float radiusOffset = (attempt % 3) * 0.2f;
            Vector2 candidate = (Vector2)transform.position +
                                direction * (reinforcementSpawnRadius + radiusOffset);

            if (IsValidReinforcementDestination(candidate, mapBounds, hasMapBounds))
            {
                destination = candidate;
                return true;
            }
        }

        destination = default;
        return false;
    }

    private bool IsValidReinforcementDestination(
        Vector2 candidate,
        Bounds mapBounds,
        bool hasMapBounds)
    {
        float clearance = Mathf.Max(0.05f, reinforcementSpawnClearance);

        if (hasMapBounds)
        {
            float minX = mapBounds.min.x + clearance;
            float maxX = mapBounds.max.x - clearance;
            float minY = mapBounds.min.y + clearance;
            float maxY = mapBounds.max.y - clearance;

            if (candidate.x < minX || candidate.x > maxX ||
                candidate.y < minY || candidate.y > maxY)
            {
                return false;
            }
        }

        int mask = reinforcementDestinationBlockingLayers.value != 0
            ? reinforcementDestinationBlockingLayers.value
            : LayerMask.GetMask(
                "Default",
                "Player",
                "Enemy",
                "Meteor",
                "HarvestObject",
                "Shop",
                "WorldSolid"
            );

        ContactFilter2D contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(mask);
        contactFilter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(
            candidate,
            clearance,
            contactFilter,
            reinforcementDestinationBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = reinforcementDestinationBuffer[i];
            reinforcementDestinationBuffer[i] = null;

            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            Transform hitTransform = hit.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private GameObject GetRandomReinforcementPrefab()
    {
        if (reinforcementPrefabs == null || reinforcementPrefabs.Length == 0)
        {
            return null;
        }

        for (int safety = 0; safety < 16; safety++)
        {
            GameObject prefab = reinforcementPrefabs[UnityEngine.Random.Range(0, reinforcementPrefabs.Length)];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private Transform FindPlayerTransform()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        return playerObject != null ? playerObject.transform : null;
    }

    private IEnumerator ReleaseAfterDeathRoutine()
    {
        yield return new WaitForSeconds(releaseDelayOnDeath);
        releaseRoutine = null;
        ReleaseSelf();
    }

    private void SetCollidersEnabled(bool value)
    {
        if (collidersToDisableOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDisableOnDeath.Length; i++)
        {
            if (collidersToDisableOnDeath[i] != null)
            {
                collidersToDisableOnDeath[i].enabled = value;
            }
        }
    }

    private void SetRenderersEnabled(bool value)
    {
        if (renderersToDisableOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < renderersToDisableOnDeath.Length; i++)
        {
            if (renderersToDisableOnDeath[i] != null)
            {
                renderersToDisableOnDeath[i].enabled = value;
            }
        }
    }

    private void SetDeathComponentsEnabled(bool value)
    {
        if (componentsToDisableOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < componentsToDisableOnDeath.Length; i++)
        {
            MonoBehaviour component = componentsToDisableOnDeath[i];

            if (component == null || component == this)
            {
                continue;
            }

            component.enabled = value;
        }
    }

    private GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        if (PoolManager.Instance != null)
        {
            return PoolManager.Instance.Get(prefab, position, rotation);
        }

        return Instantiate(prefab, position, rotation);
    }

    private void SpawnEffect(GameObject prefab, float duration, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject effect = SpawnObject(prefab, position, rotation);

        if (effect == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(effect, Mathf.Max(0.01f, duration));
        }
        else
        {
            Destroy(effect, Mathf.Max(0.01f, duration));
        }
    }

    private void ReleaseSelf()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (reinforcementSpawnChance > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, reinforcementSpawnRadius);
        }
    }
}
