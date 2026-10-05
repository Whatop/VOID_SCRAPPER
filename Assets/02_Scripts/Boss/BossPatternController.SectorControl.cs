using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    public enum SectorStage { Idle, Warning, Partition, Compression, PrecisionWarning, PrecisionFire, Recovery, Stopped, Suppression, EscalationHold, EscalationWarning, MissileWarning, MissileLaunch, DropWarning, DropActive, CounterWarning, CounterRotation, SweepAim, SweepCommit, SweepForward, SweepRecovery, SweepReverse, RectWarningA, RectExplosionA, RectWarningB, RectExplosionB, MissileFlight }
    [Header("Region A - Sector Control")]
    [SerializeField] private bool useSectorControlSequence;
    [SerializeField] private SectorPartitionLane sectorLanePrefab;
    [SerializeField] private SpriteRenderer sectorBody;
    [SerializeField] private Sprite sectorIdleSprite, sectorChargedSprite, sectorExposedSprite;
    [SerializeField] private Sprite sectorContainmentSprite;
    [SerializeField, Min(.1f)] private float sectorRecoveryTime = 1.1f;
    [SerializeField, Min(.1f)] private float sectorPressureDelay = .65f;
    [Header("Sector Weapon Sources")]
    [SerializeField] private Transform sectorLeftMuzzle, sectorRightMuzzle, sectorChargeMuzzle;
    [SerializeField] private SpriteRenderer sectorChargeCannon;
    [SerializeField] private GameObject sectorSideFlashPrefab, sectorChargeFlashPrefab;
    private readonly PooledMuzzleFlash[] sectorFlashes = new PooledMuzzleFlash[3];
    private Transform sectorBurstMuzzle;
    private bool sectorWeaponFacingLocked, sectorEscalationPresented;
    private float sectorAddedSpokeExtension = 1f;
    private readonly SectorPartitionLane[] sectorLanes = new SectorPartitionLane[6];
    private SectorPartitionLane precisionWarning;
    private int sectorCycle;
    private bool sectorCameraProfileHeld;
    public SectorStage CurrentSectorStage { get; private set; }
    public int SectorCycle => sectorCycle;
    public int SectorShotsThisCycle { get; private set; }
    public bool SectorEscalated => phase2 || phase2ShieldActive;
    public Vector2 SectorArenaCenter => arenaCenter;
    public Vector2 SectorArenaHalfExtents => GetEffectiveHalfExtents();

    // One boss-owned clock. Positive angles are the authored counter-clockwise direction.
    [SerializeField, Min(1f)] private float sectorAngularSpeed = 30f;
    [SerializeField, Min(.1f)] private float sectorEscalationWarningTime = 1.2f;
    [SerializeField, Min(.02f)] private float sectorBurstCadence = .08f;
    [SerializeField, Min(.05f)] private float sectorBurstGap = .28f;
    private float sectorAngle = 22.5f, sectorMorph, sectorWarningProgress, sectorActiveStart;
    private int sectorSpokeCount;
    private Vector2 sectorBurstOrigin, sectorBurstDirection;
    private bool sectorRotating, sectorEscalationReady;
    public int SectorSpokeCount => sectorSpokeCount;
    public float SectorAngle => sectorAngle;
    public float SectorAngularSpeed => sectorAngularSpeed;
    public bool UsesSectorControl => useSectorControlSequence;
    public static BossPatternController ActiveSectorEncounter { get; private set; }
    private MeteorObstacle[] sectorMeteors;
    private void BeginSectorArenaCleanup()
    {
        if (!useSectorControlSequence) return;
        phase2ExpeditionHUD?.SetRegionBossPresentation(this, true);
        ActiveSectorEncounter = this; // Intro publishes the owner before Core changes GameState.
        if (sectorMeteors != null) return;
        WarmSectorAttackPools();
        // One encounter-start snapshot. Map generation is finished; no scene scans in Update.
        sectorMeteors = FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);
        Vector2 half = GetEffectiveHalfExtents();
        var bounds = new Bounds(arenaCenter, new Vector3(half.x * 2 + 1, half.y * 2 + 1, 20));
        for (int i = 0; i < sectorMeteors.Length; i++)
            sectorMeteors[i].PauseForSectorEncounter(bounds);
    }
    private void EndSectorArenaCleanup()
    {
        phase2ExpeditionHUD?.SetRegionBossPresentation(this, false);
        if (ActiveSectorEncounter == this) ActiveSectorEncounter = null;
        if (sectorMeteors == null) return;
        for (int i = 0; i < sectorMeteors.Length; i++)
            if (sectorMeteors[i] != null) sectorMeteors[i].ResumeAfterSectorEncounter();
        sectorMeteors = null;
    }
    private void WarmSectorAttackPools()
    {
        if (PoolManager.Instance == null) return;
        // Reserve the bounded peak once during the existing intro. No new pool
        // owner, and no allocation/Instantiate at individual shot dispatch.
        var leases = new GameObject[24];
        void Warm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++) leases[i] = PoolManager.Instance.Get(prefab, transform.position, Quaternion.identity);
            for (int i = 0; i < count; i++) { PoolManager.Instance.Release(leases[i]); leases[i] = null; }
        }
        // Two sweeps fit inside the two-second bullet lifetime; the earlier
        // sweep expires before the third fills. Retain the existing 24 leases.
        Warm(projectileDefinition != null ? projectileDefinition.ProjectilePrefab : null, 24);
        Warm(sectorMissilePrefab != null ? sectorMissilePrefab.gameObject : null, 6);
        Warm(sectorLanePrefab != null ? sectorLanePrefab.gameObject : null, 8);
        Warm(sectorRectangularPrefab != null ? sectorRectangularPrefab.gameObject : null, 1);
        Warm(sectorSideFlashPrefab, 2);
        Warm(ResolveManagerShipPrefab(), 6);
    }

    // Existing four spokes keep their identities. Added spokes occupy the 45/225 gaps.
    public static float SectorSpokeOffset(int index, int count, float morph = 1f)
    {
        if (count <= 4) return index * 90f;
        float from = index < 4 ? index * 90f : index == 4 ? 45f : 225f;
        float to = index == 0 ? 0 : index == 1 ? 120 : index == 2 ? 180 : index == 3 ? 300 : index == 4 ? 60 : 240;
        return Mathf.Lerp(from, to, Mathf.Clamp01(morph));
    }
    public static float RayToSectorEdge(Vector2 origin, Vector2 direction, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        float cross = direction.x * edge.y - direction.y * edge.x;
        if (Mathf.Abs(cross) < .00001f) return float.PositiveInfinity;
        Vector2 delta = a - origin;
        float t = (delta.x * edge.y - delta.y * edge.x) / cross;
        float u = (delta.x * direction.y - delta.y * direction.x) / cross;
        return t >= 0 && u >= -.0001f && u <= 1.0001f ? t : float.PositiveInfinity;
    }
    public float SectorBeamLength(Vector2 origin, Vector2 direction)
    {
        float distance = float.PositiveInfinity;
        // Actual wall centerlines, including the existing rectangle -> hexagon handoff.
        // Both SpriteRenderer and collider receive this same endpoint, .05 inside the wall.
        for (int i = 0; i < boundaryLaserWalls.Count; i++)
        {
            var wall = boundaryLaserWalls[i];
            if (wall == null || !wall.activeInHierarchy) continue;
            var box = wall.GetComponent<BoxCollider2D>();
            if (box == null) continue; // Reconnect visuals use the same wall geometry while collision is disabled.
            Vector2 a = box.transform.TransformPoint(box.offset + Vector2.left * box.size.x * .5f);
            Vector2 b = box.transform.TransformPoint(box.offset + Vector2.right * box.size.x * .5f);
            distance = Mathf.Min(distance, RayToSectorEdge(origin, direction, a, b));
        }
        if (float.IsPositiveInfinity(distance))
        {
            Vector2 half = GetEffectiveHalfExtents();
            Vector2 lo = arenaCenter - half, hi = arenaCenter + half;
            distance = Mathf.Min(RayToSectorEdge(origin, direction, lo, new Vector2(hi.x, lo.y)),
                RayToSectorEdge(origin, direction, new Vector2(hi.x, lo.y), hi));
            distance = Mathf.Min(distance, RayToSectorEdge(origin, direction, hi, new Vector2(lo.x, hi.y)));
            distance = Mathf.Min(distance, RayToSectorEdge(origin, direction, new Vector2(lo.x, hi.y), lo));
        }
        return float.IsInfinity(distance) ? 0f : Mathf.Max(0, distance - .05f);
    }
    private void EnsureSectorSpokes(int count)
    {
        sectorSpokeCount = count;
        for (int i = 0; i < count; i++)
            if (sectorLanes[i] == null)
                sectorLanes[i] = RentSectorLane(transform.position, (Vector2)transform.position + Vector2.right,
                    laserWidth, laserDamage * GetDamageMultiplier());
        UpdateSectorSpokes(0);
    }
    private void UpdateSectorSpokes(float dt)
    {
        if (!useSectorControlSequence || sectorSpokeCount == 0) return;
        if (sectorRotating) sectorAngle += SectorSignedAngularSpeed * dt;
        Vector2 origin = transform.position;
        for (int i = 0; i < sectorSpokeCount; i++)
        {
            var lane = sectorLanes[i]; if (lane == null) continue;
            float radians = (sectorAngle + SectorSpokeOffset(i, sectorSpokeCount, sectorMorph)) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float extension = i < 4 ? 1f : sectorAddedSpokeExtension;
            lane.SetEmpowerment(SectorBeamEmpowerment);
            lane.SetEndpoints(origin, origin + direction * SectorBeamLength(origin, direction) * extension * sectorConnectionProgress);
            if (sectorLaserDisconnecting) lane.Disconnect(sectorDisconnectProgress);
            else if (sectorRotating) lane.Activate(Time.time - sectorActiveStart);
            else lane.Warn(sectorWarningProgress);
        }
    }
    private void PrepareSectorEscalation()
    {
        if (!useSectorControlSequence) return;
        sectorRotating = false; sectorWarningProgress = 0; sectorMorph = 0;
        sectorAddedSpokeExtension = .06f;
        EnsureSectorSpokes(4); SetSectorStage(SectorStage.EscalationHold);
    }
    private IEnumerator SectorEscalationRoutine()
    {
        if (sectorEscalationPresented) yield break;
        sectorEscalationPresented = true;
        sectorAddedSpokeExtension = .06f;
        EnsureSectorSpokes(6); SetSectorStage(SectorStage.EscalationWarning);
        BeginSectorEscalationPulse();
        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, transform.position);
        float elapsed = 0;
        while (elapsed < sectorEscalationWarningTime)
        {
            SampleSectorEscalationFade(elapsed);
            sectorWarningProgress = elapsed / sectorEscalationWarningTime;
            // Short harmless links tell the two new gaps, extend with the redistribution,
            // then hold the completed structure for the final quarter of the warning.
            sectorMorph = Mathf.SmoothStep(0, 1, Mathf.Clamp01((sectorWarningProgress - .25f) * 2));
            sectorAddedSpokeExtension = Mathf.Lerp(.06f, 1f, sectorMorph);
            yield return null; elapsed += Time.deltaTime;
        }
        RestoreSectorEscalationFade();
        SetSectorEnergy(1);
        sectorMorph = 1; sectorAddedSpokeExtension = 1; sectorWarningProgress = 1;
        UpdateSectorSpokes(0); sectorEscalationReady = true;
    }
    private IEnumerator SectorControlCycleRoutine(bool escalated)
    {
        if (sectorCycle == 0) phase2ExpeditionHUD?.HideOperationDisplayImmediate();
        BeginSectorArenaCleanup();
        casting = true;
        // One owner selects a main attack. Only Direct Burst is support; only
        // Phase 2 rotation explicitly invokes it inside a main attack window.
        SectorMainAttack kind = SectorMainAttackForCycle(sectorCycle, escalated);
        bool firstPhase2Rotation = escalated && sectorFirstPhase2RotationPending;
        if (!firstPhase2Rotation) sectorCycle++;
        SectorShotsThisCycle = 0;
        if (firstPhase2Rotation || kind == SectorMainAttack.RotatingLaser)
            yield return SectorRotationRoutine(escalated);
        else if (kind == SectorMainAttack.Sweep) yield return SectorSweepRoutine();
        else if (kind == SectorMainAttack.Missiles) yield return SectorMissileRoutine();
        else yield return AlternatingRectangularAoERoutine();
        yield return FinishPendingSectorLaser();
        ClearSectorAttacks(); Bullet.ReleaseAllActiveFromSource(transform);
        UpdatePhase();
        // Brief filler after every second main. No projectile or hazard from the
        // completed attack survives into this support slot.
        if (!firstPhase2Rotation && sectorCycle % 2 == 0 && !sectorPhase2Pending)
        {
            SetSectorStage(SectorStage.Recovery);
            yield return new WaitForSeconds(.28f);
            UpdatePhase();
            if (!sectorPhase2Pending)
            {
                yield return SectorDirectBurstRoutine(escalated, false);
                yield return new WaitForSeconds(projectileDefinition != null ? projectileDefinition.LifeTime : 2f);
                ClearSectorAttacks(); Bullet.ReleaseAllActiveFromSource(transform);
            }
        }
        casting = false; SetSectorStage(SectorStage.Recovery);
        float recovery = 0;
        while (recovery < sectorRecoveryTime) { yield return null; recovery += Time.deltaTime; }
    }

    private void CommitSectorBurst(int module)
    {
        sectorBurstMuzzle = module == 0 ? sectorLeftMuzzle : sectorRightMuzzle;
        sectorBurstOrigin = sectorBurstMuzzle != null ? (Vector2)sectorBurstMuzzle.position : ResolveFirePosition();
        sectorBurstDirection = ResolveDirectionToPlayer(sectorBurstOrigin);
        sectorWeaponFacingLocked = true;
    }
    private void DispatchSectorSuppressionShot()
    {
        // Resolve the real muzzle at dispatch; the committed direction never tracks mid-burst.
        if (sectorBurstMuzzle != null) sectorBurstOrigin = sectorBurstMuzzle.position;
        SpawnProjectile(sectorBurstOrigin, sectorBurstDirection, spreadProjectileDamage * GetDamageMultiplier(),
            spreadProjectileSpeedOverride, spreadProjectileRangePhase1, 1, false);
        SectorShotsThisCycle++;
        AudioManager.PlayAt(SoundEventIds.BossSpreadFire, sectorBurstOrigin);
        PlaySectorFlash(sectorBurstMuzzle == sectorRightMuzzle ? 1 : 0, sectorBurstMuzzle, sectorSideFlashPrefab, sectorBurstDirection);
    }

    private Vector2 ResolveSectorChargePosition() => sectorChargeMuzzle != null
        ? (Vector2)sectorChargeMuzzle.position : ResolveFirePosition();

    private void SetSectorChargeFeedback(float progress)
    {
        if (sectorChargeCannon != null)
            sectorChargeCannon.color = Color.Lerp(Color.white, SectorEmpowered ? Color.white : new Color(.55f, 1f, .65f, 1), Mathf.Clamp01(progress));
        RefreshSectorEnergyAccents(1, progress);
    }

    private void PlaySectorFlash(int slot, Transform muzzle, GameObject prefab, Vector2 direction)
    {
        if (muzzle == null || prefab == null || PoolManager.Instance == null) return;
        ClearSectorFlash(slot);
        var flash = PoolManager.Instance.Get(prefab, muzzle.position,
            Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        sectorFlashes[slot] = flash.GetComponent<PooledMuzzleFlash>();
        sectorFlashes[slot]?.Play(muzzle, gameObject);
    }

    private void ClearSectorFlash(int slot)
    {
        var flash = sectorFlashes[slot]; sectorFlashes[slot] = null;
        // Expired leases can already have returned to a shared pool.
        Transform muzzle = slot == 0 ? sectorLeftMuzzle : slot == 1 ? sectorRightMuzzle : sectorChargeMuzzle;
        if (flash == null || !flash.IsPlayingAt(muzzle)) return;
        if (PoolManager.Instance != null) PoolManager.Instance.Release(flash.gameObject);
        else flash.gameObject.SetActive(false);
    }

    private SectorPartitionLane RentSectorLane(Vector2 start, Vector2 end, float width, float damage)
    {
        if (sectorLanePrefab == null || PoolManager.Instance == null) return null;
        var instance = PoolManager.Instance.Get(sectorLanePrefab.gameObject, start, Quaternion.identity);
        var lane = instance.GetComponent<SectorPartitionLane>();
        lane.Configure(start, end, width, damage, laserDamageInterval);
        lane.SetEmpowerment(sectorEnergyBlend);
        return lane;
    }
    private void ReturnSectorLane(ref SectorPartitionLane lane)
    {
        if (lane == null) return;
        lane.Clear();
        if (PoolManager.Instance != null) PoolManager.Instance.Release(lane.gameObject);
        else lane.gameObject.SetActive(false);
        lane = null;
    }
    private void ClearSectorAttacks()
    {
        sectorDirectBurstActive = false; SectorDirectBurstStage = SectorBurstStage.Idle;
        SectorSweepRepeat = 0;
        ClearSectorRectangles();
        ClearSectorSupport();
        ReturnSectorLane(ref sectorRightBurstWarning);
        sectorRotating = false; sectorSpokeCount = 0;
        sectorRotationModeCommitted = false;
        sectorWeaponFacingLocked = false; sectorBurstMuzzle = null;
        sectorEscalationReady = false; sectorAddedSpokeExtension = 1;
        SetSectorChargeFeedback(0);
        for (int i = 0; i < sectorFlashes.Length; i++) ClearSectorFlash(i);
        for (int i = 0; i < sectorLanes.Length; i++) ReturnSectorLane(ref sectorLanes[i]);
        ReturnSectorLane(ref precisionWarning);
        SetSectorStage(SectorStage.Stopped);
    }
    private void SetSectorStage(SectorStage stage)
    {
        CurrentSectorStage = stage;
        if (!useSectorControlSequence || sectorBody == null) return;
        if (SectorEmpowered) { sectorBody.sprite = sectorPhase2Sprite != null ? sectorPhase2Sprite : sectorIdleSprite; return; }
        sectorBody.sprite = stage == SectorStage.Idle || stage == SectorStage.Recovery || stage == SectorStage.Stopped || stage == SectorStage.Suppression
            ? sectorIdleSprite : sectorChargedSprite;
    }
    private void ApplySectorContainment(BossArenaLaserWall wall, float length)
    {
        if (!useSectorControlSequence || sectorContainmentSprite == null) return;
        if (sectorBarrierVisualPrefab == null || PoolManager.Instance == null) return;
        wall.SetPresentationVisible(false);
        var visual = PoolManager.Instance.Get(sectorBarrierVisualPrefab.gameObject, wall.transform.position, wall.transform.rotation).GetComponent<SectorBarrierVisual>();
        visual.Configure(wall, length);
        for (int i = 0; i < sectorBarrierVisuals.Length; i++)
            if (sectorBarrierVisuals[i] == null) { sectorBarrierVisuals[i] = visual; break; }
    }

    private void UpdateSectorCameraFraming()
    {
        if (!useSectorControlSequence || player == null || phase2GungeonCamera == null) return;
        // Reuse the existing owner-scoped gameplay profile; cinematic transition
        // and Sniper zoom remain owned by their existing systems.
        var camera = phase2GungeonCamera.GameplayCamera;
        float baseSize = phase2CameraZoomController != null ? phase2CameraZoomController.BaseOrthographicSize : 4.21875f;
        ResolveSectorCombatFraming(player.position, transform.position, camera != null ? camera.aspect : 16f / 9f,
            baseSize, out Vector2 offset, out float size);
        sectorCameraProfileHeld = phase2GungeonCamera.AcquireGameplayFramingProfile(this, offset, .35f, size / baseSize, false);
    }
    // Fit the two actors only, not the whole arena. Saved camera smoothing and
    // cinematic/Sniper ownership remain authoritative. Padding reserves the HUD.
    public static void ResolveSectorCombatFraming(Vector2 playerPosition, Vector2 bossPosition,
        float aspect, float baseSize, out Vector2 offset, out float size)
    {
        Vector2 lo = Vector2.Min(playerPosition - Vector2.one * .7f, bossPosition - Vector2.one * 2.1f);
        Vector2 hi = Vector2.Max(playerPosition + Vector2.one * .7f, bossPosition + Vector2.one * 2.1f);
        Vector2 half = (hi - lo) * .5f;
        size = Mathf.Max(baseSize, Mathf.Max(half.y / .8f, half.x / (.9f * Mathf.Max(.1f, aspect))));
        offset = (lo + hi) * .5f - playerPosition;
    }
    private void ReleaseSectorCameraFraming()
    {
        if (sectorCameraProfileHeld && phase2GungeonCamera != null)
            phase2GungeonCamera.ReleaseGameplayFramingProfile(this, false);
        sectorCameraProfileHeld = false;
    }
}
