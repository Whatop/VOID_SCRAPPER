using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerRadarScanner : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string radarActionName = "Radar";

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private ExpeditionHUD expeditionHUD;
    [SerializeField] private RadarPanelAnimator radarPanelAnimator;
    [SerializeField] private RadarHUD radarHUD;
    [SerializeField] private PlayerRadarVFXController radarVFX;

    [Header("Scan Rule")]
    [SerializeField] private float holdTime = 1f;
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private LayerMask radarTargetLayer = ~0;

    [Tooltip("레이더가 열려 있을 때 Q를 짧게 누르면 닫습니다. 길게 누르면 다시 스캔합니다.")]
    [SerializeField] private bool shortPressClosesRadar = true;

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

    // 실제 RadarPanelAnimator 상태를 우선 사용하고, UI 참조가 없을 때만 내부 상태로 대체합니다.
    public bool IsRadarPanelOpen =>
        radarPanelAnimator != null
            ? radarPanelAnimator.IsOpen
            : isRadarOpen;

    public float HoldRatio => holdTime <= 0f ? 1f : Mathf.Clamp01(holdTimer / holdTime);
    public float ScanRadius => scanRadius;
    public float LastScanTime => lastScanTime;
    public float SniperLingerTime => sniperLingerTime;

    private void Reset()
    {
        playerHealth = GetComponent<PlayerHealth>();
        weaponController = GetComponent<PlayerWeaponController>();
        radarVFX = GetComponent<PlayerRadarVFXController>();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        if (radarAction != null)
        {
            radarAction.Disable();
        }

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

        // ߿:
        //  ¶ ؼ ̴ ڵ  ʴ´.
        //    ο ݱ δ ÷̾ Q Է  Ѵ.
    }

    private void CacheReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (radarVFX == null)
        {
            radarVFX = GetComponent<PlayerRadarVFXController>();
        }

        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (radarPanelAnimator == null)
        {
            radarPanelAnimator = FindFirstObjectByType<RadarPanelAnimator>();
        }

        if (radarHUD == null)
        {
            radarHUD = FindFirstObjectByType<RadarHUD>();
        }
    }

    private void BindInput()
    {
        if (inputActions == null)
        {
            return;
        }

        InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);

        if (actionMap == null)
        {
            return;
        }

        radarAction = actionMap.FindAction(radarActionName, false);

        if (radarAction != null)
        {
            radarAction.Enable();
        }
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
            UpdateChargeVFX();
        }

        if (releasedThisFrame)
        {
            EndHold();
        }
    }

    private bool WasRadarPressedThisFrame()
    {
        if (radarAction != null && radarAction.WasPressedThisFrame())
        {
            return true;
        }

        return Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
    }

    private bool IsRadarHeld()
    {
        if (radarAction != null && radarAction.IsPressed())
        {
            return true;
        }

        return Keyboard.current != null && Keyboard.current.qKey.isPressed;
    }

    private bool WasRadarReleasedThisFrame()
    {
        if (radarAction != null && radarAction.WasReleasedThisFrame())
        {
            return true;
        }

        return Keyboard.current != null && Keyboard.current.qKey.wasReleasedThisFrame;
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

        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(scanRadius);
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

            if (radarVFX != null)
            {
                radarVFX.CancelCharge();
            }

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

        if (radarVFX != null)
        {
            radarVFX.CancelCharge();
        }
    }

    private void UpdateChargeVFX()
    {
        if (radarVFX == null)
        {
            return;
        }

        radarVFX.SetChargeRatio(HoldRatio);
    }

    public bool TryScan()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (radarVFX != null)
            {
                radarVFX.CancelCharge();
            }

            return false;
        }

        AudioManager.StopLoop("radar_charge");
        AudioManager.PlayAt(SoundEventIds.RadarScanPulse, transform.position);

        if (radarVFX != null)
        {
            radarVFX.SetScanRadius(scanRadius);
            radarVFX.CompleteScan();
        }

        ScanTargets();

        if (scannedTargets.Count == 0)
        {
            ShowWarning(noTargetMessage);
            CloseRadar();
            return false;
        }

        OpenRadar();
        lastScanTime = Time.time;
        return true;
    }

    private void ScanTargets()
    {
        scannedTargets.Clear();
        scannedSet.Clear();

        Vector2 origin = transform.position;
        WeaponTreeType weaponTreeType = ResolveCurrentWeaponTree();

        RadarScanContext context = new RadarScanContext(
            gameObject,
            transform,
            origin,
            weaponTreeType,
            scanRadius
        );

        int count = Physics2D.OverlapCircleNonAlloc(
            origin,
            scanRadius,
            scanBuffer,
            radarTargetLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = scanBuffer[i];

            if (hit == null)
            {
                continue;
            }

            RadarTarget radarTarget = hit.GetComponentInParent<RadarTarget>();

            if (radarTarget == null)
            {
                continue;
            }

            if (scannedSet.Contains(radarTarget))
            {
                continue;
            }

            if (!radarTarget.IsRadarVisible)
            {
                continue;
            }

            RadarScanResult result = radarTarget.OnRadarScanned(context);

            if (result != RadarScanResult.Detected)
            {
                continue;
            }

            scannedSet.Add(radarTarget);
            scannedTargets.Add(radarTarget);
        }
    }

    private void OpenRadar()
    {
        if (radarHUD != null)
        {
            radarHUD.SetTargets(scannedTargets, transform);
        }

        if (!isRadarOpen && radarPanelAnimator != null)
        {
            radarPanelAnimator.Open();
        }

        isRadarOpen = true;
    }

    public void CloseRadar()
    {
        if (!isRadarOpen)
        {
            return;
        }

        if (radarHUD != null)
        {
            radarHUD.Clear();
        }

        if (radarPanelAnimator != null)
        {
            radarPanelAnimator.Close();
        }

        isRadarOpen = false;
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

        WarningMessageUI warningMessageUI = FindFirstObjectByType<WarningMessageUI>();

        if (warningMessageUI != null)
        {
            warningMessageUI.ShowMessage(message);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawScanRadius)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}