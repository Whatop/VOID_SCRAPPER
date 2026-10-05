using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    public enum SectorMainAttack { Sweep, Missiles, RectangularAoE, RotatingLaser }
    public enum SectorBurstStage { Idle, Aim, Commit, Fire }
    public SectorBurstStage SectorDirectBurstStage { get; private set; }
    public int SectorDirectBurstShots { get; private set; }
    public const float SectorDirectAimTime = .18f, SectorDirectCommitTime = .08f, SectorDirectShotInterval = .12f;
    private bool sectorDirectBurstActive;
    private Vector2 sectorDirectLeftDirection, sectorDirectRightDirection;

    public static SectorMainAttack SectorMainAttackForCycle(int cycle, bool escalated)
    {
        int slot = Mathf.Max(0, cycle) % (escalated ? 3 : 4);
        if (slot == 0) return SectorMainAttack.Sweep;
        if (slot == 1) return SectorMainAttack.Missiles;
        return !escalated && slot == 2 ? SectorMainAttack.RectangularAoE : SectorMainAttack.RotatingLaser;
    }
    public static int SectorDirectShotCount(bool escalated, bool duringLaser) => duringLaser ? 3 : escalated ? 5 : 4;

    private void AimSectorDirectBurst(float progress, bool commit)
    {
        Vector2 target = player != null ? (Vector2)player.position : (Vector2)transform.position + Vector2.down * 5;
        Vector2 left = (target - (Vector2)sectorLeftMuzzle.position).normalized;
        Vector2 right = (target - (Vector2)sectorRightMuzzle.position).normalized;
        precisionWarning?.SetEndpoints(sectorLeftMuzzle.position, (Vector2)sectorLeftMuzzle.position + left * 4);
        sectorRightBurstWarning?.SetEndpoints(sectorRightMuzzle.position, (Vector2)sectorRightMuzzle.position + right * 4);
        precisionWarning?.Warn(progress); sectorRightBurstWarning?.Warn(progress);
        if (!commit) return;
        sectorDirectLeftDirection = left; sectorDirectRightDirection = right;
        sectorWeaponFacingLocked = true;
    }
    private IEnumerator SectorDirectBurstRoutine(bool escalated, bool duringLaser)
    {
        if (sectorDirectBurstActive || sectorLeftMuzzle == null || sectorRightMuzzle == null) yield break;
        // The support entry itself rejects every unapproved combination.
        if (duringLaser ? !escalated || !sectorRotating : sectorSpokeCount != 0) yield break;
        if (SectorActiveMissiles != 0 || sectorRectangles != null ||
            CurrentSectorStage == SectorStage.SweepForward || CurrentSectorStage == SectorStage.SweepCommit || CurrentSectorStage == SectorStage.SweepAim) yield break;
        sectorDirectBurstActive = true; SectorDirectBurstShots = 0;
        if (!duringLaser) SetSectorStage(SectorStage.Suppression);
        sectorWeaponFacingLocked = false;
        SectorDirectBurstStage = SectorBurstStage.Aim;
        precisionWarning = RentSectorLane(sectorLeftMuzzle.position, sectorLeftMuzzle.position, .08f, 0);
        sectorRightBurstWarning = RentSectorLane(sectorRightMuzzle.position, sectorRightMuzzle.position, .08f, 0);
        float elapsed = 0;
        while (elapsed < SectorDirectAimTime)
        { AimSectorDirectBurst(elapsed / SectorDirectAimTime, false); yield return null; elapsed += Time.deltaTime; }
        AimSectorDirectBurst(1, true); SectorDirectBurstStage = SectorBurstStage.Commit;
        yield return new WaitForSeconds(SectorDirectCommitTime);
        ReturnSectorLane(ref precisionWarning); ReturnSectorLane(ref sectorRightBurstWarning);
        SectorDirectBurstStage = SectorBurstStage.Fire;
        int count = SectorDirectShotCount(escalated, duringLaser);
        for (int shot = 0; shot < count; shot++)
        {
            bool right = shot % 2 != 0;
            sectorBurstMuzzle = right ? sectorRightMuzzle : sectorLeftMuzzle;
            sectorBurstDirection = right ? sectorDirectRightDirection : sectorDirectLeftDirection;
            DispatchSectorSuppressionShot(); SectorDirectBurstShots++;
            if (shot + 1 < count) yield return new WaitForSeconds(SectorDirectShotInterval);
        }
        sectorWeaponFacingLocked = false; sectorBurstMuzzle = null;
        sectorDirectBurstActive = false; SectorDirectBurstStage = SectorBurstStage.Idle;
    }
}
