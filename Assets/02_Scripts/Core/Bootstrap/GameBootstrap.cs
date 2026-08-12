using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameBalanceConfig gameBalanceConfig;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private SceneFlowManager sceneFlowManager;
    [SerializeField] private RunManager runManager;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private PermanentProgress permanentProgress;

    [Header("Start Flow")]
    [SerializeField] private bool autoLoadSettlement = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CacheReferences();
        InjectConfig();
        AudioManager.EnsureExists();
    }

    private void Start()
    {
        if (gameStateManager != null)
        {
            gameStateManager.ChangeState(GameState.Boot);
        }

        LoadProgress();

        if (autoLoadSettlement && sceneFlowManager != null)
        {
            sceneFlowManager.LoadSettlement();
        }
    }

    private void CacheReferences()
    {
        if (gameStateManager == null)
        {
            gameStateManager = GetComponentInChildren<GameStateManager>();
        }

        if (sceneFlowManager == null)
        {
            sceneFlowManager = GetComponentInChildren<SceneFlowManager>();
        }

        if (runManager == null)
        {
            runManager = GetComponentInChildren<RunManager>();
        }

        if (saveManager == null)
        {
            saveManager = GetComponentInChildren<SaveManager>();
        }

        if (permanentProgress == null)
        {
            permanentProgress = GetComponentInChildren<PermanentProgress>();
        }

        if (gameStateManager == null)
        {
            Debug.LogError("GameStateManager를 찾지 못했습니다.", this);
        }

        if (sceneFlowManager == null)
        {
            Debug.LogError("SceneFlowManager를 찾지 못했습니다.", this);
        }

        if (runManager == null)
        {
            Debug.LogError("RunManager를 찾지 못했습니다.", this);
        }

        if (saveManager == null)
        {
            Debug.LogError("SaveManager를 찾지 못했습니다.", this);
        }

        if (permanentProgress == null)
        {
            Debug.LogError("PermanentProgress를 찾지 못했습니다.", this);
        }
    }

    private void InjectConfig()
    {
        if (runManager != null)
        {
            runManager.SetBalanceConfig(gameBalanceConfig);
        }
    }

    private void LoadProgress()
    {
        if (saveManager == null || permanentProgress == null)
        {
            return;
        }

        SaveData saveData = saveManager.LoadOrCreate();
        permanentProgress.LoadFromSave(saveData);
    }
}