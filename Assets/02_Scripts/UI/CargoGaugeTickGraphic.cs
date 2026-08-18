using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CargoGaugeTickGraphic : MaskableGraphic
{
    [SerializeField, Min(1)] private int interval = 25;
    [SerializeField, Min(0.5f)] private float tickWidth = 1f;
    [SerializeField, Range(0.1f, 1f)] private float tickHeightRatio = 0.72f;

    private int capacity;

    public void Configure(int newCapacity, int newInterval, Color tickColor)
    {
        newCapacity = Mathf.Max(0, newCapacity);
        newInterval = Mathf.Max(1, newInterval);
        if (capacity == newCapacity && interval == newInterval && color == tickColor)
        {
            return;
        }

        capacity = newCapacity;
        interval = newInterval;
        color = tickColor;
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (capacity <= interval)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        float height = Mathf.Max(1f, rect.height * tickHeightRatio);
        float yMin = rect.center.y - height * 0.5f;
        for (int value = interval; value < capacity; value += interval)
        {
            float x = Mathf.Lerp(rect.xMin, rect.xMax, value / (float)capacity);
            AppendQuad(vertexHelper, new Rect(x - tickWidth * 0.5f, yMin, tickWidth, height));
        }
    }

    private void AppendQuad(VertexHelper vertexHelper, Rect rect)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        int start = vertexHelper.currentVertCount;

        vertex.position = new Vector3(rect.xMin, rect.yMin);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMin, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMin);
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start + 2, start + 3, start);
    }
}
