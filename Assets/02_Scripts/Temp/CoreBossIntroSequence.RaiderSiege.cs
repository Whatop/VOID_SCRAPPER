using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class CoreBossIntroSequence
{
    private bool raiderCommanderIntro;
    private readonly List<BossArenaLaserWall> raiderLockdownWalls = new List<BossArenaLaserWall>(4);
    private readonly List<RaiderLockdownBoundaryPresentation> raiderLockdownViews = new List<RaiderLockdownBoundaryPresentation>(4);
    private ExpandingPulseRing2D raiderLockdownPulse;
    private Coroutine raiderLockdownRelease;
    public bool RaiderLockdownForming { get; private set; }
    public bool RaiderLockdownReleasing { get; private set; }
    public float RaiderLockdownProgress { get; private set; }
    public const float RaiderLockdownFormationDuration = 1.05f;
    public const float RaiderLockdownReleaseDuration = .55f;

    private IEnumerator PlayRaiderLockdownRoutine(Vector2 center)
    {
        // The Commander issues the lock. No carriers, drones or moving arena.
        Material material = raiderBarrierMaterial != null ? raiderBarrierMaterial : Resources.Load<Material>("VFX/M_SpriteWhiteFlash");
        raiderLockdownPulse = ExpandingPulseRing2D.Spawn(spawnedBoss.transform.position,
            new Color(1, .32f, .06f, .65f), .28f, 1.2f, 3.2f, .07f, .025f,
            40, laserSortingLayerName, laserSortingOrder - 1, material);
        yield return Wait(.28f);
        if (ShouldAbortIntro()) yield break;
        ActivateRaiderBarrier(center);
        RaiderLockdownForming = true;
        float elapsed = 0;
        while (elapsed < RaiderLockdownFormationDuration && !ShouldAbortIntro())
        {
            SampleRaiderLockdownFormation(Mathf.SmoothStep(0, 1, elapsed / RaiderLockdownFormationDuration));
            yield return null; elapsed += Time.unscaledDeltaTime;
        }
        if (ShouldAbortIntro()) yield break;
        SampleRaiderLockdownFormation(1);
        // Input remains locked until all four completed edges are solid together.
        for (int i = 0; i < raiderLockdownWalls.Count; i++) raiderLockdownWalls[i].SetFormationProgress(1);
        RaiderLockdownForming = false;
        yield return Wait(.18f);
    }
    private void SampleRaiderLockdownFormation(float progress)
    {
        RaiderLockdownProgress = progress;
        for (int i = 0; i < raiderLockdownViews.Count; i++)
            if (raiderLockdownViews[i] != null) raiderLockdownViews[i].SampleFormation(progress);
    }
    private IEnumerator ReleaseRaiderLockdownRoutine()
    {
        RaiderLockdownReleasing = true;
        // Gameplay releases immediately on death, as before. Only the visible
        // shutdown persists: no invisible collision or damage during release.
        for (int i = 0; i < raiderLockdownWalls.Count; i++)
            if (raiderLockdownWalls[i] != null) raiderLockdownWalls[i].SetFormationProgress(0);
        float elapsed = 0;
        while (elapsed < RaiderLockdownReleaseDuration)
        {
            for (int i = 0; i < raiderLockdownViews.Count; i++)
                if (raiderLockdownViews[i] != null) raiderLockdownViews[i].SampleRelease(elapsed / RaiderLockdownReleaseDuration);
            yield return null; elapsed += Time.unscaledDeltaTime;
        }
        raiderLockdownRelease = null;
        DestroySpawnedWalls();
    }
    private void ClearRaiderLockdown()
    {
        if (raiderLockdownRelease != null) StopCoroutine(raiderLockdownRelease);
        raiderLockdownRelease = null;
        if (raiderLockdownPulse != null) Destroy(raiderLockdownPulse.gameObject);
        raiderLockdownPulse = null;
        raiderLockdownWalls.Clear(); raiderLockdownViews.Clear();
        RaiderLockdownForming = RaiderLockdownReleasing = false;
        RaiderLockdownProgress = 0;
    }
}
