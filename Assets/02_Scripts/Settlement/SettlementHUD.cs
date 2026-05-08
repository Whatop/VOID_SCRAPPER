using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettlementHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SettlementController settlementController;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI messageText;

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

    public void SetPrompt(string prompt)
    {
        if (promptText == null)
        {
            return;
        }

        promptText.text = prompt;
    }

    public void Refresh()
    {
        if (settlementController == null)
        {
            return;
        }

        if (statusText != null)
        {
            statusText.text = settlementController.BuildStatusText();
        }

        if (messageText != null)
        {
            messageText.text = settlementController.LastMessage;
        }
    }
}