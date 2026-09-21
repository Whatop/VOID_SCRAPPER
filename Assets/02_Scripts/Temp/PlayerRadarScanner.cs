using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

[Serializable]
public sealed class RadarModeInputState
{
    private bool isRadarActive;
    private float nextScanAllowedTime = float.NegativeInfinity;

    public bool IsRadarActive => isRadarActive;

    public bool TryToggle(bool inputAvailable, out bool activeAfterToggle)
    {
        activeAfterToggle = isRadarActive;
        if (!inputAvailable)
        {
            return false;
        }

        isRadarActive = !isRadarActive;
        activeAfterToggle = isRadarActive;
        return true;
    }

    public bool TrySetActive(bool active)
    {
        if (isRadarActive == active)
        {
            return false;
        }

        isRadarActive = active;
        return true;
    }

    public bool TryBeginInstantScan(
        bool inputAvailable,
        float unscaledTime,
        float cooldown)
    {
        if (!inputAvailable || !isRadarActive || unscaledTime < nextScanAllowedTime)
        {
            return false;
        }

        nextScanAllowedTime = unscaledTime + Mathf.Max(0f, cooldown);
        return true;
    }

    public void Reset()
    {
        isRadarActive = false;
        nextScanAllowedTime = float.NegativeInfinity;
    }
}

[DisallowMultipleComponent]
public class PlayerRadarScanner : MonoBehaviour
{
    private readonly HashSet<object> externalInputLocks = new HashSet<object>();
    private readonly RadarModeInputState radarModeInputState = new RadarModeInputState();
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
    [SerializeField] private RadarPanelAnimator radarPanelAnimator;
    [SerializeField] private RadarHUD radarHUD;
    [SerializeField] private PlayerRadarVFXController radarVFX;
    [SerializeField] private MapDiscoveryController mapDiscoveryController;
    [SerializeField] private ExpeditionObjectiveDirector objectiveDirector;

    [Header("Scan Rule")]
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private LayerMask radarTargetLayer = ~0;
    [FormerlySerializedAs("quickScanCooldown")]
    [SerializeField, Min(0f)] private float scanCooldown = 0.5f;

    [Header("Audio Volume Scales")]
    [SerializeField, Range(0f, 1f)] private float scanPulseVolumeScale = 0.75f;

    [Header("Passive Local Radar")]
    [FormerlySerializedAs("enablePassiveDiscovery")]
    [SerializeField] private bool enablePassiveRadar = true;
    [FormerlySerializedAs("passiveDiscoveryRadiusRatio")]
    [SerializeField, Range(0.1f, 1f)] private float passiveRadarRadiusRatio = 0.5f;
    [FormerlySerializedAs("passiveDiscoveryTravelStep")]
    [SerializeField, Min(0.1f)] private float passiveRadarTravelStep = 0.75f;
    [SerializeField, Min(0.1f)] private float passiveRadarRefreshInterval = 0.35f;

    [Header("Global Revealed Targets")]
    [Tooltip("코어 추적 신호로 공개된 코어는 현재 스캔 반경 밖이어도 레이더 방향 표식에 포함합니다.")]
    [SerializeField] private bool includeGloballyRevealedCore = true;

    [Header("Sniper Option")]
    [SerializeField] private float sniperLingerTime = 6f;

    [Header("Debug")]
    [SerializeField] private bool drawScanRadius = true;

    private InputAction radarAction;
    private InputAction quickScanAction;
    private bool hasNormalRadarPresentation;
    private float lastScanTime = -999f;
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

