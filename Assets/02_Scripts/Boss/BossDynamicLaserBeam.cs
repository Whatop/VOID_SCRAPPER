using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossDynamicLaserBeam : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LineRenderer lineRenderer;

    private readonly Dictionary<int, float> lastDamageTimes = new Dictionary<int, float>();

    private Transform startTarget;
    private Transform endTarget;
    private Material lineMaterial;
    private Color lineColor;
    private string sortingLayerName;
    private int sortingOrder;

    private float width;
    private float damage;
    private float damageInterval;
    private bool initialized;
    private bool damageEnabled;
    private Coroutine lifetimeRoutine;
    private Coroutine recoveryRoutine;

    private void Awake()
    {
        EnsureComponents();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (startTarget == null || endTarget == null)
        {
            Deactivate();
            return;
        }

        UpdateGeometry(startTarget.position, endTarget.position);
    }

    private void OnDisable()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (recoveryRoutine != null)
        {
            StopCoroutine(recoveryRoutine);
            recoveryRoutine = null;
        }

        lastDamageTimes.Clear();
        initialized = false;
        damageEnabled = false;
        startTarget = null;
        endTarget = null;

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    public void Initialize(
        Transform startAnchor,
        Transform endAnchor,
        float beamWidth,
        float duration,
        float damageAmount,
        float damageCooldown,
        Material beamMaterial,
        Color beamColor,
        string beamSortingLayerName,
        int beamSortingOrder)
    {
        EnsureComponents();

        startTarget = startAnchor;
        endTarget = endAnchor;
        width = Mathf.Max(0.01f, beamWidth);
        damage = Mathf.Max(0f, damageAmount);
        damageInterval = Mathf.Max(0.01f, damageCooldown);
        lineMaterial = beamMaterial;
        lineColor = beamColor;
        sortingLayerName = beamSortingLayerName;
        sortingOrder = beamSortingOrder;

        if (startTarget == null || endTarget == null)
        {
            Deactivate();
            return;
        }

        ConfigureRigidbody();
        ConfigureLineRenderer();
        damageEnabled = true;
        UpdateGeometry(startTarget.position, endTarget.position);

        lastDamageTimes.Clear();
        initialized = true;

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }

        if (recoveryRoutine != null)
        {
            StopCoroutine(recoveryRoutine);
            recoveryRoutine = null;
        }

        lifetimeRoutine = StartCoroutine(LifetimeRoutine(duration));
    }

    public void Deactivate()
    {
        initialized = false;
        damageEnabled = false;

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (recoveryRoutine != null)
        {
            StopCoroutine(recoveryRoutine);
            recoveryRoutine = null;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void BeginRecovery(float duration, float widthMultiplier)
    {
        initialized = false;
        damageEnabled = false;

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (lineRenderer == null)
        {
            return;
        }

        if (recoveryRoutine != null)
        {
            StopCoroutine(recoveryRoutine);
        }

        recoveryRoutine = StartCoroutine(RecoveryRoutine(
            Mathf.Max(0.01f, duration),
            Mathf.Clamp(widthMultiplier, 0.05f, 1f)
        ));
    }

    private IEnumerator RecoveryRoutine(float duration, float widthMultiplier)
    {
        float startWidth = lineRenderer.startWidth;
        Color startColor = lineRenderer.startColor;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float recoveryWidth = Mathf.Lerp(startWidth, startWidth * widthMultiplier, t);
            Color recoveryColor = startColor;
            recoveryColor.a = Mathf.Lerp(startColor.a, 0f, t);
            lineRenderer.startWidth = recoveryWidth;
            lineRenderer.endWidth = recoveryWidth;
            lineRenderer.startColor = recoveryColor;
            lineRenderer.endColor = recoveryColor;
            yield return null;
        }

        recoveryRoutine = null;
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
        lifetimeRoutine = null;
        Deactivate();
    }

    private void ConfigureRigidbody()
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = true;
    }

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        else if (lineRenderer.material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
            }
        }
    }

    private void UpdateGeometry(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            delta = Vector2.right;
            length = 1f;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));

        if (boxCollider != null)
        {
            boxCollider.enabled = damageEnabled;
            boxCollider.isTrigger = true;
            boxCollider.offset = Vector2.zero;
            boxCollider.size = new Vector2(length, width);
        }

        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
            lineRenderer.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        if (!initialized || !damageEnabled || other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        int id = playerHealth.GetInstanceID();

        if (lastDamageTimes.TryGetValue(id, out float lastTime))
        {
            if (Time.time - lastTime < damageInterval)
            {
                return;
            }
        }

        lastDamageTimes[id] = Time.time;
        playerHealth.TakeDamage(damage);
    }

    private void EnsureComponents()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
    }
}
