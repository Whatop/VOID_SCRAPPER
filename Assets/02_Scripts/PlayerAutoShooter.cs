using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAutoShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform firePoint;       // 총알이 생성될 위치
    [SerializeField] private GameObject bulletPrefab;   // 생성할 총알 프리팹

    [Header("Fire Settings")]
    [SerializeField] private float fireInterval = 0.25f; // 연사 간격(초)
    [SerializeField] private int bulletsPerShot = 1;     // 한 번에 발사할 탄환 수
    [SerializeField] private float spreadAngle = 0f;     // 다중 탄환 발사 각도

    private float fireTimer;

    public float FireInterval => fireInterval;
    public int BulletsPerShot => bulletsPerShot;

    private void OnEnable()
    {
        fireTimer = 0f;
    }

    private void Update()
    {
        HandleFireInput();
    }

    private void HandleFireInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        bool isHoldingLeftClick = Mouse.current.leftButton.isPressed;

        if (!isHoldingLeftClick)
        {
            fireTimer = 0f;
            return;
        }

        fireTimer -= Time.deltaTime;

        if (fireTimer <= 0f)
        {
            Shoot();
            fireTimer = fireInterval;
        }
    }

    /// <summary>
    /// 현재 설정된 탄환 수와 spread 값을 기준으로 발사합니다.
    /// 레벨업 이후에는 1발이 아니라 다중 탄환 패턴을 사용할 수 있습니다.
    /// </summary>
    private void Shoot()
    {
        if (firePoint == null)
        {
            Debug.LogWarning("FirePoint가 연결되지 않았습니다.", this);
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogWarning("Bullet Prefab이 연결되지 않았습니다.", this);
            return;
        }

        int shotCount = Mathf.Max(1, bulletsPerShot);
        float startAngle = -spreadAngle * 0.5f;
        float angleStep = shotCount > 1 ? spreadAngle / (shotCount - 1) : 0f;

        for (int i = 0; i < shotCount; i++)
        {
            float currentAngle = startAngle + angleStep * i;
            Vector2 shotDirection = RotateVector(firePoint.up, currentAngle);
            SpawnBullet(shotDirection);
        }
    }

    private void SpawnBullet(Vector2 direction)
    {
        GameObject bulletObject = null;

        if (PoolManager.Instance != null)
        {
            bulletObject = PoolManager.Instance.Get(bulletPrefab, firePoint.position, Quaternion.identity);
        }
        else
        {
            bulletObject = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        }

        Bullet bullet = bulletObject.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Initialize(direction, ProjectileOwner.Player);
        }
    }

    private Vector2 RotateVector(Vector2 vector, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radian);
        float sin = Mathf.Sin(radian);

        return new Vector2(
            (cos * vector.x) - (sin * vector.y),
            (sin * vector.x) + (cos * vector.y)
        );
    }

    /// <summary>
    /// 연사 간격을 줄여 공격 속도를 높입니다.
    /// </summary>
    public void ReduceFireInterval(float amount)
    {
        fireInterval = Mathf.Max(0.05f, fireInterval - amount);
    }

    /// <summary>
    /// 다중 탄환 발사 패턴을 설정합니다.
    /// </summary>
    public void SetShotPattern(int newBulletsPerShot, float newSpreadAngle)
    {
        bulletsPerShot = Mathf.Max(1, newBulletsPerShot);
        spreadAngle = Mathf.Max(0f, newSpreadAngle);
    }

    /// <summary>
    /// 레벨업 단계에 맞는 사격 업그레이드를 적용합니다.
    /// </summary>
    public void ApplyUpgradeLevel(int level)
    {
        switch (level)
        {
            case 2:
                ReduceFireInterval(0.05f);
                break;
            case 3:
                ReduceFireInterval(0.03f);
                SetShotPattern(3, 16f);
                break;
        }
    }
}
