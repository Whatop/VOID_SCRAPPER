using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PhaseReflectorPlate : MonoBehaviour
{
    [Header("Geometry")]
    [SerializeField, Min(0.5f)] private float plateLength = 4f;
    [SerializeField, Min(0.05f)] private float colliderThickness = 0.35f;

    [Header("Presentation")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = new Color(0.35f, 0.85f, 1f, 0.95f);
    [SerializeField, Min(0.02f)] private float lineWidth = 0.16f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 12;

    [Header("Laser Hit Feedback")]
    [SerializeField] private Color hitPulseColor = new Color(0.85f, 0.45f, 1f, 1f);
    [SerializeField, Min(0.05f)] private float hitPulseDuration = 0.18f;
    [SerializeField, Min(1f)] private float hitPulseWidthMultiplier = 2.25f;

    private BoxCollider2D plateCollider;
    private Rigidbody2D plateRigidbody;
    private LineRenderer lineRenderer;
    private Coroutine hitPulseRoutine;

    public Vector2 SurfaceNormal => transform.up;
    public float PlateLength => plateLength;

    private void Awake()
    {
        EnsureComponents();
        ApplyConfiguration();
    }

    private void OnValidate()
    {
        plateLength = Mathf.Max(0.5f, plateLength);
        colliderThickness = Mathf.Max(0.05f, colliderThickness);
        lineWidth = Mathf.Max(0.02f, lineWidth);

        if (!Application.isPlaying &&
            plateCollider != null &&
            plateRigidbody != null &&
            lineRenderer != null)
        {
            ApplyConfiguration();
        }
    }

    private void OnDisable()
    {
        if (hitPulseRoutine != null)
        {
            StopCoroutine(hitPulseRoutine);
            hitPulseRoutine = null;
        }

        RestoreLinePresentation();
    }

    public void Configure(Vector2 worldPosition, float rotationDegrees, float length)
    {
        transform.SetPositionAndRotation(
            worldPosition,
            Quaternion.Euler(0f, 0f, rotationDegrees)
        );
        plateLength = Mathf.Max(0.5f, length);
        EnsureComponents();
        ApplyConfiguration();
    }

    public bool OwnsCollider(Collider2D candidate)
    {
        return candidate != null && candidate == plateCollider;
    }

    public Vector2 Reflect(Vector2 incomingDirection)
    {
        if (incomingDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector2.down;
        }

        return Vector2.Reflect(incomingDirection.normalized, SurfaceNormal).normalized;
    }

    public void PlayLaserHitPulse()
    {
        if (!isActiveAndEnabled || lineRenderer == null)
        {
            return;
        }

        if (hitPulseRoutine != null)
        {
            StopCoroutine(hitPulseRoutine);
        }

        hitPulseRoutine = StartCoroutine(LaserHitPulseRoutine());
    }

    private IEnumerator LaserHitPulseRoutine()
    {
        float duration = Mathf.Max(0.05f, hitPulseDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            float pulse = 1f - Mathf.Abs(normalized * 2f - 1f);
            float width = Mathf.Lerp(
                lineWidth,
                lineWidth * Mathf.Max(1f, hitPulseWidthMultiplier),
                pulse
            );
            Color color = Color.Lerp(lineColor, hitPulseColor, pulse);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            yield return null;
        }

        RestoreLinePresentation();
        hitPulseRoutine = null;
    }

    private void RestoreLinePresentation()
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
    }

    private void EnsureComponents()
    {
        if (plateCollider == null)
        {
            plateCollider = GetComponent<BoxCollider2D>();
        }

        if (plateCollider == null)
        {
            plateCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        if (plateRigidbody == null)
        {
            plateRigidbody = GetComponent<Rigidbody2D>();
        }

        if (plateRigidbody == null)
        {
            plateRigidbody = gameObject.AddComponent<Rigidbody2D>();
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

    private void ApplyConfiguration()
    {
        if (plateRigidbody != null)
        {
            plateRigidbody.bodyType = RigidbodyType2D.Static;
            plateRigidbody.gravityScale = 0f;
            plateRigidbody.simulated = true;
        }

        if (plateCollider != null)
        {
            plateCollider.enabled = true;
            plateCollider.isTrigger = false;
            plateCollider.offset = Vector2.zero;
            plateCollider.size = new Vector2(plateLength, colliderThickness);
        }

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.enabled = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, new Vector3(-plateLength * 0.5f, 0f, 0f));
        lineRenderer.SetPosition(1, new Vector3(plateLength * 0.5f, 0f, 0f));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingLayerName = sortingLayerName;
        lineRenderer.sortingOrder = sortingOrder;

        if (lineMaterial != null)
        {
            lineRenderer.sharedMaterial = lineMaterial;
        }
    }

}
