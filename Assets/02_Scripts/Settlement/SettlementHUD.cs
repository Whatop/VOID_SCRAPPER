using TMPro;
using UnityEngine;

public class SettlementHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;

    [Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI selectedWeaponText;

    [Header("Detail Panel")]
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailBodyText;
    [SerializeField] private TextMeshProUGUI actionButtonLabelText;

    [Header("Bottom")]
    [SerializeField] private TextMeshProUGUI launchButtonLabelText;

    [Header("Messages")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Legacy / Debug")]
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Awake()
    {
        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }
    }

    private void OnEnable()
    {
        if (settlementController != null)
        {
            settlementController.Changed += Refresh;
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (settlementController != null)
        {
            settlementController.Changed -= Refresh;
        }
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        int scrap = progress != null ? progress.ScrapParts : 0;
        int core = progress != null ? progress.CoreShards : 0;

        SetCurrency(scrap, core);
        SetSelectedWeapon(settlementController.SelectedWeaponTree.ToString());
        SetMessage(settlementController.LastMessage);

        if (statusText != null)
        {
            statusText.text = settlementController.BuildStatusText();
        }
    }

    public void SetCurrency(int scrapParts, int coreShards)
    {
        if (currencyText == null)
        {
            return;
        }

        currencyText.text = $"스크랩 부품: {scrapParts}    코어 조각: {coreShards}";
    }

    public void SetSelectedWeapon(string weaponName)
    {
        if (selectedWeaponText == null)
        {
            return;
        }

        selectedWeaponText.text = $"선택 무기: {weaponName}";
    }

    public void SetDetail(string title, string body)
    {
        if (detailTitleText != null)
        {
            detailTitleText.text = title;
        }

        if (detailBodyText != null)
        {
            detailBodyText.text = body;
        }
    }

    public void SetActionLabel(string label)
    {
        if (actionButtonLabelText == null)
        {
            return;
        }

        actionButtonLabelText.text = label;
    }

    public void SetLaunchLabel(string label)
    {
        if (launchButtonLabelText == null)
        {
            return;
        }

        launchButtonLabelText.text = label;
    }

    public void SetMessage(string message)
    {
        if (messageText == null)
        {
            return;
        }

        messageText.text = message;
    }

    // 기존 E 상호작용 스크립트가 남아 있어도 컴파일이 깨지지 않게 유지.
    public void SetPrompt(string prompt)
    {
        if (promptText == null)
        {
            return;
        }

        promptText.text = prompt;
    }
}