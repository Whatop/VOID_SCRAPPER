using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 중립 상점의 안전 구역.
/// 중립 상태에서는 적 탄환을 제거하고 일반 적을 구역 밖으로 후퇴시킨다.
/// 플레이어가 진입하면 상점 음악으로 자연스럽게 전환한다.
/// 상점이 적대화되거나 파괴되면 안전 구역 기능과 상점 음악을 즉시 해제한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ShopNeutralZone2D : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private ShopStructure shopOwner;
    [SerializeField] private Collider2D zoneCollider;

    [Header("Enemy Retreat")]
    [Min(0.1f)]
    [SerializeField] private float retreatPadding = 1.25f;
    [Min(0.1f)]
    [SerializeField] private float retreatSpeedMultiplier = 1.35f;
    [Min(0f)]
    [SerializeField] private float turretThreatMemoryDuration = 4f;

    [Header("Projectile")]
    [SerializeField] private bool clearEnemyProjectiles = true;

    [Header("Audio")]
    [Tooltip("플레이어가 중립 안전 구역에 들어오면 상점 음악 모드로 전환합니다.")]
    [SerializeField] private bool useShopMusicInsideZone = true;

    [Header("Debug")]
    [SerializeField] private bool drawZoneGizmo;

    private readonly HashSet<EnemyBaseAI> containedEnemies = new HashSet<EnemyBaseAI>();
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();

    private bool shopAudioModeEntered;

    public bool IsActiveSafeZone =>
        isActiveAndEnabled &&
        shopOwner != null &&
        shopOwner.IsNeutralSafeZoneActive;

    public float RetreatSpeedMultiplier => Mathf.Max(0.1f, retreatSpeedMultiplier);
    public float TurretThreatMemoryDuration => Mathf.Max(0f, turretThreatMemoryDuration);

    private void Reset()
    {
        zoneCollider = GetComponent<Collider2D>();
        shopOwner = GetComponentInParent<ShopStructure>();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider2D>();
        }

        if (shopOwner == null)
        {
            shopOwner = GetComponentInParent<ShopStructure>();
        }

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (shopOwner != null)
        {
            shopOwner.StateChanged -= HandleShopStateChanged;
            shopOwner.StateChanged += HandleShopStateChanged;
            shopOwner.Died -= HandleShopDied;
            shopOwner.Died += HandleShopDied;
        }
    }

    private void OnDisable()
    {
        if (shopOwner != null)
        {
            shopOwner.StateChanged -= HandleShopStateChanged;
            shopOwner.Died -= HandleShopDied;
        }

        ReleaseContainedEnemies();
        ReleaseShopAudioMode();
    }

    private void LateUpdate()
    {
        if (playerColliders.Count <= 0)
        {
            return;
        }

        playerColliders.RemoveWhere(collider => collider == null || !collider.enabled);

        if (playerColliders.Count <= 0)
        {
            ReleaseShopAudioMode();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ProcessCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        ProcessCollider(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController2D playerController = other.GetComponentInParent<PlayerController2D>();

        if (playerController != null)
        {
            playerColliders.Remove(other);

            if (playerColliders.Count <= 0)
            {
                ReleaseShopAudioMode();
            }

            return;
        }

        EnemyBaseAI enemy = other.GetComponentInParent<EnemyBaseAI>();

        if (enemy == null)
        {
            return;
        }

        containedEnemies.Remove(enemy);
        enemy.ExitShopNeutralZone(this);
    }

    public Vector2 GetRetreatPoint(Vector2 enemyPosition)
    {
        Bounds bounds = zoneCollider != null
            ? zoneCollider.bounds
            : new Bounds(transform.position, Vector3.one * 6f);

        Vector2 center = bounds.center;
        Vector2 direction = enemyPosition - center;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float radiusX = Mathf.Max(0.1f, bounds.extents.x);
        float radiusY = Mathf.Max(0.1f, bounds.extents.y);
        float denominator = Mathf.Sqrt(
            (direction.x * direction.x) / (radiusX * radiusX) +
            (direction.y * direction.y) / (radiusY * radiusY)
        );
        float edgeDistance = denominator > 0.001f
            ? 1f / denominator
            : Mathf.Max(radiusX, radiusY);

        return center + direction * (edgeDistance + Mathf.Max(0.1f, retreatPadding));
    }

    private void ProcessCollider(Collider2D other)
    {
        if (other == null || !IsActiveSafeZone)
        {
            return;
        }

        PlayerController2D playerController = other.GetComponentInParent<PlayerController2D>();

        if (playerController != null)
        {
            playerColliders.Add(other);
            EnterShopAudioMode();
            return;
        }

        Bullet bullet = other.GetComponentInParent<Bullet>();

        if (clearEnemyProjectiles && bullet != null && bullet.Owner == ProjectileOwner.Enemy)
        {
            bullet.ForceRelease(false);
            return;
        }

        EnemyBaseAI enemy = other.GetComponentInParent<EnemyBaseAI>();

        if (enemy == null || enemy.IsShopSecurityUnit)
        {
            return;
        }

        containedEnemies.Add(enemy);
        enemy.EnterShopNeutralZone(this);
    }

    private void EnterShopAudioMode()
    {
        if (!useShopMusicInsideZone || shopAudioModeEntered || !IsActiveSafeZone)
        {
            return;
        }

        shopAudioModeEntered = true;
        GameAudioLoopController.EnterShopMode();
    }

    private void ReleaseShopAudioMode()
    {
        playerColliders.Clear();

        if (!shopAudioModeEntered)
        {
            return;
        }

        shopAudioModeEntered = false;
        GameAudioLoopController.ExitShopMode();
    }

    private void HandleShopStateChanged(ShopStructure _)
    {
        if (!IsActiveSafeZone)
        {
            ReleaseContainedEnemies();
            ReleaseShopAudioMode();
        }
    }

    private void HandleShopDied(ShopStructure _)
    {
        ReleaseContainedEnemies();
        ReleaseShopAudioMode();
    }

    private void ReleaseContainedEnemies()
    {
        foreach (EnemyBaseAI enemy in containedEnemies)
        {
            if (enemy != null)
            {
                enemy.ExitShopNeutralZone(this);
            }
        }

        containedEnemies.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawZoneGizmo)
        {
            return;
        }

        Collider2D activeCollider = zoneCollider != null
            ? zoneCollider
            : GetComponent<Collider2D>();

        if (activeCollider == null)
        {
            return;
        }

        Gizmos.color = IsActiveSafeZone
            ? new Color(0.2f, 1f, 0.65f, 0.35f)
            : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawWireCube(activeCollider.bounds.center, activeCollider.bounds.size);
    }
#endif
}
