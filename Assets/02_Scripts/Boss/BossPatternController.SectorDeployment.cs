using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    public enum SectorDeploymentGroup { Left, Right, Rear }
    [Header("Region A - Core ejection and containment presentation")]
    [SerializeField] private Transform sectorMissileCore;
    [SerializeField] private Vector2 sectorDeployLeftUpper = new Vector2(-1, .55f);
    [SerializeField] private Vector2 sectorDeployLeftLower = new Vector2(-1, -.2f);
    [SerializeField] private Vector2 sectorDeployRightUpper = new Vector2(1, .55f);
    [SerializeField] private Vector2 sectorDeployRightLower = new Vector2(1, -.2f);
    [SerializeField] private Vector2 sectorDeployRearLeft = new Vector2(-.45f, -1);
    [SerializeField] private Vector2 sectorDeployRearRight = new Vector2(.45f, -1);
    [SerializeField] private SectorSupportVfx sectorCoreGeneration;
    [SerializeField] private SectorBarrierVisual sectorBarrierVisualPrefab;
    private readonly SectorMissilePresentation[] sectorMissilePresentations = new SectorMissilePresentation[6];
    private readonly SectorBarrierVisual[] sectorBarrierVisuals = new SectorBarrierVisual[6];
    private float sectorCoreFlashBorn = -1;
    private bool sectorBarrierForming;
    public const float SectorMissileDeploymentTime = .22f;
    public const float SectorBarrierChargeTime = .08f, SectorBarrierGrowthEnd = .53f, SectorBarrierFormationTime = .75f;
    public float SectorBarrierProgress { get; private set; } = 1;
    public Vector2 SectorMissileCorePosition => sectorMissileCore != null ? (Vector2)sectorMissileCore.position : (Vector2)transform.position;
    public static SectorDeploymentGroup MissileGroup(int index) => (SectorDeploymentGroup)(Mathf.Clamp(index, 0, 5) / 2);
    public Vector2 SectorDeploymentDirection(int index)
    {
        Vector2 local = index == 0 ? sectorDeployLeftUpper : index == 1 ? sectorDeployLeftLower
            : index == 2 ? sectorDeployRightUpper : index == 3 ? sectorDeployRightLower
            : index == 4 ? sectorDeployRearLeft : sectorDeployRearRight;
        return sectorMissileCore != null ? ((Vector2)sectorMissileCore.TransformDirection(local)).normalized : local.normalized;
    }
    private void BeginSectorCoreGeneration()
    {
        sectorCoreFlashBorn = Time.time;
        sectorCoreGeneration?.Sample(0, false, SectorEmpowered);
    }
    private void ClearSectorDeployment()
    {
        sectorCoreFlashBorn = -1; sectorCoreGeneration?.Clear();
        sectorBarrierForming = false;
        for (int i = 0; i < sectorMissilePresentations.Length; i++) sectorMissilePresentations[i] = null;
    }
    private void ReleaseSectorBarrierVisuals()
    {
        for (int i = 0; i < sectorBarrierVisuals.Length; i++)
        { if (sectorBarrierVisuals[i] != null) sectorBarrierVisuals[i].Release(); sectorBarrierVisuals[i] = null; }
        SectorBarrierProgress = 0; sectorBarrierForming = false;
    }
    private void SetSectorBarrierFormation(float elapsed)
    {
        SectorBarrierProgress = Mathf.Clamp01((elapsed - SectorBarrierChargeTime) / (SectorBarrierGrowthEnd - SectorBarrierChargeTime));
        for (int i = 0; i < sectorBarrierVisuals.Length; i++) sectorBarrierVisuals[i]?.SetFormation(SectorBarrierProgress, elapsed);
    }
    public IEnumerator PlaySectorBarrierFormationRoutine()
    {
        if (!useSectorControlSequence) yield break;
        ActivatePhase1BoundaryLasers();
        float elapsed = 0; sectorBarrierForming = true;
        SetSectorBarrierFormation(0);
        // Driven only by CoreBossIntroSequence while its input lock is held.
        while (sectorBarrierForming && elapsed < SectorBarrierFormationTime)
        {
            SetSectorBarrierFormation(elapsed);
            yield return null; elapsed += Time.unscaledDeltaTime;
        }
        if (!sectorBarrierForming) yield break;
        SetSectorBarrierFormation(SectorBarrierFormationTime);
        sectorBarrierForming = false;
    }
}
