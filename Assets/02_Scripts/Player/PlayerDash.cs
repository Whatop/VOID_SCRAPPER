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

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.12f;
    [SerializeField] private float dashCooldown = 1.1f;
    [SerializeField] private float invincibleTime = 0.1f;

    [Header("Projectile Clear During Dash")]
    [SerializeField] private LayerMask projectileClearLayer;
    [SerializeField] private float projectileClearRadius = 1f;

    [Header("Normal Knockback")]
    [SerializeField] private LayerMask knockbackLayer;
    [SerializeField] private float knockbackRadius = 1.2f;
    [SerializeField] private float knockbackDistance = 1.5f;

    [Header("Dash Shockwave")]
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

    public bool IsDashing => isDashing;
    public float DashDistance => dashDistance;
    public float DashDuration => dashDuration;
    public float DashCooldown => dashCooldown;
    public float RemainingCooldown => Mathf.Max(0f, lastDashTime + dashCooldown - Time.time);
    public float CooldownRatio => dashCooldown <= 0f ? 0f : Mathf.Clamp01(RemainingCooldown / dashCooldown);
    public bool CanDash => !isDashing && RemainingCooldown <= 0f;

    public event Action<Vector2> DashStarted;
    public event Action DashEnded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController2D>();
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
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

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            return;
        }

        dashAction = actionMap.FindAction(dashActionName, false);
        if (dashAction != null)
        {
            dashAction.Enable();
        }
    }

    private bool WasDashPressed()
    {
        if (dashAction != null && dashAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            return true;
        }

        return false;
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
        isDashing = true;
        lastDashTime = Time.time;

        DashStarted?.Invoke(direction);

        if (controller != null)
        {
            controller.SetMovementLocked(true);
        }

        if (health != null)
        {
            health.AddInvincibleTime(invincibleTime);
        }

        TriggerDashShockwave();
        ClearProjectilesAt(transform.position, projectileClearRadius);
        PushNearbyObjectsAt(transform.position, knockbackRadius, knockbackDistance);

        float elapsed = 0f;
        float dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);

        while (elapsed < dashDuration)
        {
            if (rb != null)
            {
                rb.linearVelocity = direction * dashSpeed;
            }

            ClearProjectilesAt(transform.position, projectileClearRadius);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        EndDashState();
    }

    private void EndDashState()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (controller != null)
        {
            controller.SetMovementLocked(false);
        }

        isDashing = false;
        dashRoutine = null;

        DashEnded?.Invoke();
    }

    private void TriggerDashShockwave()
    {
        if (!useShockwave)
        {
            return;
        }

        Vector2 center = transform.position;
        SpawnShockwaveVfx(center);
        ClearProjectilesAt(center, shockwaveProjectileClearRadius);
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

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();
            if (receiver != null)
            {
                Component receiverComponent = receiver as Component;
                int receiverId = receiverComponent != null ? receiverComponent.GetInstanceID() : receiver.GetHashCode();

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