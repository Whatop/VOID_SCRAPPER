using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ResourceCounterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private GameObject rootObject;

    [Header("Display")]
    [SerializeField] private string displayName;
    [SerializeField] private string amountFormat = "{0}";
    [SerializeField] private int maxVisibleAmount = 99999;
    [SerializeField] private bool showPlusWhenClamped;
    [SerializeField] private bool hideWhenZero = true;

    private bool externalVisible = true;

    public int Amount { get; private set; }
    public int DisplayAmount => Mathf.Min(Amount, Mathf.Max(0, maxVisibleAmount));
    public bool HideWhenZero => hideWhenZero;

    private void Reset()
    {
        iconImage = GetComponentInChildren<Image>(true);
        amountText = GetComponentInChildren<TextMeshProUGUI>(true);
        rootObject = gameObject;
    }

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        RefreshVisibility();
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

        RefreshVisibility();
    }

    public void SetExternalVisible(bool visible)
    {
        externalVisible = visible;
        RefreshVisibility();
    }

    public void SetHideWhenZero(bool value)
    {
        hideWhenZero = value;
        RefreshVisibility();
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

    private void RefreshVisibility()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        bool visible = externalVisible && (!hideWhenZero || Amount > 0);

        if (rootObject != null && rootObject.activeSelf != visible)
        {
            rootObject.SetActive(visible);
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
