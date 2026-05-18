using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string bootSceneName = "Boot";
    [SerializeField] private string settlementSceneName = "Settlement";
    [SerializeField] private string expeditionSceneName = "Expedition";

    [Header("Loading")]
    [SerializeField] private bool logSceneLoading = true;

    [Header("Fade Optional")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float defaultFadeInDuration = 0.45f;
    [SerializeField] private float defaultFadeOutDuration = 0.45f;

    private bool isLoading;

    public string BootSceneName => bootSceneName;
    public string SettlementSceneName => settlementSceneName;
    public string ExpeditionSceneName => expeditionSceneName;
    public bool IsLoading => isLoading;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("SceneFlowManager가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (screenFader == null)
        {
            screenFader = ScreenFader.Instance;
        }
    }

    public void LoadSettlement()
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(settlementSceneName, GameState.Settlement));
    }

    public void LoadSettlementWithFade()
    {
        LoadSettlementWithFade(defaultFadeInDuration, defaultFadeOutDuration);
    }

    public void LoadSettlementWithFade(float fadeInDuration, float fadeOutDuration)
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(LoadSceneWithFadeRoutine(
            settlementSceneName,
            GameState.Settlement,
            fadeInDuration,
            fadeOutDuration
        ));
    }

    public void LoadExpedition()
    {
        if (isLoading)
        {
            return;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.ExpeditionLoading);
        }

        StartCoroutine(LoadSceneRoutine(expeditionSceneName, GameState.Expedition));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, GameState stateAfterLoad)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("로드할 씬 이름이 비어 있습니다.", this);
            yield break;
        }

        isLoading = true;

        if (logSceneLoading)
        {
            Debug.Log($"Scene Load Start: {sceneName}");
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        isLoading = false;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(stateAfterLoad);
        }

        if (logSceneLoading)
        {
            Debug.Log($"Scene Load Complete: {sceneName}");
        }
    }

    private IEnumerator LoadSceneWithFadeRoutine(
        string sceneName,
        GameState stateAfterLoad,
        float fadeInDuration,
        float fadeOutDuration)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("로드할 씬 이름이 비어 있습니다.", this);
            yield break;
        }

        isLoading = true;

        if (screenFader == null)
        {
            screenFader = ScreenFader.Instance;
        }

        if (screenFader != null)
        {
            yield return screenFader.FadeIn(fadeInDuration);
        }

        if (logSceneLoading)
        {
            Debug.Log($"Scene Load Start: {sceneName}");
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(stateAfterLoad);
        }

        if (logSceneLoading)
        {
            Debug.Log($"Scene Load Complete: {sceneName}");
        }

        yield return null;

        if (screenFader != null)
        {
            yield return screenFader.FadeOut(fadeOutDuration);
        }

        isLoading = false;
    }
}