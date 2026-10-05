using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    [SerializeField] private SectorBossShieldPresentation sectorShield;
    private bool sectorIntroProtected;
    public const float SectorIntroContainmentSettle = .20f;
    public bool SectorIntroProtected => sectorIntroProtected;
    public bool SectorShieldVisible => sectorShield != null && sectorShield.IsProtectedVisualVisible;
    public bool SectorShieldReleasing => sectorShield != null && sectorShield.IsReleaseVisible;
    public bool IsSectorShieldRenderer(SpriteRenderer visual) => sectorShield != null && visual.transform.IsChildOf(sectorShield.transform);
    public void BeginSectorIntroProtection()
    { if (useSectorControlSequence) sectorIntroProtected = true; }
    public void ShowSectorIntroShield()
    { if (sectorIntroProtected) sectorShield?.ShowProtected(false); }
    public void SampleSectorIntroShield(float elapsed)
    { if (sectorIntroProtected) sectorShield?.SampleProtected(elapsed); }
    public IEnumerator ReleaseSectorIntroShieldRoutine()
    {
        if (!useSectorControlSequence) yield break;
        float elapsed = 0;
        while (elapsed < SectorBossShieldPresentation.ReleaseDuration)
        { sectorShield?.SampleRelease(elapsed); yield return null; elapsed += Time.unscaledDeltaTime; }
        sectorShield?.Clear();
        // Core still owns input/HUD handoff. OnEnable ends intro protection.
    }
    private void ClearSectorShield()
    { sectorIntroProtected = false; sectorShield?.Clear(); }
}
