using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHp = 20f;
    [SerializeField] private float invincibleTimeAfterHit = 0.5f;

    [Header("References")]
    [SerializeField] private PlayerArmor armor;
    [SerializeField] private ComponentShieldPassive componentShield;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.15f;

    [Header("Death")]
    [SerializeField] private bool disableColliderOnDeath = true;
    [SerializeField] private MonoBehaviour[] componentsToDisableOnDeath;

    [Header("Legacy Death Return")]
    [Tooltip("사망 연출 없이 바로 정착지로 돌아가고 싶을 때만 켠다. PlayerDeathSequenceController를 쓸 거면 꺼둔다.")]
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

        Healed?.Invoke(currentHp, maxHp);
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
            Healed?.Invoke(currentHp, maxHp);
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position);
    }

    public void TakeDamage(float damage, Vector2 hitPoint)
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

        SpawnHitEffect(hitPoint);
        AudioManager.PlayAt(SoundEventIds.ShipHit, hitPoint);

        if (remainingDamage <= 0f)
        {
            return;
        }

        currentHp = Mathf.Max(0f, currentHp - remainingDamage);
        Damaged?.Invoke(currentHp, maxHp);

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
    }

    public void AddInvincibleTime(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        invincibleTimer = Mathf.Max(invincibleTimer, duration);
    }

    private void SpawnHitEffect(Vector2 position)
    {
        if (hitEffectPrefab == null)
        {
            return;
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

        Died?.Invoke();

        if (completeRunDirectlyOnDeath && RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CompleteRun(RunEndReason.Death);
        }
        Debug.Log("플레이어 기체가 파괴되었습니다.");
    }
}