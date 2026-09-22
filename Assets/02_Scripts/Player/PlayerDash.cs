using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDash : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string dashActionName = "Dash";

    [Header("References")]
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Fallback")]
    [SerializeField] private WeaponTreeType fallbackWeaponTree = WeaponTreeType.MachineGun;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 1.1f;

    [Header("Projectile Clear During Dash")]
    [SerializeField] private LayerMask projectileClearLayer;
    [SerializeField] private float projectileClearRadius = 1f;

    [Header("Normal Knockback")]
    [SerializeField] private LayerMask knockbackLayer;
    [SerializeField] private float knockbackRadius = 1.2f;
    [SerializeField] private float knockbackDistance = 1.5f;

    [Header("Weapon Dash Rules")]
    [Tooltip("기관총은 대쉬 이동과 무적만 사용합니다. 탄 삭제, 적 밀침, 충격파를 사용하지 않습니다.")]
    [SerializeField] private bool machineGunDashInvincibleOnly = true;

    [Tooltip("샷건은 기본 대쉬에서 근접 적을 살짝 밀어낼 수 있습니다.")]
    [SerializeField] private bool shotgunUsesNormalKnockback = true;

    [Tooltip("스나이퍼도 대쉬 밀침을 줄지 여부입니다. 기본은 꺼두는 것을 권장합니다.")]
    [SerializeField] private bool sniperUsesNormalKnockback;

    [Tooltip("스나이퍼가 대쉬 중 적 탄환을 삭제할지 여부입니다.")]
    [SerializeField] private bool sniperClearsProjectiles = true;

    [Tooltip("샷건 충격파를 해금형 능력으로 사용할지 여부입니다.")]
    [SerializeField] private bool shotgunShockwaveRequiresUnlock = true;

    [Tooltip("샷건 충격파 해금 상태입니다. 나중에 특성/해금 시스템에서 true로 바꾸면 됩니다.")]
    [SerializeField] private bool shotgunShockwaveUnlocked;

    [Header("Dash Shockwave - Shotgun Unlock Only")]
    [SerializeField] private bool useShockwave = true;
    [SerializeField] private GameObject shockwavePrefab;
    [SerializeField] private float shockwaveVfxRadius = 2.2f;
    [SerializeField] private float shockwaveVfxDuration = 0.18f;
    [SerializeField] private float shockwaveProjectileClearRadius = 2.2f;
    [SerializeField] private float shockwaveKnockbackRadius = 2.0f;
    [SerializeField] private float shockwaveKnockbackDistance = 2.0f;

    private Rigidbody2D rb;
    private PlayerController2D controller;
    private PlayerHealth health;

    private InputAction dashAction;
    private Coroutine dashRoutine;

    private float lastDashTime = -999f;
    private bool isDashing;

    private readonly Collider2D[] projectileBuffer = new Collider2D[96];
    private readonly Collider2D[] knockbackBuffer = new Collider2D[64];
    private readonly HashSet<int> processedKnockbackTargets = new HashSet<int>();
    private readonly Dictionary<object, float> externalCooldownMultipliers =
        new Dictionary<object, float>();
    private float externalCooldownMultiplier = 1f;

    public bool IsDashing => isDashing;
    public float DashDistance => dashDistance;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown;
    public float EffectiveDashCooldown => dashCooldown * externalCooldownMultiplier;
    public float RemainingCooldown => Mathf.Max(0f, lastDashTime + EffectiveDashCooldown - Time.time);
    public float CooldownRatio => EffectiveDashCooldown <= 0f
        ? 0f
        : Mathf.Clamp01(RemainingCooldown / EffectiveDashCooldown);
    public bool CanDash => !isDashing && RemainingCooldown <= 0f;
    public bool ShotgunShockwaveUnlocked => shotgunShockwaveUnlocked;

    public event Action<Vector2> DashStarted;
    public event Action DashEnded;
    public void ClearEquipmentDashWindow() => LastCompletedDashTime = float.NegativeInfinity;
    public int CompletedDashSerial { get; private set; }
    public float LastCompletedDashTime { get; private set; } = float.NegativeInfinity;
    public WeaponTreeType LastCompletedDashWeapon { get; private set; }
    public bool HasRecentShotgunDash => LastCompletedDashWeapon == WeaponTreeType.Shotgun &&
        ResolveCurrentWeaponTree() == WeaponTreeType.Shotgun && Time.time - LastCompletedDashTime <= .6f &&
        isActiveAndEnabled && health != null && !health.IsDead;

    private struct DashEffectProfile
    {
        public bool useProjectileClear;
        public bool useNormalKnockback;
        public bool useShockwave;

        public DashEffectProfile(bool useProjectileClear, bool useNormalKnockback, bool useShockwave)
        {
            this.useProjectileClear = useProjectileClear;
            this.useNormalKnockback = useNormalKnockback;
            this.useShockwave = useShockwave;
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        BindInput();
    }

    private void OnDisable()
    {
        ClearEquipmentDashWindow();
        if (dashAction != null)
        {
            dashAction.Disable();
        }

        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
        }

        if (isDashing)
        {
            EndDashState();
        }
    }

    private void Update()
    {
        if (WasDashPressed())
        {
            TryDash();
        }
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (controller == null)
        {
            controller = GetComponent<PlayerController2D>();
        }

        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        dashAction = InputBindingUtility.ResolveAction(inputActions, actionMapName, dashActionName);
        dashAction?.Enable();
    }

    private bool WasDashPressed()
    {
        if (dashAction != null)
        {
            return dashAction.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    public bool TryDash()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return false;
        }

        if (!CanDash)
        {
            return false;
        }

        if (health != null && health.IsDead)
        {
            return false;
        }

        if (controller != null && (!controller.ControlEnabled || controller.MovementLocked))
        {
            return false;
        }

        Vector2 direction = GetDashDirection();

        if (direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        dashRoutine = StartCoroutine(DashRoutine(direction.normalized));
        return true;
    }

    private Vector2 GetDashDirection()
    {
        if (controller != null)
        {
            if (controller.MoveInput.sqrMagnitude > 0.001f)
            {
                return controller.MoveInput.normalized;
            }

            if (controller.AimDirection.sqrMagnitude > 0.001f)
            {
                return controller.AimDirection.normalized;
            }
        }

        return transform.up;
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        DashEffectProfile effectProfile = ResolveDashEffectProfile();
        float dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);
        Vector2 dashVelocity = direction * dashSpeed;

        isDashing = true;
        lastDashTime = Time.time;
        health?.SetDashInvincible(true);

        DashStarted?.Invoke(direction);
        if (!isDashing)
        {
            yield break;
        }
        AudioManager.PlayAt(SoundEventIds.ShipDashStart, transform.position);

        if (controller != null)
        {
            controller.SetMovementLocked(true);
            controller.SetMovementVelocityOverride(dashVelocity);
        }
        else if (rb != null)
        {
            rb.linearVelocity = dashVelocity;
        }

        TriggerDashShockwave(effectProfile);

        if (effectProfile.useProjectileClear)
        {
            ClearProjectilesAt(transform.position, projectileClearRadius);
        }

        if (effectProfile.useNormalKnockback)
        {
            PushNearbyObjectsAt(transform.position, knockbackRadius, knockbackDistance);
        }

        float elapsed = 0f;

        // A dash effect/event can kill the boss synchronously, before
        // StartCoroutine has returned a handle to CancelActiveDash.
        while (isDashing && elapsed < dashDuration)
        {
            if (controller != null)
            {
                controller.SetMovementVelocityOverride(dashVelocity);
            }
            else if (rb != null)
            {
                rb.linearVelocity = dashVelocity;
            }

            if (effectProfile.useProjectileClear)
            {
                ClearProjectilesAt(transform.position, projectileClearRadius);
            }

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isDashing)
        {
            EndDashState(true);
        }
    }

    // Cinematics cancel the active dash through its owner, without disabling
    // the component or capturing its transient movement lock for later restore.
    public void CancelActiveDash()
    {
        if (dashRoutine != null)
        {
            StopCoroutine(dashRoutine);
            dashRoutine = null;
        }
        if (isDashing)
        {
            EndDashState();
        }
    }

    private void EndDashState(bool completed = false)
    {
        if (completed && health != null && !health.IsDead)
        {
            CompletedDashSerial++;
            LastCompletedDashTime = Time.time;
            LastCompletedDashWeapon = ResolveCurrentWeaponTree();
        }
        else
        {
            LastCompletedDashTime = float.NegativeInfinity;
        }
        if (controller != null)
        {
            controller.ClearMovementVelocityOverride();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (controller != null)
        {
            controller.SetMovementLocked(false);
        }

        isDashing = false;
        health?.SetDashInvincible(false);
        dashRoutine = null;

        DashEnded?.Invoke();
    }

    private DashEffectProfile ResolveDashEffectProfile()
    {
        WeaponTreeType currentTree = ResolveCurrentWeaponTree();

        switch (currentTree)
        {
            case WeaponTreeType.Shotgun:
                return new DashEffectProfile(
                    useProjectileClear: true,
                    useNormalKnockback: shotgunUsesNormalKnockback,
                    useShockwave: CanUseShotgunShockwave()
                );

            case WeaponTreeType.Sniper:
                return new DashEffectProfile(
                    useProjectileClear: sniperClearsProjectiles,
                    useNormalKnockback: sniperUsesNormalKnockback,
                    useShockwave: false
                );

            case WeaponTreeType.MachineGun:
            default:
                if (machineGunDashInvincibleOnly)
                {
                    return new DashEffectProfile(
                        useProjectileClear: false,
                        useNormalKnockback: false,
                        useShockwave: false
                    );
                }

                return new DashEffectProfile(
                    useProjectileClear: false,
                    useNormalKnockback: false,
                    useShockwave: false
                );
        }
    }

    private WeaponTreeType ResolveCurrentWeaponTree()
    {
        if (weaponController != null && weaponController.CurrentWeapon != null)
        {
            return weaponController.CurrentWeaponTree;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return fallbackWeaponTree;
    }

    private bool CanUseShotgunShockwave()
    {
        if (!useShockwave)
        {
            return false;
        }

        if (!shotgunShockwaveRequiresUnlock)
        {
            return true;
        }

        return shotgunShockwaveUnlocked;
    }

    private void TriggerDashShockwave(DashEffectProfile effectProfile)
    {
        if (!effectProfile.useShockwave)
        {
            return;
        }

        Vector2 center = transform.position;

        SpawnShockwaveVfx(center);

        if (effectProfile.useProjectileClear)
        {
            ClearProjectilesAt(center, shockwaveProjectileClearRadius);
        }

        PushNearbyObjectsAt(center, shockwaveKnockbackRadius, shockwaveKnockbackDistance);
    }

    private void SpawnShockwaveVfx(Vector2 center)
    {
        if (shockwavePrefab == null)
        {
            return;
        }

        GameObject instance;

        if (PoolManager.Instance != null)
        {
            instance = PoolManager.Instance.Get(shockwavePrefab, center, Quaternion.identity);
        }
        else
        {
            instance = Instantiate(shockwavePrefab, center, Quaternion.identity);
        }

        if (instance == null)
        {
            return;
        }

        DashShockwaveVFX shockwaveVFX = instance.GetComponent<DashShockwaveVFX>();
        if (shockwaveVFX != null)
        {
            shockwaveVFX.Play(shockwaveVfxRadius, shockwaveVfxDuration);
        }

        float releaseDelay = Mathf.Max(0.01f, shockwaveVfxDuration + 0.05f);

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReleaseAfter(instance, releaseDelay);
        }
        else
        {
            Destroy(instance, releaseDelay);
        }
    }

    private void ClearProjectilesAt(Vector2 center, float radius)
    {
        if (projectileClearLayer.value == 0)
        {
            return;
        }

        if (radius <= 0f)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            radius,
            projectileBuffer,
            projectileClearLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = projectileBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Bullet bullet = hit.GetComponentInParent<Bullet>();
            if (bullet == null)
            {
                continue;
            }

            if (bullet.Owner != ProjectileOwner.Enemy)
            {
                continue;
            }

            GameObject targetObject = bullet.gameObject;

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(targetObject);
            }
            else
            {
                Destroy(targetObject);
            }
        }
    }

    private void PushNearbyObjectsAt(Vector2 center, float radius, float distance)
    {
        if (knockbackLayer.value == 0)
        {
            return;
        }

        if (radius <= 0f || distance <= 0f)
        {
            return;
        }

        processedKnockbackTargets.Clear();

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            radius,
            knockbackBuffer,
            knockbackLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = knockbackBuffer[i];
            if (hit == null)
            {
                continue;
            }

            if (hit.attachedRigidbody == rb)
            {
                continue;
            }

            BaseTurretController turret = hit.GetComponentInParent<BaseTurretController>();
            if (turret != null && turret.IsPlayerAllied)
            {
                continue;
            }

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();
            if (receiver != null)
            {
                Component receiverComponent = receiver as Component;
                int receiverId = receiverComponent != null
                    ? receiverComponent.GetInstanceID()
                    : receiver.GetHashCode();

                if (processedKnockbackTargets.Contains(receiverId))
                {
                    continue;
                }

                processedKnockbackTargets.Add(receiverId);
                receiver.ApplyKnockback(center, distance);
                continue;
            }

            Rigidbody2D targetRb = hit.attachedRigidbody;
            if (targetRb == null || targetRb == rb)
            {
                continue;
            }

            int rbId = targetRb.GetInstanceID();
            if (processedKnockbackTargets.Contains(rbId))
            {
                continue;
            }

            processedKnockbackTargets.Add(rbId);

            Vector2 direction = targetRb.position - center;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = UnityEngine.Random.insideUnitCircle;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            targetRb.position += direction * distance;
        }
    }

    public void SetDashDistance(float value)
    {
        dashDistance = Mathf.Max(0.1f, value);
    }

    public void AddDashDistance(float amount)
    {
        dashDistance = Mathf.Max(0.1f, dashDistance + amount);
    }

    public void SetDashCooldown(float value)
    {
        dashCooldown = Mathf.Max(0.05f, value);
    }

    public void AddDashCooldown(float amount)
    {
        dashCooldown = Mathf.Max(0.05f, dashCooldown + amount);
    }

    public void SetExternalCooldownMultiplier(object source, float multiplier)
    {
        if (source == null)
        {
            return;
        }

        externalCooldownMultipliers[source] = Mathf.Max(0.05f, multiplier);
        RecalculateExternalCooldownMultiplier();
    }

    public void ClearExternalCooldownMultiplier(object source)
    {
        if (source == null || !externalCooldownMultipliers.Remove(source))
        {
            return;
        }

        RecalculateExternalCooldownMultiplier();
    }

    private void RecalculateExternalCooldownMultiplier()
    {
        float multiplier = 1f;

        foreach (KeyValuePair<object, float> entry in externalCooldownMultipliers)
        {
            multiplier *= Mathf.Max(0.05f, entry.Value);
        }

        externalCooldownMultiplier = Mathf.Max(0.05f, multiplier);
    }

    public void SetShotgunShockwaveUnlocked(bool unlocked)
    {
        shotgunShockwaveUnlocked = unlocked;
    }

    public void UnlockShotgunShockwave()
    {
        shotgunShockwaveUnlocked = true;
    }

    public void LockShotgunShockwave()
    {
        shotgunShockwaveUnlocked = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, projectileClearRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, knockbackRadius);

        if (useShockwave)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, shockwaveProjectileClearRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, shockwaveKnockbackRadius);
        }
    }
}
