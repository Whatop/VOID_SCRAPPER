using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class WarningMessageUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] private float defaultDuration = 1.4f;
    [SerializeField] private float fadeSpeed = 12f;

    [Header("Option")]
    [Tooltip("켜두면 비활성화된 상태에서 ShowMessage가 호출돼도 자기 오브젝트를 다시 활성화합니다.")]
    [SerializeField] private bool reactivateSelfWhenNeeded = true;

    private Coroutine routine;

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

    private void OnDisable()
    {
        routine = null;
    }

    public void ShowMessage(string message)
    {
        ShowMessage(message, defaultDuration);
    }

    public void ShowMessage(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureActiveForCoroutine();
        EnsureReferences();

        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                "WarningMessageUI가 비활성화되어 경고 메시지를 표시할 수 없습니다. WarningMessage 오브젝트를 Hierarchy에서 켜두세요.",
                this
            );
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

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

        // 중요:
        // 여기서 gameObject.SetActive(false) 하지 않는다.
        // WarningMessageUI는 항상 켜져 있어야 Coroutine을 시작할 수 있다.
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
    }

    private IEnumerator HideRoutine()
    {
        yield return FadeTo(0f);

        if (messageText != null)
        {
            messageText.text = string.Empty;
        }

        routine = null;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        EnsureReferences();

        if (canvasGroup == null)
        {
            yield break;
        }

        targetAlpha = Mathf.Clamp01(targetAlpha);

        while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha,
                targetAlpha,
                fadeSpeed * Time.unscaledDeltaTime
            );

            bool visible = canvasGroup.alpha > 0.01f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            yield return null;
        }

        SetCanvasGroup(targetAlpha, targetAlpha > 0.01f);
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