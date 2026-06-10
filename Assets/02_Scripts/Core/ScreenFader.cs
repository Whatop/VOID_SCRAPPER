using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("DontDestroyOnLoad는 루트 오브젝트에만 안정적으로 적용됩니다. 켜두면 부모에서 분리한 뒤 유지합니다.")]
    [SerializeField] private bool detachFromParentBeforeDontDestroy = true;

    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool blockRaycastsDuringFade = true;

    [Header("Initial State")]
    [SerializeField] private bool startTransparent = true;

    public bool IsFading { get; private set; }
    public float Alpha => canvasGroup != null ? canvasGroup.alpha : 0f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (dontDestroyOnLoad)
        {
            if (detachFromParentBeforeDontDestroy && transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
        }

        if (startTransparent)
        {
            SetAlpha(0f);
            SetBlocksRaycasts(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Coroutine FadeIn(MonoBehaviour runner, float duration)
    {
        if (runner == null)
        {
            return null;
        }

        return runner.StartCoroutine(FadeIn(duration));
    }

    public Coroutine FadeOut(MonoBehaviour runner, float duration)
    {
        if (runner == null)
        {
            return null;
        }

        return runner.StartCoroutine(FadeOut(duration));
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return FadeTo(1f, duration);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return FadeTo(0f, duration);
    }

    public void SetBlackImmediate()
    {
        StopCurrentFade();
        SetAlpha(1f);
        SetBlocksRaycasts(true);
        IsFading = false;
    }

    public void SetClearImmediate()
    {
        StopCurrentFade();
        SetAlpha(0f);
        SetBlocksRaycasts(false);
        IsFading = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        StopCurrentFadeOnly();

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        yield return fadeRoutine;
        fadeRoutine = null;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
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
        else
        {
            SetBlocksRaycasts(true);
        }

        IsFading = false;
    }

    private void StopCurrentFade()
    {
        StopCurrentFadeOnly();
        IsFading = false;
    }

    private void StopCurrentFadeOnly()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
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