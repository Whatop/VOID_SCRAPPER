using UnityEngine;

[DisallowMultipleComponent]
public sealed class SalvageDevourerCorridorController : MonoBehaviour
{
    [Header("Vertical Scroll")]
    [SerializeField, Min(0.01f)] private float scrollSpeed = 1.25f;

    [Header("Boss Fight Composition")]
    [SerializeField, Min(1f)] private float combatOrthographicSizeMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float cameraStartAboveCorridorTop = 1.5f;
    [SerializeField, Min(2f)] private float combatLaneHalfWidth = 5.75f;

    [Header("Player Viewport")]
    [SerializeField, Min(0f)] private float horizontalViewportInset = 0.15f;
    [SerializeField, Min(0f)] private float topViewportInset = 1.25f;
    [SerializeField, Min(0f)] private float bottomViewportInset = 1.25f;

    [Header("Corridor Walls")]
    [SerializeField, Min(0.05f)] private float wallThickness = 0.6f;
    [SerializeField, Min(0f)] private float wallVerticalOverlap = 1f;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color wallColor = new Color(0.28f, 0.9f, 1f, 0.92f);
    [SerializeField] private string wallLayerName = "Default";
    [SerializeField] private string wallSortingLayerName = "Default";
    [SerializeField] private int wallSortingOrder = 30;

    [Header("Corridor Boundary Presentation")]
    [SerializeField] private Color boundaryCoreColor = new Color(1f, 0.16f, 0.04f, 0.96f);
    [SerializeField] private Color boundaryHazeColor = new Color(0.7f, 0.025f, 0.01f, 0.28f);
    [SerializeField] private Color boundaryFragmentColor = new Color(1f, 0.3f, 0.06f, 0.72f);
    [SerializeField, Range(0.025f, 0.05f)] private float boundaryCoreWidth = 0.045f;
    [SerializeField, Range(0.15f, 0.4f)] private float boundaryHazeWidth = 0.32f;
    [SerializeField, Range(0.08f, 0.3f)] private float boundaryFragmentBandWidth = 0.24f;
    [SerializeField, Min(0.1f)] private float boundaryPulseDuration = 1.4f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Development Testing")]
    [SerializeField] private bool showRuntimeGizmos = true;
    [SerializeField, Range(0f, 1f)] private float manualStartNormalized;
#endif

    private ExpeditionMapGenerator mapGenerator;
    private GungeonStyleCamera2D gameplayCamera;
    private Camera viewportCamera;
    private PlayerController2D playerController;
    private PlayerHealth playerHealth;
    private RunManager observedRunManager;
    private BossArenaLaserWall leftWall;
    private BossArenaLaserWall rightWall;
    private BossArenaLaserWall terminalRearWall;
    private ExpeditionMapGenerator.Region2BossCorridorData corridorData;
    private Vector2 cameraStartCenter;
    private Vector2 cameraTerminalCenter;
    private bool hasResolvedGeometry;
    private bool runtimeActive;
    private bool scrollStarted;
    private bool lifecycleSubscribed;
    private bool cleanupInProgress;
    private bool hasLoggedStartWarning;
    private bool cameraCompositionAcquired;
    private bool terminalRearWallDeployed;

    public bool IsRuntimeActive => runtimeActive;
    public bool IsPrepared => runtimeActive;
    public bool IsScrollStarted => runtimeActive && scrollStarted;
    public bool HasReachedTerminal =>
        runtimeActive &&
        gameplayCamera != null &&
        gameplayCamera.IsScriptedVerticalScrollOwnedBy(this) &&
        gameplayCamera.HasScriptedVerticalScrollReachedTerminal;
    public Vector2 CameraStartCenter => cameraStartCenter;
    public Vector2 CameraTerminalCenter => cameraTerminalCenter;
    public Vector2 CurrentCameraScrollCenter =>
        gameplayCamera != null && gameplayCamera.IsScriptedVerticalScrollOwnedBy(this)
            ? gameplayCamera.CurrentScriptedVerticalScrollCenter
            : cameraStartCenter;

