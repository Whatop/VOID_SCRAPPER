using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class GungeonStyleCamera2D : MonoBehaviour
{
    public static GungeonStyleCamera2D Instance { get; private set; }

    [Header("Rig References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform shakeRoot;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private CameraZoomController2D cameraZoomController;

    [Header("Direct Follow")]
    [SerializeField] private float cameraWorldZ = -10f;
    [Min(0f)]
    [SerializeField] private float mapBoundsPadding = 0.25f;

    [Header("Screen-Space Move Zone")]
    [SerializeField] private float maxAimOffset = 0.75f;
    [Min(0f)]
    [SerializeField] private float outwardSharpness = 10f;
    [Min(0f)]
    [SerializeField] private float recenterSharpness = 14f;
    [Range(0f, 1f)]
    [SerializeField] private float lookAheadEnterRadius = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float lookAheadExitRadius = 0.16f;
    [Range(0.01f, 2f)]
    [SerializeField] private float fullOffsetRadius = 0.8f;
    [Min(0f)]
    [SerializeField] private float mouseMotionEpsilonPixels = 0.25f;

    [Header("Gameplay Framing Profile")]
    [Min(0f)]
    [SerializeField] private float gameplayFramingTransitionSharpness = 10f;

    [Header("Cinematic Focus")]
    [Min(0f)]
    [SerializeField] private float cinematicFocusSharpness = 9f;
    [Min(0f)]
    [SerializeField] private float cinematicReturnSharpness = 12f;

    [Header("Camera Shake")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float maxShakeAmplitude = 0.42f;
    [SerializeField] private float shakeFrequency = 30f;
    [SerializeField, Range(0f, 1f)] private float repeatedHitStacking = 0.35f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Development Diagnostics")]
    [SerializeField] private bool enableCameraDiagnostics;
    [Min(0.1f)]
    [SerializeField] private float diagnosticLogInterval = 0.5f;
#endif

    private PlayerController2D playerController;
    private ExpeditionMapGenerator mapGenerator;
    private Vector2 currentAimOffset;
    private Vector2 targetAimOffset;
    private Vector2 stableMouseScreenPosition;
    private Vector3 currentCameraCenter;
    private float runtimeAimOffsetMultiplier = 1f;
    private float runtimeMouseDistanceMultiplier = 1f;
    private object gameplayFramingOwner;
    private Vector2 currentGameplayFramingOffset;
    private Vector2 targetGameplayFramingOffset;
    private float currentGameplayAimOffsetMultiplier = 1f;
    private float targetGameplayAimOffsetMultiplier = 1f;
    private bool hasStableMousePosition;
    private bool mouseLookAheadActive;
    private bool cameraCenterInitialized;

    private object scriptedVerticalScrollOwner;
    private Vector2 scriptedVerticalScrollCenter;
    private Vector2 scriptedVerticalScrollTerminalCenter;
    private float scriptedVerticalScrollSpeed;
    private bool scriptedVerticalScrollPaused;
    private bool scriptedVerticalScrollReachedTerminal;
    private Vector2 aimOffsetBeforeScriptedScroll;
    private Vector2 targetAimOffsetBeforeScriptedScroll;
    private Vector2 stableMousePositionBeforeScriptedScroll;
    private bool hadStableMousePositionBeforeScriptedScroll;
    private bool mouseLookAheadWasActiveBeforeScriptedScroll;

    private bool cinematicFocusActive;
    private bool cinematicReturnActive;
    private bool cinematicInputOffsetLocked;
    private object cinematicFocusOwner;
    private Vector3 cinematicFocusWorldPosition;
    private bool cinematicFocusBlendActive;
    private Vector3 cinematicFocusBlendStart;
    private Vector3 cinematicFocusBlendTarget;
    private float cinematicFocusBlendDuration;
    private float cinematicFocusBlendElapsed;
    private AnimationCurve cinematicFocusBlendCurve;

    private float shakeRemaining;
    private float shakeDuration;
    private float shakeAmplitude;
    private float shakeNoiseTime;
    private Vector2 shakeSeed;
    private int shakeSequence;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private float nextDiagnosticLogTime;
#endif

    public bool IsCinematicFocusActive => cinematicFocusActive;
    public bool IsCinematicFocusBlendActive => cinematicFocusBlendActive;
    public bool IsCinematicInputOffsetLocked => cinematicInputOffsetLocked;
    public Camera GameplayCamera => mainCamera;
    public bool IsScriptedVerticalScrollActive => scriptedVerticalScrollOwner != null;
    public bool HasScriptedVerticalScrollReachedTerminal =>
        IsScriptedVerticalScrollActive && scriptedVerticalScrollReachedTerminal;
    public Vector2 CurrentScriptedVerticalScrollCenter => scriptedVerticalScrollCenter;
    public Vector2 ScriptedVerticalScrollTerminalCenter => scriptedVerticalScrollTerminalCenter;
    public bool IsShakeActive => shakeRemaining > 0f ||
                                 (shakeRoot != null && shakeRoot.localPosition.sqrMagnitude > 0.000001f);

    private void Reset()
    {
        mainCamera = Camera.main;
        shakeRoot = mainCamera != null ? mainCamera.transform.parent : null;
        cameraZoomController = GetComponent<CameraZoomController2D>();
    }

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
        ResetShakeNoise();
        SnapToPlayer();
    }

    private void OnEnable()
    {
        Instance = this;
        ResolveReferences();
        SnapToPlayer();
    }

    private void OnDisable()
    {
        cinematicFocusActive = false;
        cinematicReturnActive = false;
        cinematicFocusOwner = null;
        CancelCinematicFocusBlend(false);
        cinematicInputOffsetLocked = false;
        mouseLookAheadActive = false;
        hasStableMousePosition = false;
        currentAimOffset = Vector2.zero;
        targetAimOffset = Vector2.zero;
        ClearScriptedVerticalScrollState();
        object framingOwner = gameplayFramingOwner;
        gameplayFramingOwner = null;
        currentGameplayFramingOffset = Vector2.zero;
        targetGameplayFramingOffset = Vector2.zero;
        currentGameplayAimOffsetMultiplier = 1f;
        targetGameplayAimOffsetMultiplier = 1f;
        if (cameraZoomController != null && framingOwner != null)
        {
            cameraZoomController.ReleaseGameplayFramingProfile(framingOwner, true);
        }
        ResetShakeState();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        lookAheadEnterRadius = Mathf.Clamp01(lookAheadEnterRadius);
        lookAheadExitRadius = Mathf.Clamp(lookAheadExitRadius, 0f, lookAheadEnterRadius);
        fullOffsetRadius = Mathf.Max(lookAheadEnterRadius + 0.01f, fullOffsetRadius);
        maxAimOffset = Mathf.Max(0f, maxAimOffset);
        outwardSharpness = Mathf.Max(0f, outwardSharpness);
        recenterSharpness = Mathf.Max(0f, recenterSharpness);
        cinematicFocusSharpness = Mathf.Max(0f, cinematicFocusSharpness);
        cinematicReturnSharpness = Mathf.Max(0f, cinematicReturnSharpness);
        mouseMotionEpsilonPixels = Mathf.Max(0f, mouseMotionEpsilonPixels);
        mapBoundsPadding = Mathf.Max(0f, mapBoundsPadding);
        gameplayFramingTransitionSharpness = Mathf.Max(0f, gameplayFramingTransitionSharpness);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (mainCamera == null || shakeRoot == null ||
            (!IsScriptedVerticalScrollActive && player == null))
        {
            return;
        }

        float deltaTime = Mathf.Max(0f, Time.deltaTime);
        float unscaledDeltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
        Vector3 desiredCenter;

        if (IsScriptedVerticalScrollActive)
        {
            desiredCenter = UpdateScriptedVerticalScroll(deltaTime);
        }
        else
        {
            UpdateGameplayFramingProfile(deltaTime);
            UpdateAimOffset(deltaTime);

            Vector3 playerCenter = player.position +
                                   (Vector3)currentGameplayFramingOffset +
                                   (Vector3)currentAimOffset;
            desiredCenter = ResolveDesiredCameraCenter(playerCenter, deltaTime, unscaledDeltaTime);
            desiredCenter = ClampToMapBounds(desiredCenter);
        }

        desiredCenter.z = cameraWorldZ;

        transform.position = desiredCenter;
        ApplyShake(deltaTime);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        ReportDiagnostics();
#endif
    }

    public static void RequestShake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        if (Instance == null)
        {
            Instance = FindFirstObjectByType<GungeonStyleCamera2D>();
        }

        Instance?.AddShake(amplitude, duration);
    }

    public void AddShake(float amplitude, float duration)
    {
        if (!enableCameraShake || amplitude <= 0f || duration <= 0f)
        {
            return;
        }

        amplitude *= GameSettingsRuntime.CameraShakeMultiplier;
        if (amplitude <= 0.0001f)
        {
            return;
        }

        amplitude = Mathf.Clamp(amplitude, 0f, Mathf.Max(0.01f, maxShakeAmplitude));
        duration = Mathf.Max(0.01f, duration);

        if (shakeRemaining > 0f)
        {
            shakeAmplitude = Mathf.Min(
                Mathf.Max(0.01f, maxShakeAmplitude),
                Mathf.Max(shakeAmplitude, amplitude) + amplitude * repeatedHitStacking
            );
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
            shakeDuration = Mathf.Max(shakeDuration, duration);
        }
        else
        {
            shakeAmplitude = amplitude;
            shakeRemaining = duration;
            shakeDuration = duration;
        }

        ResetShakeNoise();
    }

    public void StopShake()
    {
        ResetShakeState();
    }

    public void SetPlayer(Transform target)
    {
        player = target;
        playerController = player != null ? player.GetComponent<PlayerController2D>() : null;

        if (!IsScriptedVerticalScrollActive)
        {
            SnapToPlayer();
        }
    }

    public void SnapToPlayer()
    {
        ResolveReferences();

        if (player == null || IsScriptedVerticalScrollActive)
        {
            return;
        }

        currentAimOffset = Vector2.zero;
        targetAimOffset = Vector2.zero;
        mouseLookAheadActive = false;
        hasStableMousePosition = false;
        cinematicReturnActive = false;
        CancelCinematicFocusBlend(false);

        Vector3 center = cinematicFocusActive
            ? cinematicFocusWorldPosition
            : player.position + (Vector3)currentGameplayFramingOffset;

        currentCameraCenter = ClampToMapBounds(center);
        currentCameraCenter.z = cameraWorldZ;
        cameraCenterInitialized = true;
        transform.position = currentCameraCenter;

        if (shakeRoot != null)
        {
            shakeRoot.localPosition = Vector3.zero;
        }
    }

    public void NotifyTargetWarped()
    {
        SnapToPlayer();
    }

    public void SetCinematicFocus(Vector3 worldPosition, bool immediate = false)
    {
        cinematicFocusOwner = null;
        ResolveReferences();
        CancelCinematicFocusBlend(false);
        cinematicFocusWorldPosition = worldPosition;
        cinematicFocusActive = true;
        cinematicReturnActive = false;
        targetAimOffset = Vector2.zero;

        if (immediate)
        {
            currentAimOffset = Vector2.zero;
            currentCameraCenter = ClampToMapBounds(worldPosition);
            currentCameraCenter.z = cameraWorldZ;
            cameraCenterInitialized = true;
            transform.position = currentCameraCenter;
        }
    }

    public void UpdateCinematicFocus(Vector3 worldPosition)
    {
        cinematicFocusOwner = null;
        CancelCinematicFocusBlend(false);
        cinematicFocusWorldPosition = worldPosition;
    }

    public Vector3 BeginCinematicFocusBlend(
        Vector3 worldPosition,
        float duration,
        AnimationCurve transitionCurve)
    {
        cinematicFocusOwner = null;
        return BeginCinematicFocusBlendInternal(worldPosition, duration, transitionCurve);
    }

    public bool TryBeginOwnedCinematicFocusBlend(
        object owner,
        Vector3 worldPosition,
        float duration,
        AnimationCurve transitionCurve,
        out Vector3 resolvedTarget)
    {
        resolvedTarget = default;
        bool hasUnownedCinematic = cinematicFocusOwner == null &&
                                   (cinematicFocusActive ||
                                    cinematicFocusBlendActive ||
                                    cinematicReturnActive);
        if (owner == null ||
            scriptedVerticalScrollOwner != null ||
            hasUnownedCinematic ||
            cinematicFocusOwner != null && !ReferenceEquals(cinematicFocusOwner, owner))
        {
            return false;
        }

        cinematicFocusOwner = owner;
        resolvedTarget = BeginCinematicFocusBlendInternal(
            worldPosition,
            duration,
            transitionCurve);
        return true;
    }

    public bool IsCinematicFocusOwnedBy(object owner)
    {
        return owner != null && ReferenceEquals(cinematicFocusOwner, owner);
    }

    public bool ReleaseOwnedCinematicFocus(object owner, bool immediate)
    {
        if (!IsCinematicFocusOwnedBy(owner))
        {
            return false;
        }

        cinematicFocusOwner = null;
        ClearCinematicFocusInternal(immediate);
        return true;
    }

    private Vector3 BeginCinematicFocusBlendInternal(
        Vector3 worldPosition,
        float duration,
        AnimationCurve transitionCurve)
    {
        ResolveReferences();

        float orthographicSize = mainCamera != null
            ? mainCamera.orthographicSize
            : 0f;
        Vector3 resolvedTarget = ResolveClampedCameraCenter(worldPosition, orthographicSize);
        Vector3 renderedCenter = transform.position;
        renderedCenter.z = cameraWorldZ;

        cinematicFocusActive = true;
        cinematicReturnActive = false;
        cinematicFocusWorldPosition = resolvedTarget;
        targetAimOffset = Vector2.zero;
        currentAimOffset = Vector2.zero;
        currentCameraCenter = renderedCenter;
        cameraCenterInitialized = true;

        cinematicFocusBlendStart = renderedCenter;
        cinematicFocusBlendTarget = resolvedTarget;
        cinematicFocusBlendDuration = Mathf.Max(0f, duration);
        cinematicFocusBlendElapsed = 0f;
        cinematicFocusBlendCurve = transitionCurve;
        cinematicFocusBlendActive = cinematicFocusBlendDuration > 0.0001f &&
                                    (cinematicFocusBlendTarget - cinematicFocusBlendStart).sqrMagnitude > 0.000001f;

        if (!cinematicFocusBlendActive)
        {
            currentCameraCenter = resolvedTarget;
            transform.position = resolvedTarget;
        }

        return resolvedTarget;
    }

    public void CancelCinematicFocusBlend(bool preserveCurrentFocus = true)
    {
        if (preserveCurrentFocus && cinematicFocusBlendActive)
        {
            cinematicFocusWorldPosition = currentCameraCenter;
        }

        cinematicFocusBlendActive = false;
        cinematicFocusBlendElapsed = 0f;
        cinematicFocusBlendDuration = 0f;
        cinematicFocusBlendCurve = null;
    }

    public Vector3 ResolveClampedCameraCenter(Vector3 worldPosition, float orthographicSize)
    {
        ResolveReferences();

        float resolvedOrthographicSize = orthographicSize > 0f
            ? orthographicSize
            : mainCamera != null
                ? mainCamera.orthographicSize
                : 0f;
        Vector3 resolvedCenter = ClampToMapBounds(worldPosition, resolvedOrthographicSize);
        resolvedCenter.z = cameraWorldZ;
        return resolvedCenter;
    }

    public void ClearCinematicFocus(bool immediate = false)
    {
        cinematicFocusOwner = null;
        ClearCinematicFocusInternal(immediate);
    }

    public bool TryReleaseUnownedCinematicFocusAtCurrentPosition()
    {
        if (cinematicFocusOwner != null)
        {
            return false;
        }

        CancelCinematicFocusBlend(false);
        cinematicFocusActive = false;
        cinematicReturnActive = false;
        currentCameraCenter = transform.position;
        currentCameraCenter.z = cameraWorldZ;
        cameraCenterInitialized = true;
        return true;
    }

    private void ClearCinematicFocusInternal(bool immediate)
    {
        CancelCinematicFocusBlend(false);
        cinematicFocusActive = false;

        if (immediate)
        {
            cinematicReturnActive = false;
            SnapToPlayer();
        }
        else
        {
            currentCameraCenter = transform.position;
            currentCameraCenter.z = cameraWorldZ;
            cameraCenterInitialized = true;
            cinematicReturnActive = true;
        }
    }

    public bool IsAtGameplayFraming(
        Transform gameplayTarget,
        float positionTolerance,
        bool requireCinematicFocusAtTarget)
    {
        ResolveReferences();

        if (gameplayTarget == null || mainCamera == null)
        {
            return false;
        }

        float tolerance = Mathf.Max(0.0001f, positionTolerance);
        float toleranceSquared = tolerance * tolerance;
        Vector3 expectedCenter = ResolveGameplayFramingCenter(gameplayTarget.position);
        Vector2 expectedPosition = expectedCenter;
        Vector2 actualPosition = mainCamera.transform.position;

        if ((actualPosition - expectedPosition).sqrMagnitude > toleranceSquared ||
            currentAimOffset.sqrMagnitude > toleranceSquared ||
            targetAimOffset.sqrMagnitude > toleranceSquared ||
            IsShakeActive)
        {
            return false;
        }

        if (!requireCinematicFocusAtTarget)
        {
            return !cinematicFocusActive;
        }

        Vector2 focusPosition = cinematicFocusWorldPosition;
        Vector2 targetPosition = expectedCenter;
        return cinematicFocusActive &&
               (focusPosition - targetPosition).sqrMagnitude <= toleranceSquared;
    }

    public bool AcquireGameplayFramingProfile(
        object owner,
        Vector2 worldOffset,
        float aimOffsetMultiplier,
        float orthographicSizeMultiplier,
        bool immediate)
    {
        if (owner == null)
        {
            return false;
        }

        if (gameplayFramingOwner != null && !ReferenceEquals(gameplayFramingOwner, owner))
        {
            return false;
        }

        ResolveReferences();
        if (cameraZoomController != null &&
            !cameraZoomController.AcquireGameplayFramingProfile(
                owner,
                orthographicSizeMultiplier,
                immediate))
        {
            return false;
        }

        gameplayFramingOwner = owner;
        targetGameplayFramingOffset = worldOffset;
        targetGameplayAimOffsetMultiplier = Mathf.Max(0.01f, aimOffsetMultiplier);

        if (immediate)
        {
            currentGameplayFramingOffset = targetGameplayFramingOffset;
            currentGameplayAimOffsetMultiplier = targetGameplayAimOffsetMultiplier;
        }

        return true;
    }

    public void ReleaseGameplayFramingProfile(object owner, bool immediate)
    {
        if (owner == null || !ReferenceEquals(gameplayFramingOwner, owner))
        {
            return;
        }

        gameplayFramingOwner = null;
        targetGameplayFramingOffset = Vector2.zero;
        targetGameplayAimOffsetMultiplier = 1f;

        if (immediate)
        {
            currentGameplayFramingOffset = Vector2.zero;
            currentGameplayAimOffsetMultiplier = 1f;
        }

        ResolveReferences();
        if (cameraZoomController != null)
        {
            cameraZoomController.ReleaseGameplayFramingProfile(owner, immediate);
        }
    }

    public Vector3 ResolveGameplayFramingCenter(Vector3 gameplayTargetPosition)
    {
        Vector3 center = gameplayTargetPosition + (Vector3)targetGameplayFramingOffset;
        center = ClampToMapBounds(center);
        center.z = cameraWorldZ;
        return center;
    }

    public bool IsAtCinematicFocusCenter(Vector3 resolvedFocusCenter, float positionTolerance)
    {
        ResolveReferences();

        if (!cinematicFocusActive || cinematicFocusBlendActive || mainCamera == null)
        {
            return false;
        }

        float tolerance = Mathf.Max(0.0001f, positionTolerance);
        float toleranceSquared = tolerance * tolerance;
        Vector2 expectedPosition = resolvedFocusCenter;
        Vector2 actualPosition = transform.position;
        Vector2 focusPosition = cinematicFocusWorldPosition;

        return (actualPosition - expectedPosition).sqrMagnitude <= toleranceSquared &&
               (focusPosition - expectedPosition).sqrMagnitude <= toleranceSquared &&
               !IsShakeActive;
    }

    public void SetAimOffsetAssist(float aimOffsetMultiplier, float mouseDistanceMultiplier)
    {
        runtimeAimOffsetMultiplier = Mathf.Max(0.01f, aimOffsetMultiplier);
        runtimeMouseDistanceMultiplier = Mathf.Max(0.01f, mouseDistanceMultiplier);
    }

    public void ResetAimOffsetAssist()
    {
        runtimeAimOffsetMultiplier = 1f;
        runtimeMouseDistanceMultiplier = 1f;
    }

    public bool TryEnterScriptedVerticalScroll(
        object owner,
        Vector2 startCenter,
        Vector2 terminalCenter,
        float speed,
        bool startPaused,
        bool allowStartOutsideVerticalMapBounds = false)
    {
        if (owner == null || !IsFinite(startCenter) || !IsFinite(terminalCenter) || speed <= 0f)
        {
            return false;
        }

        if (scriptedVerticalScrollOwner != null)
        {
            return ReferenceEquals(scriptedVerticalScrollOwner, owner);
        }

        if (cinematicFocusActive || cinematicFocusBlendActive || cinematicReturnActive)
        {
            return false;
        }

        ResolveReferences();
        if (mainCamera == null || shakeRoot == null)
        {
            return false;
        }

        Vector3 resolvedStart = ClampToMapBounds(startCenter);
        Vector3 resolvedTerminal = ClampToMapBounds(terminalCenter);
        if (allowStartOutsideVerticalMapBounds)
        {
            resolvedStart.y = startCenter.y;
        }

        if (Mathf.Abs(resolvedStart.x - resolvedTerminal.x) > 0.0001f ||
            resolvedStart.y + 0.0001f < resolvedTerminal.y)
        {
            return false;
        }

        aimOffsetBeforeScriptedScroll = currentAimOffset;
        targetAimOffsetBeforeScriptedScroll = targetAimOffset;
        stableMousePositionBeforeScriptedScroll = stableMouseScreenPosition;
        hadStableMousePositionBeforeScriptedScroll = hasStableMousePosition;
        mouseLookAheadWasActiveBeforeScriptedScroll = mouseLookAheadActive;

        scriptedVerticalScrollOwner = owner;
        scriptedVerticalScrollCenter = resolvedStart;
        scriptedVerticalScrollTerminalCenter = resolvedTerminal;
        scriptedVerticalScrollSpeed = Mathf.Max(0.01f, speed);
        scriptedVerticalScrollPaused = startPaused;
        scriptedVerticalScrollReachedTerminal =
            Mathf.Abs(scriptedVerticalScrollCenter.y - scriptedVerticalScrollTerminalCenter.y) <= 0.0001f;

        currentAimOffset = Vector2.zero;
        targetAimOffset = Vector2.zero;
        mouseLookAheadActive = false;
        hasStableMousePosition = false;
        currentCameraCenter = new Vector3(
            scriptedVerticalScrollCenter.x,
            scriptedVerticalScrollCenter.y,
            cameraWorldZ
        );
        cameraCenterInitialized = true;
        transform.position = currentCameraCenter;
        return true;
    }

    public bool SetScriptedVerticalScrollPaused(object owner, bool paused)
    {
        if (owner == null || !ReferenceEquals(scriptedVerticalScrollOwner, owner))
        {
            return false;
        }

        scriptedVerticalScrollPaused = paused;
        return true;
    }

    public bool ForceScriptedVerticalScrollToTerminal(object owner)
    {
        if (owner == null || !ReferenceEquals(scriptedVerticalScrollOwner, owner))
        {
            return false;
        }

        scriptedVerticalScrollCenter = scriptedVerticalScrollTerminalCenter;
        scriptedVerticalScrollReachedTerminal = true;
        currentCameraCenter = new Vector3(
            scriptedVerticalScrollCenter.x,
            scriptedVerticalScrollCenter.y,
            cameraWorldZ
        );
        transform.position = currentCameraCenter;
        return true;
    }

    public bool ExitScriptedVerticalScroll(object owner)
    {
        if (owner == null || !ReferenceEquals(scriptedVerticalScrollOwner, owner))
        {
            return false;
        }

        currentAimOffset = aimOffsetBeforeScriptedScroll;
        targetAimOffset = targetAimOffsetBeforeScriptedScroll;
        stableMouseScreenPosition = stableMousePositionBeforeScriptedScroll;
        hasStableMousePosition = hadStableMousePositionBeforeScriptedScroll;
        mouseLookAheadActive = mouseLookAheadWasActiveBeforeScriptedScroll;

        ClearScriptedVerticalScrollState();
        currentCameraCenter = transform.position;
        currentCameraCenter.z = cameraWorldZ;
        cameraCenterInitialized = true;
        return true;
    }

    public bool IsScriptedVerticalScrollOwnedBy(object owner)
    {
        return owner != null && ReferenceEquals(scriptedVerticalScrollOwner, owner);
    }

    public void SetCinematicInputOffsetLocked(bool locked)
    {
        cinematicInputOffsetLocked = locked;

        if (locked)
        {
            mouseLookAheadActive = false;
            targetAimOffset = Vector2.zero;
        }
    }

    private void ResolveReferences()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (cameraZoomController == null)
        {
            cameraZoomController = GetComponent<CameraZoomController2D>();
        }

        if (shakeRoot == null && mainCamera != null && mainCamera.transform.parent != transform)
        {
            shakeRoot = mainCamera.transform.parent;
        }

        if (player == null)
        {
            playerController = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            player = playerController != null ? playerController.transform : null;
        }
        else if (playerController == null)
        {
            playerController = player.GetComponent<PlayerController2D>();
        }

        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
        }
    }

    private void UpdateAimOffset(float deltaTime)
    {
        bool inputLocked = cinematicFocusActive ||
                           cinematicInputOffsetLocked ||
                           (playerController != null && !playerController.ControlEnabled);

        targetAimOffset = inputLocked ? Vector2.zero : CalculateMouseAimOffset();

        float sharpness = targetAimOffset.sqrMagnitude > currentAimOffset.sqrMagnitude
            ? outwardSharpness
            : recenterSharpness;

        if (sharpness <= 0f || deltaTime <= 0f)
        {
            if (sharpness <= 0f)
            {
                currentAimOffset = targetAimOffset;
            }

            return;
        }

        float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
        currentAimOffset = Vector2.Lerp(currentAimOffset, targetAimOffset, blend);

        if (targetAimOffset == Vector2.zero && currentAimOffset.sqrMagnitude <= 0.000001f)
        {
            currentAimOffset = Vector2.zero;
        }
    }

    private Vector3 UpdateScriptedVerticalScroll(float deltaTime)
    {
        if (!scriptedVerticalScrollPaused && !scriptedVerticalScrollReachedTerminal)
        {
            scriptedVerticalScrollCenter.y = Mathf.MoveTowards(
                scriptedVerticalScrollCenter.y,
                scriptedVerticalScrollTerminalCenter.y,
                scriptedVerticalScrollSpeed * deltaTime
            );

            if (scriptedVerticalScrollCenter.y <=
                scriptedVerticalScrollTerminalCenter.y + 0.0001f)
            {
                scriptedVerticalScrollCenter.y = scriptedVerticalScrollTerminalCenter.y;
                scriptedVerticalScrollReachedTerminal = true;
            }
        }

        scriptedVerticalScrollCenter.x = scriptedVerticalScrollTerminalCenter.x;
        currentCameraCenter = new Vector3(
            scriptedVerticalScrollCenter.x,
            scriptedVerticalScrollCenter.y,
            cameraWorldZ
        );
        return currentCameraCenter;
    }

    private void ClearScriptedVerticalScrollState()
    {
        scriptedVerticalScrollOwner = null;
        scriptedVerticalScrollCenter = Vector2.zero;
        scriptedVerticalScrollTerminalCenter = Vector2.zero;
        scriptedVerticalScrollSpeed = 0f;
        scriptedVerticalScrollPaused = false;
        scriptedVerticalScrollReachedTerminal = false;
        aimOffsetBeforeScriptedScroll = Vector2.zero;
        targetAimOffsetBeforeScriptedScroll = Vector2.zero;
        stableMousePositionBeforeScriptedScroll = Vector2.zero;
        hadStableMousePositionBeforeScriptedScroll = false;
        mouseLookAheadWasActiveBeforeScriptedScroll = false;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    private Vector2 CalculateMouseAimOffset()
    {
        if (Mouse.current == null || mainCamera == null)
        {
            return Vector2.zero;
        }

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Rect pixelRect = mainCamera.pixelRect;

        if (pixelRect.width <= 0f || pixelRect.height <= 0f)
        {
            return Vector2.zero;
        }

        mouseScreen.x = Mathf.Clamp(mouseScreen.x, pixelRect.xMin, pixelRect.xMax);
        mouseScreen.y = Mathf.Clamp(mouseScreen.y, pixelRect.yMin, pixelRect.yMax);

        float epsilon = Mathf.Max(0f, mouseMotionEpsilonPixels);
        if (!hasStableMousePosition)
        {
            stableMouseScreenPosition = mouseScreen;
            hasStableMousePosition = true;
        }
        else if (epsilon <= 0f ||
                 (mouseScreen - stableMouseScreenPosition).sqrMagnitude >= epsilon * epsilon)
        {
            stableMouseScreenPosition = mouseScreen;
        }

        Vector2 fromCenterPixels = stableMouseScreenPosition - pixelRect.center;
        float normalizationRadius = Mathf.Max(1f, Mathf.Min(pixelRect.width, pixelRect.height) * 0.5f);
        float normalizedRadius = fromCenterPixels.magnitude / normalizationRadius;

        if (mouseLookAheadActive)
        {
            if (normalizedRadius <= lookAheadExitRadius)
            {
                mouseLookAheadActive = false;
            }
        }
        else if (normalizedRadius >= lookAheadEnterRadius)
        {
            mouseLookAheadActive = true;
        }

        if (!mouseLookAheadActive || fromCenterPixels.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        float effectiveFullRadius = Mathf.Max(
            lookAheadEnterRadius + 0.01f,
            fullOffsetRadius * Mathf.Max(0.01f, runtimeMouseDistanceMultiplier)
        );
        float zoneRatio = Mathf.InverseLerp(lookAheadExitRadius, effectiveFullRadius, normalizedRadius);
        zoneRatio = Mathf.SmoothStep(0f, 1f, zoneRatio);

        float effectiveMaxOffset = maxAimOffset *
                                   Mathf.Max(0.01f, runtimeAimOffsetMultiplier) *
                                   Mathf.Max(0.01f, currentGameplayAimOffsetMultiplier);
        return fromCenterPixels.normalized * (effectiveMaxOffset * zoneRatio);
    }

    private void UpdateGameplayFramingProfile(float deltaTime)
    {
        float sharpness = Mathf.Max(0f, gameplayFramingTransitionSharpness);
        if (sharpness <= 0f || deltaTime <= 0f)
        {
            if (sharpness <= 0f)
            {
                currentGameplayFramingOffset = targetGameplayFramingOffset;
                currentGameplayAimOffsetMultiplier = targetGameplayAimOffsetMultiplier;
            }

            return;
        }

        float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
        currentGameplayFramingOffset = Vector2.Lerp(
            currentGameplayFramingOffset,
            targetGameplayFramingOffset,
            blend
        );
        currentGameplayAimOffsetMultiplier = Mathf.Lerp(
            currentGameplayAimOffsetMultiplier,
            targetGameplayAimOffsetMultiplier,
            blend
        );

        if ((currentGameplayFramingOffset - targetGameplayFramingOffset).sqrMagnitude <= 0.000001f)
        {
            currentGameplayFramingOffset = targetGameplayFramingOffset;
        }

        if (Mathf.Abs(currentGameplayAimOffsetMultiplier - targetGameplayAimOffsetMultiplier) <= 0.0001f)
        {
            currentGameplayAimOffsetMultiplier = targetGameplayAimOffsetMultiplier;
        }
    }

    private Vector3 ResolveDesiredCameraCenter(
        Vector3 playerCenter,
        float deltaTime,
        float unscaledDeltaTime)
    {
        if (!cameraCenterInitialized)
        {
            currentCameraCenter = playerCenter;
            cameraCenterInitialized = true;
        }

        if (cinematicFocusBlendActive)
        {
            cinematicFocusBlendElapsed += unscaledDeltaTime;
            float normalized = cinematicFocusBlendDuration > 0.0001f
                ? Mathf.Clamp01(cinematicFocusBlendElapsed / cinematicFocusBlendDuration)
                : 1f;
            float eased = cinematicFocusBlendCurve != null && cinematicFocusBlendCurve.length > 0
                ? Mathf.Clamp01(cinematicFocusBlendCurve.Evaluate(normalized))
                : Mathf.SmoothStep(0f, 1f, normalized);

            currentCameraCenter = Vector3.LerpUnclamped(
                cinematicFocusBlendStart,
                cinematicFocusBlendTarget,
                eased
            );

            if (normalized >= 1f)
            {
                currentCameraCenter = cinematicFocusBlendTarget;
                cinematicFocusBlendActive = false;
                cinematicFocusBlendCurve = null;
            }

            return currentCameraCenter;
        }

        if (cinematicFocusActive)
        {
            currentCameraCenter = DampPosition(
                currentCameraCenter,
                cinematicFocusWorldPosition,
                cinematicFocusSharpness,
                deltaTime
            );
            return currentCameraCenter;
        }

        if (cinematicReturnActive)
        {
            currentCameraCenter = DampPosition(
                currentCameraCenter,
                playerCenter,
                cinematicReturnSharpness,
                deltaTime
            );

            if ((currentCameraCenter - playerCenter).sqrMagnitude <= 0.0001f)
            {
                cinematicReturnActive = false;
                currentCameraCenter = playerCenter;
            }

            return currentCameraCenter;
        }

        currentCameraCenter = playerCenter;
        return currentCameraCenter;
    }

    private Vector3 ClampToMapBounds(Vector3 center)
    {
        float orthographicSize = mainCamera != null ? mainCamera.orthographicSize : 0f;
        return ClampToMapBounds(center, orthographicSize);
    }

    private Vector3 ClampToMapBounds(Vector3 center, float orthographicSize)
    {
        if (mapGenerator == null || mainCamera == null)
        {
            return center;
        }

        Bounds bounds = mapGenerator.MapBounds;
        if (bounds.size.x <= 0.01f || bounds.size.y <= 0.01f)
        {
            return center;
        }

        float halfHeight = Mathf.Max(0.01f, orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.01f, mainCamera.aspect);
        float padding = Mathf.Max(0f, mapBoundsPadding);

        float minX = bounds.min.x + halfWidth + padding;
        float maxX = bounds.max.x - halfWidth - padding;
        float minY = bounds.min.y + halfHeight + padding;
        float maxY = bounds.max.y - halfHeight - padding;

        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x;
        center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : bounds.center.y;
        return center;
    }

    private void ApplyShake(float deltaTime)
    {
        if (shakeRoot == null)
        {
            return;
        }

        if (!enableCameraShake || shakeRemaining <= 0f || shakeAmplitude <= 0f)
        {
            shakeRoot.localPosition = Vector3.zero;
            return;
        }

        float safeDuration = Mathf.Max(0.01f, shakeDuration);
        float envelope = Mathf.Clamp01(shakeRemaining / safeDuration);
        envelope *= envelope;
        shakeNoiseTime += deltaTime * Mathf.Max(1f, shakeFrequency);

        float x = Mathf.PerlinNoise(shakeSeed.x, shakeNoiseTime) * 2f - 1f;
        float y = Mathf.PerlinNoise(shakeSeed.y, shakeNoiseTime + 17.37f) * 2f - 1f;
        Vector2 offset = Vector2.ClampMagnitude(new Vector2(x, y), 1f) * (shakeAmplitude * envelope);
        shakeRoot.localPosition = new Vector3(offset.x, offset.y, 0f);

        shakeRemaining -= deltaTime;
        if (shakeRemaining <= 0f)
        {
            ResetShakeState();
        }
    }

    private void ResetShakeState()
    {
        shakeRemaining = 0f;
        shakeDuration = 0f;
        shakeAmplitude = 0f;
        shakeNoiseTime = 0f;

        if (shakeRoot != null)
        {
            shakeRoot.localPosition = Vector3.zero;
        }
    }

    private void ResetShakeNoise()
    {
        shakeSequence++;
        float seed = Mathf.Abs(GetInstanceID() * 0.01357f + shakeSequence * 17.17f);
        shakeSeed = new Vector2(seed % 997f, (seed * 1.6180339f + 73.1f) % 991f);
        shakeNoiseTime = 0f;
    }

    private static Vector3 DampPosition(Vector3 current, Vector3 target, float sharpness, float deltaTime)
    {
        if (sharpness <= 0f || deltaTime <= 0f)
        {
            return sharpness <= 0f ? target : current;
        }

        float blend = 1f - Mathf.Exp(-sharpness * deltaTime);
        return Vector3.Lerp(current, target, blend);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void ReportDiagnostics()
    {
        if (!enableCameraDiagnostics || !Application.isPlaying || Time.unscaledTime < nextDiagnosticLogTime)
        {
            return;
        }

        nextDiagnosticLogTime = Time.unscaledTime + Mathf.Max(0.1f, diagnosticLogInterval);
        Vector3 playerScreen = player != null && mainCamera != null
            ? mainCamera.WorldToScreenPoint(player.position)
            : Vector3.zero;
        Vector3 playerPosition = player != null ? player.position : Vector3.zero;
        int activeControllers = FindObjectsByType<GungeonStyleCamera2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        ).Length;

        Debug.Log(
            $"[GameplayCamera] player={playerPosition:F3} rig={transform.position:F3} " +
            $"playerScreen={playerScreen:F2} aim={currentAimOffset:F3}/{targetAimOffset:F3} " +
            $"focus={cinematicFocusActive} return={cinematicReturnActive} " +
            $"scriptedScroll={IsScriptedVerticalScrollActive}/{scriptedVerticalScrollReachedTerminal} " +
            $"shake={(shakeRoot != null ? shakeRoot.localPosition : Vector3.zero):F3} " +
            $"ortho={mainCamera.orthographicSize:F4} owners={activeControllers} worldPixelSnap=false",
            this
        );
    }
#endif
}
