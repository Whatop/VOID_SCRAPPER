using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class ExpeditionTravelConfirmationUI : MonoBehaviour
{
    private static ExpeditionTravelConfirmationUI activeModal;

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Legacy Serialized Text")]
    [SerializeField, HideInInspector] private string title;
    [SerializeField, HideInInspector] private string body;
    [SerializeField, HideInInspector] private string yesLabel;
    [SerializeField, HideInInspector] private string noLabel;

    [Header("Button Labels Optional")]
    [SerializeField] private TextMeshProUGUI yesButtonLabelText;
    [SerializeField] private TextMeshProUGUI noButtonLabelText;

    [Header("Modal Layer")]
    [SerializeField] private int modalSortingOrder = 10000;

    private Image modalDimmer;
    private Canvas modalCanvas;
    private PlayerInteractor playerInteractor;
    private bool isOpen;
    private bool isConfirming;
    private bool ownsPause;
    private bool storedCursorState;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    public bool IsOpen => isOpen;

    protected virtual void Reset()
    {
        panelRoot = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    protected virtual void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        ConfigureSharedPresentation();
        SetVisible(false);
    }

    protected virtual void OnEnable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(HandleConfirmClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(Close);
        }
    }

    protected virtual void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(HandleConfirmClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(Close);
        }

        ReleaseModalOwnership();
        SetVisible(false);
        ClearSelection();
        isOpen = false;
        isConfirming = false;
    }

    protected void OpenModal(
        string modalTitle,
        string modalBody,
        string cancelLabel,
        string confirmLabel)
    {
        if (activeModal != null && activeModal != this)
        {
            activeModal.Close();
        }

        activeModal = this;
        isOpen = true;
        isConfirming = false;

        if (titleText != null)
        {
            titleText.text = modalTitle;
        }

        if (bodyText != null)
        {
            bodyText.text = modalBody;
        }

        if (noButtonLabelText != null)
        {
            noButtonLabelText.text = cancelLabel;
        }

        if (yesButtonLabelText != null)
        {
            yesButtonLabelText.text = confirmLabel;
        }

        if (noButton != null)
        {
            noButton.interactable = true;
        }

        if (yesButton != null)
        {
            yesButton.interactable = true;
        }

        transform.SetAsLastSibling();
        AcquireModalOwnership();
        SetVisible(true);
        AudioManager.Play(SoundEventIds.ReturnChoiceOpen);

        if (noButton != null)
        {
            EventSystem.current?.SetSelectedGameObject(noButton.gameObject);
        }
    }

    public void Close()
    {
        if (!isOpen && !ownsPause)
        {
            SetVisible(false);
            return;
        }

        isOpen = false;
        isConfirming = false;
        SetVisible(false);
        ReleaseModalOwnership();
        ClearSelection();
    }

    protected abstract bool ConfirmSelection();

    protected abstract void ClearSelection();

    private void HandleConfirmClicked()
    {
        if (!isOpen || isConfirming)
        {
            return;
        }

        isConfirming = true;

        if (yesButton != null)
        {
            yesButton.interactable = false;
        }

        if (noButton != null)
        {
            noButton.interactable = false;
        }

        if (!ConfirmSelection())
        {
            Close();
            return;
        }

        isOpen = false;
        SetVisible(false);
        ClearSelection();

        if (activeModal == this)
        {
            activeModal = null;
        }
    }

    private void AcquireModalOwnership()
    {
        if (!ownsPause)
        {
            GameplayPauseManager pauseManager = GameplayPauseManager.Instance;
            pauseManager.PushPause(this, "Expedition Travel Confirmation");
            pauseManager.RegisterCancelHandler(this, Close);
            ownsPause = true;
        }

        if (playerInteractor == null)
        {
            playerInteractor = FindFirstObjectByType<PlayerInteractor>();
        }

        playerInteractor?.SetExternalInputLocked(this, true);

        if (!storedCursorState)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            storedCursorState = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ReleaseModalOwnership()
    {
        if (ownsPause)
        {
            GameplayPauseManager pauseManager = GameplayPauseManager.Instance;
            pauseManager.UnregisterCancelHandler(this);
            pauseManager.PopPause(this);
            ownsPause = false;
        }

        if (playerInteractor != null)
        {
            playerInteractor.SetExternalInputLocked(this, false);
        }

        if (storedCursorState)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
            storedCursorState = false;
        }

        if (activeModal == this)
        {
            activeModal = null;
        }
    }

    private void ConfigureSharedPresentation()
    {
        RectTransform rootRect = transform as RectTransform;

        if (rootRect != null && panelRoot != gameObject)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;

            modalDimmer = GetComponent<Image>();

            if (modalDimmer == null)
            {
                modalDimmer = gameObject.AddComponent<Image>();
            }

            modalDimmer.color = new Color(0f, 0f, 0f, 0.68f);
            modalDimmer.raycastTarget = true;

            modalCanvas = GetComponent<Canvas>();

            if (modalCanvas == null)
            {
                modalCanvas = gameObject.AddComponent<Canvas>();
            }

            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = modalSortingOrder;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        RectTransform panelRect = panelRoot != null
            ? panelRoot.transform as RectTransform
            : null;

        if (panelRect != null && panelRoot != gameObject)
        {
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(250f, 112f);
        }

        ConfigureText(titleText, new Vector2(0f, 34f), new Vector2(220f, 20f), 12f, true);
        ConfigureText(bodyText, new Vector2(0f, 7f), new Vector2(220f, 34f), 9f, false);
        ConfigureButton(noButton, new Vector2(-50f, -37f));
        ConfigureButton(yesButton, new Vector2(50f, -37f));
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        Vector2 position,
        Vector2 size,
        float fontSize,
        bool isTitle)
    {
        if (text == null)
        {
            return;
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = isTitle ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = isTitle
            ? new Color(0.35f, 0.94f, 1f, 1f)
            : new Color(0.9f, 0.95f, 1f, 1f);
        text.raycastTarget = false;
    }

    private static void ConfigureButton(Button button, Vector2 position)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;

        if (rect != null)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(88f, 24f);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            label.fontSize = 9f;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void SetVisible(bool visible)
    {
        if (modalDimmer != null)
        {
            modalDimmer.enabled = visible;
        }

        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
public sealed class ReturnChoiceUI : ExpeditionTravelConfirmationUI
{
    private ReturnBeacon currentBeacon;

    public void Open(ReturnBeacon beacon)
    {
        if (beacon == null)
        {
            return;
        }

        currentBeacon = beacon;
        OpenModal(
            "안전 귀환",
            "정착지로 귀환하시겠습니까?",
            "취소",
            "귀환"
        );
    }

    protected override bool ConfirmSelection()
    {
        ReturnBeacon beacon = currentBeacon;
        RunManager runManager = RunManager.Instance;

        if (beacon == null || runManager == null || !runManager.HasActiveRun)
        {
            return false;
        }

        beacon.ConfirmReturn();
        return runManager.IsCompletingRun;
    }

    protected override void ClearSelection()
    {
        currentBeacon = null;
    }
}
