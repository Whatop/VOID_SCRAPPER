using DG.Tweening;
using UnityEngine;

public enum BossArenaCoverSide
{
    Left = 0,
    Right = 1
}

[DisallowMultipleComponent]
public sealed class BossArenaCover : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private BossArenaCoverSide side;
    [SerializeField] private MeteorObstacle meteorObstacle;
    [SerializeField] private Collider2D coverCollider;
    [SerializeField] private Rigidbody2D rootBody;
    [SerializeField] private Transform visualRoot;

    [Header("Encounter Protection")]
    [SerializeField] private bool protectedDuringBossEncounter = true;

    [Header("Pattern Presentation")]
    [SerializeField] private Color highlightColor = new Color(0.55f, 0.95f, 1f, 1f);
    [SerializeField, Min(0.01f)] private float highlightPulseDuration = 0.28f;
    [SerializeField, Min(1f)] private float highlightScale = 1.08f;

    private SpriteRenderer[] renderers;
    private Color[] authoredColors;
    private Vector3 authoredVisualScale = Vector3.one;
    private Sequence highlightSequence;

    public BossArenaCoverSide Side => side;
    public Collider2D CoverCollider => coverCollider;
    public bool IsProtected => protectedDuringBossEncounter;
    public bool IsValid => isActiveAndEnabled && coverCollider != null && coverCollider.enabled;
    public Bounds WorldBounds => coverCollider != null
        ? coverCollider.bounds
        : new Bounds(transform.position, Vector3.zero);

    private void Awake()
    {
        CacheReferences();
        CaptureAuthoredPresentation();
    }

    private void OnDisable()
    {
        StopHighlight(true);
    }

    private void OnDestroy()
    {
        highlightSequence?.Kill();
        highlightSequence = null;
    }

    public void Configure(BossArenaCoverSide arenaSide, MeteorObstacle meteor)
    {
        side = arenaSide;
        meteorObstacle = meteor != null ? meteor : GetComponentInChildren<MeteorObstacle>(true);
        CacheReferences();
        CaptureAuthoredPresentation();
    }

    public void SetEncounterProtection(bool value)
    {
        protectedDuringBossEncounter = value;
    }

    public void SetEncounterPosition(Vector2 worldPosition)
    {
        CacheReferences();

        if (rootBody != null && rootBody.transform == transform)
        {
            rootBody.position = worldPosition;
            rootBody.linearVelocity = Vector2.zero;
            rootBody.angularVelocity = 0f;
            return;
        }

        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
    }

    public void SetPatternHighlight(bool visible)
    {
        if (!visible)
        {
            StopHighlight(false);
            return;
        }

        CacheReferences();
        CaptureAuthoredPresentation();
        highlightSequence?.Kill();

        highlightSequence = DOTween.Sequence().SetLink(gameObject);
        float halfDuration = Mathf.Max(0.01f, highlightPulseDuration * 0.5f);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color target = highlightColor;
            target.a = authoredColors[i].a;
            highlightSequence.Join(renderer.DOColor(target, halfDuration));
        }

        if (visualRoot != null)
        {
            highlightSequence.Join(
                visualRoot.DOScale(authoredVisualScale * highlightScale, halfDuration)
            );
        }

        highlightSequence.SetLoops(-1, LoopType.Yoyo);
    }

    public void PlayBlockedImpact(Vector2 hitPoint)
    {
        SetPatternHighlight(false);

        if (visualRoot == null)
        {
            return;
        }

        visualRoot.DOKill();
        visualRoot.localScale = authoredVisualScale;
        visualRoot.DOPunchScale(
                Vector3.one * 0.08f,
                0.16f,
                4,
                0.35f
            )
            .SetLink(gameObject);
    }

    private void StopHighlight(bool immediate)
    {
        highlightSequence?.Kill();
        highlightSequence = null;

        if (renderers == null || authoredColors == null)
        {
            return;
        }

        float duration = immediate ? 0f : 0.12f;
        for (int i = 0; i < renderers.Length && i < authoredColors.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.DOKill();
            if (duration <= 0f)
            {
                renderer.color = authoredColors[i];
            }
            else
            {
                renderer.DOColor(authoredColors[i], duration).SetLink(gameObject);
            }
        }

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            if (duration <= 0f)
            {
                visualRoot.localScale = authoredVisualScale;
            }
            else
            {
                visualRoot.DOScale(authoredVisualScale, duration).SetLink(gameObject);
            }
        }
    }

    private void CacheReferences()
    {
        meteorObstacle ??= GetComponentInChildren<MeteorObstacle>(true);
        rootBody ??= GetComponent<Rigidbody2D>();
        coverCollider ??= meteorObstacle != null
            ? meteorObstacle.GetComponent<Collider2D>()
            : GetComponentInChildren<Collider2D>(true);

        if (visualRoot == null)
        {
            visualRoot = transform.Find("VisualRoot");
        }

        if (visualRoot == null && meteorObstacle != null)
        {
            visualRoot = meteorObstacle.transform.Find("VisualRoot");
            if (visualRoot == null && meteorObstacle.transform.parent != null)
            {
                visualRoot = meteorObstacle.transform.parent.Find("VisualRoot");
            }
        }

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CaptureAuthoredPresentation()
    {
        if (renderers == null)
        {
            renderers = System.Array.Empty<SpriteRenderer>();
        }

        authoredColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            authoredColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
        }

        if (visualRoot != null)
        {
            authoredVisualScale = visualRoot.localScale;
        }
    }
}
