using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    [Header("Region A - Controlled support windows")]
    [SerializeField] private Bullet sectorMissilePrefab;
    [SerializeField] private SectorPulseVfx sectorSpawnMarkerPrefab, sectorPulsePrefab;
    [SerializeField] private SectorSupportVfx sectorArrivalMarker;
    public const float SectorArrivalAnticipation = .40f;
    public bool IsSectorArrivalRenderer(SpriteRenderer visual) => sectorArrivalMarker != null && sectorArrivalMarker.Visual == visual;
    private readonly Bullet[] sectorMissiles = new Bullet[6];
    private readonly float[] sectorMissileBorn = new float[6];
    private readonly bool[] sectorMissileGuided = new bool[6];
    private SectorPartitionLane sectorRightBurstWarning;
    private SectorPulseVfx sectorSpawnMarker, sectorPulse;
    private Color sectorBodyBeforeFade, sectorCannonBeforeFade;
    private bool sectorFading;
    public const float SectorMissileSpeed = 6f, SectorMissileTurnRate = 140f;
    public const float SectorMissileGuidanceDelay = .25f, SectorMissileGuidanceDuration = 1.5f;
    public const float SectorZoneRadius = 1.35f, SectorZoneWarning = 1.35f, SectorZoneActive = .3f;
    public int SectorActiveMissiles
    {
        get { int count = 0; for (int i = 0; i < sectorMissiles.Length; i++) if (IsSectorMissileLive(i)) count++; return count; }
    }
    public int SectorZoneCount
    {
        get { return sectorRectangles != null ? sectorRectangles.BandCount : 0; }
    }
    // Historical QA probes can still query these; no counter lane or scheduler exists.
    public bool SectorCounterActive => false;
    public float SectorCounterAngle => 0;
    public bool SectorSpawnMarkerVisible => sectorArrivalMarker != null && sectorArrivalMarker.Visual.enabled;
    public bool SectorPulseVisible => sectorPulse != null && sectorPulse.IsPlayingFor(gameObject);

    public void PlaySectorSpawnMarker(Vector3 position, float duration)
    {
        if (!useSectorControlSequence) return;
        ReturnSectorPulse(ref sectorSpawnMarker);
        if (sectorArrivalMarker == null) return;
        sectorArrivalMarker.transform.position = position;
        SampleSectorArrivalMarker(0);
    }
    public void SampleSectorArrivalMarker(float elapsed)
    {
        if (sectorArrivalMarker == null) return;
        sectorArrivalMarker.Visual.forceRenderingOff = false;
        sectorArrivalMarker.Visual.color = Color.white;
        sectorArrivalMarker.Sample(elapsed);
    }
    public void PlaySectorSpawnPulse()
    {
        if (!useSectorControlSequence) return;
        ReturnSectorPulse(ref sectorSpawnMarker); ReturnSectorPulse(ref sectorPulse);
        sectorPulse = RentSectorPulse(sectorPulsePrefab, transform.position, .2f, 2.4f, .36f, true);
    }
    public void ClearSectorSpawnEffects()
    {
        ReturnSectorPulse(ref sectorSpawnMarker); ReturnSectorPulse(ref sectorPulse);
        sectorArrivalMarker?.Clear();
    }
    private SectorPulseVfx RentSectorPulse(SectorPulseVfx prefab, Vector3 position, float from, float to, float duration, bool unscaled)
    {
        if (prefab == null || PoolManager.Instance == null) return null;
        var effect = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity).GetComponent<SectorPulseVfx>();
        effect.Play(gameObject, new Color(.45f, 1, .6f, .95f), from, to, duration, unscaled);
        return effect;
    }
    private void ReturnSectorPulse(ref SectorPulseVfx effect)
    {
        var lease = effect; effect = null;
        if (lease != null && lease.IsPlayingFor(gameObject)) lease.Release();
    }
    private void BeginSectorEscalationPulse()
    {
        ReturnSectorPulse(ref sectorPulse);
        sectorPulse = RentSectorPulse(sectorPulsePrefab, transform.position, .35f, 5f, .42f, false);
        sectorFading = true;
        sectorBodyBeforeFade = sectorBody != null ? sectorBody.color : Color.white;
        sectorCannonBeforeFade = sectorChargeCannon != null ? sectorChargeCannon.color : Color.white;
    }
    private void SampleSectorEscalationFade(float elapsed)
    {
        if (!sectorFading) return;
        // The green pulse leads; purple arrives during the existing fade-in.
        SetSectorEnergy(Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - .12f) / .22f)));
        float opacity = elapsed < .12f ? Mathf.Lerp(1, .22f, elapsed / .12f)
            : Mathf.Lerp(.22f, 1, Mathf.Clamp01((elapsed - .12f) / .22f));
        if (sectorBody != null) { Color c = sectorBodyBeforeFade; c.a *= opacity; sectorBody.color = c; }
        if (sectorChargeCannon != null) { Color c = sectorCannonBeforeFade; c.a *= opacity; sectorChargeCannon.color = c; }
        RefreshSectorEnergyAccents(opacity, 0);
        if (elapsed >= .34f) RestoreSectorEscalationFade();
    }
    private void RestoreSectorEscalationFade()
    {
        if (!sectorFading) return;
        if (sectorBody != null) sectorBody.color = sectorBodyBeforeFade;
        if (sectorChargeCannon != null) sectorChargeCannon.color = sectorCannonBeforeFade;
        sectorFading = false;
    }

    private bool IsSectorMissileLive(int slot) => sectorMissiles[slot] != null &&
        sectorMissiles[slot].gameObject.activeInHierarchy && sectorMissiles[slot].SourceRoot == transform;
    private bool LaunchSectorMissile(int index, Vector2 committedDirection)
    {
        if (index < 0 || index >= 6 || sectorMissilePrefab == null || PoolManager.Instance == null || player == null) return false;
        if (sectorMissileCore == null) return false;
        int slot = -1;
        for (int i = 0; i < sectorMissiles.Length; i++) if (!IsSectorMissileLive(i)) { slot = i; break; }
        if (slot < 0) return false;
        Vector2 direction = SectorDeploymentDirection(index);
        Vector2 origin = SectorMissileCorePosition;
        var missile = PoolManager.Instance.Get(sectorMissilePrefab.gameObject, origin, Quaternion.identity).GetComponent<Bullet>();
        // Existing Bullet owns motion, contacts, source attribution and pool return.
        missile.Initialize(direction, ProjectileOwner.Enemy, null, spreadProjectileDamage * GetDamageMultiplier(),
            SectorMissileSpeed, SectorMissileSpeed * 3.2f, 0, projectileSource: gameObject);
        missile.ConfigureProjectileColor(Color.white);
        missile.ConfigureBossWorldImpact(false, false, false, false, false);
        sectorMissiles[slot] = missile; sectorMissileBorn[slot] = Time.time; sectorMissileGuided[slot] = false;
        sectorMissilePresentations[slot] = missile.GetComponent<SectorMissilePresentation>();
        sectorMissilePresentations[slot]?.BeginDeployment();
        SectorShotsThisCycle++;
        return true;
    }
    private void UpdateSectorSupport(float dt)
    {
        if (!useSectorControlSequence) return;
        if (sectorCoreFlashBorn >= 0) sectorCoreGeneration?.Sample(Time.time - sectorCoreFlashBorn, false, SectorEmpowered);
        for (int i = 0; i < sectorMissiles.Length; i++)
        {
            if (!IsSectorMissileLive(i)) { sectorMissiles[i] = null; sectorMissileBorn[i] = 0; sectorMissileGuided[i] = false; sectorMissilePresentations[i] = null; continue; }
            sectorMissilePresentations[i]?.Sample(Time.time - sectorMissileBorn[i]);
            if (!sectorMissileGuided[i] && Time.time - sectorMissileBorn[i] >= SectorMissileGuidanceDelay)
            {
                sectorMissileGuided[i] = true;
                sectorMissiles[i].ConfigureTimedHoming(player, SectorMissileTurnRate, 32f, false, SectorMissileGuidanceDuration, true);
            }
        }
    }

    private void ClearSectorSupport()
    {
        for (int i = 0; i < sectorMissiles.Length; i++)
        {
            if (IsSectorMissileLive(i)) sectorMissiles[i].ForceRelease();
            sectorMissiles[i] = null; sectorMissileGuided[i] = false; sectorMissileBorn[i] = 0;
        }
        ClearSectorDeployment();
        RestoreSectorEscalationFade(); ClearSectorSpawnEffects();
    }
}
