using System.Collections;
using UnityEngine;

public partial class BossPatternController
{
    public enum SectorTransitionStage { None, Disconnect, Deploy, Convert, Reconnect, Complete, NetworkHold, BossFocus, ShieldRelease }
    public const float SectorDisconnectTime = .24f, SectorRelayDeployTime = .31f;
    public const float SectorConversionTime = .30f;
    public const float SectorNetworkHoldTime = .15f, SectorBossFocusTime = .45f;
    private bool sectorPhase2Pending, sectorTransitionRunning, sectorLaserDisconnecting;
    private float sectorDisconnectProgress, sectorConnectionProgress = 1;
    private readonly SectorRelayPresentation[] sectorRelayViews = new SectorRelayPresentation[6];
    public bool SectorPhase2Pending => sectorPhase2Pending;
    public bool SectorTransitionRunning => sectorTransitionRunning;
    public SectorTransitionStage SectorTransitionState { get; private set; }
    public int SectorTransitionCount { get; private set; }
    public float SectorTransitionElapsed { get; private set; }
    public int SectorRelayCount => phase1ManagerShips.Count + (phase2TopManagerShip != null ? 1 : 0) + (phase2BottomManagerShip != null ? 1 : 0);
    public int SectorPurpleRelayCount
    { get { int count = 0; for (int i = 0; i < 6; i++) if (sectorRelayViews[i] != null && sectorRelayViews[i].IsPurple) count++; return count; } }

    private void ResetSectorTransition()
    {
        ResetSectorRotationModes(); ClearSectorShield();
        sectorPhase2Pending = sectorTransitionRunning = sectorLaserDisconnecting = false;
        sectorConnectionProgress = 1; sectorDisconnectProgress = 0;
        SectorTransitionState = SectorTransitionStage.None; SectorTransitionCount = 0; SectorTransitionElapsed = 0;
        for (int i = 0; i < 6; i++) if (sectorRelayViews[i] != null) sectorRelayViews[i].ResetPresentation();
    }
    private void ReturnSectorRelay(LaserGuardianDrone relay)
    {
        if (relay == null) return;
        int index = relay.RuntimeIndex - 1;
        if (index >= 0 && index < 6) sectorRelayViews[index] = null;
        if (useSectorControlSequence && PoolManager.Instance != null && relay.GetComponent<SectorRelayPresentation>() != null)
            PoolManager.Instance.Release(relay.gameObject);
        else Destroy(relay.gameObject);
    }
    private IEnumerator FinishPendingSectorLaser()
    {
        if (!sectorPhase2Pending || sectorSpokeCount == 0) yield break;
        sectorRotating = false; sectorLaserDisconnecting = true; sectorDisconnectProgress = 0;
        UpdateSectorSpokes(0); // Collision stops at the first visible shutdown frame.
        float elapsed = 0;
        while (elapsed < SectorDisconnectTime)
        {
            sectorDisconnectProgress = elapsed / SectorDisconnectTime;
            yield return null; elapsed += Time.deltaTime;
        }
        sectorLaserDisconnecting = false;
    }
    private IEnumerator SectorPendingTransitionRoutine()
    {
        if (!sectorPhase2Pending || sectorTransitionRunning || phase2TransitionStarted || deathHandled) yield break;
        sectorPhase2Pending = false; sectorTransitionRunning = true; phase2TransitionStarted = true;
        SectorTransitionCount++; SectorTransitionElapsed = 0;
        casting = true; sectorWeaponFacingLocked = true; StopMoving();
        EnsurePhase1ManagersImmediate();
        // Retain the existing absorption HP budget and transition immunity.
        if (usePhase2ShieldTransition) ActivatePhase2Shield();
        if (phase2ShieldActive && !phase2ShieldDamageEnabled) sectorShield?.ShowProtected(true);
        ResolvePhase2PresentationReferences();
        GameObject playerObject = player != null ? player.gameObject : null;
        if (lockPlayerDuringPhase2Setup) LockPhase2PlayerInput(playerObject);
        playerObject?.GetComponent<PlayerHealth>()?.AddInvincibleTime(SectorDisconnectTime + SectorRelayDeployTime + sectorEscalationWarningTime + SectorNetworkHoldTime + SectorBossFocusTime + SectorBossShieldPresentation.ReleaseDuration + .15f);
        phase2GungeonCamera?.SetCinematicInputOffsetLocked(this, true);
        phase2GungeonCamera?.TryBeginOwnedCinematicFocusBlend(this, arenaCenter, .2f, null, out _);
        SampleSectorTransitionFraming();
        AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position); // Existing event only.

