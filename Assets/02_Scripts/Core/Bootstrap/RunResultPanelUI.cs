using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunResultPanelUI : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool detachFromParentBeforeDontDestroy = true;

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI reasonText;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Counters")]
    [SerializeField] private ResourceCounterUI committedScrapCounter;
    [SerializeField] private ResourceCounterUI committedCoreCounter;
    [SerializeField] private ResourceCounterUI lostScrapCounter;
    [SerializeField] private ResourceCounterUI lostCoreCounter;
    [SerializeField] private ResourceCounterUI totalScrapCounter;
    [SerializeField] private ResourceCounterUI totalCoreCounter;

    [Header("Button")]
    [SerializeField] private Button continueButton;

    [Header("Timing")]
    [SerializeField] private float showDelay = 0.15f;

    [Header("Continue Fade")]
    [SerializeField] private bool useFadeOnContinue = true;
    [SerializeField] private bool loadSettlementOnContinue = true;
    [SerializeField] private string settlementSceneNameFallback = "Settlement";
    [SerializeField] private float continueFadeInDuration = 0.35f;
    [SerializeField] private float continueBlackHoldDuration = 0.08f;
    [SerializeField] private float continueFadeOutDuration = 0.35f;

    private RunManager subscribedRunManager;
    private Coroutine showRoutine;
    private Coroutine closeRoutine;
    private bool isClosing;
    private readonly List<ResourceCounterUI> counterPool = new List<ResourceCounterUI>();
    private Sprite scrapResourceIcon;
    private Sprite coreResourceIcon;
    private RectTransform resultCard;
    private RectTransform settlementPanel;
    private RectTransform recordPanel;
    private TextMeshProUGUI committedHeaderText;
    private TextMeshProUGUI lostHeaderText;
    private TextMeshProUGUI emptyCommittedText;
    private TextMeshProUGUI permanentTotalsText;
    private Image resultAccentRail;
    private Image settlementAccentRail;
    private Image recordAccentRail;
    private Sequence showSequence;
    private bool presentationLayoutReady;
    private bool runtimeInitialized;
    private bool runtimeCallbacksBound;
    private bool invalidOwnershipReported;
    private Coroutine deferredInitializationRoutine;

    private static readonly Color BackdropColor = new Color(0.018f, 0.035f, 0.052f, 1f);
    private static readonly Color CardColor = new Color(0.027f, 0.055f, 0.078f, 0.98f);
    private static readonly Color SectionColor = new Color(0.035f, 0.075f, 0.105f, 0.96f);
    private static readonly Color PrimaryTextColor = new Color(0.92f, 0.97f, 1f, 1f);
    private static readonly Color SecondaryTextColor = new Color(0.62f, 0.72f, 0.78f, 1f);
    private static readonly Color PositiveColor = new Color(0.35f, 0.94f, 1f, 1f);
    private static readonly Color LossColor = new Color(1f, 0.36f, 0.28f, 1f);

    private void Awake()
    {
        TryInitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (TryInitializeRuntime())
        {
            BindRuntimeCallbacks();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (TryInitializeRuntime())
        {
            BindRuntimeCallbacks();
            return;
        }

        ScheduleDeferredInitialization();
    }

    private void Update()
    {
        if (runtimeInitialized && subscribedRunManager == null)
        {
            SubscribeRunManager();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (deferredInitializationRoutine != null)
        {
            StopCoroutine(deferredInitializationRoutine);
            deferredInitializationRoutine = null;
        }

        showSequence?.Kill();
        showSequence = null;

        RunManager runManager = RunManager.Instance;

        if (runManager != null && runManager.IsCompletingRun)
        {
            runManager.ReleaseRunEndingPresentationOwnership();
        }

        UnbindRuntimeCallbacks();
    }

    private bool TryInitializeRuntime()
    {
        if (runtimeInitialized)
        {
            return true;
        }

        if (!Application.isPlaying || !HasUsableSceneOwnership(transform))
        {
            return false;
        }

        if (dontDestroyOnLoad)
        {
            Transform authoredRoot = transform.root;
            bool inheritedFromBootstrapRoot = authoredRoot != null &&
                                                authoredRoot != transform &&
                                                authoredRoot.GetComponent<GameBootstrap>() != null;

            if (!inheritedFromBootstrapRoot &&
                detachFromParentBeforeDontDestroy &&
                transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            if (!inheritedFromBootstrapRoot && !IsDontDestroyOnLoadScene(gameObject.scene))
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        if (!HasUsableSceneOwnership(transform))
        {
            return false;
        }

        CacheReferences();
        if (panelRoot == null || !HasUsableSceneOwnership(panelRoot.transform))
        {
            return false;
        }

        BuildCounterPool();
        if (!EnsurePresentationLayout())
        {
            return false;
        }

        HideImmediate();
        runtimeInitialized = true;
        invalidOwnershipReported = false;
        return true;
    }

    private void ScheduleDeferredInitialization()
    {
        if (ShouldScheduleDeferredInitialization(
                Application.isPlaying,
                runtimeInitialized,
                HasUsableSceneOwnership(transform),
                deferredInitializationRoutine != null,
                isActiveAndEnabled))
        {
            deferredInitializationRoutine = StartCoroutine(InitializeAfterSceneRestoration());
        }
    }

    private static bool ShouldScheduleDeferredInitialization(
        bool isPlaying,
        bool isInitialized,
        bool hasUsableSceneOwnership,
        bool isAlreadyScheduled,
        bool isActiveAndEnabled)
    {
        return isPlaying &&
               !isInitialized &&
               !hasUsableSceneOwnership &&
               !isAlreadyScheduled &&
               isActiveAndEnabled;
    }

    private IEnumerator InitializeAfterSceneRestoration()
    {
        yield return null;
        deferredInitializationRoutine = null;

        if (TryInitializeRuntime())
        {
            BindRuntimeCallbacks();
            yield break;
        }

        ReportInvalidOwnershipOnce();
    }

    private void ReportInvalidOwnershipOnce()
    {
        if (invalidOwnershipReported || !Application.isPlaying)
        {
            return;
        }

        invalidOwnershipReported = true;
        Scene scene = gameObject.scene;
        Debug.LogError(
            $"[{nameof(RunResultPanelUI)}] '{name}' could not initialize after Unity scene " +
            $"restoration. Scene='{scene.name}', Path='{scene.path}', Valid={scene.IsValid()}, " +
            $"Loaded={scene.isLoaded}. Keep it under a valid loaded CoreRoot hierarchy.",
            this);
    }

    private void BindRuntimeCallbacks()
    {
        if (runtimeCallbacksBound)
        {
            return;
        }

        SubscribeRunManager();
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Close);
            continueButton.onClick.AddListener(Close);
        }

        runtimeCallbacksBound = true;
    }

    private void UnbindRuntimeCallbacks()
    {
        UnsubscribeRunManager();
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Close);
        }

        runtimeCallbacksBound = false;
    }

    private static bool HasUsableSceneOwnership(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        Scene scene = target.gameObject.scene;
        return scene.IsValid() && scene.isLoaded;
    }

    private static bool IsDontDestroyOnLoadScene(Scene scene)
    {
        return scene.IsValid() &&
               string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
    }

    public void Close()
    {
        AudioManager.Play(SoundEventIds.UiClick);

        if (isClosing)
        {
            return;
        }

        if (closeRoutine != null)
        {
            StopCoroutine(closeRoutine);
        }

        closeRoutine = StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        isClosing = true;

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        yield return WaitUntilSceneFlowIdle();

        ScreenFader fader = ScreenFader.Instance;

        if (useFadeOnContinue && fader != null)
        {
            yield return fader.FadeIn(continueFadeInDuration);
        }

        HidePanelVisualOnly();

        if (loadSettlementOnContinue)
        {
            yield return LoadSettlementIfNeeded();
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Settlement);
        }

        RunManager runManager = RunManager.Instance;

        if (runManager != null)
        {
            runManager.ReleaseRunEndingPresentationOwnership();
        }

        if (continueBlackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(continueBlackHoldDuration);
        }

        if (useFadeOnContinue && fader != null)
        {
            yield return fader.FadeOut(continueFadeOutDuration);
        }

        HideImmediate();

        isClosing = false;
        closeRoutine = null;
    }

    private IEnumerator WaitUntilSceneFlowIdle()
    {
        while (SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLoading)
        {
            yield return null;
        }
    }

    private IEnumerator LoadSettlementIfNeeded()
    {
        string settlementSceneName = ResolveSettlementSceneName();

        if (string.IsNullOrWhiteSpace(settlementSceneName))
        {
            yield break;
        }

        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.IsValid() && activeScene.name == settlementSceneName)
        {
            yield break;
        }

        if (SceneFlowManager.Instance != null)
        {
            SceneFlowManager.Instance.LoadSettlement();

            yield return null;

            while (SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLoading)
            {
                yield return null;
            }

            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(settlementSceneName);

        while (operation != null && !operation.isDone)
        {
            yield return null;
        }
    }

    private string ResolveSettlementSceneName()
    {
        if (SceneFlowManager.Instance != null &&
            !string.IsNullOrWhiteSpace(SceneFlowManager.Instance.SettlementSceneName))
        {
            return SceneFlowManager.Instance.SettlementSceneName;
        }

        return settlementSceneNameFallback;
    }

    private void SubscribeRunManager()
    {
        if (RunManager.Instance == null)
        {
            return;
        }

        if (subscribedRunManager == RunManager.Instance)
        {
            return;
        }

        UnsubscribeRunManager();

        subscribedRunManager = RunManager.Instance;
        subscribedRunManager.RunEnded += HandleRunEnded;
    }

    private void UnsubscribeRunManager()
    {
        if (subscribedRunManager == null)
        {
            return;
        }

        subscribedRunManager.RunEnded -= HandleRunEnded;
        subscribedRunManager = null;
    }

    private void HandleRunEnded(RunResultData resultData)
    {
        if (resultData == null)
        {
            return;
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine(resultData));
    }

    private IEnumerator ShowRoutine(RunResultData resultData)
    {
        if (showDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(showDelay);
        }

        while (GameAudioLoopController.IsRunEndMusicTransitionPending)
        {
            yield return null;
        }

        Show(resultData);
        AudioManager.Play(SoundEventIds.ResultRewardTotal);
        showRoutine = null;
    }

    private void Show(RunResultData resultData)
    {
        CacheReferences();
        EnsurePresentationLayout();

        isClosing = false;

        SetVisible(true);

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        if (titleText != null)
        {
            titleText.text = "탐사 결과";
        }

        if (reasonText != null)
        {
            reasonText.text = GetReasonText(resultData.endReason);
        }

        if (summaryText != null)
        {
            summaryText.text = BuildSummaryText(resultData);
        }

        if (detailText != null)
        {
            detailText.text = BuildDetailText(resultData);
        }

        ApplyReasonVisuals(resultData.endReason);
        RefreshSettlementResourceRows(resultData);
        RefreshPermanentTotals(resultData);
        PlayShowAnimation();
    }

    private string BuildSummaryText(RunResultData resultData)
    {
        return resultData.endReason switch
        {
            RunEndReason.SafeReturn => "안전 귀환으로 회수 자원을 모두 보존했습니다.",
            RunEndReason.EmergencyReturn => "긴급 복귀 비용을 적용하고 남은 자원을 회수했습니다.",
            RunEndReason.Death => "기체 파괴 페널티를 적용한 뒤 회수 가능 자원을 정산했습니다.",
            RunEndReason.DebugAbort => "디버그 중단 결과는 진행도에 반영되지 않습니다.",
            RunEndReason.FinalVictory => "최종 작전을 완수하고 회수 자원을 확정했습니다.",
            _ => "탐사 결과를 정산했습니다."
        };
    }

    private string BuildDetailText(RunResultData resultData)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("<color=#8FF4FF><b>탐사 기록</b></color>");
        builder.AppendLine($"<color=#8297A5>최종 해역</color>  {GetDepthText(resultData.finalDepth)}");
        builder.AppendLine($"<color=#8297A5>사용 무기</color>  {GetWeaponText(resultData.selectedWeaponTree)}");
        builder.AppendLine($"<color=#8297A5>고가치 목표</color>  {resultData.objectiveSignalCount}");

        bool hasTemporaryResource = resultData.runExperience > 0 ||
                                    resultData.unusedTuningChips > 0 ||
                                    resultData.remainingCredits > 0;

        if (hasTemporaryResource)
        {
            builder.AppendLine();
            builder.AppendLine("<color=#F3B85E><b>소멸한 임시 자원</b></color>");

            if (resultData.remainingCredits > 0)
            {
                builder.AppendLine($"<color=#8297A5>크레딧</color>  {resultData.remainingCredits}");
            }

            if (resultData.unusedTuningChips > 0)
            {
                builder.AppendLine($"<color=#8297A5>미사용 튜닝 칩</color>  {resultData.unusedTuningChips}");
            }

            if (resultData.runExperience > 0)
            {
                builder.AppendLine($"<color=#8297A5>경험치</color>  {resultData.runExperience}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private string GetReasonText(RunEndReason reason)
    {
        return reason switch
        {
            RunEndReason.SafeReturn => "안전 귀환",
            RunEndReason.EmergencyReturn => "긴급 복귀",
            RunEndReason.Death => "기체 파괴",
            RunEndReason.DebugAbort => "디버그 중단",
            RunEndReason.FinalVictory => "중앙 물류망 해방",
            _ => "탐사 종료"
        };
    }

    private string GetWeaponText(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => "근접 + 샷건",
            WeaponTreeType.Sniper => "관통 스나이퍼",
            WeaponTreeType.MachineGun => "기관총",
            _ => weaponTreeType.ToString()
        };
    }

    private string GetDepthText(ExpeditionDepth depth)
    {
        return CampaignProgressionCatalog.GetRegionDisplayName(depth);
    }

    private void BuildCounterPool()
    {
        if (scrapResourceIcon == null && committedScrapCounter != null)
        {
            scrapResourceIcon = committedScrapCounter.IconSprite;
        }

        if (coreResourceIcon == null && committedCoreCounter != null)
        {
            coreResourceIcon = committedCoreCounter.IconSprite;
        }

        AddCounterToPool(committedScrapCounter);
        AddCounterToPool(committedCoreCounter);
        AddCounterToPool(lostScrapCounter);
        AddCounterToPool(lostCoreCounter);
        AddCounterToPool(totalScrapCounter);
        AddCounterToPool(totalCoreCounter);

    }

    private void AddCounterToPool(ResourceCounterUI counter)
    {
        if (counter == null || counterPool.Contains(counter))
        {
            return;
        }

        counter.SetHideWhenZero(true);
        counter.ConfigureCompactPresentation(6f, 12f);
        counterPool.Add(counter);
    }

    private void RefreshSettlementResourceRows(RunResultData resultData)
    {
        BuildCounterPool();

        for (int i = 0; i < counterPool.Count; i++)
        {
            counterPool[i].SetExternalVisible(false);
        }

        List<RunSettlementResourceResult> resources = resultData.settledResources;

        if (resources == null || resources.Count == 0)
        {
            resources = BuildLegacyResourceSnapshot(resultData);
        }

        int committedCount = CountResourceRows(resources, ResourceRowKind.Committed);
        int lostCount = CountResourceRows(resources, ResourceRowKind.Lost);
        EnsureCounterPoolCapacity(committedCount + lostCount);

        int rowIndex = 0;
        float rowStep = GetResourceRowStep(committedCount + lostCount, lostCount > 0);
        float y = 28f;

        if (committedHeaderText != null)
        {
            committedHeaderText.gameObject.SetActive(true);
            SetLocalY(committedHeaderText.rectTransform, 47f);
        }

        if (emptyCommittedText != null)
        {
            emptyCommittedText.gameObject.SetActive(committedCount == 0);
            SetLocalY(emptyCommittedText.rectTransform, 27f);
        }

        AppendResourceGroup(resources, ResourceRowKind.Committed, ref rowIndex, ref y, rowStep);

        if (committedCount == 0)
        {
            y = 10f;
        }

        bool showLoss = lostCount > 0;

        if (lostHeaderText != null)
        {
            lostHeaderText.gameObject.SetActive(showLoss);

            if (showLoss)
            {
                y -= 2f;
                SetLocalY(lostHeaderText.rectTransform, y);
                y -= 14f;
            }
        }

        if (showLoss)
        {
            AppendResourceGroup(resources, ResourceRowKind.Lost, ref rowIndex, ref y, rowStep);
        }
    }

    private void EnsureCounterPoolCapacity(int requiredCount)
    {
        ResourceCounterUI template = committedScrapCounter != null
            ? committedScrapCounter
            : counterPool.Count > 0 ? counterPool[0] : null;

        while (template != null && counterPool.Count < requiredCount)
        {
            ResourceCounterUI clone = Instantiate(template, template.transform.parent);
            clone.name = $"SettlementResourceRow_{counterPool.Count}";
            AddCounterToPool(clone);
        }
    }

    private enum ResourceRowKind
    {
        Committed,
        Lost
    }

    private int CountResourceRows(List<RunSettlementResourceResult> resources, ResourceRowKind kind)
    {
        int count = 0;

        for (int i = 0; i < resources.Count; i++)
        {
            int amount = kind == ResourceRowKind.Committed
                ? resources[i].committed
                : resources[i].lost;

            if (amount > 0)
            {
                count++;
            }
        }

        return count;
    }

    private float GetResourceRowStep(int rowCount, bool includesLossHeader)
    {
        int occupiedSlots = rowCount + (includesLossHeader ? 1 : 0);

        if (occupiedSlots <= 6)
        {
            return 14f;
        }

        return Mathf.Max(9.5f, 84f / occupiedSlots);
    }

    private void AppendResourceGroup(
        List<RunSettlementResourceResult> resources,
        ResourceRowKind kind,
        ref int rowIndex,
        ref float y,
        float rowStep)
    {
        for (int i = 0; i < resources.Count && rowIndex < counterPool.Count; i++)
        {
            RunSettlementResourceResult resource = resources[i];
            int amount = kind == ResourceRowKind.Committed ? resource.committed : resource.lost;

            if (amount <= 0)
            {
                continue;
            }

            ResourceCounterUI counter = counterPool[rowIndex++];
            ConfigureResourceRow(counter, resource.currencyType, kind, amount, y);
            y -= rowStep;
        }
    }

    private void ConfigureResourceRow(
        ResourceCounterUI counter,
        CurrencyType currencyType,
        ResourceRowKind kind,
        int amount,
        float y)
    {
        bool isLoss = kind == ResourceRowKind.Lost;
        Color amountColor = isLoss ? LossColor : PositiveColor;
        Color backgroundColor = isLoss
            ? new Color(0.15f, 0.045f, 0.04f, 0.94f)
            : new Color(0.025f, 0.105f, 0.135f, 0.9f);

        counter.SetDisplayName(GetResourceDisplayName(currencyType));
        counter.SetIcon(GetResourceIcon(currencyType));
        counter.SetIconColor(GetResourceColor(currencyType));
        counter.SetTextColors(PrimaryTextColor, amountColor);
        counter.SetAmountFormat(isLoss ? "-{0}" : "+{0}");
        counter.ConfigureResultPresentation(190f, 12f, backgroundColor);
        counter.SetAmount(amount);
        counter.SetExternalVisible(true);

        if (counter.transform is RectTransform rectTransform)
        {
            if (settlementPanel != null && rectTransform.parent != settlementPanel)
            {
                rectTransform.SetParent(settlementPanel, false);
            }

            rectTransform.anchoredPosition = new Vector2(0f, y);
        }
    }

    private List<RunSettlementResourceResult> BuildLegacyResourceSnapshot(RunResultData resultData)
    {
        return new List<RunSettlementResourceResult>
        {
            new RunSettlementResourceResult(
                CurrencyType.ScrapParts,
                resultData.collectedScrapParts,
                resultData.committedScrapParts,
                resultData.lostScrapParts
            ),
            new RunSettlementResourceResult(
                CurrencyType.CoreShards,
                resultData.collectedCoreShards,
                resultData.committedCoreShards,
                resultData.lostCoreShards
            ),
            new RunSettlementResourceResult(
                CurrencyType.StabilizedAlloy,
                resultData.collectedStabilizedAlloy,
                resultData.committedStabilizedAlloy,
                resultData.lostStabilizedAlloy
            )
        };
    }

    private int GetPermanentResourceAmount(CurrencyType currencyType)
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return 0;
        }

        return currencyType switch
        {
            CurrencyType.ScrapParts => progress.ScrapParts,
            CurrencyType.CoreShards => progress.CoreShards,
            CurrencyType.StabilizedAlloy => progress.StabilizedAlloy,
            _ => 0
        };
    }

    private string GetResourceDisplayName(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.ScrapParts => "스크랩 부품",
            CurrencyType.CoreShards => "코어",
            CurrencyType.StabilizedAlloy => "안정화 합금",
            _ => currencyType.ToString()
        };
    }

    private Sprite GetResourceIcon(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.CoreShards => coreResourceIcon,
            _ => scrapResourceIcon
        };
    }

    private Color GetResourceColor(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.CoreShards => new Color(0.35f, 0.95f, 1f),
            CurrencyType.StabilizedAlloy => new Color(1f, 0.82f, 0.35f),
            _ => Color.white
        };
    }

    private bool EnsurePresentationLayout()
    {
        if (presentationLayoutReady)
        {
            return true;
        }

        if (panelRoot == null || !HasUsableSceneOwnership(panelRoot.transform))
        {
            return false;
        }

        RectTransform panelRect = panelRoot.transform as RectTransform;

        if (panelRect == null)
        {
            return false;
        }

        Image backdrop = panelRoot.GetComponent<Image>();

        if (backdrop != null)
        {
            backdrop.color = BackdropColor;
        }

        Sprite panelSprite = null;

        if (detailText != null && detailText.transform.parent != null)
        {
            recordPanel = detailText.transform.parent as RectTransform;
            Image existingPanelImage = recordPanel != null ? recordPanel.GetComponent<Image>() : null;
            panelSprite = existingPanelImage != null ? existingPanelImage.sprite : null;
        }

        resultCard = CreateRuntimePanel(
            "ResultCard",
            panelRect,
            Vector2.zero,
            new Vector2(454f, 250f),
            CardColor,
            panelSprite
        );
        resultCard.SetAsFirstSibling();

        resultAccentRail = CreateAccentRail(
            "ResultAccentRail",
            resultCard,
            new Vector2(0f, 123f),
            new Vector2(442f, 2f)
        );

        settlementPanel = CreateRuntimePanel(
            "SettlementResultPanel",
            panelRect,
            new Vector2(-108f, -5f),
            new Vector2(208f, 122f),
            SectionColor,
            panelSprite
        );
        settlementAccentRail = CreateAccentRail(
            "SettlementAccentRail",
            settlementPanel,
            new Vector2(-102.5f, 0f),
            new Vector2(3f, 112f)
        );

        ConfigureRecordPanel(panelRect, panelSprite);
        ConfigureHeaderText();
        ConfigureSettlementTexts();
        ConfigureContinueButton();
        ReparentResourceRows();

        permanentTotalsText = CreateRuntimeText(
            "PermanentTotalsText",
            panelRect,
            new Vector2(0f, -79f),
            new Vector2(420f, 20f),
            6.5f,
            TextAlignmentOptions.Center,
            SecondaryTextColor
        );
        permanentTotalsText.enableAutoSizing = true;
        permanentTotalsText.fontSizeMin = 5f;
        permanentTotalsText.fontSizeMax = 6.5f;
        permanentTotalsText.textWrappingMode = TextWrappingModes.NoWrap;

        presentationLayoutReady = true;
        return true;
    }

    private void ConfigureRecordPanel(RectTransform panelRect, Sprite panelSprite)
    {
        if (recordPanel == null)
        {
            recordPanel = CreateRuntimePanel(
                "ExpeditionRecordPanel",
                panelRect,
                new Vector2(108f, -5f),
                new Vector2(200f, 122f),
                SectionColor,
                panelSprite
            );
        }
        else
        {
            recordPanel.SetParent(panelRect, false);
            SetCenteredRect(recordPanel, new Vector2(108f, -5f), new Vector2(200f, 122f));

            Image panelImage = recordPanel.GetComponent<Image>();

            if (panelImage != null)
            {
                panelImage.color = SectionColor;
                panelImage.raycastTarget = false;
            }

            DisableDecorativeChildImages(recordPanel, panelImage);
        }

        recordAccentRail = CreateAccentRail(
            "RecordAccentRail",
            recordPanel,
            new Vector2(-98.5f, 0f),
            new Vector2(3f, 112f)
        );

        if (detailText != null)
        {
            RectTransform detailRect = detailText.rectTransform;
            detailRect.anchorMin = Vector2.zero;
            detailRect.anchorMax = Vector2.one;
            detailRect.pivot = new Vector2(0.5f, 0.5f);
            detailRect.offsetMin = new Vector2(11f, 8f);
            detailRect.offsetMax = new Vector2(-8f, -8f);
            detailText.fontSize = 7f;
            detailText.enableAutoSizing = true;
            detailText.fontSizeMin = 5.5f;
            detailText.fontSizeMax = 7f;
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detailText.textWrappingMode = TextWrappingModes.Normal;
            detailText.overflowMode = TextOverflowModes.Ellipsis;
            detailText.lineSpacing = 5f;
            detailText.richText = true;
            detailText.raycastTarget = false;
        }
    }

    private void ConfigureHeaderText()
    {
        ConfigureTextRect(titleText, new Vector2(0f, 112f), new Vector2(220f, 18f), 11f);
        ConfigureTextRect(reasonText, new Vector2(0f, 91f), new Vector2(300f, 23f), 18f);
        ConfigureTextRect(summaryText, new Vector2(0f, 69f), new Vector2(390f, 18f), 7f);

        if (titleText != null)
        {
            titleText.color = SecondaryTextColor;
            titleText.fontStyle = FontStyles.Bold;
            titleText.characterSpacing = 8f;
            DisableDecorativeChildImages(titleText.rectTransform, null);
        }

        if (reasonText != null)
        {
            reasonText.fontStyle = FontStyles.Bold;
            reasonText.characterSpacing = 3f;
            DisableDecorativeChildImages(reasonText.rectTransform, null);
        }

        if (summaryText != null)
        {
            summaryText.color = SecondaryTextColor;
            summaryText.enableAutoSizing = true;
            summaryText.fontSizeMin = 5.5f;
            summaryText.fontSizeMax = 7f;
        }
    }

    private void ConfigureSettlementTexts()
    {
        committedHeaderText = CreateRuntimeText(
            "CommittedHeaderText",
            settlementPanel,
            new Vector2(0f, 47f),
            new Vector2(188f, 13f),
            7.5f,
            TextAlignmentOptions.MidlineLeft,
            PositiveColor
        );
        committedHeaderText.text = "이번 탐사 정산";
        committedHeaderText.fontStyle = FontStyles.Bold;

        lostHeaderText = CreateRuntimeText(
            "LostHeaderText",
            settlementPanel,
            new Vector2(0f, -10f),
            new Vector2(188f, 12f),
            7f,
            TextAlignmentOptions.MidlineLeft,
            LossColor
        );
        lostHeaderText.text = "손실";
        lostHeaderText.fontStyle = FontStyles.Bold;

        emptyCommittedText = CreateRuntimeText(
            "EmptyCommittedText",
            settlementPanel,
            new Vector2(0f, 27f),
            new Vector2(188f, 14f),
            6.5f,
            TextAlignmentOptions.MidlineLeft,
            SecondaryTextColor
        );
        emptyCommittedText.text = "회수된 정착지 자원 없음";
    }

    private void ConfigureContinueButton()
    {
        if (continueButton == null)
        {
            return;
        }

        RectTransform buttonRect = continueButton.transform as RectTransform;

        if (buttonRect != null)
        {
            buttonRect.SetParent(panelRoot.transform, false);
            SetCenteredRect(buttonRect, new Vector2(0f, -108f), new Vector2(96f, 22f));
        }

        Image buttonImage = continueButton.GetComponent<Image>();

        if (buttonImage != null)
        {
            buttonImage.color = new Color(0.045f, 0.19f, 0.24f, 1f);
        }

        ColorBlock colors = continueButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 1f, 1f, 1f);
        colors.selectedColor = new Color(0.72f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.45f, 0.82f, 0.88f, 1f);
        continueButton.colors = colors;

        TextMeshProUGUI buttonLabel = continueButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (buttonLabel != null)
        {
            buttonLabel.text = "계속하기";
            buttonLabel.fontSize = 8f;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMin = 6f;
            buttonLabel.fontSizeMax = 8f;
            buttonLabel.color = PrimaryTextColor;
            buttonLabel.alignment = TextAlignmentOptions.Center;
        }
    }

    private void ReparentResourceRows()
    {
        if (settlementPanel == null)
        {
            return;
        }

        for (int i = 0; i < counterPool.Count; i++)
        {
            RectTransform rowRect = counterPool[i] != null
                ? counterPool[i].transform as RectTransform
                : null;

            if (rowRect != null)
            {
                rowRect.SetParent(settlementPanel, false);
                rowRect.anchoredPosition = Vector2.zero;
            }
        }
    }

    private void RefreshPermanentTotals(RunResultData resultData)
    {
        if (permanentTotalsText == null)
        {
            return;
        }

        List<RunSettlementResourceResult> resources = resultData.settledResources;

        if (resources == null || resources.Count == 0)
        {
            resources = BuildLegacyResourceSnapshot(resultData);
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("<color=#8297A5>정착지 보유량</color>   ");
        bool addedAny = false;

        for (int i = 0; i < resources.Count; i++)
        {
            RunSettlementResourceResult resource = resources[i];
            int total = GetPermanentResourceAmount(resource.currencyType);

            if (total <= 0)
            {
                continue;
            }

            if (addedAny)
            {
                builder.Append("   ·   ");
            }

            builder.Append(GetCompactResourceDisplayName(resource.currencyType));
            builder.Append(' ');
            builder.Append(total);
            addedAny = true;
        }

        if (!addedAny)
        {
            builder.Append("보유 자원 없음");
        }

        permanentTotalsText.text = builder.ToString();
    }

    private string GetCompactResourceDisplayName(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.ScrapParts => "스크랩",
            CurrencyType.CoreShards => "코어",
            CurrencyType.StabilizedAlloy => "안정화 합금",
            _ => GetResourceDisplayName(currencyType)
        };
    }

    private void ApplyReasonVisuals(RunEndReason reason)
    {
        Color accentColor = GetReasonAccentColor(reason);

        if (reasonText != null)
        {
            reasonText.color = accentColor;
        }

        if (resultAccentRail != null)
        {
            resultAccentRail.color = accentColor;
        }

        if (settlementAccentRail != null)
        {
            settlementAccentRail.color = PositiveColor;
        }

        if (recordAccentRail != null)
        {
            recordAccentRail.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.68f);
        }
    }

    private Color GetReasonAccentColor(RunEndReason reason)
    {
        return reason switch
        {
            RunEndReason.SafeReturn => new Color(0.32f, 0.94f, 1f, 1f),
            RunEndReason.EmergencyReturn => new Color(1f, 0.65f, 0.24f, 1f),
            RunEndReason.Death => new Color(1f, 0.3f, 0.25f, 1f),
            RunEndReason.FinalVictory => new Color(0.79f, 0.58f, 1f, 1f),
            RunEndReason.DebugAbort => new Color(0.62f, 0.66f, 0.7f, 1f),
            _ => PositiveColor
        };
    }

    private void PlayShowAnimation()
    {
        showSequence?.Kill();

        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;

        if (reasonText != null)
        {
            reasonText.rectTransform.localScale = new Vector3(0.94f, 0.94f, 1f);
        }

        showSequence = DOTween.Sequence().SetUpdate(true);
        showSequence.Append(canvasGroup.DOFade(1f, 0.24f).SetEase(Ease.OutQuad));

        if (reasonText != null)
        {
            showSequence.Insert(
                0.08f,
                reasonText.rectTransform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutCubic)
            );
        }
    }

    private RectTransform CreateRuntimePanel(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Sprite sprite)
    {
        GameObject panelObject = CreateSceneOwnedRuntimeObject(
            objectName,
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        SetCenteredRect(rectTransform, anchoredPosition, size);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        return rectTransform;
    }

    private Image CreateAccentRail(
        string objectName,
        RectTransform parent,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        RectTransform railRect = CreateRuntimePanel(
            objectName,
            parent,
            anchoredPosition,
            size,
            PositiveColor,
            null
        );
        return railRect.GetComponent<Image>();
    }

    private TextMeshProUGUI CreateRuntimeText(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateSceneOwnedRuntimeObject(
            objectName,
            parent,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        SetCenteredRect(rectTransform, anchoredPosition, size);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();

        if (titleText != null)
        {
            text.font = titleText.font;
        }

        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    private static GameObject CreateSceneOwnedRuntimeObject(
        string objectName,
        Transform parent,
        params Type[] componentTypes)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        Scene targetScene = parent.gameObject.scene;
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            throw new InvalidOperationException(
                $"Cannot create result UI '{objectName}' under '{parent.name}' because its " +
                "scene is invalid or unloaded.");
        }

        GameObject target = new GameObject(objectName, typeof(RectTransform));
        if (target.scene != targetScene)
        {
            SceneManager.MoveGameObjectToScene(target, targetScene);
        }

        target.transform.SetParent(parent, false);
        Type[] requestedTypes = componentTypes ?? Array.Empty<Type>();
        for (int i = 0; i < requestedTypes.Length; i++)
        {
            Type componentType = requestedTypes[i];
            if (componentType == null ||
                componentType == typeof(Transform) ||
                componentType == typeof(RectTransform) ||
                target.GetComponent(componentType) != null)
            {
                continue;
            }

            target.AddComponent(componentType);
        }

        return target;
    }

#if UNITY_EDITOR
    public static GameObject CreateSceneOwnedRuntimeObjectForEditorAndTests(
        string objectName,
        Transform parent,
        params Type[] componentTypes)
    {
        return CreateSceneOwnedRuntimeObject(objectName, parent, componentTypes);
    }

    public static bool HasUsableSceneOwnershipForEditorAndTests(Transform target)
    {
        return HasUsableSceneOwnership(target);
    }

    public static bool ShouldScheduleDeferredInitializationForEditorAndTests(
        bool isPlaying,
        bool isInitialized,
        bool hasUsableSceneOwnership,
        bool isAlreadyScheduled,
        bool isActiveAndEnabled)
    {
        return ShouldScheduleDeferredInitialization(
            isPlaying,
            isInitialized,
            hasUsableSceneOwnership,
            isAlreadyScheduled,
            isActiveAndEnabled);
    }
#endif

    private void ConfigureTextRect(
        TextMeshProUGUI text,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize)
    {
        if (text == null)
        {
            return;
        }

        SetCenteredRect(text.rectTransform, anchoredPosition, size);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void SetCenteredRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
    }

    private void SetLocalY(RectTransform rectTransform, float y)
    {
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, y);
    }

    private void DisableDecorativeChildImages(RectTransform root, Image preservedImage)
    {
        if (root == null)
        {
            return;
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image != null && image != preservedImage && image.transform != root)
            {
                image.enabled = false;
            }
        }
    }

    private void CacheReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (continueButton == null)
        {
            continueButton = GetComponentInChildren<Button>(true);
        }
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private void HidePanelVisualOnly()
    {
        showSequence?.Kill();
        showSequence = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HideImmediate()
    {
        showSequence?.Kill();
        showSequence = null;

        if (reasonText != null)
        {
            reasonText.rectTransform.localScale = Vector3.one;
        }

        SetVisible(false);
    }
}
