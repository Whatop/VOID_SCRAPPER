using UnityEngine;

public enum ProjectileOwner
{
    Player,
    Enemy
}

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 12f;            // 총알 이동 속도
    [SerializeField] private float lifeTime = 2f;          // 총알이 유지되는 시간
    [SerializeField] private float rotationOffset = -90f;  // 탄환 스프라이트 방향 보정값
    [SerializeField] private int damage = 1;               // 총알 1발당 공격력

    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private float lifeTimer;
    private ProjectileOwner owner;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        lifeTimer = lifeTime;
        moveDirection = Vector2.zero;
        owner = ProjectileOwner.Player;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 총알이 날아갈 방향과 발사 주체를 초기화합니다.
    /// </summary>
    public void Initialize(Vector2 direction, ProjectileOwner projectileOwner)
    {
        moveDirection = direction.normalized;
        owner = projectileOwner;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = moveDirection * speed;
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            ReleaseSelf();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 운석은 모든 총알을 막는 장애물입니다.
        MeteorObstacle meteorObstacle = other.GetComponentInParent<MeteorObstacle>();
        if (meteorObstacle != null)
        {
            meteorObstacle.TakeDamage(damage);
            ReleaseSelf();
            return;
        }

        if (owner == ProjectileOwner.Player)
        {
            EnemyHealth enemyHealth = other.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                ReleaseSelf();
            }

            return;
        }

        if (owner == ProjectileOwner.Enemy)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                ReleaseSelf();
            }
        }
    }

    private void ReleaseSelf()
    {
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
