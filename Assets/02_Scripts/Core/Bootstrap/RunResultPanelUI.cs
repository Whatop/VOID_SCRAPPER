using System.Collections;
using System.Text;
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

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            if (detachFromParentBeforeDontDestroy && transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
        }

        CacheReferences();
        HideImmediate();
    }

    private void OnEnable()
    {
        SubscribeRunManager();

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Close);
        }
    }

    private void Update()
    {
        if (subscribedRunManager == null)
        {
            SubscribeRunManager();
        }
    }

    private void OnDisable()
    {
        UnsubscribeRunManager();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Close);
        }
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

        Show(resultData);
        AudioManager.Play(SoundEventIds.ResultRewardTotal);
        showRoutine = null;
    }

    private void Show(RunResultData resultData)
    {
        CacheReferences();

        isClosing = false;

        SetVisible(true);

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        if (titleText != null)
        {
            titleText.text = "탐색 결과";
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

        SetCounter(committedScrapCounter, resultData.committedScrapParts);
        SetCounter(committedCoreCounter, resultData.committedCoreShards);
        SetCounter(lostScrapCounter, resultData.lostScrapParts);
        SetCounter(lostCoreCounter, resultData.lostCoreShards);

        int totalScrap = PermanentProgress.Instance != null ? PermanentProgress.Instance.ScrapParts : 0;
        int totalCore = PermanentProgress.Instance != null ? PermanentProgress.Instance.CoreShards : 0;

        SetCounter(totalScrapCounter, totalScrap);
        SetCounter(totalCoreCounter, totalCore);
    }

    private string BuildSummaryText(RunResultData resultData)
    {
        return resultData.endReason switch
        {
            RunEndReason.SafeReturn => "회수한 영구 자원을 전량 정산했습니다.",
            RunEndReason.EmergencyReturn => "긴급 복귀 비용을 차감한 뒤 정산했습니다.",
            RunEndReason.Death => "기체 파괴 페널티를 적용한 뒤 정산했습니다.",
            RunEndReason.DebugAbort => "디버그 중단으로 정산하지 않았습니다.",
            _ => "탐사 결과를 정산했습니다."
        };
    }

    private string BuildDetailText(RunResultData resultData)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine($"• 최종 해역: {GetDepthText(resultData.finalDepth)}");
        builder.AppendLine($"• 사용 무기: {GetWeaponText(resultData.selectedWeaponTree)}");
        builder.AppendLine($"• 완료한 고가치 목표: {resultData.objectiveSignalCount}");
        builder.AppendLine($"• 미사용 튜닝 칩: {resultData.unusedTuningChips}");
        builder.AppendLine($"• 소멸 크레딧: {resultData.remainingCredits}");

        switch (resultData.endReason)
        {
            case RunEndReason.SafeReturn:
                builder.AppendLine("• 안전 귀환으로 스크랩과 코어 조각을 모두 보존했습니다.");
                break;

            case RunEndReason.EmergencyReturn:
                builder.AppendLine("• 긴급 복귀로 일부 자원을 잃고 나머지를 회수했습니다.");
                break;

            case RunEndReason.Death:
                builder.AppendLine("• 기체 파괴로 코어 조각은 손실되고 스크랩 일부만 회수했습니다.");
                break;

            case RunEndReason.DebugAbort:
                builder.AppendLine("• 디버그 중단 결과는 영구 성장에 반영하지 않았습니다.");
                break;
        }

        return builder.ToString();
    }

    private string GetReasonText(RunEndReason reason)
    {
        return reason switch
        {
            RunEndReason.SafeReturn => "안전 귀환",
            RunEndReason.EmergencyReturn => "긴급 복귀",
            RunEndReason.Death => "기체 파괴",
            RunEndReason.DebugAbort => "디버그 중단",
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
        return depth switch
        {
            ExpeditionDepth.Normal => "일반 해역",
            ExpeditionDepth.DeepZone1 => "심부 해역 1단계",
            _ => depth.ToString()
        };
    }

    private void SetCounter(ResourceCounterUI counter, int amount)
    {
        if (counter != null)
        {
            counter.SetAmount(amount);
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
        SetVisible(false);
    }
}