using UnityEngine;

// Local passive view. The commander owns protection, time, motion and cleanup.
public sealed class RaiderShieldPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer normalField;
    [SerializeField] private PhaseCombatVfx ramField;
    [SerializeField] private LineRenderer laneBorder, laneFill;
    [SerializeField] private SpriteRenderer[] afterimages;
    private float nextGhost;
    private int ghostIndex;
    public bool NormalVisible => normalField != null && normalField.enabled;
    public bool LaneVisible => laneBorder != null && laneBorder.enabled;
    public void ShowNormal(bool visible) { if (normalField != null) normalField.enabled = visible; }
    public void ShowLane(Vector2 start, Vector2 end, float width)
    {
        Vector2 forward = (end - start).normalized, side = new Vector2(-forward.y, forward.x) * width * .5f;
        // Include the front hull at both ends: warning matches the swept body, not just its center.
        start -= forward * 1.12f; end += forward * 1.12f;
        laneBorder.positionCount = 5;
        laneBorder.SetPosition(0, start - side); laneBorder.SetPosition(1, start + side);
        laneBorder.SetPosition(2, end + side); laneBorder.SetPosition(3, end - side); laneBorder.SetPosition(4, start - side);
        laneFill.positionCount = 2; laneFill.SetPosition(0, start); laneFill.SetPosition(1, end);
        laneFill.startWidth = laneFill.endWidth = width;
        laneBorder.enabled = laneFill.enabled = true;
    }
    public void HideLane() { if (laneBorder != null) laneBorder.enabled = false; if (laneFill != null) laneFill.enabled = false; }
    public void ClearRam() { ramField?.Clear(); HideLane(); ClearGhosts(); }
    public void BreakField() { ShowNormal(false); ramField?.Clear(); }
    public void Sample(PirateCommanderBossController.AssaultStage stage, float elapsed, bool protectedNow, SpriteRenderer body)
    {
        SampleStage(stage, elapsed, protectedNow);
        if (stage == PirateCommanderBossController.AssaultStage.RamActive && body != null && afterimages != null && afterimages.Length > 0)
        {
            if (Time.time >= nextGhost)
            {
                var ghost = afterimages[ghostIndex++ % afterimages.Length];
                ghost.sprite = body.sprite; ghost.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
                ghost.transform.localScale = body.transform.lossyScale; ghost.enabled = true; nextGhost = Time.time + .09f;
            }
        }
        else ClearGhosts();
    }
    private void SampleStage(PirateCommanderBossController.AssaultStage stage, float elapsed, bool protectedNow)
    {
        if (ramField == null) return;
        switch (stage)
        {
            case PirateCommanderBossController.AssaultStage.RamWarning:
                if (protectedNow) ramField.Sample(elapsed, 0, 4, true); else ramField.Clear(); break;
            case PirateCommanderBossController.AssaultStage.RamActive:
                if (protectedNow) ramField.Sample(elapsed % .21f, 4, 3); else ramField.Clear(); break;
            case PirateCommanderBossController.AssaultStage.RamPunish: ramField.Sample(elapsed, 7, 3, true); break;
            case PirateCommanderBossController.AssaultStage.ShieldRecover: ramField.Sample(elapsed, 10, 3); break;
            default: ramField.Clear(); break;
        }
    }
    private void ClearGhosts() { if (afterimages != null) foreach (var ghost in afterimages) if (ghost != null) ghost.enabled = false; }
    public void Clear() { ShowNormal(false); ClearRam(); nextGhost = 0; ghostIndex = 0; }
    private void OnDisable() => Clear();
}
