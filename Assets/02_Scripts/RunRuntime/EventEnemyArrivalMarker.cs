using UnityEngine;

[DisallowMultipleComponent]
public class EventEnemyArrivalMarker : MonoBehaviour
{
    private const int RingSegments = 48;

    [Header("Runtime Line Renderers")]
    [SerializeField] private LineRenderer ringRenderer;
    [SerializeField] private LineRenderer arrowShaftRenderer;
    [SerializeField] private LineRenderer arrowWingLeftRenderer;
    [SerializeField] private LineRenderer arrowWingRightRenderer;

    [Header("Shape")]
    [SerializeField] private float ringWidth = 0.045f;
    [SerializeField] private float arrowWidth = 0.045f;

    private static Material sharedLineMaterial;

    private Color color = new Color(1f, 0.05f, 0.03f, 0.9f);
    private float baseRadius = 0.85f;
    private float duration = 0.75f;
    private float timer;
    private Vector2 approachDirection = Vector2.zero;
    private bool expandInsteadOfContract;

    public static EventEnemyArrivalMarker Spawn(
        Vector2 position,
        Color color,
        float radius,
        float duration,
        Vector2 approachDirection)
    {
        GameObject markerObject = new GameObject("EventEnemyArrivalMarker");
        markerObject.transform.position = position;

        EventEnemyArrivalMarker marker = markerObject.AddComponent<EventEnemyArrivalMarker>();
        marker.Arm(color, radius, duration, approachDirection, false);
        return marker;
    }

    public static EventEnemyArrivalMarker SpawnImpact(
        Vector2 position,
        Color color,
        float radius,
        float duration)
    {
        GameObject markerObject = new GameObject("EventEnemyLandingBurst");
        markerObject.transform.position = position;

        EventEnemyArrivalMarker marker = markerObject.AddComponent<EventEnemyArrivalMarker>();
        marker.Arm(color, radius, duration, Vector2.zero, true);
        return marker;
    }

    public void Arm(
        Color markerColor,
        float radius,
        float lifeTime,
        Vector2 incomingDirection,
        bool expand)
    {
        color = markerColor;
        baseRadius = Mathf.Max(0.05f, radius);
        duration = Mathf.Max(0.05f, lifeTime);
        approachDirection = incomingDirection.sqrMagnitude > 0.001f
            ? incomingDirection.normalized
            : Vector2.zero;
        expandInsteadOfContract = expand;
        timer = 0f;

        EnsureRenderers();
        UpdateVisual(0f);
    }

    private void Awake()
    {
        EnsureRenderers();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        UpdateVisual(t);

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureRenderers()
    {
        if (ringRenderer == null)
        {
            ringRenderer = CreateLineRenderer("Ring", true, ringWidth);
        }

        if (arrowShaftRenderer == null)
        {
            arrowShaftRenderer = CreateLineRenderer("IncomingArrow_Shaft", false, arrowWidth);
        }

        if (arrowWingLeftRenderer == null)
        {
            arrowWingLeftRenderer = CreateLineRenderer("IncomingArrow_Left", false, arrowWidth);
        }

        if (arrowWingRightRenderer == null)
        {
            arrowWingRightRenderer = CreateLineRenderer("IncomingArrow_Right", false, arrowWidth);
        }
    }

    private LineRenderer CreateLineRenderer(string childName, bool loop, float width)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);

        LineRenderer line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = loop ? RingSegments : 2;
        line.widthMultiplier = Mathf.Max(0.005f, width);
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sortingOrder = 80;
        line.material = GetLineMaterial();
        return line;
    }

    private static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        sharedLineMaterial = shader != null
            ? new Material(shader)
            : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

        return sharedLineMaterial;
    }

    private void UpdateVisual(float t)
    {
        EnsureRenderers();

        float radius;
        float alpha;

        if (expandInsteadOfContract)
        {
            radius = Mathf.Lerp(baseRadius * 0.35f, baseRadius * 1.65f, EaseOutQuad(t));
            alpha = Mathf.Lerp(color.a, 0f, t);
        }
        else
        {
            radius = Mathf.Lerp(baseRadius * 1.75f, baseRadius * 0.65f, EaseOutQuad(t));
            float blink = 0.65f + Mathf.Sin(Time.time * 28f) * 0.2f;
            alpha = Mathf.Clamp01(Mathf.Lerp(color.a * 0.55f, color.a, t) * blink);
        }

        Color finalColor = color;
        finalColor.a = alpha;

        DrawRing(radius, finalColor);
        DrawApproachArrow(radius, finalColor);
    }

    private void DrawRing(float radius, Color finalColor)
    {
        if (ringRenderer == null)
        {
            return;
        }

        ringRenderer.startColor = finalColor;
        ringRenderer.endColor = finalColor;
        ringRenderer.widthMultiplier = ringWidth;
        ringRenderer.positionCount = RingSegments;

        Vector3 center = transform.position;

        for (int i = 0; i < RingSegments; i++)
        {
            float angle = (i / (float)RingSegments) * Mathf.PI * 2f;
            Vector3 position = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            ringRenderer.SetPosition(i, position);
        }
    }

    private void DrawApproachArrow(float radius, Color finalColor)
    {
        bool showArrow = !expandInsteadOfContract && approachDirection.sqrMagnitude > 0.001f;

        SetLineEnabled(arrowShaftRenderer, showArrow);
        SetLineEnabled(arrowWingLeftRenderer, showArrow);
        SetLineEnabled(arrowWingRightRenderer, showArrow);

        if (!showArrow)
        {
            return;
        }

        Vector2 forward = approachDirection.normalized;
        Vector2 perpendicular = new Vector2(-forward.y, forward.x);
        Vector3 center = transform.position;

        Vector3 tip = center - (Vector3)(forward * radius * 1.05f);
        Vector3 tail = center - (Vector3)(forward * radius * 2.35f);
        Vector3 wingBase = tip - (Vector3)(forward * radius * 0.42f);
        Vector3 leftWing = wingBase + (Vector3)(perpendicular * radius * 0.28f);
        Vector3 rightWing = wingBase - (Vector3)(perpendicular * radius * 0.28f);

        SetLine(arrowShaftRenderer, tail, tip, finalColor);
        SetLine(arrowWingLeftRenderer, leftWing, tip, finalColor);
        SetLine(arrowWingRightRenderer, rightWing, tip, finalColor);
    }

    private void SetLine(LineRenderer line, Vector3 start, Vector3 end, Color finalColor)
    {
        if (line == null)
        {
            return;
        }

        line.startColor = finalColor;
        line.endColor = finalColor;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void SetLineEnabled(LineRenderer line, bool value)
    {
        if (line != null)
        {
            line.enabled = value;
        }
    }

    private float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
