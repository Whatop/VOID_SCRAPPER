using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EventTitleDirector : MonoBehaviour
{
    [Serializable]
    private class TitlePrefabOverride
    {
        public EventTitleType type;
        public MotionTitleView prefab;
    }

    public static EventTitleDirector Instance { get; private set; }

    private const string DefaultNormalAreaName = "잔해 해역";
    private const string DefaultDeepAreaName = "심부 잔해 해역";

    [Header("Instance")]
    [SerializeField] private bool registerAsGlobalInstance = true;
    [SerializeField] private bool destroyIfGlobalDuplicate = true;
    [SerializeField] private bool dontDestroyGlobalInstance;

    [Header("References")]
    [SerializeField] private RectTransform titleRoot;
    [SerializeField] private MotionTitleView defaultTitlePrefab;

    [Header("MTP Prefab Overrides")]
    [Tooltip("비워두면 defaultTitlePrefab을 사용합니다. 탐사 시작, 코어 반응, 귀환 같은 전역 타이틀 타입별 프리팹만 지정합니다.")]
    [SerializeField] private List<TitlePrefabOverride> titlePrefabOverrides = new List<TitlePrefabOverride>();

    [Header("Title Rect - MTP")]
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
        public MotionTitleView prefabOverride;
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
        Enqueue(EventTitleType.ExpeditionStart, "탐사 시작", areaName, null);
    }

    public void ShowExpeditionStart(ExpeditionDepth depth)
    {
        ShowExpeditionStart(GetAreaName(depth));
    }

    public void ShowCoreReaction()
    {
        Enqueue(EventTitleType.CoreReaction, "코어 반응", "강력한 에너지 반응 감지", null);
    }

    public void ShowBossEncounter(string bossName = "구획 관리자", string subtitle = "SECTOR ADMINISTRATOR")
    {
        Enqueue(EventTitleType.BossEncounter, bossName, subtitle, null);
    }

    public void ShowSafeReturn()
    {
        Enqueue(EventTitleType.SafeReturn, "안전 귀환", "정착지로 귀환합니다", null);
    }

    public void ShowEmergencyReturn()
    {
        Enqueue(EventTitleType.EmergencyReturn, "긴급 복귀", "정착지로 귀환합니다", null);
    }

    public void ShowSafeReturnComplete()
    {
        Enqueue(EventTitleType.SafeReturnComplete, "안전 귀환 완료", string.Empty, null);
    }

    public void ShowEmergencyReturnComplete()
    {
        Enqueue(EventTitleType.EmergencyReturnComplete, "긴급 복귀 완료", string.Empty, null);
    }

    public void Show(EventTitleType type, string customTitle = "", string customSubtitle = "")
    {
        ShowWithPrefab(null, type, customTitle, customSubtitle);
    }

    public void ShowWithPrefab(MotionTitleView prefabOverride, EventTitleType type, string customTitle = "", string customSubtitle = "")
    {
        string title = customTitle;
        string subtitle = customSubtitle;

        switch (type)
        {
            case EventTitleType.ExpeditionStart:
                title = string.IsNullOrWhiteSpace(customTitle) ? "탐사 시작" : customTitle;
                subtitle = string.IsNullOrWhiteSpace(customSubtitle) ? NormalizeAreaName(string.Empty) : customSubtitle;
                break;

            case EventTitleType.CoreReaction:
                title = string.IsNullOrWhiteSpace(customTitle) ? "코어 반응" : customTitle;
                subtitle = string.IsNullOrWhiteSpace(customSubtitle) ? "강력한 에너지 반응 감지" : customSubtitle;
                break;

            case EventTitleType.BossEncounter:
                title = string.IsNullOrWhiteSpace(customTitle) ? "구획 관리자" : customTitle;
                subtitle = string.IsNullOrWhiteSpace(customSubtitle) ? "SECTOR ADMINISTRATOR" : customSubtitle;
                break;

            case EventTitleType.SafeReturn:
                title = string.IsNullOrWhiteSpace(customTitle) ? "안전 귀환" : customTitle;
                subtitle = string.IsNullOrWhiteSpace(customSubtitle) ? "정착지로 귀환합니다" : customSubtitle;
                break;

            case EventTitleType.EmergencyReturn:
                title = string.IsNullOrWhiteSpace(customTitle) ? "긴급 복귀" : customTitle;
                subtitle = string.IsNullOrWhiteSpace(customSubtitle) ? "정착지로 귀환합니다" : customSubtitle;
                break;

            case EventTitleType.SafeReturnComplete:
                title = string.IsNullOrWhiteSpace(customTitle) ? "안전 귀환 완료" : customTitle;
                break;

            case EventTitleType.EmergencyReturnComplete:
                title = string.IsNullOrWhiteSpace(customTitle) ? "긴급 복귀 완료" : customTitle;
                break;
        }

        Enqueue(type, title, subtitle, prefabOverride);
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

    private void Enqueue(EventTitleType type, string title, string subtitle, MotionTitleView prefabOverride)
    {
        MotionTitleView prefab = prefabOverride != null ? prefabOverride : GetPrefabForType(type);

        if (prefab == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"EventTitleDirector: {type}에 사용할 MotionTitleView 프리팹이 없습니다.", this);
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
            subtitle = subtitle,
            prefabOverride = prefabOverride
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
        MotionTitleView prefab = request.prefabOverride != null ? request.prefabOverride : GetPrefabForType(request.type);

        if (prefab == null)
        {
            yield break;
        }

        Transform parent = titleRoot != null ? titleRoot : transform;
        MotionTitleView view = Instantiate(prefab, parent);

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

    private MotionTitleView GetPrefabForType(EventTitleType type)
    {
        if (titlePrefabOverrides != null)
        {
            for (int i = 0; i < titlePrefabOverrides.Count; i++)
            {
                TitlePrefabOverride entry = titlePrefabOverrides[i];

                if (entry != null && entry.type == type && entry.prefab != null)
                {
                    return entry.prefab;
                }
            }
        }

        return defaultTitlePrefab;
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
        return CampaignProgressionCatalog.GetRegionDisplayName(depth);
    }
}
