using System.Collections.Generic;
using UnityEngine;
using Phase = NullDispatcherBossController.EncounterPhase;

// Boss-local presentation, driven explicitly by existing gameplay transitions and attack handoffs.
[DisallowMultipleComponent]
public sealed class NullDispatcherPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer shellBody, exposedBody;
    [SerializeField] private SpriteRenderer[] shellFragments = new SpriteRenderer[0];
    [SerializeField] private Sprite sealedSprite, activeSprite, unboundSprite, criticalSprite, endingCoreSprite;
    [SerializeField] private NullSignatureVfx rainWarning, rainFire, rainRelease, compression, redirect, intervention, coreBurst, criticalLoop;
    private readonly List<NullSignatureVfx> owned = new List<NullSignatureVfx>(24);
    private readonly List<Lane> lanes = new List<Lane>(3);
    private NullSignatureVfx link, critical;
    private Phase phase;
    private struct Lane { public NullSignatureVfx visual; public Vector2 start, end; public bool damaging; }

    public void ShowPhase(Phase next)
    {
        phase = next;
        if (next == Phase.Dead) { ClearAll(); return; }
        if (shellBody != null)
            shellBody.sprite = next == Phase.Intro ? sealedSprite :
                next == Phase.PolarityPhase || next == Phase.FinalTransition ? unboundSprite : activeSprite;
        if (exposedBody != null) exposedBody.sprite = criticalSprite;
        foreach (var fragment in shellFragments) if (fragment != null) fragment.enabled = next == Phase.FinalTransition;
        if (next == Phase.TreatmentDialogue || next == Phase.FinalTransition) ClearAttackEffects();
        if (next == Phase.FinalTransition) PlayBurst();
        if (next == Phase.FinalPhase && critical == null && exposedBody != null) critical = Spawn(criticalLoop, transform.position, 0f, 0f, exposedBody.transform);
    }

    private NullSignatureVfx Spawn(NullSignatureVfx prefab, Vector3 position, float angle = 0f, float fit = 0f, Transform follow = null, float lifetime = 0f)
    {
        if (prefab == null || !Application.isPlaying || !isActiveAndEnabled) return null;
        owned.RemoveAll(v => v == null || v.Owner != this);
        var rotation = Quaternion.Euler(0f, 0f, angle);
        var go = PoolManager.Instance != null ? PoolManager.Instance.Get(prefab.gameObject, position, rotation) : Instantiate(prefab.gameObject, position, rotation);
        var effect = go.GetComponent<NullSignatureVfx>();
        effect.Play(this, fit, follow, lifetime); owned.Add(effect); return effect;
    }

    public bool ShowLane(Vector2 start, Vector2 end, bool damaging, float warningDuration)
    {
        var effect = Spawn(damaging ? rainFire : rainWarning, (start + end) * .5f, 0f, damaging ? 0f : warningDuration);
        if (effect == null) return false;
        effect.SetLine(start, end, true);
        lanes.Add(new Lane { visual = effect, start = start, end = end, damaging = damaging });
        return true;
    }

    public void ClearLanes()
    {
        foreach (var lane in lanes)
        {
            if (lane.visual != null && lane.visual.Owner == this) lane.visual.Release();
            if (lane.damaging && (phase == Phase.Phase1 || phase == Phase.PolarityPhase || phase == Phase.FinalPhase))
                Spawn(rainRelease, (lane.start + lane.end) * .5f)?.SetLine(lane.start, lane.end, true);
        }
        lanes.Clear();
    }

    public void PlayCompression(Vector2 origin) => Spawn(compression, origin);
    public void PlayRedirect(Vector2 origin, Vector2 target)
    {
        Vector2 delta = target - origin;
        Spawn(redirect, origin, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 45f);
    }
    public void PlayBurst() => Spawn(coreBurst, transform.position);
    public bool UpdateForcedLink(Vector2 emitter, Vector2 target)
    {
        if (link == null || link.Owner != this) link = Spawn(intervention, emitter);
        if (link == null) return false;
        link.SetLine(emitter, target, false); return true;
    }
    public void StopForcedLink() { if (link != null && link.Owner == this) link.Release(); link = null; }

    // Wired to the existing Weapon Lab support event, including TriggerSupportNow.
    public void ShowSupportIntervention()
    {
        Spawn(intervention, transform.position, 0f, 0f, null, .64f)?.SetLine(transform.position, transform.position + Vector3.up * 4f, false);
        PlayBurst();
    }
    public void ShowEndingCore()
    {
        ClearAll();
        if (exposedBody == null) return;
        exposedBody.sprite = endingCoreSprite;
        exposedBody.transform.localScale = Vector3.one * 2.4f;
    }
    public void ClearAttackEffects()
    {
        ClearLanes();
        foreach (var v in owned) if (v != null && v.Owner == this && v != critical && v != link) v.Release();
        owned.RemoveAll(v => v == null || v.Owner != this);
    }
    public void ClearAll()
    {
        foreach (var v in owned) if (v != null && v.Owner == this) v.Release();
        owned.Clear(); lanes.Clear(); link = critical = null;
    }
    private void OnDisable() => ClearAll();
}
