using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class BootMainMenuButtonBinding
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image selectionAccent;
    [SerializeField] private MainMenuButtonPresenter presenter;

    public Button Button => button;
    public TextMeshProUGUI Label => label;
    public Image SelectionAccent => selectionAccent;
    public MainMenuButtonPresenter Presenter => presenter;

    public bool TryGetMissingReference(string bindingName, out string missingReference)
    {
        if (button == null)
        {
            return Missing(bindingName, nameof(button), out missingReference);
        }

        if (label == null)
        {
            return Missing(bindingName, nameof(label), out missingReference);
        }

        if (selectionAccent == null)
        {
            return Missing(bindingName, nameof(selectionAccent), out missingReference);
        }

        if (presenter == null)
        {
            return Missing(bindingName, nameof(presenter), out missingReference);
        }

        missingReference = string.Empty;
        return true;
    }

    private static bool Missing(
        string bindingName,
        string memberName,
        out string missingReference)
    {
        missingReference = $"{bindingName}.{memberName}";
        return false;
    }

}


[DisallowMultipleComponent]
public sealed class BootMainMenuView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform safeArea;
    [SerializeField] private RectTransform backgroundRoot;
    [SerializeField] private MainMenuSpaceBackground spaceBackground;
    [SerializeField] private CanvasGroup transitionOverlay;

    [Header("Main Menu")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private RectTransform titleRoot;
    [SerializeField] private TextMeshProUGUI gameTitle;
    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private BootMainMenuButtonBinding continueBinding;
    [SerializeField] private BootMainMenuButtonBinding newGameBinding;
    [SerializeField] private BootMainMenuButtonBinding settingsBinding;
    [SerializeField] private BootMainMenuButtonBinding exitBinding;
    [SerializeField] private Button initialSelectable;
    [SerializeField] private CanvasGroup[] entranceGroups = Array.Empty<CanvasGroup>();

    [Header("Secondary Panels")]
    [SerializeField] private GameObject optionsRoot;
    [SerializeField] private SharedOptionsMenuUI sharedOptions;
    [SerializeField] private GameObject newGameConfirmationRoot;
    [SerializeField] private Button confirmNewGameButton;
    [SerializeField] private Button cancelNewGameButton;

    [Header("Button Presentation")]
    [SerializeField] private Color accentColor = new Color(0.2f, 0.85f, 0.72f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.9f, 0.95f, 1f, 1f);

    public Canvas RootCanvas => rootCanvas;
    public CanvasScaler CanvasScaler => canvasScaler;
    public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
    public CanvasGroup RootCanvasGroup => rootCanvasGroup;
    public RectTransform SafeArea => safeArea;
    public RectTransform BackgroundRoot => backgroundRoot;
    public MainMenuSpaceBackground SpaceBackground => spaceBackground;
    public CanvasGroup TransitionOverlay => transitionOverlay;
    public GameObject MenuRoot => menuRoot;
    public RectTransform TitleRoot => titleRoot;
    public RectTransform TitleRect => gameTitle != null ? gameTitle.rectTransform : null;
    public RectTransform SubtitleRect => subtitle != null ? subtitle.rectTransform : null;
    public TextMeshProUGUI GameTitle => gameTitle;
    public TextMeshProUGUI Subtitle => subtitle;
    public TextMeshProUGUI StatusText => statusText;
    public Button ContinueButton => continueBinding != null ? continueBinding.Button : null;
    public Button NewGameButton => newGameBinding != null ? newGameBinding.Button : null;
    public Button SettingsButton => settingsBinding != null ? settingsBinding.Button : null;
    public Button ExitButton => exitBinding != null ? exitBinding.Button : null;
    public Button InitialSelectable => initialSelectable;
    public CanvasGroup[] EntranceGroups => entranceGroups ?? Array.Empty<CanvasGroup>();
    public GameObject OptionsRoot => optionsRoot;
    public SharedOptionsMenuUI SharedOptions => sharedOptions;
    public GameObject NewGameConfirmationRoot => newGameConfirmationRoot;
    public Button ConfirmNewGameButton => confirmNewGameButton;
    public Button CancelNewGameButton => cancelNewGameButton;

    public bool TryGetMissingRuntimeReference(out string missingReference)
    {
        if (rootCanvas == null)
        {
            return Missing(nameof(rootCanvas), out missingReference);
        }

        if (canvasScaler == null)
        {
            return Missing(nameof(canvasScaler), out missingReference);
        }

        if (graphicRaycaster == null)
        {
            return Missing(nameof(graphicRaycaster), out missingReference);
        }

        if (rootCanvasGroup == null)
        {
            return Missing(nameof(rootCanvasGroup), out missingReference);
        }

        if (safeArea == null)
        {
            return Missing(nameof(safeArea), out missingReference);
        }

        if (transitionOverlay == null)
        {
            return Missing(nameof(transitionOverlay), out missingReference);
        }

        if (menuRoot == null)
        {
            return Missing(nameof(menuRoot), out missingReference);
        }

        if (titleRoot == null)
        {
            return Missing(nameof(titleRoot), out missingReference);
        }

        if (gameTitle == null)
        {
            return Missing(nameof(gameTitle), out missingReference);
        }

        if (subtitle == null)
        {
            return Missing(nameof(subtitle), out missingReference);
        }

        if (statusText == null)
        {
            return Missing(nameof(statusText), out missingReference);
        }

        if (!TryGetCompleteBinding(
                continueBinding,
                nameof(continueBinding),
                out missingReference))
        {
            return false;
        }

        if (!TryGetCompleteBinding(
                newGameBinding,
                nameof(newGameBinding),
                out missingReference))
        {
            return false;
        }

        if (!TryGetCompleteBinding(
                settingsBinding,
                nameof(settingsBinding),
                out missingReference))
        {
            return false;
        }

        if (!TryGetCompleteBinding(
                exitBinding,
                nameof(exitBinding),
                out missingReference))
        {
            return false;
        }

        if (initialSelectable == null)
        {
            return Missing(nameof(initialSelectable), out missingReference);
        }

        if (optionsRoot == null)
        {
            return Missing(nameof(optionsRoot), out missingReference);
        }

        if (sharedOptions == null)
        {
            return Missing(nameof(sharedOptions), out missingReference);
        }
        if (sharedOptions.gameObject != optionsRoot || optionsRoot.transform.parent != safeArea)
        {
            return Missing("canonical SafeArea/OptionsPanel", out missingReference);
        }
        if (!sharedOptions.TryValidateAuthoredLayout(out missingReference)) return false;

        if (newGameConfirmationRoot == null)
        {
            return Missing(nameof(newGameConfirmationRoot), out missingReference);
        }

        if (confirmNewGameButton == null)
        {
            return Missing(nameof(confirmNewGameButton), out missingReference);
        }

        if (cancelNewGameButton == null)
        {
            return Missing(nameof(cancelNewGameButton), out missingReference);
        }

        missingReference = string.Empty;
        return true;
    }

    public void ConfigureButtonPresenters()
    {
        ConfigureButtonPresenter(continueBinding);
        ConfigureButtonPresenter(newGameBinding);
        ConfigureButtonPresenter(settingsBinding);
        ConfigureButtonPresenter(exitBinding);
    }

    private void ConfigureButtonPresenter(BootMainMenuButtonBinding binding)
    {
        if (!IsComplete(binding))
        {
            return;
        }

        binding.Presenter.Configure(
            binding.Button.transform as RectTransform,
            binding.Button.targetGraphic as Image,
            binding.Label,
            binding.SelectionAccent,
            accentColor,
            normalTextColor);
    }

    private static bool IsComplete(BootMainMenuButtonBinding binding)
    {
        return binding != null && binding.TryGetMissingReference(string.Empty, out _);
    }

    private static bool TryGetCompleteBinding(
        BootMainMenuButtonBinding binding,
        string bindingName,
        out string missingReference)
    {
        if (binding == null)
        {
            return Missing(bindingName, out missingReference);
        }

        return binding.TryGetMissingReference(bindingName, out missingReference);
    }

    private static bool Missing(string fieldName, out string missingReference)
    {
        missingReference = fieldName;
        return false;
    }

}
