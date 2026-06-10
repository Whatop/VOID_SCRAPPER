using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MotionTitleSceneTransition : MonoBehaviour
{
    public static MotionTitleSceneTransition Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool preserveRootObjectForDontDestroy = true;
    [SerializeField] private bool detachFromParentBeforeDontDestroy;

    [Header("References")]
    [SerializeField] private RectTransform titleRoot;
    [SerializeField] private RectTransform blackOverlayRoot;
    [SerializeField] private MotionTitleView transitionTitlePrefab;

    [Header("Black Background")]
    [SerializeField] private CanvasGroup blackOverlayCanvasGroup;
    [SerializeField] private Image blackOverlayImage;
    [SerializeField] private bool createBlackOverlayIfMissing = true;
    [SerializeField] private bool blackOverlayBlocksRaycasts = true;

    [Header("Title Rect - MTP-20")]
    [SerializeField] private bool forceTitleRect = true;
    [SerializeField] private Vector2 titleSize = new Vector2(330f, 110f);
    [SerializeField] private Vector2 titleAnchor = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 titlePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 titleAnchoredPosition = Vector2.zero;
    [SerializeField] private float titleScale = 1f;

    [Header("Timing")]
    [SerializeField] private float inDuration = 2.0f;
    [SerializeField] private float blackHoldDuration = 0.2f;
    [SerializeField] private float outDuration = 2.0f;
    [SerializeField] private float animationSpeed = 1.0f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Text - Expedition Start")]
    [SerializeField] private string expeditionInTitle = "탐사 시작!";
    [SerializeField] private string expeditionInSubtitle = "";
    [SerializeField] private bool useCurrentSeaRegionSubtitle = true;
    [SerializeField] private string seaRegionSubtitleFormat = "{0} 입장";
    [SerializeField] private string expeditionOutTitle = "";
    [SerializeField] private string expeditionOutSubtitle = "";

    [Header("Behavior")]
    [SerializeField] private bool waitOneFrameAfterLoad = true;
    [SerializeField] private bool logTransition = true;

    private MotionTitleView currentView;
    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (titleRoot == null)
        {
            titleRoot = transform as RectTransform;
        }

        if (blackOverlayRoot == null)
        {
            blackOverlayRoot = titleRoot;
        }

        if (dontDestroyOnLoad)
        {
            GameObject persistTarget = ResolveDontDestroyTarget();
            DontDestroyOnLoad(persistTarget);
        }

        EnsureBlackOverlay();
        SetBlackAlpha(0f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void TransitionToExpedition()
    {
        TransitionToScene(
            GetExpeditionSceneName(),
            GameState.Expedition,
            expeditionInTitle,
            ResolveExpeditionInSubtitle(),
            expeditionOutTitle,
            expeditionOutSubtitle
        );
    }

    public void TransitionToScene(
        string sceneName,
        GameState stateAfterLoad,
        string inTitle,
        string inSubtitle,
        string outTitle,
        string outSubtitle)
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(TransitionRoutine(
            sceneName,
            stateAfterLoad,
            inTitle,
            inSubtitle,
            outTitle,
            outSubtitle
        ));
    }

    public void SetExpeditionInSubtitle(string subtitle)
    {
        expeditionInSubtitle = subtitle ?? string.Empty;
    }

    private IEnumerator TransitionRoutine(
        string sceneName,
        GameState stateAfterLoad,
        string inTitle,
        string inSubtitle,
        string outTitle,
        string outSubtitle)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("MotionTitleSceneTransition: 로드할 씬 이름이 비어 있습니다.", this);
            yield break;
        }

        isTransitioning = true;

        if (logTransition)
        {
            Debug.Log($"Motion Title Transition Start: {sceneName}", this);
        }

        if (transitionTitlePrefab == null)
        {
            Debug.LogError("MotionTitleSceneTransition: transitionTitlePrefab이 비어 있습니다. 모션 타이틀 없이 씬을 로드합니다.", this);
            yield return LoadSceneRoutine(sceneName, stateAfterLoad);
            isTransitioning = false;
            yield break;
        }

        EnsureBlackOverlay();
        SetBlackAlpha(0f);
        CreateOrResetView();

        currentView.Setup(
            inTitle,
            inSubtitle,
            0f,
            animationSpeed,
            useUnscaledTime
        );

        currentView.gameObject.SetActive(true);
        currentView.PlayIn();

        yield return FadeBlackRoutine(1f, inDuration);
        SetBlackAlpha(1f);

        yield return Wait(blackHoldDuration);

        yield return LoadSceneRoutine(sceneName, stateAfterLoad);

        if (waitOneFrameAfterLoad)
        {
            yield return null;
        }

        if (currentView != null)
        {
            currentView.SetText(outTitle, outSubtitle);
            currentView.PlayOut();
        }

        yield return FadeBlackRoutine(0f, outDuration);
        SetBlackAlpha(0f);

        if (currentView != null)
        {
            Destroy(currentView.gameObject);
            currentView = null;
        }

        if (logTransition)
        {
            Debug.Log($"Motion Title Transition Complete: {sceneName}", this);
        }

        isTransitioning = false;
    }

    private IEnumerator LoadSceneRoutine(string sceneName, GameState stateAfterLoad)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(stateAfterLoad);
        }
    }

    private void CreateOrResetView()
    {
        if (currentView != null)
        {
            Destroy(currentView.gameObject);
            currentView = null;
        }

        Transform parent = titleRoot != null ? titleRoot : transform;
        currentView = Instantiate(transitionTitlePrefab, parent);

        PrepareTitleRect(currentView.transform as RectTransform);
        currentView.gameObject.SetActive(true);
        currentView.transform.SetAsLastSibling();
    }

    private void PrepareTitleRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, titleScale);

        if (!forceTitleRect)
        {
            return;
        }

        rectTransform.anchorMin = titleAnchor;
        rectTransform.anchorMax = titleAnchor;
        rectTransform.pivot = titlePivot;
        rectTransform.sizeDelta = titleSize;
        rectTransform.anchoredPosition = titleAnchoredPosition;
    }

    private void EnsureBlackOverlay()
    {
        if (blackOverlayCanvasGroup != null)
        {
            if (blackOverlayImage == null)
            {
                blackOverlayImage = blackOverlayCanvasGroup.GetComponent<Image>();
            }

            PrepareBlackOverlayRect(blackOverlayCanvasGroup.transform as RectTransform);
            return;
        }

        if (!createBlackOverlayIfMissing)
        {
            return;
        }

        Transform parent = blackOverlayRoot != null
            ? blackOverlayRoot
            : titleRoot != null
                ? titleRoot
                : transform;

        GameObject overlayObject = new GameObject(
            "MotionTitle_BlackOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup)
        );

        overlayObject.transform.SetParent(parent, false);
        overlayObject.transform.SetAsFirstSibling();

        blackOverlayImage = overlayObject.GetComponent<Image>();
        blackOverlayImage.color = Color.black;
        blackOverlayImage.raycastTarget = blackOverlayBlocksRaycasts;

        blackOverlayCanvasGroup = overlayObject.GetComponent<CanvasGroup>();
        PrepareBlackOverlayRect(overlayObject.transform as RectTransform);
    }

    private void PrepareBlackOverlayRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.SetAsFirstSibling();
    }

    private IEnumerator FadeBlackRoutine(float targetAlpha, float duration)
    {
        EnsureBlackOverlay();

        if (blackOverlayCanvasGroup == null)
        {
            yield return Wait(duration);
            yield break;
        }

        duration = Mathf.Max(0f, duration);
        float startAlpha = blackOverlayCanvasGroup.alpha;

        if (duration <= 0f)
        {
            SetBlackAlpha(targetAlpha);
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            SetBlackAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetBlackAlpha(targetAlpha);
    }

    private void SetBlackAlpha(float alpha)
    {
        if (blackOverlayCanvasGroup == null)
        {
            return;
        }

        alpha = Mathf.Clamp01(alpha);
        blackOverlayCanvasGroup.alpha = alpha;
        blackOverlayCanvasGroup.interactable = blackOverlayBlocksRaycasts && alpha > 0.001f;
        blackOverlayCanvasGroup.blocksRaycasts = blackOverlayBlocksRaycasts && alpha > 0.001f;

        if (blackOverlayImage != null)
        {
            blackOverlayImage.color = Color.black;
            blackOverlayImage.raycastTarget = blackOverlayBlocksRaycasts;
        }
    }

    private IEnumerator Wait(float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f)
        {
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    private GameObject ResolveDontDestroyTarget()
    {
        if (preserveRootObjectForDontDestroy && transform.root != null)
        {
            return transform.root.gameObject;
        }

        if (detachFromParentBeforeDontDestroy && transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        return gameObject;
    }

    private string GetExpeditionSceneName()
    {
        if (SceneFlowManager.Instance != null)
        {
            return SceneFlowManager.Instance.ExpeditionSceneName;
        }

        return "Expedition";
    }

    private string ResolveExpeditionInSubtitle()
    {
        if (!useCurrentSeaRegionSubtitle)
        {
            return expeditionInSubtitle;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return expeditionInSubtitle;
        }

        string seaRegionName = RunManager.Instance.CurrentRun.SeaRegionDisplayName;

        if (string.IsNullOrWhiteSpace(seaRegionName))
        {
            return expeditionInSubtitle;
        }

        if (string.IsNullOrWhiteSpace(seaRegionSubtitleFormat))
        {
            return $"{seaRegionName} 입장";
        }

        try
        {
            return string.Format(seaRegionSubtitleFormat, seaRegionName);
        }
        catch (FormatException)
        {
            return $"{seaRegionName} 입장";
        }
    }
}