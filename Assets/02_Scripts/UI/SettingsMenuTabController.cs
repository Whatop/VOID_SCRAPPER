using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsMenuTabController : MonoBehaviour
{
    [SerializeField] private GameObject[] tabRoots = new GameObject[5];
    [SerializeField] private Button[] tabButtons = new Button[5];
    [SerializeField] private int defaultTabIndex;

    private readonly List<UnityAction> boundActions = new List<UnityAction>();

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
            UnityAction action = () => ShowTab(capturedIndex);
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