    public bool IsRadarActive => radarModeInputState.IsRadarActive;
    public bool IsRadarOpen => IsRadarActive;
    public bool IsRadarPanelOpen => IsRadarActive;
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
    public event Action<bool> RadarActiveChanged;

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
        radarModeInputState.Reset();
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
        ClearAllTemporaryReveals();
        passiveRadarTargets.Clear();
        passiveRadarSet.Clear();
        CloseRadar();
    }

    private void Update()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            CloseRadar();
            return;
        }

        UpdatePassiveRadar();

        if (externalInputLocks.Count > 0)
        {
            return;
        }

        UpdateRadarInput();
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
        if (!IsRadarActive)
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

    private void UpdateRadarInput()
    {
        if (WasRadarPressedThisFrame())
        {
            TryToggleRadarMode();
        }

        if (WasQuickScanPressedThisFrame())
        {
            TryInstantScan();
        }
    }

    private bool WasQuickScanPressedThisFrame()
    {
        if (quickScanAction != null)
        {
            return quickScanAction.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.backButton.wasPressedThisFrame;
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

    public bool TryToggleRadarMode()
    {
        if (!radarModeInputState.TryToggle(
                CanAcceptRadarInput(),
                out bool activeAfterToggle))
        {
            return false;
        }

        ApplyRadarActivePresentation(activeAfterToggle, true);
        RadarActiveChanged?.Invoke(activeAfterToggle);
        return true;
    }

    public bool TryInstantScan()
    {
        if (!radarModeInputState.TryBeginInstantScan(
                CanAcceptRadarInput(),
                Time.unscaledTime,
                scanCooldown))
        {
            return false;
        }

        return ExecuteActiveScan();
    }

    public bool TryScan()
    {
        return TryInstantScan();
    }

    public bool TryQuickScan()
    {
        return TryInstantScan();
    }

    private bool ExecuteActiveScan()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        float effectiveRadius = ResolveEffectiveScanRadius();
        AudioManager.PlayAt(SoundEventIds.RadarScanPulse, transform.position, scanPulseVolumeScale);

        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(effectiveRadius);
            radarVFX.PlayPulse(effectiveRadius);
        }

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        ScanTargets(effectiveRadius);
        lastNormalScanRadius = effectiveRadius;
        hasNormalRadarPresentation = scannedTargets.Count > 0;

        mapDiscoveryController?.RegisterRadarScan(transform.position, effectiveRadius, scannedTargets);

        ScanCompleted?.Invoke(transform.position, effectiveRadius, scannedTargets);
        lastScanTime = Time.time;
        radarHUD?.SetSuccessfulScanPresentation(true);
        radarPanelAnimator?.PlayScanPulse();

        if (scannedTargets.Count == 0)
        {
            RefreshRadarPresentation();
            return true;
        }

        RefreshRadarPresentation();
        return true;
    }

    private bool CanAcceptRadarInput()
    {
        return isActiveAndEnabled &&
               externalInputLocks.Count == 0 &&
               !GameplayPauseManager.IsPaused &&
               (playerHealth == null || !playerHealth.IsDead);
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
        if (IsRadarActive)
        {
            RefreshRadarPresentation();
        }

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

        if (IsRadarActive)
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

    private void ApplyRadarActivePresentation(bool active, bool animatePanel)
    {
        if (!active)
        {
            ClearRadarPresentation(animatePanel);
            return;
        }

        BuildDisplayedTargets();

        if (radarHUD != null)
        {
            radarHUD.SetScanRadius(ResolveDisplayedRadius(ResolveEffectiveScanRadius()));
            radarHUD.SetTargets(displayedTargets, transform);
        }

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

    public void CloseRadar()
    {
        bool changed = radarModeInputState.TrySetActive(false);
        ClearRadarPresentation(true);

        if (changed)
        {
            RadarActiveChanged?.Invoke(false);
        }
    }

    private void ClearRadarPresentation(bool animatePanel)
    {
        radarVFX?.StopPulse();

        // Unity scene shutdown can destroy either UI object before this player
        // component. Use Unity's overloaded null check instead of ?. so a stale
        // native object is not invoked while leaving Tutorial/Expedition.
        if (radarHUD != null)
        {
            radarHUD.SetSuccessfulScanPresentation(false);
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

        if (changed && IsRadarActive && radarHUD != null)
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

            if (!IsRadarActive)
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
        if (!IsRadarActive || radarHUD == null)
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
        scanRadius = Mathf.Max(0.1f, scanRadius);
        scanCooldown = Mathf.Max(0f, scanCooldown);
        scanPulseVolumeScale = Mathf.Clamp01(scanPulseVolumeScale);
    }
}
