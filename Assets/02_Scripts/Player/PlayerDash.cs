using System.Collections;
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
    [SerializeField] private float dashCooldown = 0.7f;
    [SerializeField] private float invincibleTime = 0.1f;

    [Header("Projectile Clear")]
    [SerializeField] private LayerMask projectileClearLayer;
    [SerializeField] private float projectileClearRadius = 1f;

    [Header("Knockback")]
    [SerializeField] private LayerMask knockbackLayer;
    [SerializeField] private float knockbackRadius = 1.2f;
    [SerializeField] private float knockbackDistance = 1.5f;

    private Rigidbody2D rb;
    private PlayerController2D controller;
    private PlayerHealth health;

    private InputAction dashAction;
    private Coroutine dashRoutine;

    private float lastDashTime = -999f;
    private bool isDashing;

    private readonly Collider2D[] projectileBuffer = new Collider2D[64];
    private readonly Collider2D[] knockbackBuffer = new Collider2D[32];

    public bool IsDashing => isDashing;
    public bool CanDash => !isDashing && Time.time >= lastDashTime + dashCooldown;

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

        isDashing = false;

        if (controller != null)
        {
            controller.SetMovementLocked(false);
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

        if (controller != null)
        {
            controller.SetMovementLocked(true);
        }

        if (health != null)
        {
            health.AddInvincibleTime(invincibleTime);
        }

        ClearProjectiles();
        PushNearbyObjects();

        float elapsed = 0f;
        float dashSpeed = dashDistance / Mathf.Max(0.01f, dashDuration);

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = direction * dashSpeed;

            ClearProjectiles();

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        if (controller != null)
        {
            controller.SetMovementLocked(false);
        }

        isDashing = false;
        dashRoutine = null;
    }

    private void ClearProjectiles()
    {
        if (projectileClearLayer.value == 0)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            projectileClearRadius,
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
            GameObject targetObject = bullet != null ? bullet.gameObject : hit.gameObject;

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

    private void PushNearbyObjects()
    {
        if (knockbackLayer.value == 0)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            knockbackRadius,
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

            if (hit.transform == transform)
            {
                continue;
            }

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();
            if (receiver != null)
            {
                receiver.ApplyKnockback(transform.position, knockbackDistance);
                continue;
            }

            Rigidbody2D targetRb = hit.attachedRigidbody;
            if (targetRb == null || targetRb == rb)
            {
                continue;
            }

            Vector2 direction = ((Vector2)targetRb.position - (Vector2)transform.position).normalized;
            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Random.insideUnitCircle.normalized;
            }

            targetRb.position += direction * knockbackDistance;
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
    }
}