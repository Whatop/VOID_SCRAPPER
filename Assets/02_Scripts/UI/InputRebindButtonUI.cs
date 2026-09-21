using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InputRebindButtonUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string actionName;
    [SerializeField] private string bindingGroup = "Keyboard&Mouse";
    [SerializeField] private int preferredBindingIndex = -1;

    [Header("UI")]
    [SerializeField] private Button rebindButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI bindingText;
    [SerializeField] private string fallbackText = "미지정";
    [SerializeField] private string waitingText = "입력 대기...";

    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private InputAction activeAction;
    private bool actionWasEnabled;
    private int lastRebindFinishedFrame = -1;

    public bool IsRebinding => rebindOperation != null;
    public bool BlocksMenuCancel => IsRebinding || lastRebindFinishedFrame == Time.frameCount;

    private void Reset()
    {
        rebindButton = GetComponent<Button>();
        bindingText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Awake()
    {
        InputBindingPersistence.LoadOnce(inputActions);
        ResolveAction();
    }

    private void OnEnable()
    {
        if (rebindButton != null)
        {
            rebindButton.onClick.AddListener(BeginRebind);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetBinding);
        }

        RefreshLabel();
    }

    private void OnDisable()
    {
        if (rebindButton != null)
        {
            rebindButton.onClick.RemoveListener(BeginRebind);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetBinding);
        }

        CancelRebind();
    }

    public void BeginRebind()
    {
        CancelRebind();
        ResolveAction();

        if (activeAction == null)
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        int bindingIndex = InputBindingUtility.ResolveBindingIndex(
            activeAction,
            bindingGroup,
            preferredBindingIndex
        );

        if (bindingIndex < 0)
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        actionWasEnabled = activeAction.enabled;
        activeAction.Disable();

        if (bindingText != null)
        {
            bindingText.text = waitingText;
        }

        rebindOperation = activeAction.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .OnCancel(_ => FinishRebind(false))
            .OnComplete(_ => FinishRebind(true));

        rebindOperation.Start();
    }

    public void ResetBinding()
    {
        ResolveAction();

        if (activeAction == null)
        {
            return;
        }

        int bindingIndex = InputBindingUtility.ResolveBindingIndex(
            activeAction,
            bindingGroup,
            preferredBindingIndex
        );

        if (bindingIndex >= 0)
        {
            activeAction.RemoveBindingOverride(bindingIndex);
            InputBindingPersistence.Save(inputActions);
            RefreshLabel();
        }
    }

    public void RefreshLabel()
    {
        if (bindingText == null)
        {
            return;
        }

        bindingText.text = InputBindingUtility.GetDisplayString(
            inputActions,
            actionMapName,
            actionName,
            fallbackText,
            bindingGroup,
            preferredBindingIndex
        );
    }

    public void Configure(
        InputActionAsset actions,
        string mapName,
        string targetActionName,
        string targetBindingGroup,
        int bindingIndex,
        Button targetRebindButton,
        Button targetResetButton,
        TextMeshProUGUI targetBindingText,
        string unresolvedText = "Unassigned",
        string rebindWaitingText = "Press a key...")
    {
        CancelRebind();
        inputActions = actions;
        actionMapName = mapName;
        actionName = targetActionName;
        bindingGroup = targetBindingGroup;
        preferredBindingIndex = bindingIndex;
        rebindButton = targetRebindButton;
        resetButton = targetResetButton;
        bindingText = targetBindingText;
        fallbackText = unresolvedText;
        waitingText = rebindWaitingText;
        InputBindingPersistence.LoadOnce(inputActions);
        ResolveAction();
        RefreshLabel();
    }

    private void ResolveAction()
    {
        activeAction = InputBindingUtility.ResolveAction(inputActions, actionMapName, actionName);
    }

    private void FinishRebind(bool save)
    {
        lastRebindFinishedFrame = Time.frameCount;
        if (save)
        {
            InputBindingPersistence.Save(inputActions);
            AudioManager.Play(SoundEventIds.UiActivate);
        }

        DisposeRebindOperation();

        if (activeAction != null && actionWasEnabled)
        {
            activeAction.Enable();
        }

        RefreshLabel();
    }

    private void CancelRebind()
    {
        if (rebindOperation != null)
        {
            rebindOperation.Cancel();
            DisposeRebindOperation();
        }

        if (activeAction != null && actionWasEnabled && !activeAction.enabled)
        {
            activeAction.Enable();
        }
    }

    private void DisposeRebindOperation()
    {
        rebindOperation?.Dispose();
        rebindOperation = null;
    }
}
