using System.Collections;
using TMPro;
using UnityEngine;

public enum ShipCommunicationChannel
{
    Navigation,
    Cargo,
    Combat,
    Radar,
    Equipment,
    System
}

public enum ShipCommunicationSeverity
{
    Information,
    Confirmation,
    Warning,
    Danger
}

[DisallowMultipleComponent]
public class WarningMessageUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] private float defaultDuration = 1.4f;
    [SerializeField] private float fadeSpeed = 12f;
    [SerializeField, Min(0f)] private float repeatSuppressionSeconds = 0.75f;

    [Header("Compact Layout")]
    [SerializeField, Min(80f)] private float maximumWidth = 220f;
    [SerializeField, Min(16f)] private float maximumHeight = 32f;
    [SerializeField, Min(1f)] private float minimumFontSize = 5.5f;
    [SerializeField, Min(1f)] private float maximumFontSize = 7f;

    [Header("Communication Colors")]
    [SerializeField] private Color channelColor = new Color(0.58f, 0.86f, 1f, 1f);
    [SerializeField] private Color informationColor = new Color(0.88f, 0.94f, 1f, 1f);
    [SerializeField] private Color confirmationColor = new Color(0.42f, 1f, 0.72f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.24f, 1f);
    [SerializeField] private Color dangerColor = new Color(1f, 0.32f, 0.26f, 1f);

    [Header("Option")]
    [Tooltip("켜두면 오브젝트가 비활성화된 상태에서도 ShowMessage 호출 시 자동으로 다시 활성화합니다.")]
    [SerializeField] private bool reactivateSelfWhenNeeded = true;

    private Coroutine routine;
    private string lastMessageKey;
    private float lastMessageTime = float.NegativeInfinity;
    private int currentPriority = -1;
    private float currentMessageUntil;
    private bool layoutConfigured;

    private void Reset()
    {
        messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        EnsureReferences();
        HideImmediate();
    }

    private void OnEnable()
    {
        GameSettingsRuntime.Changed += HandleSettingsChanged;
    }

    private void OnDisable()
    {
        GameSettingsRuntime.Changed -= HandleSettingsChanged;
        routine = null;
        currentPriority = -1;
        currentMessageUntil = 0f;
    }

    private void HandleSettingsChanged()
    {
        if (canvasGroup != null && canvasGroup.alpha > 0.001f)
        {
            canvasGroup.alpha = Mathf.Min(canvasGroup.alpha, GameSettingsRuntime.WarningOpacity);
        }
    }

    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultDuration);
    }

    public void ShowMessage(string message, float duration)
    {
        ShowMessageInternal(message, message, duration, (int)ShipCommunicationSeverity.Warning);
    }

    public void ShowCommunication(
        ShipCommunicationChannel channel,
        string message,
        ShipCommunicationSeverity severity = ShipCommunicationSeverity.Warning)
    {
        ShowCommunication(channel, message, severity, defaultDuration);
    }

    public void ShowCommunication(
        ShipCommunicationChannel channel,
        string message,
        ShipCommunicationSeverity severity,
        float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string messageKey = $"{channel}:{message}";
        string formattedMessage = BuildCommunicationText(channel, message, severity);
        ShowMessageInternal(formattedMessage, messageKey, duration, (int)severity);
    }

    private void ShowMessageInternal(string message, string messageKey, float duration, int priority)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        float now = Time.unscaledTime;
        if (messageKey == lastMessageKey && now - lastMessageTime < repeatSuppressionSeconds)
        {
            return;
        }

        if (routine != null && now < currentMessageUntil && priority < currentPriority)
        {
            return;
        }

        EnsureActiveForCoroutine();
        EnsureReferences();

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                "WarningMessageUI가 비활성화되어 경고 메시지를 표시할 수 없습니다. WarningMessage 오브젝트를 Hierarchy에서 활성 상태로 유지하세요.",
                this
            );
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        lastMessageKey = messageKey;
        lastMessageTime = now;
        currentPriority = priority;
        currentMessageUntil = now + Mathf.Max(0f, duration);
        routine = StartCoroutine(ShowRoutine(message, duration));
    }

    public void Hide()
    {
        EnsureActiveForCoroutine();
        EnsureReferences();

        if (!isActiveAndEnabled)
        {
            HideImmediate();
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        currentPriority = -1;
        currentMessageUntil = 0f;
        routine = StartCoroutine(HideRoutine());
    }

    public void HideImmediate()
    {
        EnsureReferences();

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        SetCanvasGroup(0f, false);
        currentPriority = -1;
        currentMessageUntil = 0f;

        // 이 오브젝트 자체는 끄지 않는다. 항상 활성 상태를 유지해야
        // 다음 경고에서 Coroutine을 정상적으로 시작할 수 있다.
    }

    private IEnumerator ShowRoutine(string message, float duration)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        yield return FadeTo(1f);

        if (duration > 0f)
        {
            yield return new WaitForSecondsRealtime(duration);
            yield return FadeTo(0f);
        }

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        routine = null;
        currentPriority = -1;
        currentMessageUntil = 0f;
    }

    private IEnumerator HideRoutine()
    {
        yield return FadeTo(0f);

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        routine = null;
        currentPriority = -1;
        currentMessageUntil = 0f;
    }

    private string BuildCommunicationText(
        ShipCommunicationChannel channel,
        string message,
        ShipCommunicationSeverity severity)
    {
        string label = ResolveChannelLabel(channel);
        string labelHex = ColorUtility.ToHtmlStringRGB(channelColor);
        string messageHex = ColorUtility.ToHtmlStringRGB(ResolveSeverityColor(severity));
        return $"<color=#{labelHex}>[{label}]</color> <color=#{messageHex}>{message}</color>";
    }

    private static string ResolveChannelLabel(ShipCommunicationChannel channel)
    {
        return channel switch
        {
            ShipCommunicationChannel.Navigation => "항법",
            ShipCommunicationChannel.Cargo => "화물",
            ShipCommunicationChannel.Combat => "전투",
            ShipCommunicationChannel.Radar => "레이더",
            ShipCommunicationChannel.Equipment => "장비",
            _ => "시스템"
        };
    }

    private Color ResolveSeverityColor(ShipCommunicationSeverity severity)
    {
        return severity switch
        {
            ShipCommunicationSeverity.Information => informationColor,
            ShipCommunicationSeverity.Confirmation => confirmationColor,
            ShipCommunicationSeverity.Danger => dangerColor,
            _ => warningColor
        };
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        EnsureReferences();

        if (canvasGroup == null)
        {
            yield break;
        }

        targetAlpha = Mathf.Clamp01(targetAlpha);
        float effectiveTargetAlpha = targetAlpha > 0.001f
            ? targetAlpha * GameSettingsRuntime.WarningOpacity
            : 0f;

        while (!Mathf.Approximately(canvasGroup.alpha, effectiveTargetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                effectiveTargetAlpha,
                fadeSpeed * Time.unscaledDeltaTime
            );

            bool visible = canvasGroup.alpha > 0.01f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            yield return null;
        }

        SetCanvasGroup(effectiveTargetAlpha, effectiveTargetAlpha > 0.01f);
    }

    private void SetCanvasGroup(float alpha, bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void EnsureReferences()
    {
        if (messageText == null)
        {
            messageText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (messageText != null)
        {
            messageText.richText = true;
            if (!layoutConfigured)
            {
                RectTransform textRect = messageText.rectTransform;
                textRect.sizeDelta = new Vector2(
                    Mathf.Min(maximumWidth, Mathf.Max(80f, textRect.rect.width)),
                    maximumHeight
                );
                messageText.enableAutoSizing = true;
                messageText.fontSizeMin = minimumFontSize;
                messageText.fontSizeMax = maximumFontSize;
                messageText.textWrappingMode = TextWrappingModes.Normal;
                messageText.overflowMode = TextOverflowModes.Ellipsis;
                messageText.maxVisibleLines = 2;
                messageText.alignment = TextAlignmentOptions.Center;
                messageText.margin = new Vector4(2f, 1f, 2f, 1f);
                layoutConfigured = true;
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void EnsureActiveForCoroutine()
    {
        if (!reactivateSelfWhenNeeded)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!enabled)
        {
            enabled = true;
        }
    }
}
