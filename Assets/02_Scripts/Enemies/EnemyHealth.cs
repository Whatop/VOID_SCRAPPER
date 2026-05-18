using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : MonoBehaviour, IDamageable, IKnockbackReceiver
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 10f;

    [Header("References")]
    [SerializeField] private RewardDropper rewardDropper;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.12f;

    [Header("Death")]
    [SerializeField] private bool dropRewardOnDeath = true;
    [SerializeField] private bool releaseOnDeath = true;
    [SerializeField] private float releaseDelayOnDeath = 0.25f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider2D[] collidersToDisableOnDeath;
    [SerializeField] private MonoBehaviour[] componentsToDisableOnDeath;

    private float currentHp;
    private bool isDead;

    private Rigidbody2D rb;
    private Coroutine releaseRoutine;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;

    public event Action<EnemyHealth> Damaged;
    public event Action<EnemyHealth> Died;
    public event Action<EnemyHealth, float, float> HealthChanged;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        rewardDropper = GetComponent<RewardDropper>();
        collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
        {
            collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>();
        }
    }

    private void OnEnable()
    {
        ResetHealth();
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
        }
    }

    public void ApplyDefinition(EnemyDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        SetMaxHp(definition.MaxHp, true);

        if (rewardDropper != null)
        {
            rewardDropper.SetRewardDefinition(definition.RewardDefinition);
        }
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
        SetDeathComponentsEnabled(true);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        HealthChanged?.Invoke(this, currentHp, maxHp);
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

        HealthChanged?.Invoke(this, currentHp, maxHp);
        Damaged?.Invoke(this);

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
        }

        if (disableCollidersOnDeath)
        {
            SetCollidersEnabled(false);
        }

        Died?.Invoke(this);

        if (dropRewardOnDeath && rewardDropper != null)
        {
            rewardDropper.DropAt(transform.position);
        }

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

    private void SetDeathComponentsEnabled(bool value)
    {
        if (componentsToDisableOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < componentsToDisableOnDeath.Length; i++)
        {
            MonoBehaviour component = componentsToDisableOnDeath[i];

            if (component == null)
            {
                continue;
            }

            if (component == this)
            {
                continue;
            }

            component.enabled = value;
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
}