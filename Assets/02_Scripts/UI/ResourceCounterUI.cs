using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceCounterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI labelText;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField] private string amountFormat = "{0}";
    [SerializeField] private int maxVisibleAmount = 99999;
    [SerializeField] private bool showPlusWhenClamped;

    public int Amount { get; private set; }
    public int DisplayAmount => Mathf.Min(Amount, Mathf.Max(0, maxVisibleAmount));

    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>(true);
        amountText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void OnValidate()
    {
        maxVisibleAmount = Mathf.Max(0, maxVisibleAmount);
    }

    public void SetAmount(int amount)
    {
        Amount = Mathf.Max(0, amount);

        if (amountText != null)
        {
            amountText.text = FormatAmount(Amount);
        }

        if (labelText != null && !string.IsNullOrWhiteSpace(displayName))
        {
            labelText.text = displayName;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
    }

    public void SetDisplayName(string newDisplayName)
    {
        displayName = newDisplayName;

        if (labelText != null)
        {
            labelText.text = displayName;
        }
    }

    private string FormatAmount(int amount)
    {
        int limit = Mathf.Max(0, maxVisibleAmount);
        int displayAmount = Mathf.Min(Mathf.Max(0, amount), limit);
        string formatted = string.Format(amountFormat, displayAmount);

        if (showPlusWhenClamped && amount > limit)
        {
            formatted += "+";
        }

        return formatted;
    }
}