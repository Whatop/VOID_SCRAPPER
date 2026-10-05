using UnityEngine;

// Passive, pooled presentation and one logical hit decision per explosion.
// The boss coroutine supplies all clocks. No strip colliders, Update or coroutines.
[DisallowMultipleComponent]
public sealed class SectorRectangularAoE : MonoBehaviour
{
    [SerializeField] private LineRenderer[] borders; // inner/outer per band
    [SerializeField] private LineRenderer[] fills;   // four non-overlapping strips per band
    [SerializeField] private SpriteRenderer[] releaseEdges, releaseCorners;
    [SerializeField] private Sprite[] releaseFrames, cornerFrames;
    public const int MaxBands = 16;
    // One descending definition: arena perimeter -> shared insets -> center.
    private readonly Vector2[] boundaries = new Vector2[MaxBands + 1];
    private Vector2 center, inner;
    public Vector2 OuterHalfExtents => boundaries[0];
    public Vector2 RingBoundary(int index) => boundaries[Mathf.Clamp(index, 0, BandCount)];
    private float width, damage;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private int explodedMask;
    public int BandCount { get; private set; }
    public int WarningPhase { get; private set; } = -1;
    public int ExplodedMask => explodedMask;
    public Vector2 InnerHalfExtents => inner;
    public float BandWidth => width;
    public bool IsVisible
    {
        get
        {
            if (BandCount == 0 || borders == null) return false;
            for (int i = 0; i < BandCount * 2; i++) if (borders[i] != null && borders[i].enabled) return true;
            return false;
        }
    }

