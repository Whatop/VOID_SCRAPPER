using System.Collections;
using UnityEngine;

// Saved local renderers; state owners supply protection and presentation time.
// Only actual absorption events start the bounded hit/break feedback coroutine.
public sealed class SectorBossShieldPresentation : MonoBehaviour
{
    [SerializeField] private SectorSupportVfx shell, release, hit;
    private bool purple;
    private float hitBorn = -100, breakBorn = -100;
    private Coroutine feedback;
    public const float ReleaseDuration = .31f;
    public bool IsProtectedVisualVisible => shell != null && shell.Visual.enabled;
    public bool IsReleaseVisible => release != null && release.Visual.enabled;
    public bool IsHitVisible => hit != null && hit.Visual.enabled;
    public bool IsPurple => purple;
    public void ShowProtected(bool overdrive)
    {
        Clear(); purple = overdrive; shell.Sample(0, true, purple);
    }
    public void SampleProtected(float elapsed) => shell.Sample(elapsed, true, purple);
    public void SampleRelease(float elapsed)
    {
        shell.Clear(); release.Sample(elapsed, false, purple);
    }
    public void AbsorptionHit(Vector2 point)
    {
        purple = true;
        Vector2 outward = (point - (Vector2)transform.position).normalized;
        hit.transform.position = point;
        hit.transform.rotation = Quaternion.FromToRotation(Vector3.up, outward);
        hitBorn = Time.time;
        if (feedback == null) feedback = StartCoroutine(Feedback());
    }
    public void AbsorptionBreak()
    {
        purple = true; shell.Clear(); breakBorn = Time.time;
        if (feedback == null) feedback = StartCoroutine(Feedback());
    }
    private IEnumerator Feedback()
    {
        while (Time.time - hitBorn < .15f || Time.time - breakBorn < ReleaseDuration)
        {
            hit.Sample(Time.time - hitBorn, false, true);
            release.Sample(Time.time - breakBorn, false, true);
            yield return null;
        }
        hit.Clear(); release.Clear(); feedback = null;
    }
    public void Clear()
    {
        if (feedback != null) StopCoroutine(feedback);
        feedback = null; hitBorn = breakBorn = -100;
        shell?.Clear(); release?.Clear(); hit?.Clear();
    }
    private void OnDisable() => Clear();
}
