using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BossArenaLaserWall : MonoBehaviour
{
    private static Material fallbackLineMaterial;

    [Header("Runtime References")]
    [SerializeField] private BoxCollider2D boxCollider;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Fallback")]
    [SerializeField] private float fallbackLength = 28f;
    [SerializeField] private float fallbackThickness = 0.35f;
    [SerializeField] private float fallbackDamage = 4f;
    [SerializeField] private float fallbackDamageInterval = 0.5f;

    private readonly Dictionary<int, float> lastDamageTimes = new Dictionary<int, float>();

    private Transform followStart;
    private Transform followEnd;
    private bool followTargets;
    private bool followSolidBlock;

    private float damage;
    private float damageInterval;
    private float currentThickness;
    private Material currentLineMaterial;
    private Color currentLineColor;
    private string currentSortingLayerName;
    private int currentSortingOrder;
    private string currentLayerName;
    private bool initialized;
    private bool allowHorizontalProjectileRicochet;
    private bool presentationVisible = true;

    public bool AllowsHorizontalProjectileRicochet =>
        initialized && allowHorizontalProjectileRicochet;
    public bool AllowsProjectileRicochet => AllowsHorizontalProjectileRicochet;

    private void Awake()
    {
        EnsureComponents();
    }

    private void LateUpdate()
    {
        if (!initialized || !followTargets)
        {
            return;
        }

        if (followStart == null || followEnd == null)
        {
            Deactivate();
            return;
        }

        ApplyBetween(
            followStart.position,
            followEnd.position,
            currentThickness,
            followSolidBlock,
            currentLineMaterial,
            currentLineColor,
            currentSortingLayerName,
            currentSortingOrder,
            currentLayerName,
            false,
            true
        );
    }

    private void OnDisable()
    {
        lastDamageTimes.Clear();
        initialized = false;
        followTargets = false;
        followStart = null;
        followEnd = null;
        allowHorizontalProjectileRicochet = false;
    }

    public void InitializeBetween(
        Vector2 start,
        Vector2 end,
        float thickness,
        bool solidBlock,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName)
    {
        followTargets = false;
        followStart = null;
        followEnd = null;
        allowHorizontalProjectileRicochet = false;

        StoreDamage(damageAmount, damageCooldown);
        StoreVisualSettings(thickness, lineMaterial, lineColor, sortingLayerName, sortingOrder, layerName, solidBlock);

        ApplyBetween(
            start,
            end,
            currentThickness,
            solidBlock,
            currentLineMaterial,
            currentLineColor,
            currentSortingLayerName,
            currentSortingOrder,
            currentLayerName,
            true,
            false
        );
    }

    public void InitializeFollowBetween(
        Transform startTarget,
        Transform endTarget,
        float thickness,
        bool solidBlock,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName)
    {
        followStart = startTarget;
        followEnd = endTarget;
        followTargets = startTarget != null && endTarget != null;
        allowHorizontalProjectileRicochet = false;

        StoreDamage(damageAmount, damageCooldown);
        StoreVisualSettings(thickness, lineMaterial, lineColor, sortingLayerName, sortingOrder, layerName, solidBlock);

        if (!followTargets)
        {
            Deactivate();
            return;
        }

        ApplyBetween(
            followStart.position,
            followEnd.position,
            currentThickness,
            solidBlock,
            currentLineMaterial,
            currentLineColor,
            currentSortingLayerName,
            currentSortingOrder,
            currentLayerName,
            true,
            true
        );
    }

    public void InitializeMovableBetween(
        Vector2 start,
        Vector2 end,
        float thickness,
        bool solidBlock,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName)
    {
        followTargets = false;
        followStart = null;
        followEnd = null;
        allowHorizontalProjectileRicochet = false;

        StoreDamage(damageAmount, damageCooldown);
        StoreVisualSettings(
            thickness,
            lineMaterial,
            lineColor,
            sortingLayerName,
            sortingOrder,
            layerName,
            solidBlock
        );

        ApplyBetween(
            start,
            end,
            currentThickness,
            solidBlock,
            currentLineMaterial,
            currentLineColor,
            currentSortingLayerName,
            currentSortingOrder,
            currentLayerName,
            !initialized,
            true
        );
    }

    public void Initialize(
        Vector2 center,
        Vector2 direction,
        float length,
        float thickness,
        bool solidBlock,
        float damageAmount,
        float damageCooldown,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName)
    {
        followTargets = false;
        followStart = null;
        followEnd = null;
        allowHorizontalProjectileRicochet = false;

        StoreDamage(damageAmount, damageCooldown);
        StoreVisualSettings(thickness, lineMaterial, lineColor, sortingLayerName, sortingOrder, layerName, solidBlock);

        ApplyGeometry(
            center,
            direction,
            length,
            currentThickness,
            solidBlock,
            currentLineMaterial,
            currentLineColor,
            currentSortingLayerName,
            currentSortingOrder,
            currentLayerName,
            true,
            false
        );
    }

    public void Deactivate()
    {
        Destroy(gameObject);
    }

    public LineRenderer GetOrCreateLineRenderer()
    {
        EnsureComponents();
        return lineRenderer;
    }

    public void ConfigureHorizontalProjectileRicochet(bool allowRicochet)
    {
        ConfigureProjectileRicochet(allowRicochet);
    }

    public void ConfigureProjectileRicochet(bool allowRicochet)
    {
        allowHorizontalProjectileRicochet = allowRicochet;
    }

    public void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        EnsureComponents();

        if (lineRenderer != null)
        {
            lineRenderer.enabled = initialized && presentationVisible;
        }
    }

    public bool TryReflectHorizontalProjectile(
        Vector2 projectilePosition,
        Vector2 incomingDirection,
        out Vector2 reflectedDirection)
    {
        return TryReflectProjectile(
            projectilePosition,
            incomingDirection,
            out reflectedDirection
        );
    }

    public bool TryReflectProjectile(
        Vector2 projectilePosition,
        Vector2 incomingDirection,
        out Vector2 reflectedDirection)
    {
        reflectedDirection = incomingDirection;

        if (!AllowsProjectileRicochet ||
            boxCollider == null ||
            !boxCollider.enabled ||
            incomingDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector2 wallNormal = transform.up;
        Vector2 fromWallCenter = projectilePosition - (Vector2)boxCollider.bounds.center;
        if (Vector2.Dot(fromWallCenter, wallNormal) < 0f)
        {
            wallNormal = -wallNormal;
        }

        reflectedDirection = Vector2.Reflect(
            incomingDirection.normalized,
            wallNormal.normalized
        ).normalized;
        return reflectedDirection.sqrMagnitude > 0.001f;
    }

    private void StoreDamage(float damageAmount, float damageCooldown)
    {
        damage = damageAmount >= 0f ? damageAmount : fallbackDamage;
        damageInterval = damageCooldown > 0f ? damageCooldown : fallbackDamageInterval;
    }

    private void StoreVisualSettings(
        float thickness,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName,
        bool solidBlock)
    {
        currentThickness = Mathf.Max(0.01f, thickness > 0f ? thickness : fallbackThickness);
        currentLineMaterial = lineMaterial;
        currentLineColor = lineColor;
        currentSortingLayerName = sortingLayerName;
        currentSortingOrder = sortingOrder;
        currentLayerName = layerName;
        followSolidBlock = solidBlock;
    }

    private void ApplyBetween(
        Vector2 start,
        Vector2 end,
        float thickness,
        bool solidBlock,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName,
        bool clearDamageTimes,
        bool dynamicBody)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;

        if (length <= 0.001f)
        {
            delta = Vector2.right;
            length = fallbackLength;
            end = start + delta * length;
        }

        Vector2 center = (start + end) * 0.5f;
        Vector2 direction = delta.normalized;

        ApplyGeometry(
            center,
            direction,
            length,
            thickness,
            solidBlock,
            lineMaterial,
            lineColor,
            sortingLayerName,
            sortingOrder,
            layerName,
            clearDamageTimes,
            dynamicBody
        );
    }

    private void ApplyGeometry(
        Vector2 center,
        Vector2 direction,
        float length,
        float thickness,
        bool solidBlock,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder,
        string layerName,
        bool clearDamageTimes,
        bool dynamicBody)
    {
        EnsureComponents();

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        length = Mathf.Max(0.1f, length);
        thickness = Mathf.Max(0.01f, thickness);

        ApplyLayer(layerName);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));

        ConfigureRigidbody(dynamicBody);
        ConfigureCollider(length, thickness, solidBlock);
        ConfigureLineRenderer(length, thickness, lineMaterial, lineColor, sortingLayerName, sortingOrder);

        if (clearDamageTimes)
        {
            lastDamageTimes.Clear();
        }

        initialized = true;
    }

    private void ConfigureRigidbody(bool dynamicBody)
    {
        if (rb == null)
        {
            return;
        }

        rb.bodyType = dynamicBody ? RigidbodyType2D.Kinematic : RigidbodyType2D.Static;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = true;
    }

    private void ConfigureCollider(float length, float thickness, bool solidBlock)
    {
        if (boxCollider == null)
        {
            return;
        }

        boxCollider.enabled = true;
        boxCollider.isTrigger = !solidBlock;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = new Vector2(length, thickness);
    }

    private void ConfigureLineRenderer(
        float length,
        float thickness,
        Material lineMaterial,
        Color lineColor,
        string sortingLayerName,
        int sortingOrder)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = presentationVisible;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, new Vector3(-length * 0.5f, 0f, 0f));
        lineRenderer.SetPosition(1, new Vector3(length * 0.5f, 0f, 0f));
        lineRenderer.startWidth = thickness;
        lineRenderer.endWidth = thickness;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.sharedMaterial = lineMaterial;
        }
        else if (lineRenderer.sharedMaterial == null)
        {
            lineRenderer.sharedMaterial = GetOrCreateFallbackLineMaterial();
        }
    }

    private static Material GetOrCreateFallbackLineMaterial()
    {
        if (fallbackLineMaterial != null)
        {
            return fallbackLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        fallbackLineMaterial = new Material(shader)
        {
            name = "Runtime_BossArenaLaserWall_Fallback",
            hideFlags = HideFlags.HideAndDontSave
        };
        return fallbackLineMaterial;
    }

    private void ApplyLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);

        if (layer >= 0)
        {
            gameObject.layer = layer;
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryDamage(collision.collider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null)
        {
            TryDamage(collision.collider);
        }
    }

    private void TryDamage(Collider2D other)
    {
        if (!initialized || damage <= 0f || other == null)
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