    public void Configure(Vector2 origin, Vector2 arenaHalf, float bandWidth, float amount, PlayerHealth target)
    {
        Clear(); center = origin; width = Mathf.Max(.5f, bandWidth); damage = amount;
        BandCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(arenaHalf.x, arenaHalf.y) / width), 1, MaxBands); player = target;
        playerCollider = target != null ? target.GetComponent<Collider2D>() : null;
        transform.SetPositionAndRotation(origin, Quaternion.identity); transform.localScale = Vector3.one;
        for (int i = 0; i < BandCount; i++) boundaries[i] = Vector2.Max(Vector2.zero, arenaHalf - Vector2.one * width * i);
        boundaries[BandCount] = Vector2.zero; // The entire remainder belongs to the last ring.
        inner = boundaries[BandCount - 1];
        for (int band = 0; band < BandCount; band++)
        {
            Vector2 lo = boundaries[band + 1], hi = boundaries[band];
            ConfigureRelease(band, hi);
            SetBorder(borders[band * 2], lo); SetBorder(borders[band * 2 + 1], hi);
            for (int strip = 0; strip < 4; strip++)
            {
                Rect rect = Strip(lo, hi, strip); var line = fills[band * 4 + strip];
                line.SetPosition(0, new Vector3(rect.xMin, rect.center.y));
                line.SetPosition(1, new Vector3(rect.xMax, rect.center.y));
                line.widthMultiplier = rect.height;
            }
        }
        Warn(0, 0);
    }
    private static void SetBorder(LineRenderer line, Vector2 half)
    {
        line.SetPosition(0, new Vector3(-half.x, -half.y)); line.SetPosition(1, new Vector3(-half.x, half.y));
        line.SetPosition(2, new Vector3(half.x, half.y)); line.SetPosition(3, new Vector3(half.x, -half.y));
    }
    public static Rect Strip(Vector2 lo, Vector2 hi, int strip)
    {
        // Top/bottom own corners; left/right stop at the inner Y extent.
        if (strip == 0) return Rect.MinMaxRect(-hi.x, lo.y, hi.x, hi.y);
        if (strip == 1) return Rect.MinMaxRect(-hi.x, -hi.y, hi.x, -lo.y);
        if (strip == 2) return Rect.MinMaxRect(-hi.x, -lo.y, -lo.x, lo.y);
        return Rect.MinMaxRect(lo.x, -lo.y, hi.x, lo.y);
    }
    public static bool OwnsBand(int band, int phase) => band % 2 == phase;
    public void Warn(int phase, float progress)
    {
        WarningPhase = phase;
        for (int band = 0; band < BandCount; band++)
        {
            if (OwnsBand(band, phase)) Paint(band, new Color(1, .13f, .12f, Mathf.Lerp(.65f, 1, progress)), Mathf.Lerp(.065f, .14f, progress), .055f);
            else if ((explodedMask & (1 << (1 - phase))) == 0) Paint(band, new Color(.6f, .75f, .7f, .16f), 0, .025f);
        }
    }
    public void Explode(int phase)
    {
        if (phase != WarningPhase || (explodedMask & (1 << phase)) != 0) return;
        explodedMask |= 1 << phase; WarningPhase = -1;
        ReleaseVisual(phase, 0);
        if (player == null || player.IsDead || !IntersectsPlayer(phase)) return;
        player.TakeDamage(damage, player.transform.position, (Vector2)player.transform.position - center);
    }
    // The outer perimeter is ring 0. Shared boundaries belong to the inset ring;
    // the final ring owns the complete center remainder, including both axes.
    public int RingIndex(Vector2 worldPosition)
    {
        Vector2 local = worldPosition - center;
        local = new Vector2(Mathf.Abs(local.x), Mathf.Abs(local.y));
        Vector2 outer = OuterHalfExtents;
        if (BandCount == 0 || local.x > outer.x || local.y > outer.y) return -1;
        for (int ring = 0; ring < BandCount - 1; ring++)
            if (local.x > boundaries[ring + 1].x || local.y > boundaries[ring + 1].y) return ring;
        return BandCount - 1;
    }
    public bool IntersectsPlayer(int phase)
    {
        if (player == null) return false;
        Vector2 position = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
        int ring = RingIndex(position);
        return ring >= 0 && OwnsBand(ring, phase);
    }
    private void ConfigureRelease(int band, Vector2 half)
    {
        if (releaseEdges == null || releaseEdges.Length < BandCount * 4) return;
        // The 0.5-unit corner cells own each bend. Straights tile BETWEEN them;
        // only length changes, never the source pixel thickness.
        for (int side = 0; side < 4; side++)
        {
            var edge = releaseEdges[band * 4 + side];
            bool horizontal = side < 2;
            edge.transform.localPosition = horizontal
                ? new Vector3(-half.x + .5f, side == 0 ? half.y : -half.y)
                : new Vector3(side == 2 ? -half.x : half.x, -half.y + .5f);
            edge.transform.localRotation = Quaternion.Euler(0, 0, horizontal ? 0 : 90);
            edge.size = new Vector2(Mathf.Max(0, (horizontal ? half.x : half.y) * 2 - 1), .5f);
            edge.flipY = side == 1 || side == 2;
            var corner = releaseCorners[band * 4 + side];
            corner.transform.localPosition = new Vector3(side % 2 == 0 ? -half.x : half.x, side < 2 ? half.y : -half.y);
            corner.flipX = side % 2 != 0; corner.flipY = side >= 2;
        }
    }
    private void SampleRelease(int band, float progress)
    {
        if (releaseEdges == null || releaseEdges.Length < BandCount * 4 || releaseFrames == null || releaseFrames.Length < 5) return;
        float time = Mathf.Clamp01(progress) * BossPatternController.SectorRectangleReleaseTime;
        int frame = time < .04f ? 0 : time < .09f ? 1 : time < .15f ? 2 : time < .21f ? 3 : 4;
        for (int side = 0; side < 4; side++)
        {
            var edge = releaseEdges[band * 4 + side]; var corner = releaseCorners[band * 4 + side];
            edge.sprite = releaseFrames[frame]; corner.sprite = cornerFrames[frame];
            edge.enabled = corner.enabled = progress < 1;
        }
    }
    public void ReleaseVisual(int phase, float progress)
    {
        float fade = 1 - Mathf.Clamp01(progress);
        // A short pale-hot fill and thick red rim make release distinct from warning.
        Color flash = Color.Lerp(new Color(1, .2f, .12f), new Color(1, .85f, .55f), fade);
        flash.a = fade;
        for (int band = 0; band < BandCount; band++)
            if (OwnsBand(band, phase)) { Paint(band, flash, .32f * fade, .12f); SampleRelease(band, progress); }
    }
    private void Paint(int band, Color color, float fillAlpha, float borderWidth)
    {
        for (int i = 0; i < 2; i++)
        { var line = borders[band * 2 + i]; line.startColor = line.endColor = color; line.widthMultiplier = borderWidth; line.enabled = color.a > 0 && (band != BandCount - 1 || i != 0); }
        color.a = fillAlpha;
        for (int i = 0; i < 4; i++)
        { var line = fills[band * 4 + i]; line.startColor = line.endColor = color; line.enabled = fillAlpha > 0; }
    }
    public void Clear()
    {
        if (borders != null) for (int i = 0; i < borders.Length; i++) if (borders[i] != null) borders[i].enabled = false;
        if (fills != null) for (int i = 0; i < fills.Length; i++) if (fills[i] != null) fills[i].enabled = false;
        if (releaseEdges != null) for (int i = 0; i < releaseEdges.Length; i++) if (releaseEdges[i] != null) releaseEdges[i].enabled = false;
        if (releaseCorners != null) for (int i = 0; i < releaseCorners.Length; i++) if (releaseCorners[i] != null) releaseCorners[i].enabled = false;
        BandCount = 0; explodedMask = 0; WarningPhase = -1; player = null; playerCollider = null;
    }
    private void OnDisable() => Clear();
}
