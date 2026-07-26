using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudioLoopController : MonoBehaviour
{
    public static GameAudioLoopController Instance { get; private set; }

    private const string AmbienceChannel = "ambience";
    private const string MusicChannel = "music";

    [SerializeField] private float ambienceVolumeScale = 1f;
    [SerializeField] private float musicVolumeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GameAudioLoopController>() != null)
        {
            Instance = FindFirstObjectByType<GameAudioLoopController>();
            return;
        }

        GameObject root = new GameObject("GameAudioLoopController");
        DontDestroyOnLoad(root);
        Instance = root.AddComponent<GameAudioLoopController>();
    }

    public static void PauseEnvironmentForMenu()
    {
        AudioManager.StopLoop(AmbienceChannel);
    }

    public static void ResumeForCurrentState()
    {
        if (Instance == null)
        {
            Instance = FindFirstObjectByType<GameAudioLoopController>();
        }

        if (Instance == null)
        {
            return;
        }

        if (GameStateManager.Instance != null)
        {
            Instance.ApplyState(GameStateManager.Instance.CurrentState);
            return;
        }

        Instance.ApplyBySceneName(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged += HandleStateChanged;
            ApplyState(GameStateManager.Instance.CurrentState);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged -= HandleStateChanged;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged -= HandleStateChanged;
            GameStateManager.Instance.StateChanged += HandleStateChanged;
            ApplyState(GameStateManager.Instance.CurrentState);
            return;
        }

        ApplyBySceneName(scene.name);
    }

    private void HandleStateChanged(GameState previous, GameState next)
    {
        ApplyState(next);
    }

    private void ApplyState(GameState state)
    {
        switch (state)
        {
            case GameState.Settlement:
                AudioManager.PlayLoop(SoundEventIds.AmbSettlementLoop, AmbienceChannel, ambienceVolumeScale);
                AudioManager.PlayLoop(SoundEventIds.MusicSettlementLoop, MusicChannel, musicVolumeScale);
                break;

            case GameState.Expedition:
            case GameState.ReturnChoice:
            case GameState.RunResult:
                AudioManager.PlayLoop(SoundEventIds.AmbSpaceLoop, AmbienceChannel, ambienceVolumeScale);
                AudioManager.PlayLoop(SoundEventIds.MusicCombatLoop, MusicChannel, musicVolumeScale);
                break;

            case GameState.BossBattle:
                AudioManager.PlayLoop(SoundEventIds.AmbSpaceLoop, AmbienceChannel, ambienceVolumeScale);
                AudioManager.PlayLoop(SoundEventIds.MusicBossLoop, MusicChannel, musicVolumeScale);
                break;

            case GameState.Boot:
            case GameState.ExpeditionLoading:
            default:
                break;
        }
    }

    private void ApplyBySceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        string lower = sceneName.ToLowerInvariant();

        if (lower.Contains("settlement"))
        {
            AudioManager.PlayLoop(SoundEventIds.AmbSettlementLoop, AmbienceChannel, ambienceVolumeScale);
            AudioManager.PlayLoop(SoundEventIds.MusicSettlementLoop, MusicChannel, musicVolumeScale);
        }
        else if (lower.Contains("expedition") || lower.Contains("game"))
        {
            AudioManager.PlayLoop(SoundEventIds.AmbSpaceLoop, AmbienceChannel, ambienceVolumeScale);
            AudioManager.PlayLoop(SoundEventIds.MusicCombatLoop, MusicChannel, musicVolumeScale);
        }
    }
}
