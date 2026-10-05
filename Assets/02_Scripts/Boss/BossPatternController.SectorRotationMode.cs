using UnityEngine;

public partial class BossPatternController
{
    public enum SectorRotationMode { Green, Purple }
    [SerializeField, Range(0, 1)] private float sectorPurpleRotationProbability = .5f;
    private bool sectorFirstPhase2RotationPending = true, sectorRotationModeCommitted;
    public SectorRotationMode CurrentSectorRotationMode { get; private set; }
    public bool FirstPhase2RotationPending => sectorFirstPhase2RotationPending;
    public int SectorForcedPurpleRotations { get; private set; }
    public int SectorRandomRotationSelections { get; private set; }
    public float SectorPurpleRotationProbability => sectorPurpleRotationProbability;
    public static SectorRotationMode SectorModeForRoll(float roll, float purpleProbability) =>
        roll < purpleProbability ? SectorRotationMode.Purple : SectorRotationMode.Green;
    private void ResetSectorRotationModes()
    {
        sectorFirstPhase2RotationPending = true; sectorRotationModeCommitted = false;
        CurrentSectorRotationMode = SectorRotationMode.Green;
        SectorForcedPurpleRotations = SectorRandomRotationSelections = 0;
    }
    private void CommitSectorRotationMode(bool escalated)
    {
        if (!escalated) CurrentSectorRotationMode = SectorRotationMode.Green;
        else if (sectorFirstPhase2RotationPending)
        {
            sectorFirstPhase2RotationPending = false;
            CurrentSectorRotationMode = SectorRotationMode.Purple; SectorForcedPurpleRotations++;
        }
        else
        {
            CurrentSectorRotationMode = SectorModeForRoll(Random.value, sectorPurpleRotationProbability);
            SectorRandomRotationSelections++;
        }
        sectorRotationModeCommitted = true;
    }
    private float SectorBeamEmpowerment => sectorRotationModeCommitted
        ? (CurrentSectorRotationMode == SectorRotationMode.Purple ? 1 : 0) : sectorEnergyBlend;
}
