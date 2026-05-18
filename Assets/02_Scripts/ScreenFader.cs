using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool blockRaycastsDuringFade = true;

    public bool IsFading { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        SetAlpha(0f);
        SetBlocksRaycasts(false);
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return FadeTo(1f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return FadeTo(0f, duration);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        IsFading = true;
        SetBlocksRaycasts(blockRaycastsDuringFade);

        float startAlpha = canvasGroup.alpha;
        float timer = 0f;

        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);

        if (Mathf.Approximately(targetAlpha, 0f))
        {
            SetBlocksRaycasts(false);
        }

        IsFading = false;
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetBlocksRaycasts(bool value)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.blocksRaycasts = value;
        canvasGroup.interactable = value;
    }
}