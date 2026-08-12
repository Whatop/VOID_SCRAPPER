using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public class PlayerRadarScanner : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string radarActionName = "Radar";
    [SerializeField] private Key radarFallbackKey = Key.Q;

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

    [Tooltip("레이더가 열려 있을 때 Q를 짧게 누르면 닫습니다. 길게 누르면 다시 스캔합니다.")]
    [SerializeField] private bool shortPressClosesRadar = true;

    [Header("Global Revealed Targets")]
    [Tooltip("코어 추적 신호로 공개된 코어는 현재 스캔 반경 밖이어도 레이더 방향 표식에 포함합니다.")]
    [SerializeField] private bool includeGloballyRevealedCore = true;

    [Header("Sniper Option")]
    [SerializeField] private float sniperLingerTime = 6f;

    [Header("Warning Messages")]
    [SerializeField] private string holdNotEnoughMessage = "Q를 1초 동안 눌러야 레이더 스캔이 가능합니다.";
    [SerializeField] private string noTargetMessage = "탐지된 대상이 없습니다.";

    [Header("Debug")]
    [SerializeField] private bool drawScanRadius = true;

    private InputAction radarAction;
    private bool isHolding;
    private bool isRadarOpen;
    private float holdTimer;
    private float lastScanTime = -999f;

    private readonly Collider2D[] scanBuffer = new Collider2D[256];
    private readonly List<RadarTarget> scannedTargets = new List<RadarTarget>(128);
    private readonly HashSet<RadarTarget> scannedSet = new HashSet<RadarTarget>();

    public bool IsHolding => isHolding;
    public bool IsRadarOpen => isRadarOpen;
    public bool IsRadarPanelOpen => radarPanelAnimator != null ? radarPanelAnimator.IsOpen : isRadarOpen;
    public float HoldRatio => holdTime <= 0f ? 1f : Mathf.Clamp01(holdTimer / holdTime);
    public float ScanRadius => ResolveEffectiveScanRadius();
    public float LastScanTime => lastScanTime;
    public float SniperLingerTime => sniperLingerTime + (runtimeBonusState != null ? runtimeBonusState.RadarStealthDurationBonus : 0f);
    public IReadOnlyList<RadarTarget> LastScannedTargets => scannedTargets;

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
        objectiveDirector ??= ExpeditionObjectiveDirector.Instance;

        if (objectiveDirector != null)
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
        CancelHold();
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

        UpdateHoldInput();
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
        radarAction = InputBindingUtility.ResolveAction(inputActions, actionMapName, radarActionName);
        radarAction?.Enable();
    }

    private void UpdateHoldInput()
    {
        bool pressedThisFrame = WasRadarPressedThisFrame();
        bool held = IsRadarHeld();
        bool releasedThisFrame = WasRadarReleasedThisFrame();

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

        AudioManager.PlayAt(SoundEventIds.RadarChargeStart, transform.position);
        AudioManager.PlayLoop(SoundEventIds.RadarChargeLoop, "radar_charge", 1f);

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
            AudioManager.PlayAt(SoundEventIds.RadarChargeCancel, transform.position, 0.7f);
            radarVFX?.CancelCharge();

            if (isRadarOpen && shortPressClosesRadar)
            {
                CloseRadar();
                return;
            }

            ShowWarning(holdNotEnoughMessage);
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

    public bool TryScan()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            radarVFX?.CancelCharge();
            return false;
        }

        float effectiveRadius = ResolveEffectiveScanRadius();

        AudioManager.StopLoop("radar_charge");
        AudioManager.PlayAt(SoundEventIds.RadarScanPulse, transform.position);

        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(effectiveRadius);
            radarVFX.CompleteScan();
        }

        ScanTargets(effectiveRadius);

        mapDiscoveryController ??= MapDiscoveryController.Instance;
        mapDiscoveryController?.RegisterRadarScan(transform.position, effectiveRadius, scannedTargets);

        ScanCompleted?.Invoke(transform.position, effectiveRadius, scannedTargets);
        lastScanTime = Time.time;

        if (scannedTargets.Count == 0)
        {
            ShowWarning(noTargetMessage);
            CloseRadar();
            return true;
        }

        OpenRadar(effectiveRadius);
        return true;
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

    private void OpenRadar(float effectiveRadius)
    {
        if (radarHUD != null)
        {
            radarHUD.SetScanRadius(effectiveRadius);
            radarHUD.SetTargets(scannedTargets, transform);
        }

        if (!isRadarOpen)
        {
            radarPanelAnimator?.Open();
        }

        isRadarOpen = true;
    }

    public void CloseRadar()
    {
        if (!isRadarOpen)
        {
            return;
        }

        radarHUD?.Clear();
        radarPanelAnimator?.Close();
        isRadarOpen = false;
    }

    private void HandleCoreGloballyRevealed()
    {
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
            radarHUD.SetTargets(scannedTargets, transform);
        }
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
            expeditionHUD.ShowWarning(message);
            return;
        }

        FindFirstObjectByType<WarningMessageUI>()?.ShowMessage(message);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawScanRadius)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? ResolveEffectiveScanRadius() : scanRadius);
    }
}
