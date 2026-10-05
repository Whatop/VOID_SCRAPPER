using UnityEngine;

// Authored once on the pooled thief. Sampled by its existing collection channel;
// no timer, Update, cargo authority, dynamic hierarchy or independent VFX lifetime.
[DisallowMultipleComponent]
public sealed class PickupCollectionPresentation : MonoBehaviour
{
    [SerializeField] private LineRenderer tether;
    [SerializeField] private LineRenderer progressRing;
    [SerializeField] private Color channelColor = new Color(1f, .78f, .22f, 1f);
    [SerializeField] private float ringRadius = .34f;
    private RewardPickup target;
    private PlayerHealth player;
    private RunManager run;
    public RewardPickup Target => target;
    public bool IsShowing => target != null && progressRing != null && progressRing.enabled;

    public void Sample(Transform thief, RewardPickup pickup, float progress, PlayerHealth observer)
    {
        // AI may receive another (even zero-delta) sample while the result screen is up.
        // RunEnded cleanup must not be undone by that final combat update.
        var currentRun = RunManager.Instance;
        if (thief == null || pickup == null || !pickup.IsAvailable || (observer != null && observer.IsDead))
        { Clear(); return; }
        if (currentRun != null && (!currentRun.HasActiveRun || currentRun.IsCompletingRun))
        { Clear(); return; }
        if (target != pickup)
        {
            Clear(); target = pickup; target.BecameUnavailable += Clear;
            player = observer; if (player != null) player.Died += Clear;
            run = currentRun; if (run != null) run.RunEnded += HandleRunEnded;
        }
        Vector3 center = pickup.transform.position;
        if (tether != null)
        {
            tether.enabled = true; tether.positionCount = 2;
            tether.startColor = tether.endColor = channelColor;
            tether.SetPosition(0, thief.position); tether.SetPosition(1, center);
        }
        if (progressRing == null) return;
        progressRing.enabled = true;
        progressRing.startColor = progressRing.endColor = channelColor;
        const int segments = 24;
        progressRing.positionCount = segments + 1;
        float sweep = Mathf.Max(.08f, Mathf.Clamp01(progress)) * Mathf.PI * 2f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * .5f - sweep * i / segments;
            progressRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * ringRadius);
        }
    }
    private void HandleRunEnded(RunResultData _) => Clear();
    public void Clear()
    {
        if (target != null) target.BecameUnavailable -= Clear;
        if (player != null) player.Died -= Clear;
        if (run != null) run.RunEnded -= HandleRunEnded;
        target = null; player = null; run = null;
        if (tether != null) tether.enabled = false;
        if (progressRing != null) progressRing.enabled = false;
    }
    private void OnEnable() => Clear();
    private void OnDisable() => Clear();
}
