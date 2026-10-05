using UnityEngine;

public partial class BossPatternController
{
    [Header("Region A - Empowered energy (chassis stays neutral)")]
    [SerializeField] private LineRenderer[] sectorEnergyAccents;
    [Tooltip("Optional future authored Phase 2 body. Empty uses the existing chassis and energy accents.")]
    [SerializeField] private Sprite sectorPhase2Sprite;
    private float sectorEnergyBlend;
    private bool sectorEnergyStopped;
    public static Color SectorGreen => new Color(.45f, 1f, .6f, 1f);
    public static Color SectorPurple => new Color(.82f, .27f, 1f, 1f);
    public Color SectorEnergyColor => Color.Lerp(SectorGreen, SectorPurple, sectorEnergyBlend);
    public bool SectorEmpowered => sectorEnergyBlend >= .999f;
    public float SectorSignedAngularSpeed => Mathf.Max(1f, sectorAngularSpeed) * (CurrentSectorRotationMode == SectorRotationMode.Purple ? -1f : 1f);

    private void ResetSectorIdentity()
    {
        sectorEnergyStopped = false;
        SetSectorEnergy(0);
    }
    private void SetSectorEnergy(float blend)
    {
        sectorEnergyBlend = Mathf.Clamp01(blend);
        RefreshSectorEnergyAccents(1, 0);
        if (SectorEmpowered && sectorBody != null)
            sectorBody.sprite = sectorPhase2Sprite != null ? sectorPhase2Sprite : sectorIdleSprite;
    }
    private void RefreshSectorEnergyAccents(float opacity, float charge)
    {
        if (!useSectorControlSequence || sectorEnergyAccents == null) return;
        // Finite transition/charge samples only. These saved local lines follow the
        // existing visual root; no new clock, material instance, texture or collider.
        Color color = Color.Lerp(SectorEnergyColor, Color.white, Mathf.Clamp01(charge) * .25f);
        color.a = sectorEnergyBlend * opacity;
        for (int i = 0; i < sectorEnergyAccents.Length; i++)
        {
            var accent = sectorEnergyAccents[i]; if (accent == null) continue;
            accent.enabled = !sectorEnergyStopped && color.a > 0;
            accent.startColor = accent.endColor = color;
        }
    }
    private void StopSectorIdentity()
    {
        sectorEnergyStopped = true;
        RefreshSectorEnergyAccents(0, 0);
    }
}
