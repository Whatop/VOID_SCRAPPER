using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class EmergencyReturnGaugeUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Gauge")]
    [SerializeField] private GaugeBarUI gauge;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Format")]
    [SerializeField] private string preparingFormat = "긴급탈출 준비 중... {0:0.0}s";

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        Hide();
    }

    public void ShowPreparing(float ratio, float remainingSeconds)
    {
        SetVisible(true);

        if (gauge != null)
        {
            gauge.SetRatio(Mathf.Clamp01(ratio));
        }

        if (messageText != null)
        {
            messageText.text = string.Format(preparingFormat, Mathf.Max(0f, remainingSeconds));
        }
    }

    public void ShowReady(string message)
    {
        SetVisible(true);

        if (gauge != null)
        {
            gauge.SetRatio(1f);
        }

        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public void Hide()
    {
        if (gauge != null)
        {
            gauge.SetRatio(0f);
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }
}