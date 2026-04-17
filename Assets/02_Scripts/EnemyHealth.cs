using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHp = 3; // 적 최대 체력

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab; // 피격 이펙트 프리팹
    [SerializeField] private float hitEffectDuration = 0.12f;

    [Header("Drop Settings")]
    [SerializeField] private GameObject dropContainerPrefab; // 파츠 컨테이너 프리팹
    [SerializeField] private float dropChance = 1f;

    private int currentHp;
    private bool isDead;
    private EnemyBaseAI enemyAI;

    public int CurrentHp => currentHp;
    public bool IsDead => isDead;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyBaseAI>();
    }

    private void OnEnable()
    {
        currentHp = maxHp;
        isDead = false;
    }

    /// <summary>
    /// 외부에서 호출하는 피격 함수입니다.
    /// damage만큼 체력을 감소시키고 0 이하가 되면 적을 비활성화합니다.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead)
        {
            return;
        }

        currentHp -= damage;
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

    /// <summary>
    /// 적이 사망했을 때 호출됩니다.
    /// 적은 Destroy 대신 풀로 반환되어 이후 재사용됩니다.
    /// </summary>
    private void Die()
    {
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