    public bool TryGetConstrainedPlayerViewportBounds(out Bounds bounds)
    {
        bounds = default;
        return runtimeActive &&
               playerController != null &&
               playerController.TryGetTemporaryCameraViewportConstraintBounds(this, out bounds);
    }

    public bool TryGetCurrentCombatViewportBounds(out Bounds bounds)
    {
        bounds = default;
        if (!runtimeActive || viewportCamera == null || !viewportCamera.orthographic ||
            gameplayCamera == null ||
            !gameplayCamera.IsScriptedVerticalScrollOwnedBy(this))
        {
            return false;
        }

        Vector2 center = gameplayCamera.CurrentScriptedVerticalScrollCenter;
        float halfHeight = Mathf.Max(0.1f, viewportCamera.orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.1f, viewportCamera.aspect);
        float wallInset = Mathf.Max(0.025f, wallThickness * 0.5f);
        float activeHalfWidth = ResolveCombatHalfWidth();
        float corridorLeft = corridorData.CenterX - activeHalfWidth + wallInset;
        float corridorRight = corridorData.CenterX + activeHalfWidth - wallInset;
        float minimumX = Mathf.Max(center.x - halfWidth, corridorLeft);
        float maximumX = Mathf.Min(center.x + halfWidth, corridorRight);

        if (minimumX >= maximumX)
        {
            return false;
        }

        bounds = new Bounds(
            new Vector3((minimumX + maximumX) * 0.5f, center.y, 0f),
            new Vector3(maximumX - minimumX, halfHeight * 2f, 1f)
        );
        return true;
    }

    public bool SetScrollPaused(bool paused)
    {
        return runtimeActive &&
               gameplayCamera != null &&
               gameplayCamera.IsScriptedVerticalScrollOwnedBy(this) &&
               gameplayCamera.SetScriptedVerticalScrollPaused(this, paused);
    }

    public void Initialize(ExpeditionMapGenerator sourceMapGenerator)
    {
        if (runtimeActive || lifecycleSubscribed)
        {
            CleanupCorridorRuntime();
        }

        mapGenerator = sourceMapGenerator;
        ResolveRuntimeReferences();
        TryValidateAndResolveGeometry(out _);
    }

    public bool BeginCorridorRuntime()
    {
        return BeginCorridorRuntime(0f);
    }

    public bool BeginCorridorRuntime(float normalizedStartPosition)
    {
        if (!PrepareCorridorRuntime(normalizedStartPosition))
        {
            return false;
        }

        return BeginScroll();
    }

    public bool PrepareCorridorRuntime()
    {
        return PrepareCorridorRuntime(0f);
    }

    public bool PrepareCorridorRuntime(float normalizedStartPosition)
    {
        if (runtimeActive)
        {
            return gameplayCamera != null &&
                   gameplayCamera.IsScriptedVerticalScrollOwnedBy(this) &&
                   playerController != null &&
                   playerController.HasTemporaryCameraViewportConstraint(this);
        }

        ResolveRuntimeReferences();
        if (!AcquireCameraComposition())
        {
            WarnStartFailure("the gameplay Camera framing profile is owned by another presentation");
            return false;
        }

        if (!TryValidateAndResolveGeometry(out string failureReason))
        {
            WarnStartFailure(failureReason);
            ReleaseCameraComposition();
            return false;
        }

        EnsureWallObjects();
        if (leftWall == null || rightWall == null)
        {
            WarnStartFailure("left/right BossArenaLaserWall instances could not be created");
            CleanupFailedStartup();
            return false;
        }

        terminalRearWallDeployed = false;
        SetWallsActive(true);
        ConfigureWalls();

        float normalized = Mathf.Clamp01(normalizedStartPosition);
        Vector2 requestedStart = new Vector2(
            cameraStartCenter.x,
            Mathf.Lerp(cameraStartCenter.y, cameraTerminalCenter.y, normalized)
        );

        if (!gameplayCamera.TryEnterScriptedVerticalScroll(
                this,
                requestedStart,
                cameraTerminalCenter,
                scrollSpeed,
                true,
                true))
        {
            WarnStartFailure(
                "the gameplay Camera authority rejected scripted-scroll ownership " +
                "(another scripted/cinematic mode may be active)"
            );
            CleanupFailedStartup();
            return false;
        }

        SetWallsActive(true);

        float halfWallThickness = Mathf.Max(0.025f, wallThickness * 0.5f);
        float activeHalfWidth = ResolveCombatHalfWidth();
        float leftInnerLimit = corridorData.CenterX - activeHalfWidth + halfWallThickness;
        float rightInnerLimit = corridorData.CenterX + activeHalfWidth - halfWallThickness;
        if (!playerController.AcquireTemporaryCameraViewportConstraint(
                this,
                viewportCamera,
                gameplayCamera.transform,
                leftInnerLimit,
                rightInnerLimit,
                horizontalViewportInset,
                topViewportInset,
                bottomViewportInset))
        {
            WarnStartFailure("the Player movement authority rejected the temporary viewport constraint");
            CleanupFailedStartup();
            return false;
        }

        runtimeActive = true;
        scrollStarted = false;
        hasLoggedStartWarning = false;
        SubscribeLifecycle();

        return true;
    }

