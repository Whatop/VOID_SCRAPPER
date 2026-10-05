using UnityEngine;

// A pooled visual attached to an existing containment wall, never a second wall.
public sealed class SectorBarrierVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer beam;
    [SerializeField] private SectorSupportVfx activation, front, connection;
    [SerializeField] private Sprite[] purpleBeamFrames;
    private Sprite greenBeam;
    private Color greenColor;
    private Vector3 greenScale;
    private BossArenaLaserWall wall;
    private float length;
    public float Progress { get; private set; }
    public void Configure(BossArenaLaserWall owner, float fullLength)
    {
        if (greenBeam == null) { greenBeam = beam.sprite; greenColor = beam.color; greenScale = beam.transform.localScale; }
        beam.sprite = greenBeam; beam.color = greenColor; beam.transform.localScale = greenScale;
        wall = owner; length = fullLength;
        transform.SetParent(owner.transform, false);
        transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
        SetFormation(1, -1); // Ordinary/rebuilt walls retain their existing full state.
    }
    public void SetFormation(float progress, float elapsed)
    {
        Progress = Mathf.Clamp01(progress);
        wall?.SetFormationProgress(Progress);
        beam.enabled = Progress > 0;
        beam.size = new Vector2(length * Progress, 2);
        beam.transform.localPosition = new Vector3(-length * (1 - Progress) * .5f, 0);
        activation.transform.localPosition = new Vector3(-length * .5f, 0);
        front.transform.localPosition = new Vector3(-length * .5f + length * Progress, 0);
        connection.transform.localPosition = new Vector3(-length * .5f, 0);
        connection.Visual.size = new Vector2(length, .5f);
        if (elapsed < 0) { activation.Clear(); front.Clear(); connection.Clear(); return; }
        activation.Sample(elapsed);
        if (Progress > 0 && Progress < 1) front.Sample(elapsed, true); else front.Clear();
        connection.Sample(elapsed - BossPatternController.SectorBarrierGrowthEnd);
    }
    public void SampleDisconnect(float progress)
    {
        // The first visibly dim shutdown frame is also the damage/collision cutoff.
        wall?.SetFormationProgress(0);
        activation.Clear(); front.Clear(); connection.Clear();
        beam.color = new Color(1, 1, 1, .65f * (1 - Mathf.Clamp01(progress)));
        beam.enabled = progress < 1;
    }
    public void SamplePurpleConnection(float progress, float elapsed, bool damaging)
    {
        activation.Clear(); front.Clear(); connection.Clear();
        if (purpleBeamFrames == null || purpleBeamFrames.Length != 4) return;
        beam.sprite = purpleBeamFrames[Mathf.FloorToInt(elapsed / .06f) % 4];
        beam.drawMode = SpriteDrawMode.Tiled; beam.transform.localScale = Vector3.one;
        beam.color = new Color(1, 1, 1, damaging ? .8f : .45f);
        beam.transform.localPosition = Vector3.left * length * .5f;
        beam.size = new Vector2(length * Mathf.Clamp01(progress), 1);
        beam.enabled = progress > 0; Progress = progress;
        wall?.SetFormationProgress(damaging ? 1 : 0);
    }
    public void Release()
    {
        wall = null; Progress = 0; beam.enabled = false;
        activation.Clear(); front.Clear(); connection.Clear(); transform.SetParent(null, false);
        if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject); else gameObject.SetActive(false);
    }
}
