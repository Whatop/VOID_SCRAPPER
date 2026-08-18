using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TutorialPromptUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string bindingGroup = "Keyboard&Mouse";

    [Header("Presentation")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI progressText;

    private readonly StringBuilder compositeBindingBuilder = new StringBuilder(32);

    private string currentTemplate;
    private string currentActionMapName;
    private string currentActionName;
    private string currentFallbackBinding;
    private bool currentUsesCompositeParts;

    private void Reset()
    {
        promptRoot = gameObject;
        instructionText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Awake()
    {
        if (promptRoot == null)
        {
            promptRoot = gameObject;
        }

        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
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

    public void ShowInstruction(
        string instructionTemplate,
        string actionMapName,
        string actionName,
        string fallbackBinding,
        bool useCompositeParts)
    {
        currentTemplate = instructionTemplate;
        currentActionMapName = actionMapName;
        currentActionName = actionName;
        currentFallbackBinding = fallbackBinding;
        currentUsesCompositeParts = useCompositeParts;

        if (promptRoot != null)
        {
            promptRoot.SetActive(true);
        }

        Refresh();
    }

    public void SetProgress(int currentStepNumber, int totalStepCount)
    {
        if (progressText == null)
        {
            return;
        }

        progressText.text = totalStepCount > 0
            ? $"{Mathf.Clamp(currentStepNumber, 0, totalStepCount)}/{totalStepCount}"
            : string.Empty;
    }

    public void Hide()
    {
        if (promptRoot != null)
        {
            promptRoot.SetActive(false);
        }
    }

    public void Refresh()
    {
        if (instructionText == null || string.IsNullOrWhiteSpace(currentTemplate))
        {
            return;
        }

        string bindingDisplay = ResolveBindingDisplay();
        instructionText.text = currentTemplate.Contains("{0}")
            ? string.Format(currentTemplate, bindingDisplay)
            : currentTemplate;
    }

    private string ResolveBindingDisplay()
    {
        if (string.IsNullOrWhiteSpace(currentActionName))
        {
            return currentFallbackBinding;
        }

        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputAction action = InputBindingUtility.ResolveAction(
            inputActions,
            currentActionMapName,
            currentActionName
        );

        if (action == null)
        {
            return currentFallbackBinding;
        }

        if (currentUsesCompositeParts)
        {
            string compositeDisplay = BuildCompositeDisplay(action);

            if (!string.IsNullOrWhiteSpace(compositeDisplay))
            {
                return compositeDisplay;
            }
        }

        return InputBindingUtility.GetDisplayString(
            inputActions,
            currentActionMapName,
            currentActionName,
            currentFallbackBinding,
            bindingGroup
        );
    }

    private string BuildCompositeDisplay(InputAction action)
    {
        compositeBindingBuilder.Clear();

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!binding.isPartOfComposite || !MatchesBindingGroup(binding))
            {
                continue;
            }

            string display = action.GetBindingDisplayString(
                i,
                InputBinding.DisplayStringOptions.DontIncludeInteractions
            );

            if (string.IsNullOrWhiteSpace(display))
            {
                continue;
            }

            if (compositeBindingBuilder.Length > 0)
            {
                compositeBindingBuilder.Append('/');
            }

            compositeBindingBuilder.Append(display);
        }

        return compositeBindingBuilder.ToString();
    }

    private bool MatchesBindingGroup(InputBinding binding)
    {
        return string.IsNullOrWhiteSpace(bindingGroup) ||
               (!string.IsNullOrWhiteSpace(binding.groups) && binding.groups.Contains(bindingGroup));
    }

    private void HandleActionChange(object changedObject, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            Refresh();
        }
    }
}