    public bool BeginScroll()
    {
        if (!runtimeActive || gameplayCamera == null ||
            !gameplayCamera.IsScriptedVerticalScrollOwnedBy(this))
        {
            WarnStartFailure("the corridor must be prepared before scrolling begins");
            return false;
        }

        if (scrollStarted)
        {
            return true;
        }

        if (!gameplayCamera.SetScriptedVerticalScrollPaused(this, false))
        {
            WarnStartFailure("scripted-scroll ownership was lost before scrolling could begin");
            CleanupCorridorRuntime();
            return false;
        }

        scrollStarted = true;
        return true;
    }

    public void StopCorridorRuntime()
    {
        CleanupCorridorRuntime();
    }

    public bool ForceCameraToTerminal()
    {
        bool forced = runtimeActive && gameplayCamera != null &&
                      gameplayCamera.ForceScriptedVerticalScrollToTerminal(this);
        if (forced)
        {
            TryDeployTerminalRearWall();
        }

        return forced;
    }

    public void CleanupCorridorRuntime()
    {
        if (cleanupInProgress)
        {
            return;
        }

        cleanupInProgress = true;
        runtimeActive = false;
        scrollStarted = false;

        if (gameplayCamera != null)
        {
            gameplayCamera.ExitScriptedVerticalScroll(this);
        }

        ReleaseCameraComposition();

        if (playerController != null)
        {
            playerController.ReleaseTemporaryCameraViewportConstraint(this);
        }

        SetWallsActive(false);
        terminalRearWallDeployed = false;
        UnsubscribeLifecycle();
        corridorData = default;
        cameraStartCenter = Vector2.zero;
        cameraTerminalCenter = Vector2.zero;
        hasResolvedGeometry = false;
        cleanupInProgress = false;
    }

    private void Update()
    {
        if (!runtimeActive)
        {
            return;
        }

        if ((observedRunManager != null && observedRunManager.IsCompletingRun) ||
            playerController == null ||
            gameplayCamera == null ||
            !gameplayCamera.IsScriptedVerticalScrollOwnedBy(this) ||
            !playerController.HasTemporaryCameraViewportConstraint(this))
        {
            CleanupCorridorRuntime();
            return;
        }

        TryDeployTerminalRearWall();
    }

    private void OnDisable()
    {
        CleanupCorridorRuntime();
    }

    private void OnDestroy()
    {
        CleanupCorridorRuntime();
        mapGenerator = null;
        gameplayCamera = null;
        viewportCamera = null;
        playerController = null;
        playerHealth = null;
        observedRunManager = null;
        leftWall = null;
        rightWall = null;
        terminalRearWall = null;
    }

