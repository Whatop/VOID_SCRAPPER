using System;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-32000)]
public class GameBootstrap : MonoBehaviour
{
    private const string BootSceneName = "Boot";
    private const string BootScenePath = "Assets/01_Scenes/Boot.unity";
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

    public static GameBootstrap Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameBalanceConfig gameBalanceConfig;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private SceneFlowManager sceneFlowManager;
    [SerializeField] private RunManager runManager;
    [SerializeField] private SaveManager saveManager;
    [SerializeField] private PermanentProgress permanentProgress;

    [Header("Start Flow")]
    [SerializeField] private bool autoLoadSettlement;

    public bool IsProgressLoaded { get; private set; }
    public bool HasUsableProgression =>
        IsProgressLoaded && saveManager != null && saveManager.HasUsableProgression;

    public event Action ProgressLoaded;

    private bool runtimeInitialized;
    private bool startFlowExecuted;
    private bool rejectedAsDuplicate;
    private bool configurationErrorReported;
    private bool awaitingSceneLoad;
    private bool persistenceRequestIssued;

    private void Awake()
    {
        TryInitializeRuntime();
    }

    private bool TryInitializeRuntime()
    {
        if (runtimeInitialized)
        {
            return true;
        }

        if (rejectedAsDuplicate || !Application.isPlaying)
        {
            return false;
        }

        Scene owningScene = gameObject.scene;
        bool isPreviewScene = IsPreviewScene(owningScene);
        // Incoming scene objects can receive Awake before scene.isLoaded becomes
        // true. Reject duplicate ownership now, at this script's early execution
        // order, rather than letting the incoming managers initialize before Start.
        if (isPreviewScene || !owningScene.IsValid())
        {
            return false;
        }

        GameBootstrap canonical = ResolveCanonicalBootstrap();
        if (canonical != null && canonical != this)
        {
            rejectedAsDuplicate = true;
            RemoveDialogueManagersCreatedByIncomingBootScene(owningScene);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return false;
        }

        // Initial authority still waits for a fully loaded scene before requesting
        // persistence, initializing services or loading any player progression.
        if (IsTransientLifecycleState(
                Application.isPlaying,
                isPreviewScene,
                owningScene.IsValid(),
                owningScene.isLoaded))
        {
            if (!isPreviewScene && owningScene.IsValid())
            {
                awaitingSceneLoad = true;
                SceneManager.sceneLoaded -= HandleOwningSceneLoaded;
                SceneManager.sceneLoaded += HandleOwningSceneLoaded;
            }
            return false;
        }

        bool isAlreadyPersistent = IsDontDestroyOnLoadScene(owningScene);
        if (!isAlreadyPersistent &&
            (!IsAuthorizedBootScene(owningScene) || transform.parent != null))
        {
            ReportConfigurationError(owningScene);
            enabled = false;
            return false;
        }

        // Editor backup authorization recognizes the configuration, not safe mutation ownership.
        if (!isAlreadyPersistent && IsEditorRestorationScene(owningScene)) return false;
        if (persistenceRequestIssued && !isAlreadyPersistent) return false;

        Instance = this;
        if (ShouldRequestPersistence(
                Application.isPlaying,
                isPreviewScene,
                owningScene.IsValid(),
                owningScene.isLoaded,
                transform.parent == null,
                isAlreadyPersistent,
                persistenceRequestIssued,
                IsEditorRestorationScene(owningScene)))
        {
            // Claim the mutation before invoking Unity: a reentrant callback cannot request it twice.
            persistenceRequestIssued = true;
            DontDestroyOnLoad(gameObject);
        }

        CacheReferences();
        InjectConfig();
        AudioManager.EnsureExists();
        runtimeInitialized = true;
        return true;
    }

