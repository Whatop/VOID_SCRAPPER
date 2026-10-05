using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    [Header("Region A - Standalone core attacks")]
    [SerializeField] private Transform sectorLeftMissileLauncher, sectorRightMissileLauncher, sectorRearMissileLauncher;
    [SerializeField] private SectorRectangularAoE sectorRectangularPrefab;
    [SerializeField, Min(.1f)] private float sectorSweepAimTime = .5f;
    [SerializeField, Min(.02f)] private float sectorSweepStepTime = .12f;
    // Retained serialized compatibility only; core ejection releases all six together.
    [SerializeField, HideInInspector] private float sectorMissileGroupDelay = .16f;
    [SerializeField, Min(.5f)] private float sectorRectangleWarningTime = 1.35f;
    [SerializeField, Min(.5f)] private float sectorRectangleBandWidth = 1.2f;
    [SerializeField, HideInInspector] private int sectorRectangleMaxBands = 4; // Legacy serialization; arena bounds now own coverage.
    private SectorRectangularAoE sectorRectangles;
    public Vector2 SectorCommittedAim => sectorBurstDirection;
    public const float SectorSweepHalfArc = 40f, SectorSweepCrossArc = 10f;
    public const int SectorSweepSteps = 6;
    private Vector2 sectorSweepLeftBasis, sectorSweepRightBasis;
    private WaitForSeconds sectorSweepStepWait;
    public const float SectorMissileWarningTime = .65f;
    public const float SectorRectangleReleaseTime = .24f;
    public const float SectorSweepReAimTime = .18f, SectorSweepCommitTime = .08f;
    public int SectorSweepRepeat { get; private set; }

    private Transform SectorMissileLauncher(int group) => group == 0 ? sectorLeftMissileLauncher
        : group == 1 ? sectorRightMissileLauncher : sectorRearMissileLauncher;

    private IEnumerator SectorRotationRoutine(bool escalated)
    {
        CommitSectorRotationMode(escalated);
        sectorMorph = 1; sectorAddedSpokeExtension = 1; sectorWarningProgress = 0;
        EnsureSectorSpokes(escalated ? 6 : 4);
        // Relay reconnect is presentation only. Every combat rotation has its
        // own complete polarity warning, including the forced first Purple.
        {
            SetSectorStage(SectorStage.Warning);
            AudioManager.PlayAt(SoundEventIds.BossLaserWarning, transform.position);
            float warning = 0;
            while (warning < laserTelegraphTime)
            { sectorWarningProgress = warning / laserTelegraphTime; yield return null; warning += Time.deltaTime; }
        }
        sectorEscalationReady = false;
        sectorActiveStart = Time.time; sectorRotating = true; SetSectorStage(SectorStage.Partition);
        AudioManager.PlayAt(SoundEventIds.BossLaserLoop, transform.position);
        float duration = escalated ? laserDurationPhase2 : laserDurationPhase1;
        int burst = 0;
        while (Time.time - sectorActiveStart < duration)
        {
            // The same coroutine owns the support windows. The rotation clock
            // continues during aim/fire, and its authored end time never extends.
            float elapsed = Time.time - sectorActiveStart;
            if (escalated && burst < 2 && elapsed >= .7f + burst * 1.0f && elapsed + .62f < duration)
            {
                burst++;
                yield return SectorDirectBurstRoutine(true, true);
            }
            else yield return null;
        }
    }

    private IEnumerator SectorSweepRoutine()
    {
        if (sectorSweepStepWait == null) sectorSweepStepWait = new WaitForSeconds(sectorSweepStepTime);
        int repeats = SectorEscalated ? 3 : 1;
        for (int sweep = 0; sweep < repeats; sweep++)
        {
            SectorSweepRepeat = sweep + 1;
            sectorWeaponFacingLocked = false;
            SetSectorStage(SectorStage.SweepAim);
            precisionWarning = RentSectorLane(sectorLeftMuzzle.position, sectorLeftMuzzle.position, .08f, 0);
            sectorRightBurstWarning = RentSectorLane(sectorRightMuzzle.position, sectorRightMuzzle.position, .08f, 0);
            float elapsed = 0, aimTime = sweep == 0 ? sectorSweepAimTime : SectorSweepReAimTime;
            while (elapsed < aimTime)
            {
                UpdateSectorSweepWarnings(ResolveDirectionToPlayer(transform.position), elapsed / aimTime);
                yield return null; elapsed += Time.deltaTime;
            }
            CommitSectorSweep();
            SetSectorStage(SectorStage.SweepCommit);
            UpdateSectorSweepWarnings(sectorBurstDirection, 1);
            yield return new WaitForSeconds(SectorSweepCommitTime);
            ReturnSectorLane(ref precisionWarning); ReturnSectorLane(ref sectorRightBurstWarning);
            SetSectorStage(SectorStage.SweepForward);
            for (int step = 0; step < SectorSweepSteps; step++)
            {
                DispatchSectorSweepPair(step);
                if (step + 1 < SectorSweepSteps) yield return sectorSweepStepWait;
            }
            if (sweep + 1 < repeats)
            { SetSectorStage(SectorStage.SweepRecovery); yield return new WaitForSeconds(Mathf.Max(.28f, sectorBurstGap)); }
        }
        sectorWeaponFacingLocked = false;
        // Let the last shots finish their existing lifetime before another attack.
        yield return new WaitForSeconds(projectileDefinition != null ? projectileDefinition.LifeTime : 2f);
    }
    private void UpdateSectorSweepWarnings(Vector2 aim, float progress)
    {
        Vector2 left = SectorSweepDirection(sectorWeaponFacingLocked ? sectorSweepLeftBasis : ResolveDirectionToPlayer(sectorLeftMuzzle.position), 0, false);
        Vector2 right = SectorSweepDirection(sectorWeaponFacingLocked ? sectorSweepRightBasis : ResolveDirectionToPlayer(sectorRightMuzzle.position), 0, true);
        precisionWarning?.SetEndpoints(sectorLeftMuzzle.position, (Vector2)sectorLeftMuzzle.position + left * 4);
        sectorRightBurstWarning?.SetEndpoints(sectorRightMuzzle.position, (Vector2)sectorRightMuzzle.position + right * 4);
        precisionWarning?.Warn(progress); sectorRightBurstWarning?.Warn(progress);
    }
    private void CommitSectorSweep()
    {
        // No velocity prediction: the spatial sweep supplies pressure without auto-leading.
        sectorBurstDirection = ResolveDirectionToPlayer(transform.position);
        // The same frozen target point supplies fixed muzzle parallax. Step 4
        // crosses that point; step 5 goes beyond it. No center-distance blind spot.
        Vector2 target = player != null ? (Vector2)player.position : (Vector2)transform.position + sectorBurstDirection * 5;
        sectorSweepLeftBasis = (target - (Vector2)sectorLeftMuzzle.position).normalized;
        sectorSweepRightBasis = (target - (Vector2)sectorRightMuzzle.position).normalized;
        sectorWeaponFacingLocked = true;
    }
    public static float SectorSweepAngle(int step, bool right) => Mathf.Lerp(
        right ? SectorSweepHalfArc : -SectorSweepHalfArc, right ? -SectorSweepCrossArc : SectorSweepCrossArc,
        Mathf.Clamp(step, 0, SectorSweepSteps - 1) / (float)(SectorSweepSteps - 1));
    // Weapon-local angles are clockwise from the chassis +Y forward axis.
    // Unity's Quaternion angle is counter-clockwise; converting here keeps the
    // actual left/right muzzle streams OUTSIDE at commit, then through the target.
    public static Vector2 SectorSweepDirection(Vector2 committed, int step, bool right) =>
        Quaternion.Euler(0, 0, -SectorSweepAngle(step, right)) * committed;
    private void DispatchSectorSweepPair(int step)
    {
        Vector2 committed = sectorBurstDirection;
        for (int side = 0; side < 2; side++)
        {
            Vector2 basis = side == 0 ? sectorSweepLeftBasis : sectorSweepRightBasis;
            sectorBurstDirection = SectorSweepDirection(basis.sqrMagnitude > .001f ? basis : committed, step, side == 1);
            sectorBurstMuzzle = side == 0 ? sectorLeftMuzzle : sectorRightMuzzle;
            DispatchSectorSuppressionShot();
        }
        sectorBurstDirection = committed;
    }

    private IEnumerator SectorMissileRoutine()
    {
        sectorWeaponFacingLocked = true;
        Vector2 aim = ResolveDirectionToPlayer(transform.position);
        SetSectorStage(SectorStage.MissileWarning);
        Vector2 origin = ResolveSectorChargePosition();
        precisionWarning = RentSectorLane(origin, origin + aim * 3.5f, .1f, 0);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, origin);
        float elapsed = 0;
        while (elapsed < SectorMissileWarningTime)
        {
            precisionWarning?.Warn(elapsed / SectorMissileWarningTime);
            SetSectorChargeFeedback(elapsed / SectorMissileWarningTime);
            yield return null; elapsed += Time.deltaTime;
        }
        ReturnSectorLane(ref precisionWarning); SetSectorChargeFeedback(0);
        SetSectorStage(SectorStage.MissileLaunch);
        BeginSectorCoreGeneration();
        yield return new WaitForSeconds(.06f); // Authored white flash / six-port release.
        for (int index = 0; index < 6; index++) LaunchSectorMissile(index, aim);
        sectorWeaponFacingLocked = false; SetSectorStage(SectorStage.MissileFlight);
        while (SectorActiveMissiles > 0) yield return null;
    }

    private IEnumerator AlternatingRectangularAoERoutine()
    {
        if (SectorEscalated || sectorRectangularPrefab == null || PoolManager.Instance == null) yield break;
        StopMoving(); // Snapshot geometry after cancelling any recovery velocity.
        Rect arena = SectorRectangleArenaBounds;
        Vector2 center = arena.center;
        sectorRectangles = PoolManager.Instance.Get(sectorRectangularPrefab.gameObject, center, Quaternion.identity).GetComponent<SectorRectangularAoE>();
        sectorRectangles.Configure(center, arena.size * .5f, sectorRectangleBandWidth, laserDamage * GetDamageMultiplier(), player != null ? player.GetComponent<PlayerHealth>() : null);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, ResolveSectorChargePosition());
        for (int phase = 0; phase < 2; phase++)
        {
            SetSectorStage(phase == 0 ? SectorStage.RectWarningA : SectorStage.RectWarningB);
            float elapsed = 0;
            while (elapsed < sectorRectangleWarningTime)
            {
                sectorRectangles.Warn(phase, elapsed / sectorRectangleWarningTime);
                if (phase == 1) sectorRectangles.ReleaseVisual(0, elapsed / SectorRectangleReleaseTime);
                yield return null; elapsed += Time.deltaTime;
            }
            SetSectorStage(phase == 0 ? SectorStage.RectExplosionA : SectorStage.RectExplosionB);
            sectorRectangles.Explode(phase);
            // B warning begins on the very next frame while A's non-damaging flash fades.
            yield return null;
        }
        float release = 0;
        while (release < SectorRectangleReleaseTime)
        { sectorRectangles.ReleaseVisual(1, release / SectorRectangleReleaseTime); yield return null; release += Time.deltaTime; }
        ClearSectorRectangles();
    }
    public Rect SectorRectangleArenaBounds
    {
        get
        {
            Vector2 half = GetEffectiveHalfExtents();
            Vector2 lo = arenaCenter - half, hi = arenaCenter + half;
            // Phase 1's actual solid wall inner faces are the playable perimeter.
            // Center and dimensions come from containment, never the moving boss.
            foreach (var wall in boundaryLaserWalls)
            {
                if (wall == null || !wall.activeInHierarchy || !wall.TryGetComponent<BoxCollider2D>(out var box) || !box.enabled) continue;
                Bounds b = box.bounds;
                if (b.size.x < b.size.y)
                { if (b.center.x < arenaCenter.x) lo.x = b.max.x; else hi.x = b.min.x; }
                else
                { if (b.center.y < arenaCenter.y) lo.y = b.max.y; else hi.y = b.min.y; }
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }
    }
    private void ClearSectorRectangles()
    {
        if (sectorRectangles == null) return;
        sectorRectangles.Clear();
        if (PoolManager.Instance != null) PoolManager.Instance.Release(sectorRectangles.gameObject);
        else sectorRectangles.gameObject.SetActive(false);
        sectorRectangles = null;
    }
}
