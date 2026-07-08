using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MeteorObstacle : MonoBehaviour
{
    [Header("Meteor Settings")]
    [SerializeField] private int maxHp = 3;                  // 운석 최대 체력
    [SerializeField] private float minDriftSpeed = 0.4f;     // 최소 이동 속도
    [SerializeField] private float maxDriftSpeed = 1.2f;     // 최대 이동 속도
    [SerializeField] private float minSpinSpeed = -50f;      // 최소 회전 속도
    [SerializeField] private float maxSpinSpeed = 50f;       // 최대 회전 속도
    [SerializeField] private float boundsPadding = 0.5f;     // 맵 경계에서 반사될 때 사용하는 여유값

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;     // 피격 이펙트 프리팹
    [SerializeField] private float hitEffectDuration = 0.12f;

    private int currentHp;
    private Vector2 moveDirection;
    private float moveSpeed;
    private float spinSpeed;

    private Bounds roamingBounds;
    private bool hasRoamingBounds;

    private void OnEnable()
    {
        currentHp = maxHp;
        RandomizeMovement();
    }

    private void Update()
    {
        transform.position += (Vector3)(moveDirection * moveSpeed * Time.deltaTime);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        HandleBoundsBounce();
    }

    /// <summary>
    /// 스포너가 전달한 맵 경계 안에서 운석이 떠돌아다니도록 범위를 저장합니다.
    /// </summary>
    public void SetRoamingBounds(Bounds bounds)
    {
        roamingBounds = bounds;
        hasRoamingBounds = true;
    }

    /// <summary>
    /// 총알이 운석에 맞았을 때 호출됩니다.
    /// 운석은 모든 총알을 막는 장애물 역할을 합니다.
    /// </summary>
    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        SpawnHitEffect();
        AudioManager.PlayAt(SoundEventIds.ObjectMeteorHit, transform.position, 0.65f);

        if (currentHp <= 0)
        {
            ReleaseSelf();
        }
    }

    private void RandomizeMovement()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        moveSpeed = Random.Range(minDriftSpeed, maxDriftSpeed);
        spinSpeed = Random.Range(minSpinSpeed, maxSpinSpeed);
    }

    private void HandleBoundsBounce()
    {
        if (!hasRoamingBounds)
        {
            return;
        }

        Vector3 position = transform.position;
        bool bounced = false;

        if (position.x <= roamingBounds.min.x + boundsPadding && moveDirection.x < 0f)
        {
            moveDirection.x *= -1f;
            position.x = roamingBounds.min.x + boundsPadding;
            bounced = true;
        }
        else if (position.x >= roamingBounds.max.x - boundsPadding && moveDirection.x > 0f)
        {
            moveDirection.x *= -1f;
            position.x = roamingBounds.max.x - boundsPadding;
            bounced = true;
        }

        if (position.y <= roamingBounds.min.y + boundsPadding && moveDirection.y < 0f)
        {
            moveDirection.y *= -1f;
            position.y = roamingBounds.min.y + boundsPadding;
            bounced = true;
        }
        else if (position.y >= roamingBounds.max.y - boundsPadding && moveDirection.y > 0f)
        {
            moveDirection.y *= -1f;
            position.y = roamingBounds.max.y - boundsPadding;
            bounced = true;
        }

        if (bounced)
        {
            transform.position = position;
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

    private void ReleaseSelf()
    {
        AudioManager.PlayAt(SoundEventIds.ObjectMeteorBreak, transform.position);

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
