using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EventTitleDirector : MonoBehaviour
{
    public static EventTitleDirector Instance { get; private set; }

    private const string DefaultNormalAreaName = "잔해 해역";
    private const string DefaultDeepAreaName = "심부 잔해 해역";

    [Header("Instance")]
    [Tooltip("켜면 EventTitleDirector.Instance로 등록됩니다. 여러 개를 쓸 때는 메인 하나만 켜세요.")]
    [SerializeField] private bool registerAsGlobalInstance = true;

    [Tooltip("Global Instance가 중복될 때 이 오브젝트를 삭제합니다.")]
    [SerializeField] private bool destroyIfGlobalDuplicate = true;

    [Tooltip("Global Instance로 쓸 때 씬 전환에도 유지합니다.")]
    [SerializeField] private bool dontDestroyGlobalInstance;

    [Header("References")]
    [SerializeField] private RectTransform titleRoot;
    [SerializeField] private MotionTitleView defaultTitlePrefab;

    [Header("Title Rect - MTP-20")]
    [SerializeField] private bool forceTitleRect = true;
    [SerializeField] private Vector2 titleSize = new Vector2(330f, 110f);
    [SerializeField] private Vector2 titleAnchor = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 titlePivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 titleAnchoredPosition = Vector2.zero;
    [SerializeField] private float titleScale = 1f;

    [Header("Timing")]
    [SerializeField] private float defaultShowFor = 1.8f;
    [SerializeField] private float defaultTotalLifetime = 3.0f;
    [SerializeField] private float defaultAnimationSpeed = 1.0f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Cooldown")]
    [SerializeField] private float sameTitleCooldown = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;

    private readonly Queue<TitleRequest> queue = new Queue<TitleRequest>();
    private readonly Dictionary<EventTitleType, float> lastShownTimes = new Dictionary<EventTitleType, float>();

    private Coroutine playRoutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public bool IsGlobalInstance => registerAsGlobalInstance && Instance == this;

    private struct TitleRequest
    {
        public EventTitleType type;
        public string title;
        public string subtitle;
    }

    private void Awake()
    {
        if (registerAsGlobalInstance)
        {
            if (Instance != null && Instance != this)
            {
                if (destroyIfGlobalDuplicate)
                {
                    Destroy(gameObject);
                    return;
                }

                registerAsGlobalInstance = false;
            }
            else
            {
                Instance = this;

                if (dontDestroyGlobalInstance)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
        }

        if (titleRoot == null)
        {
            titleRoot = transform as RectTransform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowExpeditionStart(string areaName)
    {
        areaName = NormalizeAreaName(areaName);
        Enqueue(EventTitleType.ExpeditionStart, "탐사시작!", areaName);
    }

    public void ShowExpeditionStart(ExpeditionDepth depth)
    {
        ShowExpeditionStart(GetAreaName(depth));
    }

    public void ShowCoreReaction()
    {
        Enqueue(EventTitleType.CoreReaction, "코어 반응", "강렬한 에너지 반응 감지");
    }

    public void ShowSafeReturn()
    {
        Enqueue(EventTitleType.SafeReturn, "안전복귀!", "정착지로 귀환합니다");
    }

    public void ShowEmergencyReturn()
    {
        Enqueue(EventTitleType.EmergencyReturn, "긴급복귀!", "정착지로 귀환합니다");
    }

    public void ShowSafeReturnComplete()
    {
        Enqueue(EventTitleType.SafeReturnComplete, "안전복귀완료!", "");
    }

    public void ShowEmergencyReturnComplete()
    {
        Enqueue(EventTitleType.EmergencyReturnComplete, "긴급복귀완료!", "");
    }

    public void Show(EventTitleType type, string customTitle = "", string customSubtitle = "")
    {
        switch (type)
        {
            case EventTitleType.ExpeditionStart:
                Enqueue(
                    EventTitleType.ExpeditionStart,
                    string.IsNullOrWhiteSpace(customTitle) ? "탐사시작!" : customTitle,
                    string.IsNullOrWhiteSpace(customSubtitle) ? NormalizeAreaName("") : customSubtitle
                );
                break;

            case EventTitleType.CoreReaction:
                Enqueue(
                    EventTitleType.CoreReaction,
                    string.IsNullOrWhiteSpace(customTitle) ? "코어 반응" : customTitle,
                    string.IsNullOrWhiteSpace(customSubtitle) ? "강렬한 에너지 반응 감지" : customSubtitle
                );
                break;

            case EventTitleType.SafeReturn:
                Enqueue(
                    EventTitleType.SafeReturn,
                    string.IsNullOrWhiteSpace(customTitle) ? "안전복귀!" : customTitle,
                    string.IsNullOrWhiteSpace(customSubtitle) ? "정착지로 귀환합니다" : customSubtitle
                );
                break;

            case EventTitleType.EmergencyReturn:
                Enqueue(
                    EventTitleType.EmergencyReturn,
                    string.IsNullOrWhiteSpace(customTitle) ? "긴급복귀!" : customTitle,
                    string.IsNullOrWhiteSpace(customSubtitle) ? "정착지로 귀환합니다" : customSubtitle
                );
                break;

            case EventTitleType.SafeReturnComplete:
                Enqueue(
                    EventTitleType.SafeReturnComplete,
                    string.IsNullOrWhiteSpace(customTitle) ? "안전복귀완료!" : customTitle,
                    customSubtitle
                );
                break;

            case EventTitleType.EmergencyReturnComplete:
                Enqueue(
                    EventTitleType.EmergencyReturnComplete,
                    string.IsNullOrWhiteSpace(customTitle) ? "긴급복귀완료!" : customTitle,
                    customSubtitle
                );
                break;

            default:
                Enqueue(type, customTitle, customSubtitle);
                break;
        }
    }

    public void ClearQueue()
    {
        queue.Clear();
    }

    public void StopCurrentAndClear()
    {
        queue.Clear();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        isPlaying = false;

        if (titleRoot == null)
        {
            return;
        }

        for (int i = titleRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = titleRoot.GetChild(i);

            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void Enqueue(EventTitleType type, string title, string subtitle)
    {
        if (defaultTitlePrefab == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning("EventTitleDirector: defaultTitlePrefab이 비어 있습니다.", this);
            }

            return;
        }

        float now = Time.unscaledTime;

        if (lastShownTimes.TryGetValue(type, out float lastShownTime) &&
            now - lastShownTime < sameTitleCooldown)
        {
            return;
        }

        lastShownTimes[type] = now;

        queue.Enqueue(new TitleRequest
        {
            type = type,
            title = title,
            subtitle = subtitle
        });

        if (!isPlaying && isActiveAndEnabled)
        {
            playRoutine = StartCoroutine(PlayQueueRoutine());
        }
    }

    private IEnumerator PlayQueueRoutine()
    {
        isPlaying = true;

        while (queue.Count > 0)
        {
            TitleRequest request = queue.Dequeue();
            yield return PlaySingleRoutine(request);
        }

        playRoutine = null;
        isPlaying = false;
    }

    private IEnumerator PlaySingleRoutine(TitleRequest request)
    {
        if (defaultTitlePrefab == null)
        {
            yield break;
        }

        Transform parent = titleRoot != null ? titleRoot : transform;
        MotionTitleView view = Instantiate(defaultTitlePrefab, parent);

        PrepareTitleRect(view.transform as RectTransform);
        view.gameObject.SetActive(true);

        view.Setup(
            request.title,
            request.subtitle,
            defaultShowFor,
            defaultAnimationSpeed,
            useUnscaledTime
        );

        view.Play();

        float timer = 0f;
        float lifetime = Mathf.Max(0.1f, defaultTotalLifetime);

        while (timer < lifetime)
        {
            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        if (view != null)
        {
            Destroy(view.gameObject);
        }
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

    private string NormalizeAreaName(string areaName)
    {
        if (!string.IsNullOrWhiteSpace(areaName))
        {
            return areaName;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return GetAreaName(RunManager.Instance.CurrentRun.ExpeditionDepth);
        }

        return DefaultNormalAreaName;
    }

    private string GetAreaName(ExpeditionDepth depth)
    {
        return depth == ExpeditionDepth.DeepZone1
            ? DefaultDeepAreaName
            : DefaultNormalAreaName;
    }
}