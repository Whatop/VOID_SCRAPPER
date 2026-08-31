using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossLaserHazard : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LineRenderer lineRenderer;

    private readonly Dictionary<int, float> lastDamageTimes = new Dictionary<int, float>();

    private float damage;
    private float damageInterval;
    private bool initialized;
    private bool damageEnabled;
    private Coroutine lifetimeRoutine;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnDisable()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        lastDamageTimes.Clear();
        initialized = false;
        damageEnabled = false;

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    public void InitializeBetween(
        Vector2 start,
        Vector2 end,
        float width,
        float duration,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        float recoveryDuration = 0f,
        int lineCapVertices = 0)
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

        Initialize(
            center,
            direction,
            length,
            width,
            duration,
            damageAmount,
            damageCooldown,
            lineMaterial,
            lineColor,
            sortingLayerName,
            sortingOrder,
            recoveryDuration,
            lineCapVertices
        );
    }

    public void Initialize(
        Vector2 center,
        Vector2 direction,
        float length,
        float width,
        float duration,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        float recoveryDuration = 0f,
        int lineCapVertices = 0)
    {
        EnsureComponents();

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        length = Mathf.Max(0.1f, length);
        width = Mathf.Max(0.01f, width);

        damage = Mathf.Max(0f, damageAmount);
        damageInterval = Mathf.Max(0.01f, damageCooldown);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));

        ConfigureRigidbody();
        ConfigureLineRenderer(
            length,
            width,
            lineMaterial,
            lineColor,
            sortingLayerName,
            sortingOrder,
            lineCapVertices
        );

        lastDamageTimes.Clear();
        damageEnabled = true;
        ConfigureCollider(length, width);
        initialized = true;

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }

        lifetimeRoutine = StartCoroutine(LifetimeRoutine(duration, recoveryDuration));
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

    private void ConfigureCollider(float length, float width)
    {
        if (boxCollider == null)
        {
            return;
        }

        boxCollider.enabled = true;
        boxCollider.isTrigger = true;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = new Vector2(length, width);
    }

    private void ConfigureLineRenderer(
        float length,
        float width,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        int lineCapVertices)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.numCapVertices = Mathf.Clamp(lineCapVertices, 0, 8);
        lineRenderer.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
        lineRenderer.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
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

    public void SetRuntimeWidth(float width)
    {
        if (!initialized)
        {
            return;
        }

        float safeWidth = Mathf.Max(0.01f, width);
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = safeWidth;
            lineRenderer.endWidth = safeWidth;
        }

        if (boxCollider != null)
        {
            Vector2 size = boxCollider.size;
            size.y = safeWidth;
            boxCollider.size = size;
        }
    }

    private IEnumerator LifetimeRoutine(float duration, float recoveryDuration)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, duration));

        damageEnabled = false;
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        float recovery = Mathf.Max(0f, recoveryDuration);
        if (recovery > 0f && lineRenderer != null && lineRenderer.enabled)
        {
            float startWidth = lineRenderer.startWidth;
            Color startColor = lineRenderer.startColor;
            float timer = 0f;

            while (timer < recovery)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / recovery);
                float width = Mathf.Lerp(startWidth, startWidth * 0.2f, t);
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                yield return null;
            }
        }

        lifetimeRoutine = null;
        ReleaseOrDestroy();
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

    public void Deactivate()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        initialized = false;
        damageEnabled = false;

        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        ReleaseOrDestroy();
    }

    private void ReleaseOrDestroy()
    {
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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
