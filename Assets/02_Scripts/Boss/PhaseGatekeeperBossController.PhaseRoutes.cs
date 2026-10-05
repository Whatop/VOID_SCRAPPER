using System.Collections;
using UnityEngine;

public sealed partial class PhaseGatekeeperBossController
{
    public enum RouteStage { Stopped, Idle, PortalWarning, Acquiring, Committed, Entering, Redirected, PrecisionFire, PortalClosing, Recovery, Exposed, Escalation, Reposition }
    [Header("Region C — phase-route sequence")]
    [SerializeField] private bool usePhaseRouteSequence;
    [SerializeField] private Sprite phaseLockSprite, lensExposedSprite;
    [SerializeField] private PhaseCombatVfx phasePortalPrefab, precisionLockPrefab, phaseRedirectPrefab;
    [SerializeField] private Vector2 phaseArenaHalfSize = new Vector2(7f, 3.7f);
    [SerializeField, Min(.1f)] private float portalWarningDuration = .56f;
    [SerializeField, Min(.01f)] private float redirectTransferDelay = .18f;
    [SerializeField, Min(.01f)] private float phaseBeamWidth = .12f;
    [SerializeField, Min(.1f)] private float phaseEscalatedRecovery = .55f;
    [SerializeField, Min(.1f)] private float lensTransitionDuration = .8f;
    private PhaseCombatVfx entryPortal, exitPortal, precisionMarker, redirectFlash;
    private MeteorObstacle[] phaseMeteors;
    private Vector2 routeEntry, routeExit, routeEnd, routeOrigin;
    private RouteStage routeStage;
    private int lensEscalationCount, routeAttackCount;
    private bool portalPairActive;
    public static PhaseGatekeeperBossController ActivePhaseEncounter { get; private set; }
    public RouteStage CurrentRouteStage => routeStage;
    public bool UsesPhaseRoutes => usePhaseRouteSequence;
    public bool LensEscalated => phaseTwoActive;
    public int LensEscalationCount => lensEscalationCount;
    public int RouteAttackCount => routeAttackCount;
    public bool PortalPairActive => portalPairActive;
    public int PortalEndpointCount => (entryPortal != null ? 1 : 0) + (exitPortal != null ? 1 : 0);
    public Vector2 RouteEntry => routeEntry;
    public Vector2 RouteExit => routeExit;
    public Vector2 CommittedRouteTarget => lockedTargetPosition;
    public Vector2 CommittedRouteEnd => routeEnd;
    public Bounds RouteArenaBounds => currentArenaBounds;
    public BossLaserHazard RouteBeam => activeLaserHazards[0];

    private void ResetPhaseRouteSequence()
    { routeStage = RouteStage.Stopped; lensEscalationCount = 0; routeAttackCount = 0; portalPairActive = false; }

