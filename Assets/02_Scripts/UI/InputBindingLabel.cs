using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class InputBindingLabel : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionName;
    [SerializeField] private string bindingGroup = "Keyboard&Mouse";
    [SerializeField] private int preferredBindingIndex = -1;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI targetText;
    [SerializeField] private string fallbackText = "?";
    [SerializeField] private string format = "[{0}]";

    private void Reset()
    {
        targetText = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TextMeshProUGUI>();
        }

        InputBindingPersistence.LoadOnce(inputActions);
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += HandleActionChange;
        Refresh();
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= HandleActionChange;
    }

    public void Refresh()
    {
        if (targetText == null)
        {
            return;
        }

        string display = InputBindingUtility.GetDisplayString(
            inputActions,
            actionMapName,
            actionName,
            fallbackText,
            bindingGroup,
            preferredBindingIndex
        );

        targetText.text = string.IsNullOrWhiteSpace(format)
            ? display
            : string.Format(format, display);
    }

    public void Configure(
        InputActionAsset actions,
        string mapName,
        string targetActionName,
        string fallback,
        string labelFormat = "[{0}]")
    {
        inputActions = actions;
        actionMapName = mapName;
        actionName = targetActionName;
        fallbackText = fallback;
        format = labelFormat;
        InputBindingPersistence.LoadOnce(inputActions);
        Refresh();
    }

    private void HandleActionChange(object changedObject, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged ||
            change == InputActionChange.ActionMapEnabled ||
            change == InputActionChange.ActionMapDisabled)
        {
            Refresh();
        }
    }
}
