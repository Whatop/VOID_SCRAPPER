using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WormholeChoiceUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Buttons")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Text Values")]
    [SerializeField] private string title = "웜홀";
    [SerializeField] private string body = "다음 지역을 탐색하시겠습니까?";
    [SerializeField] private string yesLabel = "예";
    [SerializeField] private string noLabel = "아니오";

    [Header("Button Labels Optional")]
    [SerializeField] private TextMeshProUGUI yesButtonLabelText;
    [SerializeField] private TextMeshProUGUI noButtonLabelText;

    private WormholePortal currentPortal;

    private void Reset()
    {
        panelRoot = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        Close();
    }

    private void OnEnable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.AddListener(HandleYesClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.AddListener(HandleNoClicked);
        }
    }

    private void OnDisable()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(HandleYesClicked);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(HandleNoClicked);
        }
    }

    public void Open(WormholePortal portal)
    {
        currentPortal = portal;

        RefreshTexts();
        SetVisible(true);
    }

    public void Close()
    {
        currentPortal = null;
        SetVisible(false);
    }

    private void RefreshTexts()
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (bodyText != null)
        {
            bodyText.text = body;
        }

        if (yesButtonLabelText != null)
        {
            yesButtonLabelText.text = yesLabel;
        }

        if (noButtonLabelText != null)
        {
            noButtonLabelText.text = noLabel;
        }
    }

    private void HandleYesClicked()
    {
        WormholePortal portal = currentPortal;
        Close();

        if (portal != null)
        {
            portal.ConfirmEnterNextArea();
        }
    }

    private void HandleNoClicked()
    {
        Close();
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
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