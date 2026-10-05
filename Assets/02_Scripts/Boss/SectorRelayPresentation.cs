using UnityEngine;

// Passive presentation on the reusable Region A relay. The boss supplies time.
[DisallowMultipleComponent]
public sealed class SectorRelayPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Sprite green, purple;
    [SerializeField] private SectorSupportVfx deploy, disconnect, reconnect, stabilize;
    public bool IsPurple { get; private set; }
    public bool IsBodyVisible => body != null && body.enabled && body.color.a > 0;
    public void ResetPresentation()
    {
        ClearEffects(); IsPurple = false;
        body.sprite = green; body.color = Color.white; body.enabled = true;
    }
    public void SetEnergy(bool overdrive)
    {
        IsPurple = overdrive; body.sprite = overdrive ? purple : green; body.color = Color.white;
    }
    public void SampleDisconnect(float elapsed, Vector2 link)
    {
        disconnect.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(link.y, link.x) * Mathf.Rad2Deg);
        disconnect.Sample(elapsed);
        body.color = new Color(1, 1, 1, Mathf.Lerp(1, .7f, Mathf.Clamp01(elapsed / .24f)));
    }
    public void SampleDeploy(float elapsed)
    {
        deploy.Sample(elapsed); body.enabled = elapsed >= .09f;
        body.color = new Color(1, 1, 1, Mathf.Clamp01((elapsed - .09f) / .14f));
        stabilize.Sample(elapsed - .18f, false, IsPurple);
    }
    public void SampleConversion(float elapsed)
    {
        if (elapsed >= .15f) SetEnergy(true);
        stabilize.Sample(elapsed - .15f, false, true);
    }
    public void SampleReconnect(float elapsed, Vector2 link)
    {
        reconnect.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(link.y, link.x) * Mathf.Rad2Deg);
        reconnect.Sample(elapsed);
    }
    public void ClearEffects() { deploy?.Clear(); disconnect?.Clear(); reconnect?.Clear(); stabilize?.Clear(); }
    private void OnDisable() => ClearEffects();
}