        SectorTransitionState = SectorTransitionStage.Disconnect;
        SetSectorStage(SectorStage.EscalationHold);
        float elapsed = 0;
        while (elapsed < SectorDisconnectTime)
        {
            SampleSectorTransitionFraming();
            for (int i = 0; i < 4; i++)
            {
                sectorBarrierVisuals[i]?.SampleDisconnect(elapsed / SectorDisconnectTime);
                var relay = phase1ManagerShips[i];
                sectorRelayViews[i]?.SampleDisconnect(elapsed, phase1ManagerShips[(i + 1) % 4].transform.position - relay.transform.position);
            }
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        DestroyBoundaryLaserWalls();
        for (int i = 0; i < 4; i++) sectorRelayViews[i]?.ClearEffects();

        SectorTransitionState = SectorTransitionStage.Deploy;
        Vector2[] positions = GetPhase2HexagonFinalPositionsCounterClockwise();
        phase2TopManagerShip = CreateLaserManagerShip("BossLaserManager_05_Top", 5, positions[0], GetManagerRotationForPosition(positions[0]), Color.white, runtimePhase2ManagerColor);
        phase2BottomManagerShip = CreateLaserManagerShip("BossLaserManager_06_Bottom", 6, positions[3], GetManagerRotationForPosition(positions[3]), Color.white, runtimePhase2ManagerColor);
        var slots = GetHexagonSlotsCounterClockwise();
        var starts = new Vector3[6]; for (int i = 0; i < 6; i++) starts[i] = slots[i].transform.position;
        elapsed = 0;
        while (elapsed < SectorRelayDeployTime)
        {
            SampleSectorTransitionFraming();
            float t = Mathf.SmoothStep(0, 1, elapsed / SectorRelayDeployTime);
            for (int i = 0; i < 6; i++)
            {
                slots[i].transform.position = Vector3.Lerp(starts[i], positions[i], t);
                slots[i].ApplySlotRotation(GetManagerRotationForPosition(positions[i]));
            }
            sectorRelayViews[4]?.SampleDeploy(elapsed); sectorRelayViews[5]?.SampleDeploy(elapsed);
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        for (int i = 0; i < 6; i++) slots[i].transform.position = positions[i];
        sectorRelayViews[4]?.SampleDeploy(SectorRelayDeployTime); sectorRelayViews[5]?.SampleDeploy(SectorRelayDeployTime);
        phase2TopBottomManagersSpawned = true;
        // Preserve a full authored escalation window with player control restored.
        RestorePhase2PlayerInput();
        SectorTransitionState = SectorTransitionStage.Convert;
        BeginSectorEscalationPulse(); elapsed = 0;
        while (elapsed < sectorEscalationWarningTime)
        {
            SampleSectorTransitionFraming();
            if (elapsed < SectorConversionTime)
            {
                SampleSectorEscalationFade(elapsed * (.34f / SectorConversionTime));
                for (int i = 0; i < 6; i++) sectorRelayViews[i]?.SampleConversion(elapsed);
            }
            else
            {
                float connectionAge = elapsed - SectorConversionTime;
                if (SectorTransitionState != SectorTransitionStage.Reconnect)
                {
                    RestoreSectorEscalationFade(); SetSectorEnergy(1);
                    for (int i = 0; i < 6; i++) { sectorRelayViews[i]?.SetEnergy(true); sectorRelayViews[i]?.ClearEffects(); }
                    SectorTransitionState = SectorTransitionStage.Reconnect;
                    SetSectorStage(SectorStage.EscalationWarning);
                    RebuildBoundaryLasersAsPhase2Hexagon();
                    sectorConnectionProgress = 0; sectorMorph = 0; sectorAddedSpokeExtension = .06f;
                    // Only relay-to-relay visuals exist here. Combat spokes are
                    // rented later, by the forced attack and its full warning.
                }
                sectorConnectionProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((connectionAge - .14f) / .25f));
                sectorMorph = sectorConnectionProgress; sectorAddedSpokeExtension = Mathf.Lerp(.06f, 1, sectorConnectionProgress);
                sectorWarningProgress = Mathf.Clamp01(connectionAge / (sectorEscalationWarningTime - SectorConversionTime));
                for (int i = 0; i < 6; i++)
                {
                    sectorBarrierVisuals[i]?.SamplePurpleConnection(sectorConnectionProgress, connectionAge, false);
                    sectorRelayViews[slots[i].RuntimeIndex - 1]?.SampleReconnect(connectionAge, slots[(i + 1) % 6].transform.position - slots[i].transform.position);
                }
            }
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        sectorConnectionProgress = sectorMorph = sectorAddedSpokeExtension = sectorWarningProgress = 1;
        for (int i = 0; i < 6; i++) { sectorRelayViews[i]?.ClearEffects(); sectorBarrierVisuals[i]?.SamplePurpleConnection(1, 1, false); }
        SectorTransitionState = SectorTransitionStage.NetworkHold; elapsed = 0;
        while (elapsed < SectorNetworkHoldTime)
        {
            SampleSectorTransitionFraming();
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        SectorTransitionState = SectorTransitionStage.BossFocus;
        phase2GungeonCamera?.TryBeginOwnedCinematicFocusBlend(this, transform.position, SectorBossFocusTime, null, out _);
        elapsed = 0;
        float startSize = phase2CameraZoomController != null ? phase2CameraZoomController.CurrentOrthographicSize : 16.67f;
        while (elapsed < SectorBossFocusTime)
        {
            float size = Mathf.Lerp(startSize, 6.8f, Mathf.SmoothStep(0, 1, elapsed / SectorBossFocusTime));
            if (phase2CameraZoomController != null) phase2CameraZoomController.SetEffectiveZoomMultiplier(size / phase2CameraZoomController.BaseOrthographicSize, true);
            if (phase2ShieldActive) sectorShield?.SampleProtected(SectorTransitionElapsed);
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        SectorTransitionState = SectorTransitionStage.ShieldRelease; elapsed = 0;
        while (elapsed < SectorBossShieldPresentation.ReleaseDuration)
        {
            if (phase2ShieldActive) sectorShield?.SampleRelease(elapsed);
            yield return null; elapsed += Time.deltaTime; SectorTransitionElapsed += Time.deltaTime;
        }
        sectorShield?.Clear();
        // Full invulnerability ends; the existing finite combat absorption
        // layer remains on its SH bar and emits only actual hit/break feedback.
        for (int i = 0; i < 6; i++) sectorBarrierVisuals[i]?.SamplePurpleConnection(1, 1, true);
        sectorEscalationPresented = true; sectorEscalationReady = false;
        phase2ShieldDamageEnabled = usePhase2ShieldTransition;
        if (!usePhase2ShieldTransition) EnterTruePhase2();
        phase2GungeonCamera?.ReleaseOwnedCinematicFocus(this, false);
        phase2GungeonCamera?.SetCinematicInputOffsetLocked(this, false);
        phase2CameraZoomController?.ResetZoom(false);
        casting = false; sectorWeaponFacingLocked = false; sectorTransitionRunning = false;
        SectorTransitionState = SectorTransitionStage.Complete;
    }
    private void SampleSectorTransitionFraming()
    {
        if (phase2ShieldActive && !phase2ShieldDamageEnabled) sectorShield?.SampleProtected(SectorTransitionElapsed);
        if (phase2CameraZoomController == null) return;
        // Fit both new end relays below the native HUD, through the existing
        // zoom owner. Sampling also accounts for its changing framing profile.
        float halfHeight = (GetEffectiveHalfExtents().y + 1f) / .78f;
        phase2CameraZoomController.SetEffectiveZoomMultiplier(halfHeight / phase2CameraZoomController.BaseOrthographicSize, false);
    }
}
