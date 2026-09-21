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
    public Sprite IconSprite => iconImage != null ? iconImage.sprite : null;

    // Authored compact HUD rows hide their own root, so layout can omit zero rows.
    public bool HasAuthoredBindings => rootObject == gameObject &&
        iconImage != null && iconImage.sprite != null && iconImage.transform.IsChildOf(transform) &&
        amountText != null && amountText.font != null && amountText.transform.IsChildOf(transform) &&
        (labelText == null || (labelText != amountText && labelText.font != null && labelText.transform.IsChildOf(transform)));

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

    public void SetIconColor(Color color)
    {
        if (iconImage != null)
        {
            iconImage.color = color;
        }
    }

    public void SetDisplayName(string newDisplayName)
    {
        displayName = newDisplayName;

        if (labelText != null)
        {
            labelText.text = displayName;
        }
    }

    public void SetTextColor(Color color)
    {
        if (amountText != null)
        {
            amountText.color = color;
        }

        if (labelText != null)
        {
            labelText.color = color;
        }
    }

    public void SetTextColors(Color labelColor, Color amountColor)
    {
        if (labelText != null)
        {
            labelText.color = labelColor;
        }

        if (amountText != null)
        {
            amountText.color = amountColor;
        }
    }

    public void SetAmountFormat(string format)
    {
        amountFormat = string.IsNullOrWhiteSpace(format) ? "{0}" : format;

        if (amountText != null)
        {
            amountText.text = FormatAmount(Amount);
        }
    }

    public void ConfigureCompactPresentation(float fontSize, float rowHeight)
    {
        float clampedFontSize = Mathf.Max(4f, fontSize);

        if (amountText != null)
        {
            amountText.fontSize = clampedFontSize;
            amountText.enableAutoSizing = true;
            amountText.fontSizeMin = 4.5f;
            amountText.fontSizeMax = clampedFontSize;
            amountText.textWrappingMode = TextWrappingModes.NoWrap;
            amountText.raycastTarget = false;
        }

        if (labelText != null)
        {
            labelText.fontSize = clampedFontSize;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 4.5f;
            labelText.fontSizeMax = clampedFontSize;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            labelText.raycastTarget = false;
        }

        if (transform is RectTransform rectTransform)
        {
            rectTransform.sizeDelta = new Vector2(
                rectTransform.sizeDelta.x,
                Mathf.Max(10f, rowHeight)
            );
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
        }
    }

    public void ConfigureResultPresentation(float rowWidth, float rowHeight, Color backgroundColor)
    {
        ConfigureCompactPresentation(7f, rowHeight);

        if (transform is RectTransform rowRect)
        {
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(Mathf.Max(80f, rowWidth), Mathf.Max(10f, rowHeight));
        }

        Image rowBackground = GetComponent<Image>();

        if (rowBackground != null)
        {
            rowBackground.color = backgroundColor;
            rowBackground.raycastTarget = false;
        }

        Image[] childImages = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < childImages.Length; i++)
        {
            Image childImage = childImages[i];

            if (childImage != null && childImage != iconImage && childImage != rowBackground)
            {
                childImage.enabled = false;
            }
        }

        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(8f, 8f);
            iconImage.preserveAspect = true;
        }

        if (labelText != null)
        {
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.offsetMin = new Vector2(17f, 1f);
            labelRect.offsetMax = new Vector2(-42f, -1f);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.fontSize = 6.5f;
            labelText.fontSizeMax = 6.5f;
        }

        if (amountText != null)
        {
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = new Vector2(1f, 0.5f);
            amountRect.anchorMax = new Vector2(1f, 0.5f);
            amountRect.pivot = new Vector2(1f, 0.5f);
            amountRect.anchoredPosition = new Vector2(-5f, 0f);
            amountRect.sizeDelta = new Vector2(35f, Mathf.Max(10f, rowHeight - 2f));
            amountText.alignment = TextAlignmentOptions.MidlineRight;
            amountText.fontSize = 7f;
            amountText.fontSizeMax = 7f;
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
