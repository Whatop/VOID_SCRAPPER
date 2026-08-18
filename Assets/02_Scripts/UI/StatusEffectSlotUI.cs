using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StatusEffectSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image durationFillImage;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private TextMeshProUGUI remainingTimeText;
    [SerializeField] private Sprite fallbackIcon;

    [Header("Presentation")]
    [SerializeField] private Color positiveBackgroundColor = new Color(0.08f, 0.16f, 0.2f, 0.92f);
    [SerializeField] private Color negativeBackgroundColor = new Color(0.22f, 0.06f, 0.3f, 0.96f);
    [SerializeField] private Color positiveDurationColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Color negativeDurationColor = new Color(0.18f, 0f, 0.25f, 0.68f);

    private string statusId = string.Empty;
    private string statusDisplayName = string.Empty;
    private string statusDescription = string.Empty;
    private string statusClassification = string.Empty;
    private bool showsDuration;
    private float lastRemainingSeconds;
    private float lastDurationSeconds;
    private RectTransform tooltipRoot;
    private TextMeshProUGUI tooltipNameText;
    private TextMeshProUGUI tooltipDescriptionText;
    private TextMeshProUGUI tooltipTimeText;
    private RectTransform tooltipCanvasRoot;

    public string StatusId => statusId;

    public void Bind(
        string newStatusId,
        string displayName,
        string description,
        Sprite icon,
        Color accentColor,
        bool negative,
        int stackCount,
        bool showDuration,
        float remainingSeconds,
        float durationSeconds)
    {
        statusId = newStatusId ?? string.Empty;
        statusDisplayName = string.IsNullOrWhiteSpace(displayName) ? statusId : displayName;
        statusDescription = description ?? string.Empty;
        statusClassification = negative ? "Debuff" : "Buff";
        showsDuration = showDuration;
        gameObject.SetActive(true);

        if (backgroundImage != null)
        {
            backgroundImage.color = negative ? negativeBackgroundColor : positiveBackgroundColor;
            backgroundImage.raycastTarget = true;
        }

        if (iconImage != null)
        {
            bool usesFallback = icon == null;
            iconImage.sprite = usesFallback ? fallbackIcon : icon;
            iconImage.color = usesFallback ? accentColor : Color.white;
            iconImage.enabled = true;
        }

        if (durationFillImage != null)
        {
            durationFillImage.color = negative ? negativeDurationColor : positiveDurationColor;
        }

        SetStackCount(stackCount);
        UpdateDuration(remainingSeconds, durationSeconds);
    }

    public void UpdateDuration(float remainingSeconds, float durationSeconds)
    {
        lastRemainingSeconds = remainingSeconds;
        lastDurationSeconds = durationSeconds;
        bool visible = showsDuration && durationSeconds > 0f && remainingSeconds > 0f;

        if (durationFillImage != null)
        {
            durationFillImage.gameObject.SetActive(visible);
            durationFillImage.fillAmount = visible
                ? 1f - Mathf.Clamp01(remainingSeconds / durationSeconds)
                : 0f;
        }

        if (remainingTimeText != null)
        {
            remainingTimeText.gameObject.SetActive(visible);

            if (!visible)
            {
                remainingTimeText.text = string.Empty;
            }
            else if (remainingSeconds >= 10f)
            {
                remainingTimeText.SetText("{0:0}", Mathf.CeilToInt(remainingSeconds));
            }
            else
            {
                remainingTimeText.SetText("{0:0.0}", remainingSeconds);
            }
        }

        RefreshTooltipTime();
    }

    public void Release()
    {
        statusId = string.Empty;
        statusDisplayName = string.Empty;
        statusDescription = string.Empty;
        statusClassification = string.Empty;
        showsDuration = false;
        HideTooltip();
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(statusId))
        {
            return;
        }

        EnsureTooltip();
        tooltipNameText.text = string.IsNullOrWhiteSpace(statusClassification)
            ? statusDisplayName
            : $"{statusClassification} · {statusDisplayName}";
        tooltipDescriptionText.text = statusDescription;
        RefreshTooltipTime();
        tooltipRoot.gameObject.SetActive(true);
        PositionTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    private void EnsureTooltip()
    {
        if (tooltipRoot != null)
        {
            return;
        }

        GameObject tooltipObject = new GameObject(
            "StatusTooltip",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Canvas)
        );
        tooltipRoot = tooltipObject.GetComponent<RectTransform>();
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        tooltipCanvasRoot = parentCanvas != null ? parentCanvas.transform as RectTransform : null;
        tooltipRoot.SetParent(tooltipCanvasRoot != null ? tooltipCanvasRoot : transform, false);
        tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRoot.pivot = new Vector2(0f, 1f);
        tooltipRoot.anchoredPosition = Vector2.zero;
        tooltipRoot.sizeDelta = new Vector2(150f, 64f);

        Image background = tooltipObject.GetComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.065f, 0.97f);
        background.raycastTarget = false;

        Canvas canvas = tooltipObject.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 60;

        tooltipNameText = CreateTooltipText("Name", 6f, FontStyles.Bold);
        SetTooltipTextRect(tooltipNameText.rectTransform, 4f, -4f, 142f, 12f);

        tooltipDescriptionText = CreateTooltipText("Description", 4.5f, FontStyles.Normal);
        SetTooltipTextRect(tooltipDescriptionText.rectTransform, 4f, -17f, 142f, 32f);

        tooltipTimeText = CreateTooltipText("RemainingTime", 4.5f, FontStyles.Bold);
        tooltipTimeText.color = new Color(0.55f, 0.9f, 1f, 1f);
        SetTooltipTextRect(tooltipTimeText.rectTransform, 4f, -51f, 142f, 9f);

        tooltipObject.SetActive(false);
    }

    private void PositionTooltip()
    {
        if (tooltipRoot == null || tooltipCanvasRoot == null)
        {
            return;
        }

        RectTransform slotRect = transform as RectTransform;
        if (slotRect == null)
        {
            return;
        }

        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, corners[1]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            tooltipCanvasRoot,
            screenPoint,
            null,
            out Vector2 localPoint);

        Rect canvasRect = tooltipCanvasRoot.rect;
        Vector2 size = tooltipRoot.rect.size;
        float x = Mathf.Clamp(localPoint.x, canvasRect.xMin + 3f, canvasRect.xMax - size.x - 3f);
        float y = Mathf.Clamp(localPoint.y, canvasRect.yMin + size.y + 3f, canvasRect.yMax - 3f);
        tooltipRoot.anchoredPosition = new Vector2(x, y);
        tooltipRoot.SetAsLastSibling();
    }

    private TextMeshProUGUI CreateTooltipText(string objectName, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(tooltipRoot, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI fontSource = remainingTimeText != null ? remainingTimeText : stackCountText;
        if (fontSource != null)
        {
            text.font = fontSource.font;
        }

        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTooltipTextRect(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void RefreshTooltipTime()
    {
        if (tooltipTimeText == null)
        {
            return;
        }

        bool timed = showsDuration && lastDurationSeconds > 0f && lastRemainingSeconds > 0f;
        tooltipTimeText.gameObject.SetActive(timed);
        tooltipTimeText.text = timed
            ? $"남은 시간 {lastRemainingSeconds:0.0}초"
            : string.Empty;
        if (timed)
        {
            tooltipTimeText.text = string.Format("Remaining {0:0.0}s", lastRemainingSeconds);
        }
    }

    private void HideTooltip()
    {
        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void SetStackCount(int stackCount)
    {
        if (stackCountText == null)
        {
            return;
        }

        bool visible = stackCount > 1;
        stackCountText.gameObject.SetActive(visible);
        stackCountText.text = visible ? stackCount.ToString() : string.Empty;
    }
}
