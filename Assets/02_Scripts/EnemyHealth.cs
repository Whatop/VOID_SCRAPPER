using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable, IKnockbackReceiver
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 10f;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.12f;

    [Header("Drop Settings")]
    [SerializeField] private GameObject dropContainerPrefab;
    [SerializeField] private float dropChance = 1f;

    private float currentHp;
    private bool isDead;

    private EnemyBaseAI enemyAI;
    private Rigidbody2D rb;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyBaseAI>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        currentHp = maxHp;
        isDead = false;
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
    }

    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage);
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
        SpawnHitEffect();

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void ApplyKnockback(Vector2 origin, float distance)
    {
        if (isDead)
        {
            return;
        }

        Vector2 direction = (Vector2)transform.position - origin;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        direction.Normalize();

        if (rb != null)
        {
            rb.position += direction * distance;
        }
        else
        {
            transform.position += (Vector3)(direction * distance);
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.SpawnAutoRelease(hitEffectPrefab, transform.position, hitEffectDuration);
        }
        else
        {
            GameObject effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, hitEffectDuration);
        }
    }

    private void SpawnContainerDrop()
    {
        if (dropContainerPrefab == null)
        {
            return;
        }

        if (Random.value > dropChance)
        {
            return;
        }

        Vector3 spawnPosition = transform.position + (Vector3)(Random.insideUnitCircle * 0.15f);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Get(dropContainerPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            Instantiate(dropContainerPrefab, spawnPosition, Quaternion.identity);
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (enemyAI != null)
        {
            enemyAI.OnDeath();
        }

        SpawnContainerDrop();

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}