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
    [SerializeField] private bool useProceduralHitEffectWhenPrefabMissing = true;
    [SerializeField] private float hitEffectIntensity = 0.9f;

    [Header("Camera Shake")]
    [SerializeField] private float hitShakeAmplitude = 0.04f;
    [SerializeField] private float hitShakeDuration = 0.065f;
    [SerializeField] private float deathShakeAmplitude = 0.11f;
    [SerializeField] private float deathShakeDuration = 0.13f;
    [SerializeField] private float bossFeedbackMultiplier = 1.65f;

    [Header("Knockback")]
    [SerializeField] private bool useSmoothKnockback = true;
    [SerializeField] private float knockbackDuration = 0.14f;
    [SerializeField] private float knockbackDistanceMultiplier = 1f;
    [SerializeField] private bool clearVelocityDuringKnockback = true;

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
    private bool isBoss;
    private Coroutine releaseRoutine;
    private Coroutine knockbackRoutine;

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
        isBoss = GetComponent<BossPatternController>() != null;

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

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
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

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
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
        TakeDamage((float)damage, transform.position, Vector2.zero);
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

        PlayHitFeedback(damage, hitPoint, incomingDirection);
        AudioManager.PlayAt(SoundEventIds.EnemyHit, hitPoint, 0.7f);

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

        float finalDistance = distance * Mathf.Max(0f, knockbackDistanceMultiplier);
        Vector2 displacement = direction * finalDistance;

        if (!useSmoothKnockback || knockbackDuration <= 0f)
        {
            MoveImmediately(displacement);
            return;
        }

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(SmoothKnockbackRoutine(displacement, knockbackDuration));
    }

    private IEnumerator SmoothKnockbackRoutine(Vector2 displacement, float duration)
    {
        duration = Mathf.Max(0.01f, duration);

        Vector2 startPosition = rb != null
            ? rb.position
            : (Vector2)transform.position;

        Vector2 targetPosition = startPosition + displacement;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (isDead)
            {
                knockbackRoutine = null;
                yield break;
            }

            elapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            Vector2 nextPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);

            if (rb != null)
            {
                if (clearVelocityDuringKnockback)
                {
                    rb.linearVelocity = Vector2.zero;
                }

                rb.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
        {
            if (clearVelocityDuringKnockback)
            {
                rb.linearVelocity = Vector2.zero;
            }

            rb.MovePosition(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }

        knockbackRoutine = null;
    }

    private void MoveImmediately(Vector2 displacement)
    {
        if (rb != null)
        {
            rb.position += displacement;
        }
        else
        {
            transform.position += (Vector3)displacement;
        }
    }

    private void PlayHitFeedback(float damage, Vector2 position, Vector2 incomingDirection)
    {
        bool customEffectSpawned = SpawnHitEffect(position);
        float typeMultiplier = isBoss ? Mathf.Max(1f, bossFeedbackMultiplier) : 1f;
        float damageScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f, damage) / 2f), 0.65f, 1.75f);

        CombatFeedbackManager.PlayHit(
            position,
            incomingDirection,
            isBoss ? CombatFeedbackKind.Boss : CombatFeedbackKind.Enemy,
            hitEffectIntensity * damageScale * typeMultiplier,
            hitShakeAmplitude * damageScale * typeMultiplier,
            hitShakeDuration,
            useProceduralHitEffectWhenPrefabMissing && !customEffectSpawned
        );
    }

    private bool SpawnHitEffect(Vector2 position)
    {
        if (hitEffectPrefab == null)
        {
            return false;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.SpawnAutoRelease(hitEffectPrefab, position, hitEffectDuration);
        }
        else
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, hitEffectDuration);
        }

        return true;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHp = 0f;

        if (!isBoss)
        {
            AudioManager.PlayAt(SoundEventIds.EnemyDeath, transform.position);
        }

        float typeMultiplier = isBoss ? Mathf.Max(1f, bossFeedbackMultiplier) : 1f;
        CombatFeedbackManager.PlayBreak(
            transform.position,
            isBoss ? CombatFeedbackKind.Boss : CombatFeedbackKind.Enemy,
            1.15f * typeMultiplier,
            deathShakeAmplitude * typeMultiplier,
            deathShakeDuration * (isBoss ? 1.35f : 1f),
            true
        );

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
            knockbackRoutine = null;
        }

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