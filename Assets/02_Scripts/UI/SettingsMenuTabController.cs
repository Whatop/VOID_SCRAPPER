using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsMenuTabController : MonoBehaviour
{
    [SerializeField] private GameObject[] tabRoots = new GameObject[5];
    [SerializeField] private Button[] tabButtons = new Button[5];
    [SerializeField] private int defaultTabIndex;
    [SerializeField] private GameObject[] navigationBlockers = new GameObject[0];

    private readonly List<UnityAction> boundActions = new List<UnityAction>();
    [SerializeField] private InputRebindButtonUI[] rebindRows = new InputRebindButtonUI[0];
    [SerializeField] private TMP_Dropdown[] dropdowns = new TMP_Dropdown[0];

    public int CurrentTabIndex { get; private set; } = -1;
    public IReadOnlyList<InputRebindButtonUI> RebindRows => rebindRows;
    public IReadOnlyList<TMP_Dropdown> DropdownGuards => dropdowns;
    public void CollectSharedOptionsBindingErrors(List<string> errors)
    {
        var roles = new HashSet<Component>();
        void Role(string field, Component value)
        {
            if (value == null || value.gameObject.scene != gameObject.scene || value.transform == transform || !value.transform.IsChildOf(transform))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettingsMenuTabController." + field,
                    value, transform, gameObject.scene, value == null ? "Missing binding" : "Scene ownership or ancestry"));
            else if (!roles.Add(value))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettingsMenuTabController." + field,
                    value, transform, gameObject.scene, "Duplicate control mapping"));
        }
        if (tabRoots == null || tabButtons == null || tabRoots.Length != 4 || tabButtons.Length != 4)
            errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("SettingsMenuTabController.tabRoots/tabButtons", this, transform, gameObject.scene, "Expected the four shared Options tabs"));
        if (tabRoots != null)
            for (int i = 0; i < tabRoots.Length; i++) Role("tabRoots.Array.data[" + i + "]", tabRoots[i] != null ? tabRoots[i].transform : null);
        if (tabButtons != null)
            for (int i = 0; i < tabButtons.Length; i++) Role("tabButtons.Array.data[" + i + "]", tabButtons[i]);
        string[] tabNames = { "GameplayTab", "SoundTab", "KeyboardTab", "DisplayTab" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            if (tabRoots == null || i >= tabRoots.Length || tabRoots[i] == null || tabRoots[i].name != tabNames[i])
                errors.Add("SettingsMenuTabController.tabRoots[" + i + "] must reference " + tabNames[i] + ".");
            if (tabButtons == null || i >= tabButtons.Length || tabButtons[i] == null || tabButtons[i].name != tabNames[i] + "Button")
                errors.Add("SettingsMenuTabController.tabButtons[" + i + "] must reference " + tabNames[i] + "Button.");
        }
        if (rebindRows == null || rebindRows.Length != 18) Role(nameof(rebindRows), null);
        else for (int i = 0; i < rebindRows.Length; i++) Role("rebindRows.Array.data[" + i + "]", rebindRows[i]);
        if (dropdowns == null || dropdowns.Length != 3) Role(nameof(dropdowns), null);
        else for (int i = 0; i < dropdowns.Length; i++) Role("dropdowns.Array.data[" + i + "]", dropdowns[i]);
        string[] dropdownNames = { "ResolutionDropdown", "FullscreenDropdown", "FrameLimitDropdown" };
        for (int i = 0; i < dropdownNames.Length; i++)
            if (dropdowns == null || i >= dropdowns.Length || dropdowns[i] == null || dropdowns[i].name != dropdownNames[i] ||
                tabRoots == null || tabRoots.Length != 4 || tabRoots[3] == null || !dropdowns[i].transform.IsChildOf(tabRoots[3].transform))
                errors.Add("SettingsMenuTabController.dropdowns[" + i + "] must reference DisplayTab/" + dropdownNames[i] + ".");
        if (rebindRows != null && tabRoots != null && tabRoots.Length == 4 && tabRoots[2] != null)
            foreach (InputRebindButtonUI row in rebindRows)
                if (row != null && !row.transform.IsChildOf(tabRoots[2].transform)) errors.Add("Rebind guard must belong to KeyboardTab: " + row.name);
        if (navigationBlockers == null || navigationBlockers.Length != 1) Role(nameof(navigationBlockers), null);
        else Role("navigationBlockers.Array.data[0]", navigationBlockers[0] != null ? navigationBlockers[0].transform : null);
    }
    public bool TryHandleMenuCancel()
    {
        foreach (InputRebindButtonUI row in rebindRows)
            if (row != null && row.BlocksMenuCancel) return true;
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null || !dropdown.IsExpanded) continue;
            dropdown.Hide();
            return true;
        }
        return false;
    }

    private void OnEnable()
    {
        BindButtons();
        ShowTab(defaultTabIndex);
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || IsTabNavigationBlocked())
        {
            return;
        }

        int direction = 0;
        if (keyboard.aKey.wasPressedThisFrame)
        {
            direction = -1;
        }
        else if (keyboard.dKey.wasPressedThisFrame)
        {
            direction = 1;
        }

        if (direction == 0 || tabRoots == null || tabRoots.Length == 0)
        {
            return;
        }

        int current = CurrentTabIndex >= 0 ? CurrentTabIndex : defaultTabIndex;
        int next = (current + direction + tabRoots.Length) % tabRoots.Length;
        ShowTab(next);
        SelectFirstControlInCurrentTab();
        AudioManager.Play(SoundEventIds.UiClick);
    }

    public void ShowTab(int index)
    {
        if (tabRoots == null || tabRoots.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, tabRoots.Length - 1);
        CurrentTabIndex = index;

        for (int i = 0; i < tabRoots.Length; i++)
        {
            if (tabRoots[i] != null)
            {
                tabRoots[i].SetActive(i == index);
            }

            if (tabButtons != null && i < tabButtons.Length && tabButtons[i] != null)
            {
                tabButtons[i].interactable = i != index;
            }
        }
    }

    public void Configure(GameObject[] roots, Button[] buttons, int initialTabIndex = 0)
    {
        UnbindButtons();
        tabRoots = roots ?? new GameObject[0];
        tabButtons = buttons ?? new Button[0];
        defaultTabIndex = Mathf.Clamp(
            initialTabIndex,
            0,
            Mathf.Max(0, tabRoots.Length - 1)
        );

        if (isActiveAndEnabled)
        {
            BindButtons();
            ShowTab(defaultTabIndex);
        }
    }

    public void ConfigureInputGuards(
        InputRebindButtonUI[] rows,
        TMP_Dropdown[] settingsDropdowns,
        GameObject[] modalRoots)
    {
        rebindRows = rows ?? new InputRebindButtonUI[0];
        dropdowns = settingsDropdowns ?? new TMP_Dropdown[0];
        navigationBlockers = modalRoots ?? new GameObject[0];
    }

    public void SelectFirstControlInCurrentTab()
    {
        if (CurrentTabIndex < 0 || tabRoots == null || CurrentTabIndex >= tabRoots.Length)
        {
            return;
        }

        GameObject currentRoot = tabRoots[CurrentTabIndex];
        if (currentRoot == null)
        {
            return;
        }

        Selectable[] controls = currentRoot.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < controls.Length; i++)
        {
            Selectable control = controls[i];
            if (control != null && control.IsActive() && control.IsInteractable())
            {
                EventSystem.current?.SetSelectedGameObject(control.gameObject);
                return;
            }
        }
    }

    private bool IsTabNavigationBlocked()
    {
        for (int i = 0; i < rebindRows.Length; i++)
        {
            if (rebindRows[i] != null && rebindRows[i].IsRebinding)
            {
                return true;
            }
        }

        for (int i = 0; i < dropdowns.Length; i++)
        {
            if (dropdowns[i] != null && dropdowns[i].IsExpanded)
            {
                return true;
            }
        }

        for (int i = 0; i < navigationBlockers.Length; i++)
        {
            if (navigationBlockers[i] != null && navigationBlockers[i].activeInHierarchy)
            {
                return true;
            }
        }

        GameObject selectedObject = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;

        return selectedObject != null &&
               (selectedObject.GetComponentInParent<TMP_InputField>() != null ||
                selectedObject.GetComponentInParent<InputField>() != null);
    }

    private void BindButtons()
    {
        UnbindButtons();

        if (tabButtons == null)
        {
            return;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button button = tabButtons[i];
            if (button == null)
            {
                boundActions.Add(null);
                continue;
            }

            int capturedIndex = i;
            UnityAction action = () =>
            {
                ShowTab(capturedIndex);
                SelectFirstControlInCurrentTab();
            };
            boundActions.Add(action);
            button.onClick.AddListener(action);
        }
    }

    private void UnbindButtons()
    {
        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length && i < boundActions.Count; i++)
            {
                Button button = tabButtons[i];
                UnityAction action = boundActions[i];

                if (button != null && action != null)
                {
                    button.onClick.RemoveListener(action);
                }
            }
        }

        boundActions.Clear();
    }
}