    private void HandleOwningSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!awaitingSceneLoad || scene != gameObject.scene) return;
        awaitingSceneLoad = false;
        SceneManager.sceneLoaded -= HandleOwningSceneLoaded;
        TryInitializeRuntime();
    }

    private void ReportConfigurationError(Scene owningScene)
    {
        if (configurationErrorReported)
        {
            return;
        }

        configurationErrorReported = true;
        string parentName = transform.parent != null ? transform.parent.name : "<scene root>";
        Debug.LogError(
            $"[{nameof(GameBootstrap)}] '{name}' is not the authoritative Boot-scene root. " +
            $"Scene='{owningScene.name}', Path='{owningScene.path}', Parent='{parentName}'. " +
            "Keep the complete CoreRoot hierarchy at the root of Assets/01_Scenes/Boot.unity.",
            this);
    }

    private GameBootstrap ResolveCanonicalBootstrap()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameBootstrap[] candidates = FindObjectsByType<GameBootstrap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            GameBootstrap candidate = candidates[i];
            if (candidate != null &&
                candidate != this &&
                IsDontDestroyOnLoadScene(candidate.gameObject.scene))
            {
                return candidate;
            }
        }

        return IsDontDestroyOnLoadScene(gameObject.scene) ? this : null;
    }

    private static bool ShouldRequestPersistence(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool isRoot,
        bool isAlreadyPersistent,
        bool requestAlreadyIssued = false,
        bool isEditorRestorationScene = false)
    {
        return !requestAlreadyIssued && !isEditorRestorationScene && isPlaying &&
               !isPreviewScene &&
               sceneIsValid &&
               sceneIsLoaded &&
               isRoot &&
               !isAlreadyPersistent;
    }

    private static bool IsTransientLifecycleState(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded)
    {
        return !isPlaying || isPreviewScene || !sceneIsValid || !sceneIsLoaded;
    }

    private static bool IsAuthorizedBootScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        if (string.Equals(scene.name, BootSceneName, StringComparison.Ordinal) ||
            string.Equals(
                NormalizeScenePath(scene.path),
                NormalizeScenePath(BootScenePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

#if UNITY_EDITOR
        return IsEditorPlayModeBackupScene(scene);
#else
        return false;
#endif
    }

    private static string NormalizeScenePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

#if UNITY_EDITOR
    private static bool IsEditorPlayModeBackupScene(Scene scene)
    {
        string path = NormalizeScenePath(scene.path);
        return Application.isPlaying &&
               path.StartsWith("Temp/__Backupscenes/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith(".backup", StringComparison.OrdinalIgnoreCase);
    }
#endif

    private static bool IsEditorRestorationScene(Scene scene)
    {
#if UNITY_EDITOR
        return IsEditorPlayModeBackupScene(scene);
#else
        return false;
#endif
    }

    private static bool IsDontDestroyOnLoadScene(Scene scene)
    {
        return scene.IsValid() &&
               string.Equals(scene.name, DontDestroyOnLoadSceneName, StringComparison.Ordinal);
    }

    private static bool IsPreviewScene(Scene scene)
    {
#if UNITY_EDITOR
        return scene.IsValid() &&
               UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(scene);
#else
        return false;
#endif
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleOwningSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private static void RemoveDialogueManagersCreatedByIncomingBootScene(Scene incomingBootScene)
    {
        if (!DialogueManager.hasInstance || !incomingBootScene.IsValid())
        {
            return;
        }

        DialogueSystemController canonicalController = DialogueManager.instance;
        DialogueSystemController[] controllers = FindObjectsByType<DialogueSystemController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueSystemController candidate = controllers[i];
            if (DeactivateIncomingDialogueManager(incomingBootScene, canonicalController, candidate))
            {
                Destroy(candidate.gameObject);
            }
        }
    }

    private static bool DeactivateIncomingDialogueManager(
        Scene incomingBootScene,
        DialogueSystemController canonicalController,
        DialogueSystemController candidate)
    {
        if (!incomingBootScene.IsValid() || canonicalController == null ||
            candidate == null || candidate == canonicalController ||
            candidate.gameObject.scene != incomingBootScene)
        {
            return false;
        }

        // Deactivate the complete incoming root BEFORE deferred destruction. Its
        // localization, bridge, dialogue UI and input components must not initialize.
        candidate.gameObject.SetActive(false);
        return true;
    }

#if UNITY_EDITOR
    public static bool ShouldRequestPersistenceForEditorAndTests(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded,
        bool isRoot,
        bool isAlreadyPersistent,
        bool requestAlreadyIssued = false,
        bool isEditorRestorationScene = false)
    {
        return ShouldRequestPersistence(
            isPlaying,
            isPreviewScene,
            sceneIsValid,
            sceneIsLoaded,
            isRoot,
            isAlreadyPersistent,
            requestAlreadyIssued,
            isEditorRestorationScene);
    }

    public static bool IsTransientLifecycleStateForEditorAndTests(
        bool isPlaying,
        bool isPreviewScene,
        bool sceneIsValid,
        bool sceneIsLoaded)
    {
        return IsTransientLifecycleState(
            isPlaying,
            isPreviewScene,
            sceneIsValid,
            sceneIsLoaded);
    }

    public static bool IsAuthorizedBootSceneForEditorAndTests(Scene scene)
    {
        return IsAuthorizedBootScene(scene);
    }

    public static bool ShouldPruneIncomingDialogueManagerForEditorAndTests(
        bool hasCanonicalManager,
        bool isIncomingBootManager)
    {
        return hasCanonicalManager && isIncomingBootManager;
    }
#endif

    private void Start()
    {
        if (!runtimeInitialized || startFlowExecuted)
        {
            return;
        }

        startFlowExecuted = true;
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
        IsProgressLoaded = true;
        ProgressLoaded?.Invoke();
    }

    public bool TryResetProgress()
    {
        if (saveManager == null || permanentProgress == null)
        {
            Debug.LogError("Cannot start a new game because progression services are unavailable.", this);
            return false;
        }

        if (!saveManager.TryResetToDefault(out SaveData freshSave))
        {
            return false;
        }

        permanentProgress.LoadFromSave(freshSave);
        IsProgressLoaded = true;
        ProgressLoaded?.Invoke();
        return true;
    }
}
