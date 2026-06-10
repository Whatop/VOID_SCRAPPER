using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ShopProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float defaultSpeed = 9f;
    [SerializeField] private float defaultDamage = 2f;
    [SerializeField] private float defaultLifetime = 4f;

    [Header("Hit")]
    [SerializeField] private LayerMask hitLayer;
    [SerializeField] private bool releaseOnHit = true;

    private Rigidbody2D rb;
    private Vector2 direction = Vector2.up;
    private float speed;
    private float damage;
    private float lifetime;
    private float timer;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        timer = 0f;

        if (!initialized)
        {
            speed = defaultSpeed;
            damage = defaultDamage;
            lifetime = defaultLifetime;
            direction = transform.up;
        }
    }

    private void OnDisable()
    {
        initialized = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void Initialize(Vector2 newDirection, float newSpeed, float newDamage, float newLifetime)
    {
        direction = newDirection.sqrMagnitude > 0.001f ? newDirection.normalized : Vector2.up;
        speed = Mathf.Max(0.01f, newSpeed);
        damage = Mathf.Max(0f, newDamage);
        lifetime = Mathf.Max(0.1f, newLifetime);
        timer = 0f;
        initialized = true;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifetime)
        {
            ReleaseSelf();
            return;
        }

        if (rb == null)
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || other.isTrigger)
        {
            return;
        }

        if (hitLayer.value != 0 && ((1 << other.gameObject.layer) & hitLayer.value) == 0)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            ComponentShieldPassive shield = playerHealth.GetComponent<ComponentShieldPassive>();

            if (shield != null && shield.TryBlockDamage(transform.position))
            {
                ReleaseSelf();
                return;
            }

            playerHealth.TakeDamage(damage, transform.position);

            if (releaseOnHit)
            {
                ReleaseSelf();
            }

            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage);

            if (releaseOnHit)
            {
                ReleaseSelf();
            }
        }
    }

    public void ReleaseSelf()
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