    private void OnValidate()
    {
        scrollSpeed = Mathf.Max(0.01f, scrollSpeed);
        combatOrthographicSizeMultiplier = Mathf.Max(1f, combatOrthographicSizeMultiplier);
        cameraStartAboveCorridorTop = Mathf.Max(0f, cameraStartAboveCorridorTop);
        combatLaneHalfWidth = Mathf.Max(2f, combatLaneHalfWidth);
        horizontalViewportInset = Mathf.Max(0f, horizontalViewportInset);
        topViewportInset = Mathf.Max(0f, topViewportInset);
        bottomViewportInset = Mathf.Max(0f, bottomViewportInset);
        wallThickness = Mathf.Max(0.05f, wallThickness);
        wallVerticalOverlap = Mathf.Max(0f, wallVerticalOverlap);
        boundaryCoreWidth = Mathf.Clamp(boundaryCoreWidth, 0.025f, 0.05f);
        boundaryHazeWidth = Mathf.Clamp(boundaryHazeWidth, 0.15f, 0.4f);
        boundaryFragmentBandWidth = Mathf.Clamp(boundaryFragmentBandWidth, 0.08f, 0.3f);
        boundaryPulseDuration = Mathf.Max(0.1f, boundaryPulseDuration);
    }

    private void ResolveRuntimeReferences()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = GungeonStyleCamera2D.Instance;
            if (gameplayCamera == null)
            {
                gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>(FindObjectsInactive.Include);
            }
        }

        viewportCamera = gameplayCamera != null ? gameplayCamera.GameplayCamera : null;

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
        }

        playerHealth = playerController != null
            ? playerController.GetComponent<PlayerHealth>()
            : null;
        observedRunManager = RunManager.Instance;
    }

    private bool TryValidateAndResolveGeometry(out string failureReason)
    {
        failureReason = string.Empty;

        if (observedRunManager == null || !observedRunManager.HasActiveRun)
        {
            failureReason = "there is no active run";
            return false;
        }

        RunContext currentRun = observedRunManager.CurrentRun;
        if (currentRun == null ||
            currentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1 ||
            currentRun.CurrentBossId != CampaignBossId.SalvageDevourer)
        {
            failureReason = "the active encounter is not DeepZone1 / SalvageDevourer";
            return false;
        }

        if (observedRunManager.IsCompletingRun)
        {
            failureReason = "the run is already completing";
            return false;
        }

        if (mapGenerator == null || !mapGenerator.HasRegion2BossCorridor)
        {
            failureReason = "Phase-1 Region-2 corridor data is unavailable";
            return false;
        }

        if (gameplayCamera == null || viewportCamera == null || !viewportCamera.orthographic)
        {
            failureReason = "the direct orthographic gameplay Camera authority is unavailable";
            return false;
        }

        if (playerController == null)
        {
            failureReason = "PlayerController2D is unavailable";
            return false;
        }

        ExpeditionMapGenerator.Region2BossCorridorData data =
            mapGenerator.CurrentRegion2BossCorridor;
        if (!IsFinite(data.CenterX) || !IsFinite(data.TopY) || !IsFinite(data.BottomY) ||
            !IsFinite(data.HalfWidth) || data.HalfWidth <= 0f ||
            data.TopY <= data.BottomY)
        {
            failureReason = "Phase-1 Region-2 corridor values are invalid";
            return false;
        }

        Bounds mapBounds = mapGenerator.MapBounds;
        if (mapBounds.size.x <= 0.01f || mapBounds.size.y <= 0.01f)
        {
            failureReason = "generated map bounds are unavailable";
            return false;
        }

        float cameraHalfHeight = Mathf.Max(0.01f, viewportCamera.orthographicSize);
        Vector2 terminalEndpoint = data.TerminalPoint;
        Vector2 rawStartCenter = new Vector2(
            data.CenterX,
            data.TopY + cameraStartAboveCorridorTop
        );
        Vector2 rawTerminalCenter = new Vector2(
            terminalEndpoint.x,
            terminalEndpoint.y + cameraHalfHeight
        );
        Vector2 resolvedStartCenter = gameplayCamera.ResolveClampedCameraCenter(
            rawStartCenter,
            cameraHalfHeight
        );
        resolvedStartCenter.y = rawStartCenter.y;
        Vector2 resolvedTerminalCenter = gameplayCamera.ResolveClampedCameraCenter(
            rawTerminalCenter,
            cameraHalfHeight
        );

        if (Mathf.Abs(resolvedStartCenter.x - data.CenterX) > 0.0001f ||
            Mathf.Abs(resolvedTerminalCenter.x - data.CenterX) > 0.0001f)
        {
            failureReason = "map bounds cannot keep Camera X fixed at the generated corridor center";
            return false;
        }

        if (resolvedStartCenter.y + 0.0001f < resolvedTerminalCenter.y)
        {
            failureReason = "the generated corridor is shorter than the current Camera viewport";
            return false;
        }

        float playerVerticalSpace = cameraHalfHeight * 2f -
                                    topViewportInset -
                                    bottomViewportInset;
        if (playerVerticalSpace <= 0.1f)
        {
            failureReason = "the configured Player viewport insets leave no vertical movement space";
            return false;
        }

        float halfWallThickness = wallThickness * 0.5f;
        if (data.ReservedBounds.min.x > data.CenterX - data.HalfWidth - halfWallThickness ||
            data.ReservedBounds.max.x < data.CenterX + data.HalfWidth + halfWallThickness)
        {
            failureReason = "the configured wall thickness exceeds the Phase-1 reserved corridor bounds";
            return false;
        }

        corridorData = data;
        cameraStartCenter = resolvedStartCenter;
        cameraTerminalCenter = resolvedTerminalCenter;
        hasResolvedGeometry = true;
        return true;
    }

    private void EnsureWallObjects()
    {
        if (leftWall == null)
        {
            leftWall = CreateWall("Region2BossCorridorWall_Left");
        }

        if (rightWall == null)
        {
            rightWall = CreateWall("Region2BossCorridorWall_Right");
        }

        if (terminalRearWall == null)
        {
            terminalRearWall = CreateWall("Region2BossCorridorWall_TerminalRear");
        }
    }

    private BossArenaLaserWall CreateWall(string objectName)
    {
        GameObject wallObject = new GameObject(objectName);
        wallObject.transform.SetParent(transform, false);
        BossArenaLaserWall wall = wallObject.AddComponent<BossArenaLaserWall>();
        wallObject.SetActive(false);
        return wall;
    }

    private void ConfigureWalls()
    {
        float bottomOverlap = Mathf.Min(
            wallVerticalOverlap,
            Mathf.Max(0f, corridorData.BottomY - corridorData.ReservedBounds.min.y)
        );
        float topOverlap = Mathf.Min(
            wallVerticalOverlap,
            Mathf.Max(0f, corridorData.ReservedBounds.max.y - corridorData.TopY)
        );
        float startY = corridorData.BottomY - bottomOverlap;
        float cameraHalfHeight = viewportCamera != null
            ? Mathf.Max(0.1f, viewportCamera.orthographicSize)
            : 0f;
        float endY = Mathf.Max(
            corridorData.TopY + topOverlap,
            cameraStartCenter.y + cameraHalfHeight + wallVerticalOverlap
        );
        ConfigureWall(
            leftWall,
            new Vector2(corridorData.CenterX - ResolveCombatHalfWidth(), startY),
            new Vector2(corridorData.CenterX - ResolveCombatHalfWidth(), endY)
        );
        ConfigureWall(
            rightWall,
            new Vector2(corridorData.CenterX + ResolveCombatHalfWidth(), startY),
            new Vector2(corridorData.CenterX + ResolveCombatHalfWidth(), endY)
        );
    }

    private void ConfigureWall(BossArenaLaserWall wall, Vector2 start, Vector2 end)
    {
        if (wall == null)
        {
            return;
        }

        wall.InitializeBetween(
            start,
            end,
            wallThickness,
            true,
            0f,
            0.5f,
            wallMaterial,
            wallColor,
            wallSortingLayerName,
            wallSortingOrder,
            wallLayerName
        );
        wall.ConfigureProjectileRicochet(true);

        RaiderArenaBoundaryPresentation presentation =
            wall.GetComponent<RaiderArenaBoundaryPresentation>();
        if (presentation == null)
        {
            presentation = wall.gameObject.AddComponent<RaiderArenaBoundaryPresentation>();
        }

        presentation.Configure(
            wall.GetOrCreateLineRenderer(),
            Vector2.Distance(start, end),
            wallMaterial,
            wallSortingLayerName,
            wallSortingOrder,
            boundaryCoreColor,
            boundaryHazeColor,
            boundaryFragmentColor,
            boundaryCoreWidth,
            boundaryHazeWidth,
            boundaryFragmentBandWidth,
            boundaryPulseDuration
        );
    }

    private float ResolveCombatHalfWidth()
    {
        return Mathf.Min(
            Mathf.Max(2f, combatLaneHalfWidth),
            Mathf.Max(0.1f, corridorData.HalfWidth)
        );
    }

    private void SetWallsActive(bool active)
    {
        if (leftWall != null)
        {
            leftWall.gameObject.SetActive(active);
        }

        if (rightWall != null)
        {
            rightWall.gameObject.SetActive(active);
        }

        if (terminalRearWall != null)
        {
            terminalRearWall.gameObject.SetActive(active && terminalRearWallDeployed);
        }
    }

    private bool TryDeployTerminalRearWall()
    {
        if (terminalRearWallDeployed)
        {
            return true;
        }

        if (!HasReachedTerminal || terminalRearWall == null || viewportCamera == null)
        {
            return false;
        }

        float halfHeight = Mathf.Max(0.1f, viewportCamera.orthographicSize);
        float rearWallY = cameraTerminalCenter.y - halfHeight + bottomViewportInset -
                          wallThickness * 0.5f;
        float halfWidth = ResolveCombatHalfWidth();
        Vector2 start = new Vector2(corridorData.CenterX - halfWidth, rearWallY);
        Vector2 end = new Vector2(corridorData.CenterX + halfWidth, rearWallY);

        terminalRearWall.gameObject.SetActive(true);
        ConfigureWall(terminalRearWall, start, end);
        terminalRearWallDeployed = true;
        return true;
    }

    private void CleanupFailedStartup()
    {
        if (gameplayCamera != null)
        {
            gameplayCamera.ExitScriptedVerticalScroll(this);
        }

        ReleaseCameraComposition();

        if (playerController != null)
        {
            playerController.ReleaseTemporaryCameraViewportConstraint(this);
        }

        SetWallsActive(false);
        runtimeActive = false;
        scrollStarted = false;
        UnsubscribeLifecycle();
        corridorData = default;
        cameraStartCenter = Vector2.zero;
        cameraTerminalCenter = Vector2.zero;
        hasResolvedGeometry = false;
    }

    private bool AcquireCameraComposition()
    {
        if (cameraCompositionAcquired)
        {
            return true;
        }

        if (gameplayCamera == null)
        {
            return false;
        }

        cameraCompositionAcquired = gameplayCamera.AcquireGameplayFramingProfile(
            this,
            Vector2.zero,
            1f,
            combatOrthographicSizeMultiplier,
            true
        );
        return cameraCompositionAcquired;
    }

    private void ReleaseCameraComposition()
    {
        if (!cameraCompositionAcquired)
        {
            return;
        }

        gameplayCamera?.ReleaseGameplayFramingProfile(this, true);
        cameraCompositionAcquired = false;
    }

    private void SubscribeLifecycle()
    {
        if (lifecycleSubscribed)
        {
            return;
        }

        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
        }

        lifecycleSubscribed = true;
    }

    private void UnsubscribeLifecycle()
    {
        if (!lifecycleSubscribed)
        {
            return;
        }

        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }

        lifecycleSubscribed = false;
    }

    private void HandleRunEnded(RunResultData _)
    {
        CleanupCorridorRuntime();
    }

    private void HandlePlayerDied()
    {
        CleanupCorridorRuntime();
    }

    private void WarnStartFailure(string reason)
    {
        if (hasLoggedStartWarning)
        {
            return;
        }

        hasLoggedStartWarning = true;
        Debug.LogWarning(
            $"Region-2 Salvage Devourer corridor runtime did not start: {reason}. " +
            "Normal exploration Camera and Player movement remain unchanged.",
            this
        );
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Development/Prepare Corridor Runtime")]
    private void DevelopmentPrepareCorridorRuntime()
    {
        PrepareCorridorRuntime();
    }

    [ContextMenu("Development/Start Prepared Scroll")]
    private void DevelopmentStartPreparedScroll()
    {
        BeginScroll();
    }

    [ContextMenu("Development/Begin Corridor Runtime")]
    private void DevelopmentBeginCorridorRuntime()
    {
        BeginCorridorRuntime();
    }

    [ContextMenu("Development/Begin Corridor At Normalized Position")]
    private void DevelopmentBeginCorridorAtNormalizedPosition()
    {
        BeginCorridorRuntime(manualStartNormalized);
    }

    [ContextMenu("Development/Force Camera To Terminal")]
    private void DevelopmentForceCameraToTerminal()
    {
        ForceCameraToTerminal();
    }

    [ContextMenu("Development/Force Terminal Rear Wall Deploy")]
    private void DevelopmentForceTerminalRearWallDeploy()
    {
        ForceCameraToTerminal();
        TryDeployTerminalRearWall();
    }

    [ContextMenu("Development/Stop Corridor Runtime")]
    private void DevelopmentStopCorridorRuntime()
    {
        CleanupCorridorRuntime();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRuntimeGizmos)
        {
            return;
        }

        ExpeditionMapGenerator.Region2BossCorridorData data = corridorData;
        bool canDrawGeometry = hasResolvedGeometry;
        Vector2 debugStartCenter = cameraStartCenter;
        Vector2 debugTerminalCenter = cameraTerminalCenter;
        if (!canDrawGeometry && mapGenerator != null && mapGenerator.HasRegion2BossCorridor)
        {
            data = mapGenerator.CurrentRegion2BossCorridor;
            canDrawGeometry = true;

            if (gameplayCamera != null && viewportCamera != null)
            {
                float cameraHalfHeight = Mathf.Max(0.01f, viewportCamera.orthographicSize);
                debugStartCenter = gameplayCamera.ResolveClampedCameraCenter(
                    new Vector2(data.CenterX, data.TopY + cameraStartAboveCorridorTop),
                    cameraHalfHeight
                );
                debugStartCenter.y = data.TopY + cameraStartAboveCorridorTop;
                debugTerminalCenter = gameplayCamera.ResolveClampedCameraCenter(
                    new Vector2(data.TerminalPoint.x, data.TerminalPoint.y + cameraHalfHeight),
                    cameraHalfHeight
                );
            }
        }

        if (canDrawGeometry)
        {
            Gizmos.color = new Color(0.65f, 0.12f, 1f, 0.9f);
            Gizmos.DrawWireCube(data.CorridorBounds.center, data.CorridorBounds.size);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(debugStartCenter, 0.45f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(debugTerminalCenter, 0.45f);
        }

        if (runtimeActive && gameplayCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gameplayCamera.CurrentScriptedVerticalScrollCenter, 0.32f);
        }

        if (playerController != null &&
            playerController.TryGetTemporaryCameraViewportConstraintBounds(this, out Bounds playerBounds))
        {
            Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.9f);
            Gizmos.DrawWireCube(playerBounds.center, playerBounds.size);
        }

        DrawWallBounds(leftWall, new Color(0.2f, 0.85f, 1f, 0.95f));
        DrawWallBounds(rightWall, new Color(0.2f, 0.85f, 1f, 0.95f));
        DrawWallBounds(terminalRearWall, new Color(1f, 0.3f, 0.06f, 0.95f));
    }

    private static void DrawWallBounds(BossArenaLaserWall wall, Color color)
    {
        if (wall == null)
        {
            return;
        }

        BoxCollider2D wallCollider = wall.GetComponent<BoxCollider2D>();
        if (wallCollider == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireCube(wallCollider.bounds.center, wallCollider.bounds.size);
    }
#endif
}
