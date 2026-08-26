using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class PlayerRadarScanner : MonoBehaviour
{
    private readonly HashSet<object> externalInputLocks = new HashSet<object>();
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string radarActionName = "Radar";
    [SerializeField] private Key radarFallbackKey = Key.Q;
    [SerializeField] private string quickScanActionName = "RadarQuickScan";
    [SerializeField] private string quickScanFallbackDisplay = "Mouse 4";

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerRuntimeBonusState runtimeBonusState;
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private RadarPanelAnimator radarPanelAnimator;
    [SerializeField] private RadarHUD radarHUD;
    [SerializeField] private PlayerRadarVFXController radarVFX;
    [SerializeField] private MapDiscoveryController mapDiscoveryController;
    [SerializeField] private ExpeditionObjectiveDirector objectiveDirector;

    [Header("Scan Rule")]
    [SerializeField] private float holdTime = 1f;
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private LayerMask radarTargetLayer = ~0;
    [SerializeField, Min(0f)] private float quickScanCooldown = 0.5f;

    [Header("Audio Volume Scales")]
    [SerializeField, Range(0f, 1f)] private float chargeStartVolumeScale = 0.55f;
    [SerializeField, Range(0f, 1f)] private float chargeLoopVolumeScale = 0.3f;
    [SerializeField, Range(0f, 1f)] private float chargeCancelVolumeScale = 0.4f;
    [SerializeField, Range(0f, 1f)] private float scanPulseVolumeScale = 0.75f;

    [Header("Passive Local Radar")]
    [FormerlySerializedAs("enablePassiveDiscovery")]
    [SerializeField] private bool enablePassiveRadar = true;
    [FormerlySerializedAs("passiveDiscoveryRadiusRatio")]
    [SerializeField, Range(0.1f, 1f)] private float passiveRadarRadiusRatio = 0.5f;
    [FormerlySerializedAs("passiveDiscoveryTravelStep")]
    [SerializeField, Min(0.1f)] private float passiveRadarTravelStep = 0.75f;
    [SerializeField, Min(0.1f)] private float passiveRadarRefreshInterval = 0.35f;

    [Tooltip("레이더가 열려 있을 때 Q를 짧게 누르면 닫습니다. 길게 누르면 다시 스캔합니다.")]
    [SerializeField] private bool shortPressClosesRadar = true;

    [Header("Global Revealed Targets")]
    [Tooltip("코어 추적 신호로 공개된 코어는 현재 스캔 반경 밖이어도 레이더 방향 표식에 포함합니다.")]
    [SerializeField] private bool includeGloballyRevealedCore = true;

    [Header("Sniper Option")]
    [SerializeField] private float sniperLingerTime = 6f;

    [Header("Warning Messages")]
    [SerializeField] private string holdNotEnoughMessage = "{0}를 길게 눌러 스캔할 수 있습니다.";
    [SerializeField] private string noTargetMessage = "탐지된 대상이 없습니다.";

    [Header("Debug")]
    [SerializeField] private bool drawScanRadius = true;

    private InputAction radarAction;
    private InputAction quickScanAction;
    private bool isHolding;
    private bool isRadarOpen;
    private bool passiveAutoOpenSuppressed;
    private bool hasNormalRadarPresentation;
    private float holdTimer;
    private float lastScanTime = -999f;
    private float nextQuickScanAllowedTime;
    private float lastNormalScanRadius;
    private float lastPassiveRadarRadius;
    private float nextPassiveRadarRefreshTime;
    private Vector2 lastPassiveRadarPosition;
    private bool hasPassiveRadarPosition;

    private readonly Collider2D[] scanBuffer = new Collider2D[256];
    private readonly List<RadarTarget> scannedTargets = new List<RadarTarget>(128);
    private readonly HashSet<RadarTarget> scannedSet = new HashSet<RadarTarget>();
    private readonly List<RadarTarget> displayedTargets = new List<RadarTarget>(128);
    private readonly HashSet<RadarTarget> displayedSet = new HashSet<RadarTarget>();
    private readonly List<RadarTarget> passiveRadarTargets = new List<RadarTarget>(64);
    private readonly HashSet<RadarTarget> passiveRadarSet = new HashSet<RadarTarget>();
    private readonly HashSet<RadarTarget> temporaryPulseSet = new HashSet<RadarTarget>();
    private readonly Dictionary<UnityEngine.Object, TemporaryRevealState> temporaryRevealStates =
        new Dictionary<UnityEngine.Object, TemporaryRevealState>(2);
    private readonly List<UnityEngine.Object> expiredRevealSources = new List<UnityEngine.Object>(2);
    private Coroutine temporaryRevealRoutine;

    private sealed class TemporaryRevealState
    {
        public float expiresAt;
        public float radius;
    }

    public bool IsHolding => isHolding;
    public bool IsRadarOpen => isRadarOpen;
    public bool IsRadarPanelOpen => radarPanelAnimator != null ? radarPanelAnimator.IsOpen : isRadarOpen;
    public float HoldRatio => holdTime <= 0f ? 1f : Mathf.Clamp01(holdTimer / holdTime);
    public float ScanRadius => ResolveEffectiveScanRadius();
    public float PassiveRadarRadius => ResolveEffectiveScanRadius() * Mathf.Clamp01(passiveRadarRadiusRatio);
    public float LastScanTime => lastScanTime;
    public float SniperLingerTime => sniperLingerTime + (runtimeBonusState != null ? runtimeBonusState.RadarStealthDurationBonus : 0f);
    public IReadOnlyList<RadarTarget> LastScannedTargets => scannedTargets;
    public string RadarBindingDisplay => InputBindingUtility.GetDisplayString(
        inputActions,
        actionMapName,
        radarActionName,
        radarFallbackKey.ToString()
    );
    public string QuickScanBindingDisplay => InputBindingUtility.GetDisplayString(
        inputActions,
        actionMapName,
        quickScanActionName,
        quickScanFallbackDisplay
    );

    public event Action<Vector2, float, IReadOnlyList<RadarTarget>> ScanCompleted;

    private void Reset()
    {
        playerHealth = GetComponent<PlayerHealth>();
        weaponController = GetComponent<PlayerWeaponController>();
        runtimeBonusState = GetComponent<PlayerRuntimeBonusState>();
        radarVFX = GetComponent<PlayerRadarVFXController>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        BindInput();
        lastPassiveRadarPosition = transform.position;
        hasPassiveRadarPosition = true;
        nextPassiveRadarRefreshTime = 0f;
        passiveAutoOpenSuppressed = false;
        objectiveDirector ??= ExpeditionObjectiveDirector.Instance;

        if (includeGloballyRevealedCore && objectiveDirector != null)
        {
            objectiveDirector.CoreRevealedEvent += HandleCoreGloballyRevealed;
        }
    }

    private void OnDisable()
    {
        if (objectiveDirector != null)
        {
            objectiveDirector.CoreRevealedEvent -= HandleCoreGloballyRevealed;
        }

        radarAction?.Disable();
        quickScanAction?.Disable();
        CancelHold();
        ClearAllTemporaryReveals();
        passiveRadarTargets.Clear();
        passiveRadarSet.Clear();
        CloseRadar();
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            CancelHold();
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            CancelHold();
            CloseRadar();
            return;
        }

        UpdatePassiveRadar();

        if (externalInputLocks.Count > 0)
        {
            CancelHold();
            return;
        }

        UpdateQuickScanInput();
        UpdateHoldInput();
    }

    public void SetExternalInputLocked(object source, bool locked)
    {
        if (source == null)
        {
            return;
        }

        if (locked)
        {
            externalInputLocks.Add(source);
            CancelHold();
        }
        else
        {
            externalInputLocks.Remove(source);
        }
    }

    private void UpdatePassiveRadar()
    {
        if (!enablePassiveRadar)
        {
            return;
        }

        Vector2 currentPosition = transform.position;
        if (!hasPassiveRadarPosition)
        {
            lastPassiveRadarPosition = currentPosition;
            hasPassiveRadarPosition = true;
            return;
        }

        float travelStep = Mathf.Max(0.1f, passiveRadarTravelStep);
        bool movedEnough = (currentPosition - lastPassiveRadarPosition).sqrMagnitude >= travelStep * travelStep;
        bool intervalElapsed = Time.time >= nextPassiveRadarRefreshTime;
        if (!movedEnough && !intervalElapsed)
        {
            return;
        }

        lastPassiveRadarPosition = currentPosition;
        nextPassiveRadarRefreshTime = Time.time + Mathf.Max(0.1f, passiveRadarRefreshInterval);
        lastPassiveRadarRadius = PassiveRadarRadius;
        passiveRadarTargets.Clear();
        passiveRadarSet.Clear();

        int count = Physics2D.OverlapCircleNonAlloc(
            currentPosition,
            lastPassiveRadarRadius,
            scanBuffer,
            radarTargetLayer
        );

        for (int i = 0; i < count; i++)
        {
            RadarTarget target = scanBuffer[i] != null
                ? scanBuffer[i].GetComponentInParent<RadarTarget>()
                : null;

            if (target == null || !target.IsRadarVisible || !passiveRadarSet.Add(target))
            {
                continue;
            }

            passiveRadarTargets.Add(target);
        }

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        if (mapDiscoveryController != null)
        {
            mapDiscoveryController.RevealCircle(
                currentPosition,
                mapDiscoveryController.TraversalRevealRadius
            );
        }

        RefreshPassiveRadarPresentation();
    }

    private void RefreshPassiveRadarPresentation()
    {
        if (passiveRadarTargets.Count > 0)
        {
            if (isRadarOpen)
            {
                RefreshRadarPresentation();
            }
            else if (!passiveAutoOpenSuppressed)
            {
                OpenRadar(lastPassiveRadarRadius, false, false);
            }

            return;
        }

        if (!isRadarOpen)
        {
            return;
        }

        RefreshRadarPresentation();
    }

    private void CacheReferences()
    {
        playerHealth ??= GetComponent<PlayerHealth>();
        weaponController ??= GetComponent<PlayerWeaponController>();
        runtimeBonusState ??= GetComponent<PlayerRuntimeBonusState>();
        radarVFX ??= GetComponent<PlayerRadarVFXController>();
        expeditionHUD ??= FindFirstObjectByType<ExpeditionHUD>();
        radarPanelAnimator ??= FindFirstObjectByType<RadarPanelAnimator>();
        radarHUD ??= FindFirstObjectByType<RadarHUD>();
        mapDiscoveryController ??= MapDiscoveryController.Instance;

        if (objectiveDirector == null && Application.isPlaying)
        {
            objectiveDirector = ExpeditionObjectiveDirector.Instance;
        }
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        radarAction = InputBindingUtility.ResolveAction(inputActions, actionMapName, radarActionName);
        quickScanAction = InputBindingUtility.ResolveAction(inputActions, actionMapName, quickScanActionName);
        radarAction?.Enable();
        quickScanAction?.Enable();
    }

    private void UpdateQuickScanInput()
    {
        if (!isRadarOpen || isHolding || !WasQuickScanPressedThisFrame())
        {
            return;
        }

        TryQuickScan();
    }

    private bool WasQuickScanPressedThisFrame()
    {
        if (quickScanAction != null)
        {
            return quickScanAction.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.backButton.wasPressedThisFrame;
    }

    private void UpdateHoldInput()
    {
        bool pressedThisFrame = WasRadarPressedThisFrame();
        bool held = IsRadarHeld();
        bool releasedThisFrame = WasRadarReleasedThisFrame();

        if (pressedThisFrame && isRadarOpen && shortPressClosesRadar)
        {
            CancelHold();
            CloseRadar();
            return;
        }

        if (pressedThisFrame)
        {
            BeginHold();
        }

        if (isHolding && held)
        {
            holdTimer += Time.deltaTime;
            radarVFX?.SetChargeRatio(HoldRatio);
        }

        if (releasedThisFrame)
        {
            EndHold();
        }
    }

    private bool WasRadarPressedThisFrame()
    {
        if (radarAction != null)
        {
            return radarAction.WasPressedThisFrame();
        }

        KeyControl key = Keyboard.current != null ? Keyboard.current[radarFallbackKey] : null;
        return key != null && key.wasPressedThisFrame;
    }

    private bool IsRadarHeld()
    {
        if (radarAction != null)
        {
            return radarAction.IsPressed();
        }

        KeyControl key = Keyboard.current != null ? Keyboard.current[radarFallbackKey] : null;
        return key != null && key.isPressed;
    }

    private bool WasRadarReleasedThisFrame()
    {
        if (radarAction != null)
        {
            return radarAction.WasReleasedThisFrame();
        }

        KeyControl key = Keyboard.current != null ? Keyboard.current[radarFallbackKey] : null;
        return key != null && key.wasReleasedThisFrame;
    }

    private void BeginHold()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        isHolding = true;
        holdTimer = 0f;

        AudioManager.PlayAt(SoundEventIds.RadarChargeStart, transform.position, chargeStartVolumeScale);
        AudioManager.PlayLoop(SoundEventIds.RadarChargeLoop, "radar_charge", chargeLoopVolumeScale);

        float effectiveRadius = ResolveEffectiveScanRadius();
        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(effectiveRadius);
            radarVFX.BeginCharge();
            radarVFX.SetChargeRatio(0f);
        }
    }

    private void EndHold()
    {
        if (!isHolding)
        {
            return;
        }

        float finalHoldTime = holdTimer;
        isHolding = false;
        holdTimer = 0f;

        if (finalHoldTime < holdTime)
        {
            AudioManager.StopLoop("radar_charge");
            AudioManager.PlayAt(SoundEventIds.RadarChargeCancel, transform.position, chargeCancelVolumeScale);
            radarVFX?.CancelCharge();

            if (isRadarOpen && shortPressClosesRadar)
            {
                CloseRadar();
                return;
            }

            ShowWarning(ResolveHoldNotEnoughMessage());
            return;
        }

        TryScan();
    }

    private void CancelHold()
    {
        bool wasHolding = isHolding;
        isHolding = false;
        holdTimer = 0f;

        if (wasHolding)
        {
            AudioManager.StopLoop("radar_charge");
        }

        radarVFX?.CancelCharge();
    }

    private string ResolveHoldNotEnoughMessage()
    {
        string radarKey = InputBindingUtility.GetDisplayString(
            inputActions,
            actionMapName,
            radarActionName,
            radarFallbackKey.ToString()
        );

        return string.IsNullOrWhiteSpace(holdNotEnoughMessage)
            ? radarKey
            : string.Format(holdNotEnoughMessage, radarKey);
    }

    public bool TryScan()
    {
        return ExecuteActiveScan(false);
    }

    public bool TryQuickScan()
    {
        if (!isRadarOpen || isHolding || Time.unscaledTime < nextQuickScanAllowedTime)
        {
            return false;
        }

        nextQuickScanAllowedTime = Time.unscaledTime + Mathf.Max(0f, quickScanCooldown);
        return ExecuteActiveScan(true);
    }

    private bool ExecuteActiveScan(bool isQuickScan)
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            radarVFX?.CancelCharge();
            return false;
        }

        float effectiveRadius = ResolveEffectiveScanRadius();

        if (!isQuickScan)
        {
            AudioManager.StopLoop("radar_charge");
        }

        AudioManager.PlayAt(SoundEventIds.RadarScanPulse, transform.position, scanPulseVolumeScale);

        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(effectiveRadius);

            if (isQuickScan)
            {
                radarVFX.PlayPulse(effectiveRadius);
            }
            else
            {
                radarVFX.CompleteScan();
            }
        }

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        ScanTargets(effectiveRadius);
        lastNormalScanRadius = effectiveRadius;
        hasNormalRadarPresentation = scannedTargets.Count > 0;

        mapDiscoveryController?.RegisterRadarScan(transform.position, effectiveRadius, scannedTargets);

        ScanCompleted?.Invoke(transform.position, effectiveRadius, scannedTargets);
        lastScanTime = Time.time;

        if (scannedTargets.Count == 0)
        {
            if (!isQuickScan)
            {
                ShowWarning(noTargetMessage);
            }

            OpenRadar(effectiveRadius);
            return true;
        }

        OpenRadar(effectiveRadius);
        return true;
    }

    public int RevealTargetsTemporarily(UnityEngine.Object source, float radius, float duration)
    {
        if (!isActiveAndEnabled ||
            source == null ||
            duration <= 0f ||
            playerHealth != null && playerHealth.IsDead)
        {
            return 0;
        }

        float effectiveRadius = radius > 0f ? radius : ResolveEffectiveScanRadius();
        Vector2 origin = transform.position;
        temporaryPulseSet.Clear();
        int revealedCount = 0;
        int count = Physics2D.OverlapCircleNonAlloc(
            origin,
            effectiveRadius,
            scanBuffer,
            radarTargetLayer
        );

        for (int i = 0; i < count; i++)
        {
            RadarTarget target = scanBuffer[i] != null
                ? scanBuffer[i].GetComponentInParent<RadarTarget>()
                : null;

            if (target == null || !temporaryPulseSet.Add(target) || !target.IsRadarVisible)
            {
                continue;
            }

            if (target.SetTemporaryReveal(source, duration))
            {
                revealedCount++;
            }
        }

        if (revealedCount <= 0)
        {
            radarVFX?.PlayPulse(effectiveRadius);
            return 0;
        }

        float requestedExpiration = Time.time + duration;

        if (!temporaryRevealStates.TryGetValue(source, out TemporaryRevealState state))
        {
            state = new TemporaryRevealState();
            temporaryRevealStates.Add(source, state);
        }

        state.expiresAt = Mathf.Max(state.expiresAt, requestedExpiration);
        state.radius = Mathf.Max(state.radius, effectiveRadius);

        radarVFX?.PlayPulse(effectiveRadius);
        OpenRadar(effectiveRadius);

        if (temporaryRevealRoutine == null)
        {
            temporaryRevealRoutine = StartCoroutine(TemporaryRevealLifecycleRoutine());
        }

        return revealedCount;
    }

    public bool RegisterNearbyTarget(RadarTarget target, float maximumDistance, bool revealMapCell = true)
    {
        if (!isActiveAndEnabled || target == null || !target.IsRadarVisible || maximumDistance <= 0f)
        {
            return false;
        }

        Vector2 offset = target.WorldPosition - transform.position;
        if (offset.sqrMagnitude > maximumDistance * maximumDistance)
        {
            return false;
        }

        bool added = scannedSet.Add(target);
        if (added)
        {
            scannedTargets.Add(target);
        }

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        mapDiscoveryController?.DiscoverTarget(target, revealMapCell);
        hasNormalRadarPresentation = scannedTargets.Count > 0;

        if (isRadarOpen)
        {
            RefreshRadarPresentation();
        }

        return added;
    }

    public void PlayTacticalPulse(float radius)
    {
        radarVFX?.PlayPulse(radius > 0f ? radius : ResolveEffectiveScanRadius());
    }

    private void ScanTargets(float effectiveRadius)
    {
        scannedTargets.Clear();
        scannedSet.Clear();

        Vector2 origin = transform.position;
        WeaponTreeType weaponTreeType = ResolveCurrentWeaponTree();
        RadarScanContext context = new RadarScanContext(gameObject, transform, origin, weaponTreeType, effectiveRadius);

        int count = Physics2D.OverlapCircleNonAlloc(origin, effectiveRadius, scanBuffer, radarTargetLayer);

        for (int i = 0; i < count; i++)
        {
            RadarTarget target = scanBuffer[i] != null ? scanBuffer[i].GetComponentInParent<RadarTarget>() : null;
            TryAddScannedTarget(target, context);
        }

        if (!includeGloballyRevealedCore)
        {
            return;
        }

        foreach (RadarTarget target in RadarTarget.ActiveTargets)
        {
            if (target == null || target.MarkerType != RadarMarkerType.Core || !target.IsRadarVisible)
            {
                continue;
            }

            bool globallyRevealed = target.IsMapDiscovered ||
                                    (ExpeditionObjectiveDirector.Instance != null && ExpeditionObjectiveDirector.Instance.CoreRevealed);

            if (globallyRevealed && scannedSet.Add(target))
            {
                scannedTargets.Add(target);
                mapDiscoveryController?.DiscoverTarget(target, false);
            }
        }
    }

    private void TryAddScannedTarget(RadarTarget target, RadarScanContext context)
    {
        if (target == null || scannedSet.Contains(target) || !target.IsRadarVisible)
        {
            return;
        }

        if (target.OnRadarScanned(context) != RadarScanResult.Detected)
        {
            return;
        }

        scannedSet.Add(target);
        scannedTargets.Add(target);
        mapDiscoveryController?.DiscoverTarget(target, false);
    }

    private void OpenRadar(
        float effectiveRadius,
        bool animatePanel = true,
        bool clearPassiveAutoOpenSuppression = true)
    {
        if (clearPassiveAutoOpenSuppression)
        {
            passiveAutoOpenSuppressed = false;
        }

        BuildDisplayedTargets();

        if (radarHUD != null)
        {
            radarHUD.SetScanRadius(ResolveDisplayedRadius(effectiveRadius));
            radarHUD.SetTargets(displayedTargets, transform);
        }

        if (!isRadarOpen)
        {
            if (radarPanelAnimator != null)
            {
                if (animatePanel)
                {
                    radarPanelAnimator.Open();
                }
                else
                {
                    radarPanelAnimator.OpenImmediate();
                }
            }
        }

        isRadarOpen = true;
    }

    public void CloseRadar()
    {
        CloseRadar(true);
    }

    private void CloseRadar(bool animatePanel)
    {
        if (!isRadarOpen)
        {
            return;
        }

        // Unity scene shutdown can destroy either UI object before this player
        // component. Use Unity's overloaded null check instead of ?. so a stale
        // native object is not invoked while leaving Tutorial/Expedition.
        if (radarHUD != null)
        {
            radarHUD.Clear();
        }

        if (radarPanelAnimator != null)
        {
            if (animatePanel)
            {
                radarPanelAnimator.Close();
            }
            else
            {
                radarPanelAnimator.CloseImmediate();
            }
        }

        isRadarOpen = false;
        passiveAutoOpenSuppressed = true;
        hasNormalRadarPresentation = false;
    }

    private void HandleCoreGloballyRevealed()
    {
        if (!includeGloballyRevealedCore)
        {
            return;
        }

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        bool changed = false;

        foreach (RadarTarget target in RadarTarget.ActiveTargets)
        {
            if (target == null || target.MarkerType != RadarMarkerType.Core)
            {
                continue;
            }

            target.SetVisible(true);
            target.SetMapDiscovered(true);
            mapDiscoveryController?.DiscoverTarget(target, true);

            if (scannedSet.Add(target))
            {
                scannedTargets.Add(target);
                changed = true;
            }
        }

        if (changed && isRadarOpen && radarHUD != null)
        {
            RefreshRadarPresentation();
        }
    }

    private IEnumerator TemporaryRevealLifecycleRoutine()
    {
        while (temporaryRevealStates.Count > 0)
        {
            float nextExpiration = FindNextTemporaryRevealExpiration();
            float delay = Mathf.Max(0.05f, nextExpiration - Time.time);
            yield return new WaitForSeconds(delay);

            PruneExpiredTemporaryReveals();

            if (!isRadarOpen)
            {
                continue;
            }

            BuildDisplayedTargets();

            RefreshRadarPresentation();
        }

        temporaryRevealRoutine = null;
    }

    private void PruneExpiredTemporaryReveals()
    {
        float now = Time.time;
        expiredRevealSources.Clear();

        foreach (KeyValuePair<UnityEngine.Object, TemporaryRevealState> pair in temporaryRevealStates)
        {
            if (pair.Key == null || pair.Value == null || pair.Value.expiresAt <= now)
            {
                expiredRevealSources.Add(pair.Key);
            }
        }

        for (int i = 0; i < expiredRevealSources.Count; i++)
        {
            UnityEngine.Object source = expiredRevealSources[i];

            foreach (RadarTarget target in RadarTarget.ActiveTargets)
            {
                if (target != null)
                {
                    target.ClearTemporaryReveal(source);
                }
            }

            temporaryRevealStates.Remove(source);
        }
    }

    private void ClearAllTemporaryReveals()
    {
        if (temporaryRevealRoutine != null)
        {
            StopCoroutine(temporaryRevealRoutine);
            temporaryRevealRoutine = null;
        }

        foreach (UnityEngine.Object source in temporaryRevealStates.Keys)
        {
            foreach (RadarTarget target in RadarTarget.ActiveTargets)
            {
                if (target != null)
                {
                    target.ClearTemporaryReveal(source);
                }
            }
        }

        temporaryRevealStates.Clear();
        expiredRevealSources.Clear();
    }

    private bool HasActiveTemporaryReveals()
    {
        PruneExpiredTemporaryReveals();
        return temporaryRevealStates.Count > 0;
    }

    private float FindNextTemporaryRevealExpiration()
    {
        float nextExpiration = float.MaxValue;

        foreach (TemporaryRevealState state in temporaryRevealStates.Values)
        {
            if (state != null)
            {
                nextExpiration = Mathf.Min(nextExpiration, state.expiresAt);
            }
        }

        return nextExpiration == float.MaxValue ? Time.time : nextExpiration;
    }

    private void BuildDisplayedTargets()
    {
        displayedTargets.Clear();
        displayedSet.Clear();

        if (hasNormalRadarPresentation)
        {
            for (int i = 0; i < scannedTargets.Count; i++)
            {
                RadarTarget target = scannedTargets[i];

                if (target != null && target.IsRadarVisible && displayedSet.Add(target))
                {
                    displayedTargets.Add(target);
                }
            }
        }

        for (int i = 0; i < passiveRadarTargets.Count; i++)
        {
            RadarTarget target = passiveRadarTargets[i];

            if (target != null && target.IsRadarVisible && displayedSet.Add(target))
            {
                displayedTargets.Add(target);
            }
        }

        foreach (RadarTarget target in RadarTarget.ActiveTargets)
        {
            if (target != null && target.IsTemporarilyRevealed && displayedSet.Add(target))
            {
                displayedTargets.Add(target);
            }
        }
    }

    private void RefreshRadarPresentation()
    {
        if (!isRadarOpen || radarHUD == null)
        {
            return;
        }

        BuildDisplayedTargets();
        radarHUD.SetScanRadius(ResolveDisplayedRadius(lastNormalScanRadius));
        radarHUD.SetTargets(displayedTargets, transform);
    }

    private float ResolveDisplayedRadius(float fallbackRadius)
    {
        float radius = hasNormalRadarPresentation
            ? Mathf.Max(0.1f, lastNormalScanRadius)
            : passiveRadarTargets.Count > 0
                ? Mathf.Max(0.1f, lastPassiveRadarRadius)
                : Mathf.Max(0.1f, fallbackRadius);

        foreach (TemporaryRevealState state in temporaryRevealStates.Values)
        {
            if (state != null && state.expiresAt > Time.time)
            {
                radius = Mathf.Max(radius, state.radius);
            }
        }

        return radius;
    }

    private float ResolveEffectiveScanRadius()
    {
        float bonus = runtimeBonusState != null ? runtimeBonusState.RadarScanRadiusBonus : 0f;
        return Mathf.Max(0.1f, scanRadius + bonus);
    }

    private WeaponTreeType ResolveCurrentWeaponTree()
    {
        if (weaponController != null)
        {
            return weaponController.CurrentWeaponTree;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        return WeaponTreeType.MachineGun;
    }

    private void ShowWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (expeditionHUD != null)
        {
            expeditionHUD.ShowCommunication(
                ShipCommunicationChannel.Radar,
                message,
                ShipCommunicationSeverity.Warning
            );
            return;
        }

        FindFirstObjectByType<WarningMessageUI>()?.ShowCommunication(
            ShipCommunicationChannel.Radar,
            message,
            ShipCommunicationSeverity.Warning
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawScanRadius)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        float activeRadius = Application.isPlaying ? ResolveEffectiveScanRadius() : Mathf.Max(0.1f, scanRadius);
        Gizmos.DrawWireSphere(transform.position, activeRadius);

        if (enablePassiveRadar)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                transform.position,
                activeRadius * Mathf.Clamp01(passiveRadarRadiusRatio)
            );
        }
    }

    private void OnValidate()
    {
        holdTime = Mathf.Max(0f, holdTime);
        scanRadius = Mathf.Max(0.1f, scanRadius);
        quickScanCooldown = Mathf.Max(0f, quickScanCooldown);
        chargeStartVolumeScale = Mathf.Clamp01(chargeStartVolumeScale);
        chargeLoopVolumeScale = Mathf.Clamp01(chargeLoopVolumeScale);
        chargeCancelVolumeScale = Mathf.Clamp01(chargeCancelVolumeScale);
        scanPulseVolumeScale = Mathf.Clamp01(scanPulseVolumeScale);
    }
}
