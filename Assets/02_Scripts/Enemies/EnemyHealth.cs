using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHealth : MonoBehaviour, IDamageable, IKnockbackReceiver
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 10f;

    [Header("References")]
    [SerializeField] private RewardDropper rewardDropper;

    [Header("Hit Feedback")]
    [SerializeField] private SpriteHitFlash2D spriteHitFlash;
    [SerializeField] private bool autoCreateSpriteHitFlash = true;
    [FormerlySerializedAs("addProceduralHitFeedback")]
    [SerializeField] private bool useProceduralHitFeedback;
    [Min(0.1f)]
    [SerializeField] private float proceduralHitIntensity = 1f;

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

    [Header("Death Effect")]
    [SerializeField] private GameObject deathEffectPrefab;
    [Min(0.01f)]
    [SerializeField] private float deathEffectLifeTime = 0.45f;
    [SerializeField] private bool useProceduralDeathEffectWhenPrefabMissing = true;

    [Header("Death")]
    [SerializeField] private bool dropRewardOnDeath = true;
    [SerializeField] private bool releaseOnDeath = true;
    [SerializeField] private float releaseDelayOnDeath = 0.25f;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider2D[] collidersToDisableOnDeath;
    [SerializeField] private MonoBehaviour[] componentsToDisableOnDeath;

    private float currentHp;
    private readonly System.Collections.Generic.Dictionary<object, float> damageFloors =
        new System.Collections.Generic.Dictionary<object, float>();
    private bool isDead;

    private Rigidbody2D rb;
    private bool isBoss;
    private bool configuredDropRewardOnDeath;
    private BossPatternController bossPatternController;
    private PhaseGatekeeperBossController phaseGatekeeperBossController;
    private Coroutine releaseRoutine;
    private Coroutine knockbackRoutine;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;

    public event Action<EnemyHealth> Damaged;
    public event Action<EnemyHealth> Died;
    public event Action<EnemyHealth, float, float> HealthChanged;

    public void SetRewardDropEnabled(bool enabled)
    {
        dropRewardOnDeath = enabled;
    }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        rewardDropper = GetComponent<RewardDropper>();
        collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        configuredDropRewardOnDeath = dropRewardOnDeath;
        bossPatternController = GetComponent<BossPatternController>();
        phaseGatekeeperBossController = GetComponent<PhaseGatekeeperBossController>();
        isBoss = bossPatternController != null ||
                 GetComponent<BossDummyController>() != null ||
                 GetComponent<PirateCommanderBossController>() != null ||
                 GetComponent<FrigateTriadBossController>() != null ||
                 phaseGatekeeperBossController != null;

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
        {
            collidersToDisableOnDeath = GetComponentsInChildren<Collider2D>();
        }

        if (spriteHitFlash == null)
        {
            spriteHitFlash = GetComponent<SpriteHitFlash2D>();
        }

        if (spriteHitFlash == null && autoCreateSpriteHitFlash)
        {
            spriteHitFlash = gameObject.AddComponent<SpriteHitFlash2D>();
        }
    }

    private void OnEnable()
    {
        dropRewardOnDeath = configuredDropRewardOnDeath;
        ResetHealth();
    }

    private void OnDisable()
    {
        damageFloors.Clear();
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

    // A phase owner may reserve HP without changing another owner's protection.
    // This clamps damage before HealthChanged/Die, including a single lethal hit.
    public void SetDamageFloor(object source, float hpRatio)
    {
        if (source == null)
        {
            return;
        }
        damageFloors[source] = Mathf.Clamp01(hpRatio);
    }

    public void RemoveDamageFloor(object source)
    {
        if (source != null)
        {
            damageFloors.Remove(source);
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

        if (phaseGatekeeperBossController != null &&
            phaseGatekeeperBossController.RejectsIncomingDamage)
        {
            return;
        }

        if (bossPatternController != null &&
            bossPatternController.TryAbsorbIncomingDamage(damage, hitPoint, incomingDirection))
        {
            return;
        }

        float nextHp = Mathf.Max(0f, currentHp - damage);
        foreach (float floor in damageFloors.Values)
        {
            nextHp = Mathf.Max(nextHp, Mathf.Min(currentHp, maxHp * floor));
        }
        if (nextHp >= currentHp)
        {
            return;
        }
        currentHp = nextHp;

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
        float typeMultiplier = isBoss ? Mathf.Max(1f, bossFeedbackMultiplier) : 1f;
        float damageScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f, damage) / 2f), 0.65f, 1.75f);

        spriteHitFlash?.Play(damageScale * typeMultiplier);

        // ProjectileDefinition의 무기별 Impact VFX와 Sprite White Flash를 기본으로 사용합니다.
        // Procedural Spark는 더 강한 피드백이 필요할 때만 Inspector에서 켭니다.
        CombatFeedbackManager.PlayHit(
            position,
            incomingDirection,
            isBoss ? CombatFeedbackKind.Boss : CombatFeedbackKind.Enemy,
            Mathf.Clamp(proceduralHitIntensity * damageScale * typeMultiplier, 0.5f, 2.4f),
            hitShakeAmplitude * damageScale * typeMultiplier,
            hitShakeDuration,
            useProceduralHitFeedback
        );
    }

    private bool SpawnDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            return false;
        }

        float lifeTime = Mathf.Max(0.01f, deathEffectLifeTime);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.SpawnAutoRelease(deathEffectPrefab, transform.position, lifeTime);
        }
        else
        {
            GameObject effect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, lifeTime);
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
        bool deathEffectSpawned = SpawnDeathEffect();

        CombatFeedbackManager.PlayBreak(
            transform.position,
            isBoss ? CombatFeedbackKind.Boss : CombatFeedbackKind.Enemy,
            1.15f * typeMultiplier,
            deathShakeAmplitude * typeMultiplier,
            deathShakeDuration * (isBoss ? 1.35f : 1f),
            useProceduralDeathEffectWhenPrefabMissing && !deathEffectSpawned
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
