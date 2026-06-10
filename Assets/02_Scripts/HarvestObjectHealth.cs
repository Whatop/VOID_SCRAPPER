using System;
using System.Collections;
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

    [Header("Knockback Optional")]
    [SerializeField] private bool receiveDashKnockback;
    [SerializeField] private float knockbackMultiplier = 1f;

    private Rigidbody2D rb;
    private float currentHp;
    private bool isDead;
    private Coroutine releaseRoutine;

    public HarvestObjectKind ObjectKind => objectKind;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;

    public event Action<HarvestObjectHealth, float, float> HealthChanged;
    public event Action<HarvestObjectHealth> Damaged;
    public event Action<HarvestObjectHealth> Died;

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
        if (resetHealthOnEnable)
        {
            ResetHealth();
        }
    }

    private void OnDisable()
    {
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

    public void TakeDamage(float damage)
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

        SpawnEffect(hitEffectPrefab, hitEffectDuration, transform.position, Quaternion.identity);
        HealthChanged?.Invoke(this, currentHp, maxHp);
        Damaged?.Invoke(this);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage);
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

        if (rb != null)
        {
            rb.position += direction * finalDistance;
        }
        else
        {
            transform.position += (Vector3)(direction * finalDistance);
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

        for (int i = 0; i < count; i++)
        {
            GameObject prefab = GetRandomReinforcementPrefab();
            if (prefab == null)
            {
                continue;
            }

            Vector2 direction = GetSpawnDirection(i, count);
            Vector3 position = transform.position + (Vector3)(direction * reinforcementSpawnRadius);
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

                enemyAI.ApplyRadarAlert(transform.position);
            }
        }
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

    private Vector2 GetSpawnDirection(int index, int totalCount)
    {
        if (totalCount <= 1)
        {
            Vector2 random = UnityEngine.Random.insideUnitCircle;
            return random.sqrMagnitude > 0.001f ? random.normalized : Vector2.up;
        }

        float angle = (360f / totalCount) * index + UnityEngine.Random.Range(-25f, 25f);
        float radian = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)).normalized;
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