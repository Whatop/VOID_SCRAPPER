using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RaiderBarricadeCarrier : MonoBehaviour
{
    public enum ArenaSide
    {
        North = 0,
        South = 1,
        East = 2,
        West = 3
    }

    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Color deploymentFlashColor = new Color(0.78f, 0.95f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float deploymentPulseDuration = 0.32f;

    private Vector3 authoredScale = Vector3.one;
    private Quaternion authoredRotation = Quaternion.identity;
    private Color authoredColor = Color.white;
    private Vector3 anchorPosition;
    private ArenaSide arenaSide;

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredPresentation();
    }

    private void OnDisable()
    {
        transform.DOKill();
        RestorePresentation();
    }

    public void BeginArrival(
        Vector3 startPosition,
        Vector3 destination,
        ArenaSide side,
        float duration,
        AnimationCurve movementCurve)
    {
        ResolveReferences();
        anchorPosition = destination;
        arenaSide = side;
        transform.position = startPosition;
        ApplyOutwardFacing();
        transform.DOKill();
        transform.DOMove(destination, Mathf.Max(0.01f, duration))
            .SetEase(movementCurve)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    public void CompleteArrival()
    {
        transform.DOKill();
        transform.position = anchorPosition;
        ApplyOutwardFacing();
    }

    public void PlayBarrierDeployment()
    {
        RestorePresentation();
        ApplyOutwardFacing();

        if (visualRoot != null)
        {
            visualRoot.DOPunchScale(
                    Vector3.one * 0.12f,
                    deploymentPulseDuration,
                    5,
                    0.35f
                )
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.DOColor(deploymentFlashColor, deploymentPulseDuration * 0.5f)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    public void Retire()
    {
        transform.DOKill();
        if (visualRoot != null)
        {
            visualRoot.DOKill();
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.DOKill();
        }

        Destroy(gameObject);
    }

    private void ApplyOutwardFacing()
    {
        if (visualRoot == null)
        {
            return;
        }

        // Pirate_Elite_Candidates_64x64_0 is authored facing right (+X).
        // Carriers point away from the contained arena on their cardinal side.
        float angle = arenaSide switch
        {
            ArenaSide.North => 90f,
            ArenaSide.South => -90f,
            ArenaSide.East => 0f,
            ArenaSide.West => 180f,
            _ => 0f
        };
        visualRoot.localRotation = authoredRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void ResolveReferences()
    {
        visualRoot ??= transform.Find("VisualRoot");
        bodyRenderer ??= visualRoot != null
            ? visualRoot.GetComponentInChildren<SpriteRenderer>(true)
            : GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CaptureAuthoredPresentation()
    {
        authoredScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        authoredRotation = visualRoot != null ? visualRoot.localRotation : Quaternion.identity;
        authoredColor = bodyRenderer != null ? bodyRenderer.color : Color.white;
    }

    private void RestorePresentation()
    {
        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = authoredScale;
            visualRoot.localRotation = authoredRotation;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.DOKill();
            bodyRenderer.color = authoredColor;
        }
    }
}
