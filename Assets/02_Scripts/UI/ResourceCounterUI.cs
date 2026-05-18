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

    public int Amount { get; private set; }

    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>(true);
        amountText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void SetAmount(int amount)
    {
        Amount = Mathf.Max(0, amount);

        if (amountText != null)
        {
            amountText.text = string.Format(amountFormat, Amount);
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
}
