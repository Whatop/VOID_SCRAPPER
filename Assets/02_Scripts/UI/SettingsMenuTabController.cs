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
    private InputRebindButtonUI[] rebindRows = new InputRebindButtonUI[0];
    private TMP_Dropdown[] dropdowns = new TMP_Dropdown[0];

    public int CurrentTabIndex { get; private set; } = -1;

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
