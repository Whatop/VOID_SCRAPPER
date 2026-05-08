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
    }

    public void LoadSettlement()
    {
        if (isLoading)
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(settlementSceneName, GameState.Settlement));
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
}