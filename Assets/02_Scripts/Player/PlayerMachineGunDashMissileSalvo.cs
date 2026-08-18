using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMachineGunDashMissileSalvo : MonoBehaviour
{
    private const string MissileResourcePath = "VFX/PF_Player_MG_DashMissile";
    private const int MissileCount = 3;
    private static readonly float[] LaunchAngles = { -10f, 0f, 10f };
    private static GameObject cachedMissilePrefab;

    [Header("Missile Salvo")]
    [Min(0f)]
    [SerializeField] private float missileDamage = 0.65f;
    [Min(0.1f)]
    [SerializeField] private float missileSpeed = 10f;
    [Min(0.1f)]
    [SerializeField] private float missileRange = 12f;
    [Min(0.1f)]
    [SerializeField] private float targetQueryRange = 10f;
    [Min(0f)]
    [SerializeField] private float lateralLaunchOffset = 0.14f;
    [Min(0f)]
    [SerializeField] private float forwardLaunchOffset = 0.12f;
    [Min(0f)]
    [SerializeField] private float homingTurnRate = 280f;
    [Min(0.1f)]
    [SerializeField] private float homingRange = 10f;
    [SerializeField] private LayerMask hostileTargetLayers;

    private readonly HashSet<UnityEngine.Object> activationSources =
        new HashSet<UnityEngine.Object>();
    private readonly Collider2D[] targetBuffer = new Collider2D[64];
    private readonly EnemyHealth[] selectedTargets = new EnemyHealth[MissileCount];
    private readonly float[] selectedTargetDistances = new float[MissileCount];

    private PlayerDash playerDash;
    private PlayerController2D playerController;
    private PlayerWeaponController weaponController;
    private PlayerWeaponModifiers weaponModifiers;
    private PlayerVisualStateController visualStateController;
    private bool subscribed;
    private bool warnedMissingPrefab;
    private bool warnedMissingPool;

    public static bool SetSourceEnabled(
        GameObject player,
        UnityEngine.Object source,
        bool value)
    {
        if (player == null || source == null)
        {
            return false;
        }

        PlayerMachineGunDashMissileSalvo salvo =
            player.GetComponent<PlayerMachineGunDashMissileSalvo>();

        if (salvo == null && value)
        {
            salvo = player.AddComponent<PlayerMachineGunDashMissileSalvo>();
        }

        if (salvo == null)
        {
            return false;
        }

        salvo.SetActivationSource(source, value);
        return true;
    }

    private void Awake()
    {
        CacheReferences();

        if (hostileTargetLayers.value == 0)
        {
            hostileTargetLayers = LayerMask.GetMask("Enemy", "Shop");
        }
    }

    private void OnEnable()
    {
        CacheReferences();

        if (activationSources.Count > 0)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        activationSources.Clear();
        ClearSelectedTargets();
    }

    private void SetActivationSource(UnityEngine.Object source, bool value)
    {
        if (value)
        {
            activationSources.Add(source);
            CacheReferences();
            Subscribe();
        }
        else
        {
            activationSources.Remove(source);

            if (activationSources.Count == 0)
            {
                Unsubscribe();
            }
        }
    }

    private void HandleDashStarted(Vector2 dashDirection)
    {
        if (activationSources.Count == 0 ||
            weaponController == null ||
            weaponController.CurrentWeapon == null ||
            weaponController.CurrentWeaponTree != WeaponTreeType.MachineGun)
        {
            return;
        }

        Vector2 aimDirection = ResolveAimDirection(dashDirection);
        Vector2 launchOrigin = weaponController.FirePoint != null
            ? weaponController.FirePoint.position
            : transform.position;
        int targetCount = SelectTargets(launchOrigin);

        if (!TryResolveMissilePrefab(out GameObject missilePrefab) ||
            PoolManager.Instance == null)
        {
            if (PoolManager.Instance == null && !warnedMissingPool)
            {
                warnedMissingPool = true;
                Debug.LogWarning(
                    "MG Dash Missile Salvo requires the scene PoolManager.",
                    this
                );
            }

            ClearSelectedTargets();
            return;
        }

        Vector2 lateral = new Vector2(-aimDirection.y, aimDirection.x);
        float finalDamage = missileDamage * (weaponModifiers != null
            ? weaponModifiers.DamageMultiplier
            : 1f);

        for (int i = 0; i < MissileCount; i++)
        {
            float side = i - 1f;
            Vector2 missileDirection = RotateDirection(aimDirection, LaunchAngles[i]);
            Vector2 spawnPosition = launchOrigin +
                aimDirection * forwardLaunchOffset +
                lateral * (side * lateralLaunchOffset);
            GameObject missileObject = PoolManager.Instance.Get(
                missilePrefab,
                spawnPosition,
                Quaternion.identity
            );

            if (missileObject == null)
            {
                continue;
            }

            Bullet missile = missileObject.GetComponent<Bullet>();

            if (missile == null)
            {
                PoolManager.Instance.Release(missileObject);
                continue;
            }

            EnemyHealth assignedTarget = targetCount > 0
                ? selectedTargets[i % targetCount]
                : null;

            missile.Initialize(
                missileDirection,
                ProjectileOwner.Player,
                null,
                finalDamage,
                missileSpeed,
                missileRange,
                0,
                0f,
                0f,
                1f,
                false,
                gameObject,
                1f
            );
            missile.ConfigureStandaloneHoming(
                assignedTarget != null ? assignedTarget.transform : null,
                homingTurnRate,
                homingRange,
                assignedTarget != null
            );
        }

        visualStateController?.PlayAuxiliaryLaunchPulse(
            launchOrigin,
            aimDirection,
            new Color(0.62f, 1f, 0.18f, 1f),
            0.24f
        );
        ClearSelectedTargets();
    }

    private int SelectTargets(Vector2 origin)
    {
        ClearSelectedTargets();

        if (hostileTargetLayers.value == 0)
        {
            return 0;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hostileTargetLayers);
        filter.useTriggers = true;
        int hitCount = Physics2D.OverlapCircle(
            origin,
            targetQueryRange,
            filter,
            targetBuffer
        );
        int selectedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidateCollider = targetBuffer[i];
            EnemyHealth candidate = candidateCollider != null
                ? candidateCollider.GetComponentInParent<EnemyHealth>()
                : null;

            if (!IsValidHostileTarget(candidate) || ContainsTarget(candidate, selectedCount))
            {
                continue;
            }

            float sqrDistance = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            InsertNearest(candidate, sqrDistance, ref selectedCount);
        }

        return selectedCount;
    }

    private void InsertNearest(
        EnemyHealth candidate,
        float sqrDistance,
        ref int selectedCount)
    {
        if (selectedCount >= MissileCount &&
            sqrDistance >= selectedTargetDistances[MissileCount - 1])
        {
            return;
        }

        int insertIndex = Mathf.Min(selectedCount, MissileCount - 1);

        while (insertIndex > 0 && selectedTargetDistances[insertIndex - 1] > sqrDistance)
        {
            if (insertIndex < MissileCount)
            {
                selectedTargets[insertIndex] = selectedTargets[insertIndex - 1];
                selectedTargetDistances[insertIndex] = selectedTargetDistances[insertIndex - 1];
            }

            insertIndex--;
        }

        if (insertIndex < MissileCount)
        {
            selectedTargets[insertIndex] = candidate;
            selectedTargetDistances[insertIndex] = sqrDistance;
            selectedCount = Mathf.Min(MissileCount, selectedCount + 1);
        }
    }

    private static bool IsValidHostileTarget(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null || enemyHealth.IsDead || !enemyHealth.gameObject.activeInHierarchy)
        {
            return false;
        }

        IPlayerOwnedAlly playerOwnedAlly = enemyHealth.GetComponentInParent<IPlayerOwnedAlly>();

        if (playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly)
        {
            return false;
        }

        BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();

        if (turret != null)
        {
            if (turret.IsPlayerAllied)
            {
                return false;
            }

            if (turret.IsShopDefense && turret.ShopOwner != null && !turret.ShopOwner.IsHostile)
            {
                return false;
            }
        }

        EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
        return enemyAI == null ||
               !enemyAI.IsShopSecurityUnit ||
               ShopRunBridge.IsShopHostileThisRun();
    }

    private bool ContainsTarget(EnemyHealth target, int selectedCount)
    {
        for (int i = 0; i < selectedCount; i++)
        {
            if (selectedTargets[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 ResolveAimDirection(Vector2 dashDirection)
    {
        if (playerController != null && playerController.AimDirection.sqrMagnitude > 0.001f)
        {
            return playerController.AimDirection.normalized;
        }

        if (dashDirection.sqrMagnitude > 0.001f)
        {
            return dashDirection.normalized;
        }

        return transform.up;
    }

    private static Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine
        ).normalized;
    }

    private bool TryResolveMissilePrefab(out GameObject missilePrefab)
    {
        if (cachedMissilePrefab == null)
        {
            cachedMissilePrefab = Resources.Load<GameObject>(MissileResourcePath);
        }

        missilePrefab = cachedMissilePrefab;

        if (missilePrefab == null && !warnedMissingPrefab)
        {
            warnedMissingPrefab = true;
            Debug.LogWarning(
                $"MG Dash Missile prefab is missing from Resources/{MissileResourcePath}.",
                this
            );
        }

        return missilePrefab != null;
    }

    private void CacheReferences()
    {
        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (weaponModifiers == null)
        {
            weaponModifiers = GetComponent<PlayerWeaponModifiers>();
        }

        if (visualStateController == null)
        {
            visualStateController = GetComponent<PlayerVisualStateController>();
        }
    }

    private void Subscribe()
    {
        if (subscribed || playerDash == null)
        {
            return;
        }

        playerDash.DashStarted += HandleDashStarted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || playerDash == null)
        {
            return;
        }

        playerDash.DashStarted -= HandleDashStarted;
        subscribed = false;
    }

    private void ClearSelectedTargets()
    {
        for (int i = 0; i < MissileCount; i++)
        {
            selectedTargets[i] = null;
            selectedTargetDistances[i] = float.MaxValue;
        }
    }
}
