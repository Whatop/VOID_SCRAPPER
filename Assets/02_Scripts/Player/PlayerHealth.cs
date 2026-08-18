using System;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 20f;
    [SerializeField] private float invincibleTimeAfterHit = 0.35f;

    [Header("References")]
    [SerializeField] private PlayerArmor armor;
    [SerializeField] private ComponentShieldPassive componentShield;

    [Header("Hit Feedback")]
    [SerializeField] private SpriteHitFlash2D spriteHitFlash;
    [SerializeField] private bool autoCreateSpriteHitFlash = true;
    [FormerlySerializedAs("addProceduralHitFeedback")]
    [SerializeField] private bool useProceduralHitFeedback;
    [Min(0.1f)]
    [SerializeField] private float proceduralHitIntensity = 1.15f;

    [Header("Camera Shake")]
    [SerializeField] private float hitShakeAmplitude = 0.17f;
    [SerializeField] private float hitShakeDuration = 0.14f;
    [SerializeField] private float deathShakeAmplitude = 0.34f;
    [SerializeField] private float deathShakeDuration = 0.32f;

    [Header("Death Effect")]
    [SerializeField] private GameObject deathEffectPrefab;
    [Min(0.01f)]
    [SerializeField] private float deathEffectLifeTime = 0.5f;
    [SerializeField] private bool useProceduralDeathEffectWhenPrefabMissing = true;

    [Header("Death")]
    [SerializeField] private bool disableColliderOnDeath = true;
    [SerializeField] private MonoBehaviour[] componentsToDisableOnDeath;

    [Header("Legacy Death Return")]
    [Tooltip("사망 연출 없이 바로 정착지로 돌아가고 싶을 때만 켭니다. PlayerDeathSequenceController를 쓸 거면 꺼둡니다.")]
    [SerializeField] private bool completeRunDirectlyOnDeath;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerCombatState combatState;

    private float currentHp;
    private float invincibleTimer;
    private bool isDead;

    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : currentHp / maxHp;
    public bool IsDead => isDead;
    public bool IsInvincible => invincibleTimer > 0f;

    public event Action<float, float> Damaged;
    public event Action<float, float> Healed;
    public event Action<float, float> Changed;
    public event Action Died;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        combatState = GetComponent<PlayerCombatState>();

        if (armor == null)
        {
            armor = GetComponent<PlayerArmor>();
        }

        if (componentShield == null)
        {
            componentShield = GetComponent<ComponentShieldPassive>();
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
        ResetHealth();
    }

    private void Update()
    {
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
        }
    }

    public void ResetHealth()
    {
        currentHp = maxHp;
        invincibleTimer = 0f;
        isDead = false;

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }

        if (componentsToDisableOnDeath != null)
        {
            foreach (MonoBehaviour component in componentsToDisableOnDeath)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }
        }

        Healed?.Invoke(currentHp, maxHp);
        Changed?.Invoke(currentHp, maxHp);
    }

    public void SetMaxHp(float newMaxHp, bool refill)
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

        if (refill)
        {
            Healed?.Invoke(currentHp, maxHp);
        }

        Changed?.Invoke(currentHp, maxHp);
    }

    public void AddMaxHp(float amount, bool healAddedAmount)
    {
        if (amount <= 0f)
        {
            return;
        }

        maxHp += amount;

        if (healAddedAmount)
        {
            Heal(amount);
        }
        else
        {
            currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
            Changed?.Invoke(currentHp, maxHp);
        }
    }

    public void RestoreCurrentHp(float value)
    {
        currentHp = Mathf.Clamp(value, 0f, maxHp);
        Changed?.Invoke(currentHp, maxHp);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position, Vector2.zero);
    }

    public void TakeDamage(float damage, Vector2 hitPoint)
    {
        Vector2 incomingDirection = (Vector2)transform.position - hitPoint;
        TakeDamage(damage, hitPoint, incomingDirection);
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

        if (invincibleTimer > 0f)
        {
            return;
        }

        if (combatState != null)
        {
            combatState.RegisterHit();
        }

        if (componentShield == null)
        {
            componentShield = GetComponent<ComponentShieldPassive>();
        }

        if (componentShield != null && componentShield.TryBlockDamage(hitPoint))
        {
            return;
        }

        float remainingDamage = damage;

        if (armor != null)
        {
            remainingDamage = armor.AbsorbDamage(damage);
        }

        invincibleTimer = invincibleTimeAfterHit;

        PlayHitFeedback(damage, hitPoint, incomingDirection);
        AudioManager.PlayAt(SoundEventIds.ShipHit, hitPoint);

        if (remainingDamage <= 0f)
        {
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - remainingDamage);
        Damaged?.Invoke(currentHp, maxHp);
        Changed?.Invoke(currentHp, maxHp);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead)
        {
            return;
        }

        if (amount <= 0f)
        {
            return;
        }

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        Healed?.Invoke(currentHp, maxHp);
        Changed?.Invoke(currentHp, maxHp);
    }

    public void AddInvincibleTime(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    private void PlayHitFeedback(float damage, Vector2 position, Vector2 incomingDirection)
    {
        float damageScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0.01f, damage) / 2f), 0.8f, 1.8f);

        spriteHitFlash?.Play(damageScale);

        CombatFeedbackManager.PlayHit(
            position,
            incomingDirection,
            CombatFeedbackKind.Player,
            Mathf.Clamp(proceduralHitIntensity * damageScale, 0.6f, 2.2f),
            hitShakeAmplitude * damageScale,
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
        invincibleTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (disableColliderOnDeath && playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        if (componentsToDisableOnDeath != null)
        {
            foreach (MonoBehaviour component in componentsToDisableOnDeath)
            {
                if (component != null)
                {
                    component.enabled = false;
                }
            }
        }

        bool deathEffectSpawned = SpawnDeathEffect();

        CombatFeedbackManager.PlayBreak(
            transform.position,
            CombatFeedbackKind.Player,
            1.6f,
            deathShakeAmplitude,
            deathShakeDuration,
            useProceduralDeathEffectWhenPrefabMissing && !deathEffectSpawned
        );

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            GameAudioLoopController.BeginRunEndMusicTransition();
        }

        Died?.Invoke();

        if (completeRunDirectlyOnDeath && RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CompleteRun(RunEndReason.Death);
        }

        Debug.Log("플레이어 기체가 파괴되었습니다.");
    }
}
