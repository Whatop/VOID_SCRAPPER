using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ExpeditionEventDamageReceiver : MonoBehaviour, IDamageable
{
    [Header("Owner")]
    [SerializeField] private ExpeditionEventObject eventObject;

    [Header("Collider")]
    [SerializeField] private Collider2D damageCollider;
    [SerializeField] private bool disableColliderOnAwake = true;
    [SerializeField] private bool forceTriggerCollider = true;

    [Header("Hit Forwarding")]
    [Tooltip("켜두면 소유 이벤트가 데미지를 받을 수 없는 상태일 때는 입력 데미지를 무시합니다.")]
    [SerializeField] private bool ignoreDamageWhenInactive = true;

    [Header("Hit Feedback")]
    [SerializeField] private bool useProceduralHitEffect = true;
    [SerializeField] private float hitEffectIntensity = 0.9f;
    [SerializeField] private float hitShakeAmplitude = 0.025f;
    [SerializeField] private float hitShakeDuration = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool logMissingOwner;

    public bool IsDead => eventObject == null || !eventObject.CanReceiveEventDamage;
    public bool IsDamageEnabled => damageCollider != null && damageCollider.enabled;

    private void Reset()
    {
        damageCollider = GetComponent<Collider2D>();
        eventObject = GetComponentInParent<ExpeditionEventObject>();

        if (damageCollider != null)
        {
            damageCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        CacheReferences();

        if (damageCollider != null)
        {
            if (forceTriggerCollider)
            {
                damageCollider.isTrigger = true;
            }

            if (disableColliderOnAwake)
            {
                damageCollider.enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    public void Bind(ExpeditionEventObject owner)
    {
        eventObject = owner;
    }

    public void SetDamageEnabled(bool enabled)
    {
        CacheReferences();

        if (damageCollider != null)
        {
            damageCollider.enabled = enabled;
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, transform.position, Vector2.zero);
    }

    public void TakeDamage(float damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        if (damage <= 0f)
        {
            return;
        }

        CacheReferences();

        if (eventObject == null)
        {
            if (logMissingOwner)
            {
                Debug.LogWarning("ExpeditionEventDamageReceiver: 연결된 ExpeditionEventObject가 없습니다.", this);
            }

            return;
        }

        if (ignoreDamageWhenInactive && !eventObject.CanReceiveEventDamage)
        {
            return;
        }

        CombatFeedbackManager.PlayHit(
            hitPoint,
            incomingDirection,
            CombatFeedbackKind.Structure,
            hitEffectIntensity,
            hitShakeAmplitude,
            hitShakeDuration,
            useProceduralHitEffect
        );

        eventObject.ReceiveEventDamage(damage);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage((float)damage, transform.position, Vector2.zero);
    }

    private void CacheReferences()
    {
        if (damageCollider == null)
        {
            damageCollider = GetComponent<Collider2D>();
        }

        if (eventObject == null)
        {
            eventObject = GetComponentInParent<ExpeditionEventObject>();
        }
    }
}