    private void BeginPhaseRouteArena()
    {
        ActivePhaseEncounter = this;
        Vector2 center = ((Vector2)transform.position + (Vector2)playerHealth.transform.position) * .5f;
        Bounds safe = cameraSafeBounds.size.x > 1 ? cameraSafeBounds : encounterBounds;
        Vector2 half = new Vector2(Mathf.Min(phaseArenaHalfSize.x, safe.extents.x - .5f), Mathf.Min(phaseArenaHalfSize.y, safe.extents.y - .5f));
        center.x = Mathf.Clamp(center.x, safe.min.x + half.x, safe.max.x - half.x);
        center.y = Mathf.Clamp(center.y, safe.min.y + half.y, safe.max.y - half.y);
        currentArenaBounds = new Bounds(center, new Vector3(half.x * 2, half.y * 2, 0));
        EnsureArenaPresentation(); UpdateArenaPresentation(currentArenaBounds);
        UpdatePlayerArenaConstraint(currentArenaBounds);
        // Preserve the existing solid walls/player constraint; only mute the line
        // presentation so it cannot be confused with a precision attack.
        for (int i = 0; i < arenaWalls.Length; i++)
        {
            arenaWalls[i].SetPresentationVisible(true);
            var line = arenaWalls[i].GetComponent<LineRenderer>();
            line.startWidth = line.endWidth = .06f;
            line.startColor = line.endColor = new Color(.32f, .46f, .55f, .8f);
            line.sortingOrder = -3;
        }
        if (reflectorPlates != null)
            for (int i = 0; i < reflectorPlates.Length; i++)
                if (reflectorPlates[i] != null) { reflectorPlates[i].SetGameplayEnabled(false); reflectorPlates[i].SetPresentationVisible(false); }
        phaseMeteors = FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);
        Bounds footprint = currentArenaBounds; footprint.Expand(new Vector3(3, 3, 20));
        for (int i = 0; i < phaseMeteors.Length; i++) phaseMeteors[i].PauseForSectorEncounter(footprint);
    }
    private void SetPhaseRouteBody(bool preparing)
    {
        if (bodyRenderer == null) return;
        Sprite sprite = preparing ? phaseLockSprite : state == EncounterState.Exposed || phaseTwoActive ? lensExposedSprite : idleSprite;
        if (sprite != null) bodyRenderer.sprite = sprite;
    }
    private IEnumerator RunPhaseRouteSequence()
    {
        expeditionHud?.SetRegionBossPresentation(this, true);
        // Region-C-only fixed framing uses the authored normal combat profile.
        specialCameraFocusHeld = gameplayCamera != null && gameplayCamera.TrySetOwnedCinematicFocus(this, currentArenaBounds.center);
        StopChargeSpriteAnimation(); RestoreRevealedPresentation();
        routeStage = RouteStage.Idle;
        yield return PhaseWait(.5f);
        while (!ShouldStopCombat())
        {
            for (int i = 0; i < LasersPerCycle && !ShouldStopCombat(); i++)
            {
                bool redirect = phaseTwoActive ? i != 1 : i == 1;
                yield return RunPhasePrecision(redirect);
                if (ShouldStopCombat()) yield break;
                lasersCompletedInCycle++; attackSequenceIndex++; routeAttackCount++;
            }
            if (ShouldStopCombat()) yield break;
            routeStage = RouteStage.Exposed;
            yield return RunExposedWindow(); // Same five-second health gate after three attacks.
            if (ShouldStopCombat()) yield break;
            lasersCompletedInCycle = 0;
            if (!phaseTwoActive)
            {
                routeStage = RouteStage.Escalation; state = EncounterState.Converging;
                ClearPhaseAttacks(); SetPhaseRouteBody(true);
                yield return PhaseWait(lensTransitionDuration);
                if (ShouldStopCombat()) yield break;
                phaseTwoActive = true; combatCyclePhase = CombatCyclePhase.Special; lensEscalationCount++;
                SetPhaseRouteBody(false);
            }
            yield return PhaseReposition();
        }
    }
    private IEnumerator PhaseWait(float duration)
    { float elapsed = 0; while (elapsed < duration && !ShouldStopCombat()) { yield return null; elapsed += Time.deltaTime; } }

    private IEnumerator PhaseReposition()
    {
        routeStage = RouteStage.Reposition; state = EncounterState.Decloaking;
        Vector3 start = transform.position;
        float side = (routeAttackCount / LasersPerCycle) % 2 == 0 ? -1 : 1;
        Vector3 destination = currentArenaBounds.center + new Vector3(side * 2.6f, 1.7f, 0);
        // Deliberate visible translation during a harmless window, not a random teleport.
        if (Vector2.Distance(destination, playerHealth.transform.position) < 2f) destination.x = currentArenaBounds.center.x - side * 2.6f;
        float elapsed = 0;
        while (elapsed < .65f && !ShouldStopCombat())
        { elapsed += Time.deltaTime; transform.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0, 1, elapsed / .65f)); yield return null; }
    }
    public static bool TryResolvePhasePair(Bounds bounds, Vector2 boss, Vector2 player, int sequence, out Vector2 entry, out Vector2 exit)
    {
        entry = exit = Vector2.zero; float bestEntry = -1, bestExit = -1;
        for (int i = 0; i < 4; i++)
        {
            int slot = (i + sequence) % 4;
            Vector2 offset = slot == 0 ? Vector2.left * 2.1f : slot == 1 ? Vector2.right * 2.1f : slot == 2 ? Vector2.down * 1.8f : Vector2.up * 1.8f;
            Vector2 point = ClampRoutePoint(bounds, boss + offset, .9f);
            float clearance = (point - player).sqrMagnitude;
            if ((point - boss).sqrMagnitude >= 1.44f && clearance >= 4f && clearance > bestEntry)
            { entry = point; bestEntry = clearance; }
        }
        if (bestEntry < 0) return false;
        for (int i = 0; i < 4; i++)
        {
            int slot = (i + sequence) % 4;
            Vector2 point = new Vector2(slot % 2 == 0 ? bounds.min.x + 1.2f : bounds.max.x - 1.2f, slot < 2 ? bounds.min.y + 1.2f : bounds.max.y - 1.2f);
            float clearance = (point - player).sqrMagnitude;
            if (clearance >= 9f && (point - boss).sqrMagnitude >= 4f && (point - entry).sqrMagnitude >= 9f && clearance > bestExit)
            { exit = point; bestExit = clearance; }
        }
        return bestExit >= 0;
    }
    private static Vector2 ClampRoutePoint(Bounds bounds, Vector2 point, float inset)
        => new Vector2(Mathf.Clamp(point.x, bounds.min.x + inset, bounds.max.x - inset), Mathf.Clamp(point.y, bounds.min.y + inset, bounds.max.y - inset));
    public static Vector2 ResolvePhaseEndpoint(Bounds bounds, Vector2 origin, Vector2 target, float maxRange)
    {
        Vector2 direction = target - origin; if (direction.sqrMagnitude < .0001f) direction = Vector2.down;
        direction.Normalize(); float distance = maxRange;
        if (Mathf.Abs(direction.x) > .0001f) distance = Mathf.Min(distance, ((direction.x > 0 ? bounds.max.x : bounds.min.x) - origin.x) / direction.x);
        if (Mathf.Abs(direction.y) > .0001f) distance = Mathf.Min(distance, ((direction.y > 0 ? bounds.max.y : bounds.min.y) - origin.y) / direction.y);
        return origin + direction * Mathf.Max(.1f, distance - .15f);
    }
    private PhaseCombatVfx GetPhaseVfx(PhaseCombatVfx prefab, Vector2 position)
    {
        if (prefab == null || PoolManager.Instance == null) return null;
        var instance = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity).GetComponent<PhaseCombatVfx>();
        instance.transform.localScale = prefab.transform.localScale; instance.Sample(0); return instance;
    }
    private void ReleasePhaseVfx(ref PhaseCombatVfx instance)
    {
        var old = instance; instance = null; if (old == null) return;
        old.Clear(); if (PoolManager.Instance != null) PoolManager.Instance.Release(old.gameObject); else old.gameObject.SetActive(false);
    }
    private IEnumerator RunPhasePrecision(bool redirect)
    {
        ClearPhaseAttacks(); SetDamageWindow(false); state = EncounterState.PreparingRoute;
        SetPhaseRouteBody(true); routeOrigin = ResolveLaserOrigin();
        if (redirect && TryResolvePhasePair(currentArenaBounds, routeOrigin, playerHealth.transform.position, routeAttackCount, out routeEntry, out routeExit))
        {
            entryPortal = GetPhaseVfx(phasePortalPrefab, routeEntry); exitPortal = GetPhaseVfx(phasePortalPrefab, routeExit);
            if (entryPortal == null || exitPortal == null) { ClearPhaseAttacks(); yield break; }
            routeStage = RouteStage.PortalWarning; float elapsed = 0;
            while (elapsed < portalWarningDuration && !ShouldStopCombat())
            { entryPortal.Sample(elapsed, 0, 8, true); exitPortal.Sample(elapsed, 0, 8, true); yield return null; elapsed += Time.deltaTime; }
            if (ShouldStopCombat()) yield break;
            entryPortal.Sample(portalWarningDuration, 0, 8, true); exitPortal.Sample(portalWarningDuration, 0, 8, true); portalPairActive = true;
        }
        else redirect = false;
        precisionMarker = GetPhaseVfx(precisionLockPrefab, playerHealth.transform.position);
        routeStage = RouteStage.Acquiring; state = EncounterState.Tracking;
        float tracking = phaseTwoActive ? specialTrackingDuration : normalTrackingDuration;
        float timer = 0;
        while (timer < tracking && !ShouldStopCombat())
        {
            predictedTargetPosition = ResolvePredictedPlayerTarget();
            if (precisionMarker != null) { precisionMarker.transform.position = predictedTargetPosition; precisionMarker.Sample(timer, 0, 0, true); }
            ShowPhaseRouteLines(redirect, predictedTargetPosition, false);
            yield return null; timer += Time.deltaTime;
        }
        if (ShouldStopCombat()) yield break;
        lockedTargetPosition = predictedTargetPosition; hasLockedTarget = true;
        routeEnd = ResolvePhaseEndpoint(currentArenaBounds, redirect ? routeExit : routeOrigin, lockedTargetPosition, laserRange);
        routeStage = RouteStage.Committed; state = EncounterState.TargetLocked;
        ShowPhaseRouteLines(redirect, lockedTargetPosition, true);
        yield return PhaseWait(laserLockDuration);
        if (ShouldStopCombat()) yield break;
        HideTelegraphChain(); ReleasePhaseVfx(ref precisionMarker);
        var instance = PoolManager.Instance.Get(laserHazardPrefab.gameObject, routeOrigin, Quaternion.identity);
        var beam = instance.GetComponent<BossLaserHazard>(); activeLaserHazards[0] = beam;
        beam.InitializeBetweenAttached(transform, routeOrigin, redirect ? routeEntry : routeEnd, phaseBeamWidth,
            laserFireDuration, laserDamage, laserDamageInterval, laserMaterial, activeLaserColor, laserSortingLayerName, -2);
        state = EncounterState.Firing; routeStage = redirect ? RouteStage.Entering : RouteStage.PrecisionFire;
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, routeOrigin);
        timer = 0; bool transferred = false;
        while (timer < laserFireDuration && !ShouldStopCombat())
        {
            if (redirect && !transferred && timer >= redirectTransferDelay)
            {
                transferred = beam.TryRedirectOnce(routeExit, routeEnd);
                if (transferred)
                {
                    redirectFlash = GetPhaseVfx(phaseRedirectPrefab, routeExit);
                    if (redirectFlash != null) redirectFlash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(routeEnd.y-routeExit.y, routeEnd.x-routeExit.x) * Mathf.Rad2Deg);
                    routeStage = RouteStage.Redirected;
                }
            }
            if (redirectFlash != null) redirectFlash.Sample(timer - redirectTransferDelay);
            yield return null; timer += Time.deltaTime;
        }
        DeactivateLaserHazards(); ReleasePhaseVfx(ref redirectFlash); portalPairActive = false;
        if (ShouldStopCombat()) yield break;
        if (entryPortal != null)
        {
            routeStage = RouteStage.PortalClosing; timer = 0;
            while (timer < .435f && !ShouldStopCombat())
            { entryPortal.Sample(timer, 8, 8); exitPortal.Sample(timer, 8, 8); yield return null; timer += Time.deltaTime; }
        }
        ClearPhaseAttacks(); SetPhaseRouteBody(false);
        routeStage = RouteStage.Recovery;
        yield return PhaseWait(phaseTwoActive ? phaseEscalatedRecovery : laserRecoveryDuration);
    }
    private void ShowPhaseRouteLines(bool redirect, Vector2 target, bool committed)
    {
        Vector2 output = redirect ? routeExit : routeOrigin;
        Vector2 end = ResolvePhaseEndpoint(currentArenaBounds, output, target, laserRange);
        for (int i = 0; i < 2; i++)
        {
            var line = telegraphLines[i]; line.enabled = i == 0 || redirect;
            if (!line.enabled) continue;
            Vector2 a = redirect && i == 0 ? routeOrigin : output;
            Vector2 b = redirect && i == 0 ? routeEntry : end;
            line.positionCount = 2; line.SetPosition(0, a); line.SetPosition(1, b);
            line.startWidth = line.endWidth = committed ? .055f : .035f;
            line.startColor = line.endColor = committed ? lockedTelegraphColor : telegraphColor;
            line.sortingOrder = -2;
        }
    }
    private void ClearPhaseAttacks()
    {
        DeactivateLaserHazards(); HideTelegraphChain(); portalPairActive = false;
        ReleasePhaseVfx(ref entryPortal); ReleasePhaseVfx(ref exitPortal); ReleasePhaseVfx(ref precisionMarker); ReleasePhaseVfx(ref redirectFlash);
    }
    private void EndPhaseRouteSequence()
    {
        ClearPhaseAttacks(); routeStage = RouteStage.Stopped;
        if (ActivePhaseEncounter == this) ActivePhaseEncounter = null;
        if (expeditionHud != null) expeditionHud.SetRegionBossPresentation(this, false);
        if (phaseMeteors != null)
            for (int i = 0; i < phaseMeteors.Length; i++) if (phaseMeteors[i] != null) phaseMeteors[i].ResumeAfterSectorEncounter();
        phaseMeteors = null;
    }
}
