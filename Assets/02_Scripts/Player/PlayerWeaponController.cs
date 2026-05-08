using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private InputAction fireAction;
    private PlayerWeaponBase currentWeapon;
    private WeaponTreeType currentWeaponTree;

    public PlayerWeaponBase CurrentWeapon => currentWeapon;
    public WeaponTreeType CurrentWeaponTree => currentWeaponTree;

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
        if (!CanUseWeapon())
        {
            return;
        }

        if (currentWeapon == null)
        {
            return;
        }

        WeaponFireInput input = ReadFireInput();
        currentWeapon.TickWeapon(input, Time.deltaTime);
    }

    private void CacheReferences()
    {
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
            Debug.LogWarning($"{weaponTreeType} 무기 컴포넌트를 찾지 못했습니다.", this);
            return false;
        }

        if (currentWeapon == nextWeapon)
        {
            return true;
        }

        if (currentWeapon != null)
        {
            currentWeapon.OnUnequip();
        }

        currentWeapon = nextWeapon;
        currentWeaponTree = weaponTreeType;
        currentWeapon.OnEquip();

        Debug.Log($"무기 트리 장착: {weaponTreeType}");
        return true;
    }

    private PlayerWeaponBase FindWeapon(WeaponTreeType weaponTreeType)
    {
        foreach (WeaponLoadoutEntry entry in loadout)
        {
            if (entry == null)
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

    [ContextMenu("Equip Shotgun")]
    private void DebugEquipShotgun()
    {
        EquipWeapon(WeaponTreeType.Shotgun);
    }

    [ContextMenu("Equip Sniper")]
    private void DebugEquipSniper()
    {
        EquipWeapon(WeaponTreeType.Sniper);
    }

    [ContextMenu("Equip MachineGun")]
    private void DebugEquipMachineGun()
    {
        EquipWeapon(WeaponTreeType.MachineGun);
    }
}