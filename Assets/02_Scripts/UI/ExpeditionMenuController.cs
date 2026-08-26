using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum ExpeditionMenuTab
{
    Map = 0,
    Inventory = 1
}

[DisallowMultipleComponent]
public sealed class ExpeditionMenuController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string mapActionName = "Map";
    [SerializeField] private string inventoryActionName = "Inventory";

    [Header("Root")]
    [Tooltip("이 컨트롤러가 붙은 오브젝트와 분리된 실제 메뉴 비주얼 루트를 연결합니다.")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private bool deactivateMenuRootWhenClosed = true;

    [Header("Tabs")]
    [SerializeField] private GameObject mapTabRoot;
    [SerializeField] private GameObject inventoryTabRoot;
    [SerializeField] private Button mapTabButton;
    [SerializeField] private Button inventoryTabButton;
    [SerializeField] private Button closeButton;

    [Header("Contents")]
    [SerializeField] private ExpeditionMapPanelUI mapPanel;
    [SerializeField] private PlayerBuildStatusPanelUI inventoryPanel;

    [Header("Radar Presentation")]
    [SerializeField] private PlayerRadarScanner radarScanner;
    [SerializeField] private RadarPanelAnimator radarPanelAnimator;

    [Header("Gameplay Input / Cursor")]
    [FormerlySerializedAs("pauseWhileOpen")]
    [SerializeField] private bool lockGameplayInputWhileOpen = true;
    [SerializeField] private bool blockOpenWhileAnotherPauseActive = true;
    [SerializeField] private bool showCursorWhileOpen = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    [Header("Player Input Owners")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerReinforcementController reinforcementController;
    [SerializeField] private PlayerInteractor playerInteractor;

    [Header("Keyboard Fallback")]
    [SerializeField] private Key mapFallbackKey = Key.Tab;
    [SerializeField] private Key inventoryFallbackKey = Key.E;

    [Header("Start")]
    [SerializeField] private ExpeditionMenuTab defaultTab = ExpeditionMenuTab.Map;

    private InputAction mapAction;
    private InputAction inventoryAction;
    private bool isOpen;
    private ExpeditionMenuTab currentTab;

    private bool cursorStateStored;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private GameObject previousSelectedGameObject;
    private bool gameplayInputLockHeld;

    public bool IsOpen => isOpen;
    public ExpeditionMenuTab CurrentTab => currentTab;

    public event Action<bool> OpenStateChanged;
    public event Action<ExpeditionMenuTab> TabChanged;

    private void Awake()
    {
        if (menuCanvasGroup == null && menuRoot != null)
        {
            menuCanvasGroup = menuRoot.GetComponent<CanvasGroup>();
        }

        if (mapPanel == null)
        {
            mapPanel = GetComponentInChildren<ExpeditionMapPanelUI>(true);
        }

        if (inventoryPanel == null)
        {
            inventoryPanel = GetComponentInChildren<PlayerBuildStatusPanelUI>(true);
        }

        InputBindingPersistence.LoadOnce(inputActions);
        SetMenuVisual(false);
    }

    private void OnEnable()
    {
        BindInput();
        BindButtons();

        if (inventoryPanel != null)
        {
            inventoryPanel.CloseRequested += HandleInventoryCloseRequested;
        }
    }

    private void OnDisable()
    {
        UnbindButtons();

        if (inventoryPanel != null)
        {
            inventoryPanel.CloseRequested -= HandleInventoryCloseRequested;
        }

        mapAction?.Disable();
        inventoryAction?.Disable();

        if (isOpen)
        {
            Close(false);
        }
    }

    private void Update()
    {
        bool mapPressed = WasPressedThisFrame(mapAction, mapFallbackKey);
        bool inventoryPressed = WasPressedThisFrame(inventoryAction, inventoryFallbackKey);

        if (mapPressed)
        {
            HandleTabHotkey(ExpeditionMenuTab.Map);
            return;
        }

        if (inventoryPressed)
        {
            HandleTabHotkey(ExpeditionMenuTab.Inventory);
        }
    }

    public void OpenMap()
    {
        Open(ExpeditionMenuTab.Map);
    }

    public void OpenInventory()
    {
        Open(ExpeditionMenuTab.Inventory);
    }

    public void Open(ExpeditionMenuTab tab)
    {
        if (!isOpen)
        {
            if (blockOpenWhileAnotherPauseActive &&
                GameplayPauseManager.IsPaused &&
                !GameplayPauseManager.Instance.IsPausedBy(this))
            {
                return;
            }

            isOpen = true;
            StoreAndApplyCursorState();
            AcquireGameplayInputLock();
            SetMenuVisual(true);

            // Keep an already-open radar alive, but render the menu above it.
            if (menuRoot != null)
            {
                menuRoot.transform.SetAsLastSibling();
            }

            GameplayPauseManager.Instance?.RegisterCancelHandler(this, Close);
            AudioManager.Play(SoundEventIds.UiPanelOpen);
            OpenStateChanged?.Invoke(true);
        }

        ShowTab(tab, false);
    }

    public void ToggleMap()
    {
        HandleTabHotkey(ExpeditionMenuTab.Map);
    }

    public void ToggleInventory()
    {
        HandleTabHotkey(ExpeditionMenuTab.Inventory);
    }

    public void ShowMapTab()
    {
        if (!isOpen)
        {
            OpenMap();
            return;
        }

        ShowTab(ExpeditionMenuTab.Map, true);
    }

    public void ShowInventoryTab()
    {
        if (!isOpen)
        {
            OpenInventory();
            return;
        }

        ShowTab(ExpeditionMenuTab.Inventory, true);
    }

    public void Close()
    {
        Close(true);
    }

    private void Close(bool playSound)
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        mapPanel?.SetVisible(false);
        inventoryPanel?.Close();
        SetRadarPresentationSuppressed(false);

        if (GameplayPauseManager.Instance != null)
        {
            GameplayPauseManager.Instance.UnregisterCancelHandler(this);
        }

        ReleaseGameplayInputLock();
        SetMenuVisual(false);
        RestoreCursorState();

        if (playSound)
        {
            AudioManager.Play(SoundEventIds.UiPanelClose);
        }

        OpenStateChanged?.Invoke(false);
    }

    private void HandleTabHotkey(ExpeditionMenuTab requestedTab)
    {
        if (!isOpen)
        {
            Open(requestedTab);
            return;
        }

        if (currentTab == requestedTab)
        {
            Close();
            return;
        }

        ShowTab(requestedTab, true);
    }

    private void ShowTab(ExpeditionMenuTab tab, bool playSound)
    {
        currentTab = tab;
        bool showMap = tab == ExpeditionMenuTab.Map;

        SetActive(mapTabRoot, showMap);
        SetActive(inventoryTabRoot, !showMap);

        if (mapPanel != null)
        {
            mapPanel.SetVisible(showMap);
        }

        if (inventoryPanel != null)
        {
            if (showMap)
            {
                inventoryPanel.Close();
            }
            else
            {
                inventoryPanel.Open();
            }
        }

        SetRadarPresentationSuppressed(!showMap);

        if (mapTabButton != null)
        {
            mapTabButton.interactable = !showMap;
        }

        if (inventoryTabButton != null)
        {
            inventoryTabButton.interactable = showMap;
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(!showMap);
        }

        if (playSound)
        {
            AudioManager.Play(SoundEventIds.UiClick);
        }

        RefreshEventSystemSelection(tab);
        TabChanged?.Invoke(tab);
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        mapAction = InputBindingUtility.ResolveAction(
            inputActions,
            playerActionMapName,
            mapActionName
        );

        inventoryAction = InputBindingUtility.ResolveAction(
            inputActions,
            playerActionMapName,
            inventoryActionName
        );

        mapAction?.Enable();
        inventoryAction?.Enable();
    }

    private static bool WasPressedThisFrame(InputAction action, Key fallbackKey)
    {
        if (action != null)
        {
            return action.WasPressedThisFrame();
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        KeyControl key = Keyboard.current[fallbackKey];
        return key != null && key.wasPressedThisFrame;
    }

    private void BindButtons()
    {
        if (mapTabButton != null)
        {
            mapTabButton.onClick.AddListener(ShowMapTab);
        }

        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.AddListener(ShowInventoryTab);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void UnbindButtons()
    {
        if (mapTabButton != null)
        {
            mapTabButton.onClick.RemoveListener(ShowMapTab);
        }

        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.RemoveListener(ShowInventoryTab);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    private void HandleInventoryCloseRequested()
    {
        Close();
    }

    private void SetMenuVisual(bool visible)
    {
        if (menuRoot != null && visible && !menuRoot.activeSelf)
        {
            menuRoot.SetActive(true);
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = visible ? 1f : 0f;
            menuCanvasGroup.interactable = visible;
            menuCanvasGroup.blocksRaycasts = visible;
        }

        if (!visible && deactivateMenuRootWhenClosed && menuRoot != null && menuRoot != gameObject)
        {
            menuRoot.SetActive(false);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void StoreAndApplyCursorState()
    {
        previousSelectedGameObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        cursorStateStored = true;

        if (showCursorWhileOpen)
        {
            Cursor.visible = true;
        }

        if (unlockCursorWhileOpen)
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void RestoreCursorState()
    {
        if (!cursorStateStored)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateStored = false;

        if (EventSystem.current != null)
        {
            GameObject selection = previousSelectedGameObject != null &&
                                   previousSelectedGameObject.activeInHierarchy
                ? previousSelectedGameObject
                : null;
            EventSystem.current.SetSelectedGameObject(selection);
        }

        previousSelectedGameObject = null;
    }

    private void ResolveRadarScanner()
    {
        if (radarScanner == null)
        {
            radarScanner = FindFirstObjectByType<PlayerRadarScanner>();
        }
    }

    private void SetRadarPresentationSuppressed(bool suppressed)
    {
        if (radarPanelAnimator == null)
        {
            radarPanelAnimator = FindFirstObjectByType<RadarPanelAnimator>();
        }

        radarPanelAnimator?.SetPresentationSuppressed(this, suppressed);
    }

    private void AcquireGameplayInputLock()
    {
        if (!lockGameplayInputWhileOpen || gameplayInputLockHeld)
        {
            return;
        }

        ResolvePlayerInputOwners();
        playerController?.SetExternalControlLocked(this, true);
        weaponController?.SetExternalInputLocked(this, true);
        reinforcementController?.SetExternalInputLocked(this, true);
        radarScanner?.SetExternalInputLocked(this, true);
        playerInteractor?.SetExternalInputLocked(this, true);
        gameplayInputLockHeld = true;
    }

    private void ReleaseGameplayInputLock()
    {
        if (!gameplayInputLockHeld)
        {
            return;
        }

        playerController?.SetExternalControlLocked(this, false);
        weaponController?.SetExternalInputLocked(this, false);
        reinforcementController?.SetExternalInputLocked(this, false);
        radarScanner?.SetExternalInputLocked(this, false);
        playerInteractor?.SetExternalInputLocked(this, false);
        gameplayInputLockHeld = false;
    }

    private void ResolvePlayerInputOwners()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<PlayerController2D>();
        }

        if (playerController != null)
        {
            GameObject playerObject = playerController.gameObject;
            weaponController ??= playerObject.GetComponent<PlayerWeaponController>();
            reinforcementController ??= playerObject.GetComponent<PlayerReinforcementController>();
            radarScanner ??= playerObject.GetComponent<PlayerRadarScanner>();
            playerInteractor ??= playerObject.GetComponent<PlayerInteractor>();
        }

        if (radarScanner == null)
        {
            ResolveRadarScanner();
        }
    }

    private void RefreshEventSystemSelection(ExpeditionMenuTab tab)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        Selectable preferred = tab == ExpeditionMenuTab.Inventory
            ? inventoryPanel?.FirstCargoSelectable
            : inventoryTabButton;

        if (preferred == null || !preferred.IsActive() || !preferred.IsInteractable())
        {
            GameObject activeTabRoot = tab == ExpeditionMenuTab.Map ? mapTabRoot : inventoryTabRoot;
            preferred = activeTabRoot != null
                ? activeTabRoot.GetComponentInChildren<Selectable>(false)
                : null;
        }

        EventSystem.current.SetSelectedGameObject(preferred != null ? preferred.gameObject : null);
    }
}
