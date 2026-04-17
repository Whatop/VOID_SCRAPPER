using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHp = 3;                  // 플레이어 최대 체력
    [SerializeField] private float invincibleTime = 0.5f;    // 연속 피격 방지 무적 시간

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;     // 피격 이펙트 프리팹
    [SerializeField] private float hitEffectDuration = 0.15f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;
    private PlayerController2D playerController;
    private PlayerAutoShooter playerAutoShooter;

    private int currentHp;
    private float invincibleTimer;
    private bool isDead;

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool IsDead => isDead;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        playerController = GetComponent<PlayerController2D>();
        playerAutoShooter = GetComponent<PlayerAutoShooter>();
    }

    private void OnEnable()
    {
        currentHp = maxHp;
        invincibleTimer = 0f;
        isDead = false;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        if (playerAutoShooter != null)
        {
            playerAutoShooter.enabled = true;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = true;
        }
    }

    private void Update()
    {
        if (invincibleTimer > 0f)
        {
            invincibleTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 플레이어가 대미지를 받을 때 호출합니다.
    /// 무적 시간 중에는 추가 대미지를 받지 않습니다.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        if (invincibleTimer > 0f)
        {
            return;
        }

        currentHp -= damage;
        invincibleTimer = invincibleTime;
        SpawnHitEffect();

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void SpawnHitEffect()
    {
        if (hitEffectPrefab == null || PoolManager.Instance == null)
        {
            return;
        }

        PoolManager.Instance.SpawnAutoRelease(hitEffectPrefab, transform.position, hitEffectDuration);
    }

    /// <summary>
    /// 플레이어가 사망했을 때 이동과 사격을 정지합니다.
    /// 오브젝트 자체는 남겨 두어 카메라가 마지막 위치를 유지할 수 있도록 하였습니다.
    /// </summary>
    private void Die()
    {
        isDead = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerAutoShooter != null)
        {
            playerAutoShooter.enabled = false;
        }

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        Debug.Log("플레이어가 3번 피격되어 사망했습니다.");
    }
}
