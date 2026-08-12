using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[Serializable]
public class WeaponLoadoutEntry
{
    public WeaponTreeType weaponTreeType;
    public PlayerWeaponBase weapon;
}

public class PlayerWeaponController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string fireActionName = "Fire";

    [Header("References")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerCombatState combatState;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerWeaponModifiers weaponModifiers;

    [Header("Loadout")]
    [SerializeField] private WeaponTreeType defaultWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private List<WeaponLoadoutEntry> loadout = new List<WeaponLoadoutEntry>();

    [Header("State Rule")]
    [SerializeField] private bool requireGameplayState;

    [Header("UI Fire Blocking")]
    [SerializeField] private bool blockFireOverUi = true;
    [Tooltip("켜면 실제 버튼/스크롤 등 입력 가능한 UI만 사격을 막고, 장식용 HUD 이미지는 무시합니다.")]
    [SerializeField] private bool blockOnlyInteractiveUi = true;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(16);
    private PointerEventData pointerEventData;
    private EventSystem pointerEventSystem;

    private InputAction fireAction;
    private PlayerWeaponBase currentWeapon;
    private WeaponTreeType currentWeaponTree;
    private bool externalInputLocked;

    public Transform FirePoint => firePoint;
    public PlayerWeaponBase CurrentWeapon => currentWeapon;
    public WeaponTreeType CurrentWeaponTree => currentWeaponTree;
    public bool ExternalInputLocked => externalInputLocked;

    public event Action<WeaponTreeType, PlayerWeaponBase> WeaponEquipped;

    public void SetExternalInputLocked(bool locked)
    {
        externalInputLocked = locked;

        if (locked && currentWeapon != null)
        {
            currentWeapon.ForceCancel();
        }
    }

    private void Awake()
    {
        CacheReferences();
        InitializeWeapons();
    }

    private void OnEnable()
    {
        BindInput();

        if (RunManager.Instance != null)
        {
            RunManager.Instance.RunStarted += HandleRunStarted;
        }
    }

    private void Start()
    {
        EquipFromCurrentRunOrDefault();
    }

    private void OnDisable()
    {
        if (fireAction != null)
        {
            fireAction.Disable();
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.RunStarted -= HandleRunStarted;
        }

        if (currentWeapon != null)
        {
            currentWeapon.OnUnequip();
        }
    }

    private void Update()
    {
        if (currentWeapon == null)
        {
            return;
        }

        if (!CanUseWeapon())
        {
            currentWeapon.ForceCancel();
            return;
        }

        WeaponFireInput input = ReadFireInput();
        currentWeapon.TickWeapon(input, Time.deltaTime);
    }

    private void CacheReferences()
    {
        if (GameplayPauseManager.IsPaused)
        {
            return;
        }
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (combatState == null)
        {
            combatState = GetComponent<PlayerCombatState>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (weaponModifiers == null)
        {
            weaponModifiers = GetComponent<PlayerWeaponModifiers>();
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    private void InitializeWeapons()
    {
        foreach (WeaponLoadoutEntry entry in loadout)
        {
            if (entry == null || entry.weapon == null)
            {
                continue;
            }

            entry.weapon.SetRuntimeReferences(
                this,
                firePoint,
                playerController,
                combatState,
                weaponModifiers
            );
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

        fireAction = actionMap.FindAction(fireActionName, false);
        if (fireAction != null)
        {
            fireAction.Enable();
        }
    }

    private WeaponFireInput ReadFireInput()
    {
        if (fireAction != null)
        {
            return new WeaponFireInput(
                fireAction.IsPressed(),
                fireAction.WasPressedThisFrame(),
                fireAction.WasReleasedThisFrame()
            );
        }

        if (Mouse.current != null)
        {
            return new WeaponFireInput(
                Mouse.current.leftButton.isPressed,
                Mouse.current.leftButton.wasPressedThisFrame,
                Mouse.current.leftButton.wasReleasedThisFrame
            );
        }

        return new WeaponFireInput(false, false, false);
    }

    private bool CanUseWeapon()
    {
        if (externalInputLocked)
        {
            return false;
        }

        if (GameplayPauseManager.IsPaused)
        {
            return false;
        }

        if (IsPointerOverBlockingUi())
        {
            return false;
        }
        if (playerHealth != null && playerHealth.IsDead)
        {
            return false;
        }

        if (requireGameplayState && GameStateManager.Instance != null)
        {
            return GameStateManager.Instance.CurrentState == GameState.Expedition ||
                   GameStateManager.Instance.CurrentState == GameState.BossBattle;
        }

        return true;
    }

    private bool IsPointerOverBlockingUi()
    {
        if (!blockFireOverUi || EventSystem.current == null)
        {
            return false;
        }

        if (!blockOnlyInteractiveUi)
        {
            return EventSystem.current.IsPointerOverGameObject();
        }

        Vector2 pointerPosition;

        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
        }
        else
        {
            return false;
        }

        if (pointerEventData == null || pointerEventSystem != EventSystem.current)
        {
            pointerEventSystem = EventSystem.current;
            pointerEventData = new PointerEventData(pointerEventSystem);
        }

        pointerEventData.position = pointerPosition;
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;

            if (hitObject == null)
            {
                continue;
            }

            Selectable selectable = hitObject.GetComponentInParent<Selectable>();

            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
            {
                return true;
            }

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) != null ||
                ExecuteEvents.GetEventHandler<IBeginDragHandler>(hitObject) != null ||
                ExecuteEvents.GetEventHandler<IDragHandler>(hitObject) != null ||
                ExecuteEvents.GetEventHandler<IScrollHandler>(hitObject) != null)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleRunStarted(RunContext runContext)
    {
        if (runContext == null)
        {
            return;
        }

        EquipWeapon(runContext.SelectedWeaponTree);
    }

    public void EquipFromCurrentRunOrDefault()
    {
        WeaponTreeType targetTree = defaultWeaponTree;

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            targetTree = RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }
        else if (PermanentProgress.Instance != null)
        {
            targetTree = PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        EquipWeapon(targetTree);
    }

    public bool EquipWeapon(WeaponTreeType weaponTreeType)
    {
        PlayerWeaponBase nextWeapon = FindWeapon(weaponTreeType);
        if (nextWeapon == null)
        {
            Debug.LogWarning($"Weapon not found in loadout: {weaponTreeType}", this);
            return false;
        }

        if (currentWeapon != null)
        {
            currentWeapon.OnUnequip();
        }

        currentWeapon = nextWeapon;
        currentWeaponTree = weaponTreeType;

        currentWeapon.OnEquip();

        WeaponEquipped?.Invoke(currentWeaponTree, currentWeapon);
        return true;
    }

    private PlayerWeaponBase FindWeapon(WeaponTreeType weaponTreeType)
    {
        foreach (WeaponLoadoutEntry entry in loadout)
        {
            if (entry == null || entry.weapon == null)
            {
                continue;
            }

            if (entry.weaponTreeType == weaponTreeType)
            {
                return entry.weapon;
            }
        }

        return null;
    }
}