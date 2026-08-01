using System;
using UnityEngine;

/// <summary>
/// 기지 외벽 파츠를 부표처럼 좌우로 살짝 흔들기 위한 연출용 스크립트.
/// 판정 Root가 아닌 벽 VisualRoot 또는 벽 파츠 Transform 배열에 붙이는 것을 권장.
/// </summary>
[DisallowMultipleComponent]
public class FieldBaseWallChainMotion2D : MonoBehaviour
{
    [Serializable]
    private struct SegmentState
    {
        public Transform Target;
        public Vector3 BaseLocalPosition;
        public bool Captured;
    }

    [Header("Target")]
    [SerializeField] private Transform[] wallSegments;
    [SerializeField] private bool includeDirectChildrenWhenEmpty = true;

    [Header("Motion")]
    [SerializeField] private Vector2 localMoveAxis = Vector2.right;
    [Min(0f)]
    [SerializeField] private float amplitude = 0.06f;
    [Min(0.05f)]
    [SerializeField] private float duration = 3.25f;
    [SerializeField] private float phaseOffsetPerSegment = 0.42f;
    [SerializeField] private bool useUnscaledTime;

    private SegmentState[] segmentStates = Array.Empty<SegmentState>();

    private void Awake()
    {
        ResolveSegments();
        CaptureBasePositions();
    }

    private void OnEnable()
    {
        ResolveSegments();
        CaptureBasePositions();
    }

    private void LateUpdate()
    {
        if (segmentStates == null || segmentStates.Length == 0 || amplitude <= 0f)
        {
            return;
        }

        Vector2 axis = localMoveAxis.sqrMagnitude > 0.001f
            ? localMoveAxis.normalized
            : Vector2.right;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float frequency = Mathf.PI * 2f / Mathf.Max(0.05f, duration);

        for (int i = 0; i < segmentStates.Length; i++)
        {
            if (!segmentStates[i].Captured || segmentStates[i].Target == null)
            {
                continue;
            }

            float phase = phaseOffsetPerSegment * i;
            float offset = Mathf.Sin(time * frequency + phase) * amplitude;
            segmentStates[i].Target.localPosition = segmentStates[i].BaseLocalPosition + (Vector3)(axis * offset);
        }
    }

    private void OnDisable()
    {
        RestoreBasePositions();
    }

    public void Recapture()
    {
        ResolveSegments();
        CaptureBasePositions();
    }

    private void ResolveSegments()
    {
        if (wallSegments != null && wallSegments.Length > 0)
        {
            segmentStates = new SegmentState[wallSegments.Length];

            for (int i = 0; i < wallSegments.Length; i++)
            {
                segmentStates[i].Target = wallSegments[i];
            }

            return;
        }

        if (!includeDirectChildrenWhenEmpty)
        {
            segmentStates = Array.Empty<SegmentState>();
            return;
        }

        int childCount = transform.childCount;
        segmentStates = new SegmentState[childCount];

        for (int i = 0; i < childCount; i++)
        {
            segmentStates[i].Target = transform.GetChild(i);
        }
    }

    private void CaptureBasePositions()
    {
        if (segmentStates == null)
        {
            return;
        }

        for (int i = 0; i < segmentStates.Length; i++)
        {
            if (segmentStates[i].Target == null)
            {
                segmentStates[i].Captured = false;
                continue;
            }

            segmentStates[i].BaseLocalPosition = segmentStates[i].Target.localPosition;
            segmentStates[i].Captured = true;
        }
    }

    private void RestoreBasePositions()
    {
        if (segmentStates == null)
        {
            return;
        }

        for (int i = 0; i < segmentStates.Length; i++)
        {
            if (!segmentStates[i].Captured || segmentStates[i].Target == null)
            {
                continue;
            }

            segmentStates[i].Target.localPosition = segmentStates[i].BaseLocalPosition;
        }
    }
}
