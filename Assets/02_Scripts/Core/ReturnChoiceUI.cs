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

    [SerializeField] private Image modalDimmer;
    [SerializeField] private Canvas modalCanvas;
    private bool missingPresentationReported;
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
        if (panelRoot == null || canvasGroup == null || titleText == null || bodyText == null ||
            yesButton == null || noButton == null || (panelRoot != gameObject && (modalDimmer == null || modalCanvas == null)))
        {
            if (!missingPresentationReported)
            {
                missingPresentationReported = true;
                string screen = this is ReturnChoiceUI ? "Return Confirmation" : "Wormhole Confirmation";
                Debug.LogWarning($"[{GetType().Name}] Missing panelRoot/canvasGroup/titleText/bodyText/yesButton/noButton/modalDimmer/modalCanvas at '{ConfirmationPath(transform)}', scene '{gameObject.scene.path}'. Restore the listed authored Inspector bindings. Confirmation was skipped before acquiring input or pause.", this);
            }
            return;
        }
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


    private static string ConfirmationPath(Transform target) => target.parent != null ? ConfirmationPath(target.parent) + "/" + target.name : target.name;

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
