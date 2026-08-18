using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerReturnMarker : MonoBehaviour, IPlayerOwnedAlly
{
    [Header("Visual")]
    [SerializeField] private Color markerColor = new Color(0.48f, 1f, 1f, 1f);
    [Min(0.01f)]
    [SerializeField] private float spawnDuration = 0.18f;
    [Min(0.05f)]
    [SerializeField] private float pulseDuration = 0.7f;
    [Min(8)]
    [SerializeField] private int ringSegments = 32;
    [SerializeField] private int sortingOrder = 24;

    private static Material sharedLineMaterial;

    private GameObject playerOwner;
    private Transform visualRoot;
    private Transform pulseRoot;
    private LineRenderer coreLine;
    private LineRenderer ringLine;
    private Tween spawnTween;
    private Sequence pulseTween;
    private bool configured;

    public bool IsPlayerOwnedAlly => configured && playerOwner != null;
    public Vector2 ReturnPosition => transform.position;

    private void Awake()
    {
        EnsureVisuals();
    }

    private void OnEnable()
    {
        configured = false;
        playerOwner = null;
        EnsureVisuals();
        StopTweens();
        ResetVisuals();
    }

    private void OnDisable()
    {
        configured = false;
        playerOwner = null;
        StopTweens();
        ResetVisuals();
    }

    public bool Configure(GameObject owner)
    {
        if (owner == null)
        {
            return false;
        }

        playerOwner = owner;
        configured = true;
        PlayPlacementFeedback();
        return true;
    }

    public void PrepareForRelease()
    {
        configured = false;
        playerOwner = null;
        StopTweens();
        ResetVisuals();
    }

    private void EnsureVisuals()
    {
        if (visualRoot == null)
        {
            Transform existing = transform.Find("VisualRoot");
            if (existing == null)
            {
                GameObject visualObject = new GameObject("VisualRoot");
                existing = visualObject.transform;
                existing.SetParent(transform, false);
            }

            visualRoot = existing;
        }

        if (coreLine == null)
        {
            coreLine = EnsureLine("Core", false, sortingOrder + 1);
            Vector3[] points =
            {
                new Vector3(0f, 0.25f, 0f),
                new Vector3(0.2f, 0f, 0f),
                new Vector3(0f, -0.25f, 0f),
                new Vector3(-0.2f, 0f, 0f),
                new Vector3(0f, 0.25f, 0f)
            };
            coreLine.positionCount = points.Length;
            coreLine.SetPositions(points);
            coreLine.startWidth = 0.055f;
            coreLine.endWidth = 0.055f;
        }

        if (ringLine == null)
        {
            ringLine = EnsureLine("Ring", true, sortingOrder);
            pulseRoot = ringLine.transform;
            int segments = Mathf.Clamp(ringSegments, 8, 96);
            ringLine.positionCount = segments;

            for (int i = 0; i < segments; i++)
            {
                float radians = i * Mathf.PI * 2f / segments;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * 0.42f);
            }

            ringLine.startWidth = 0.035f;
            ringLine.endWidth = 0.035f;
        }

        ApplyColors();
    }

    private LineRenderer EnsureLine(string childName, bool loop, int order)
    {
        Transform child = visualRoot.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(visualRoot, false);
        }

        LineRenderer line = child.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = child.gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.loop = loop;
        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.sortingLayerName = "Default";
        line.sortingOrder = order;
        line.sharedMaterial = GetSharedLineMaterial();
        return line;
    }

    private void PlayPlacementFeedback()
    {
        StopTweens();
        ResetVisuals();

        visualRoot.localScale = Vector3.one * 0.45f;
        spawnTween = visualRoot
            .DOScale(Vector3.one, Mathf.Max(0.01f, spawnDuration))
            .SetEase(Ease.OutBack)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        pulseTween = DOTween.Sequence()
            .Append(pulseRoot.DOScale(Vector3.one * 1.13f, Mathf.Max(0.05f, pulseDuration)))
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void StopTweens()
    {
        spawnTween?.Kill();
        pulseTween?.Kill();
        spawnTween = null;
        pulseTween = null;
    }

    private void ResetVisuals()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one;
        }

        if (pulseRoot != null)
        {
            pulseRoot.localScale = Vector3.one;
        }

        ApplyColors();
    }

    private void ApplyColors()
    {
        if (coreLine != null)
        {
            coreLine.startColor = markerColor;
            coreLine.endColor = markerColor;
        }

        if (ringLine != null)
        {
            Color ringColor = markerColor;
            ringColor.a = 0.58f;
            ringLine.startColor = ringColor;
            ringLine.endColor = ringColor;
        }
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            sharedLineMaterial = new Material(shader)
            {
                name = "Runtime Player Return Marker Line",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return sharedLineMaterial;
    }
}
