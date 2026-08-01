using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudioLoopController : MonoBehaviour
{
    public static GameAudioLoopController Instance { get; private set; }

    private const string AmbienceChannel = "ambience";
    private const string MusicChannel = "music";
    private const string ShopMusicChannel = "music_shop";

    [Header("Volume")]
    [SerializeField] private float ambienceVolumeScale = 1f;
    [SerializeField] private float musicVolumeScale = 1f;
    [SerializeField] private float shopMusicVolumeScale = 1f;

    [Header("Shop Crossfade")]
    [Min(0f)]
    [SerializeField] private float shopMusicCrossfadeDuration = 0.65f;
    [SerializeField] private bool useUnscaledTimeForCrossfade = true;

    private GameState currentState = GameState.Boot;
    private bool hasResolvedState;
    private int shopModeDepth;
    private bool environmentPausedForMenu;

    private Coroutine shopBlendRoutine;
    private float shopBlend;
    private bool shopLoopPrepared;

    public bool IsShopModeActive => shopModeDepth > 0;
    public float ShopBlend => shopBlend;

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
        GameAudioLoopController existing = FindFirstObjectByType<GameAudioLoopController>();

        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject root = new GameObject("GameAudioLoopController");
        DontDestroyOnLoad(root);
        Instance = root.AddComponent<GameAudioLoopController>();
    }

    public static void EnterShopMode()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller == null)
        {
            return;
        }

        controller.shopModeDepth++;
        controller.ApplyCurrentContext();
    }

    public static void ExitShopMode()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller == null)
        {
            return;
        }

        controller.shopModeDepth = Mathf.Max(0, controller.shopModeDepth - 1);
        controller.ApplyCurrentContext();
    }

    public static void PauseEnvironmentForMenu()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller != null)
        {
            controller.environmentPausedForMenu = true;
        }

        AudioManager.StopLoop(AmbienceChannel);
    }

    public static void ResumeForCurrentState()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller == null)
        {
            return;
        }

        controller.environmentPausedForMenu = false;

        if (GameStateManager.Instance != null)
        {
            controller.ApplyState(GameStateManager.Instance.CurrentState);
            return;
        }

        controller.ApplyBySceneName(SceneManager.GetActiveScene().name);
    }

    private static GameAudioLoopController ResolveInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindFirstObjectByType<GameAudioLoopController>();

        if (Instance != null)
        {
            return Instance;
        }

        GameObject root = new GameObject("GameAudioLoopController");
        Instance = root.AddComponent<GameAudioLoopController>();
        return Instance;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged -= HandleStateChanged;
            GameStateManager.Instance.StateChanged += HandleStateChanged;
            ApplyState(GameStateManager.Instance.CurrentState);
        }
        else
        {
            ApplyBySceneName(SceneManager.GetActiveScene().name);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged -= HandleStateChanged;
        }

        StopShopBlendRoutine();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        shopModeDepth = 0;
        environmentPausedForMenu = false;
        shopBlend = 0f;
        shopLoopPrepared = false;
        StopShopBlendRoutine();
        AudioManager.StopLoop(ShopMusicChannel);

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
        if (!SupportsShopMode(next))
        {
            shopModeDepth = 0;
        }

        ApplyState(next);
    }

    private void ApplyState(GameState state)
    {
        currentState = state;
        hasResolvedState = true;
        ApplyCurrentContext();
    }

    private void ApplyCurrentContext()
    {
        if (environmentPausedForMenu)
        {
            AudioManager.StopLoop(AmbienceChannel);
            return;
        }

        bool wantsShopAudio = shopModeDepth > 0 && SupportsShopMode(currentState);

        if (wantsShopAudio)
        {
            PrepareBaseAudio(shopBlend);
            PrepareShopLoop();
            StartShopBlend(1f);
            return;
        }

        if (shopLoopPrepared || shopBlend > 0.001f)
        {
            PrepareBaseAudio(shopBlend);
            StartShopBlend(0f);
            return;
        }

        ApplyBaseAudioImmediate();
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
            currentState = GameState.Settlement;
            hasResolvedState = true;
            ApplyCurrentContext();
        }
        else if (lower.Contains("expedition") || lower.Contains("game"))
        {
            currentState = GameState.Expedition;
            hasResolvedState = true;
            ApplyCurrentContext();
        }
        else if (!hasResolvedState)
        {
            currentState = GameState.Boot;
            hasResolvedState = true;
            ApplyCurrentContext();
        }
    }

    private void ApplyBaseAudioImmediate()
    {
        StopShopBlendRoutine();
        shopBlend = 0f;
        AudioManager.StopLoop(ShopMusicChannel);
        shopLoopPrepared = false;
        PrepareBaseAudio(0f);
        ApplyLoopBlend(0f);
    }

    private void PrepareBaseAudio(float currentShopBlend)
    {
        float baseVolume = Mathf.Clamp01(1f - currentShopBlend);

        switch (currentState)
        {
            case GameState.Settlement:
                StopAmbience();
                PlayMusic(SoundEventIds.MusicSettlementLoop, musicVolumeScale);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

            case GameState.Expedition:
            case GameState.ReturnChoice:
                PlayAmbience(SoundEventIds.AmbSpaceLoop);
                PlayMusic(SoundEventIds.MusicCombatLoop, musicVolumeScale);
                AudioManager.SetLoopModulation(AmbienceChannel, 1f, baseVolume);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

            case GameState.RunResult:
                StopAmbience();
                PlayMusic(SoundEventIds.MusicCombatLoop, musicVolumeScale);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

            case GameState.BossBattle:
            case GameState.FinalBossBattle:
                PlayAmbience(SoundEventIds.AmbSpaceLoop);
                PlayMusic(SoundEventIds.MusicBossLoop, musicVolumeScale);
                AudioManager.SetLoopModulation(AmbienceChannel, 1f, baseVolume);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

            case GameState.SettlementDefense:
                StopAmbience();
                PlayMusic(SoundEventIds.MusicCombatLoop, musicVolumeScale);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

            case GameState.Boot:
            case GameState.ExpeditionLoading:
            default:
                StopAmbience();
                AudioManager.StopLoop(MusicChannel);
                break;
        }
    }

    private void PrepareShopLoop()
    {
        if (!SupportsShopMode(currentState))
        {
            return;
        }

        AudioManager.PlayLoop(
            SoundEventIds.MusicShopLoop,
            ShopMusicChannel,
            shopMusicVolumeScale
        );

        shopLoopPrepared = true;
        AudioManager.SetLoopModulation(ShopMusicChannel, 1f, shopBlend);
    }

    private void StartShopBlend(float targetBlend)
    {
        targetBlend = Mathf.Clamp01(targetBlend);

        if (Mathf.Approximately(shopBlend, targetBlend))
        {
            FinishShopBlend(targetBlend);
            return;
        }

        StopShopBlendRoutine();

        if (shopMusicCrossfadeDuration <= 0f || !isActiveAndEnabled)
        {
            shopBlend = targetBlend;
            ApplyLoopBlend(shopBlend);
            FinishShopBlend(targetBlend);
            return;
        }

        shopBlendRoutine = StartCoroutine(ShopBlendRoutine(targetBlend));
    }

    private IEnumerator ShopBlendRoutine(float targetBlend)
    {
        float startBlend = shopBlend;
        float duration = Mathf.Max(0.01f, shopMusicCrossfadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTimeForCrossfade
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - (2f * t));
            shopBlend = Mathf.Lerp(startBlend, targetBlend, smooth);
            ApplyLoopBlend(shopBlend);
            yield return null;
        }

        shopBlend = targetBlend;
        ApplyLoopBlend(shopBlend);
        shopBlendRoutine = null;
        FinishShopBlend(targetBlend);
    }

    private void ApplyLoopBlend(float blend)
    {
        blend = Mathf.Clamp01(blend);
        float baseBlend = 1f - blend;

        AudioManager.SetLoopModulation(MusicChannel, 1f, baseBlend);
        AudioManager.SetLoopModulation(AmbienceChannel, 1f, baseBlend);

        if (shopLoopPrepared)
        {
            AudioManager.SetLoopModulation(ShopMusicChannel, 1f, blend);
        }
    }

    private void FinishShopBlend(float targetBlend)
    {
        if (targetBlend >= 0.999f)
        {
            // 중립 상점 구역에서는 우주 환경음을 완전히 끈다.
            AudioManager.StopLoop(AmbienceChannel);
            return;
        }

        AudioManager.StopLoop(ShopMusicChannel);
        shopLoopPrepared = false;
        shopBlend = 0f;
        ApplyLoopBlend(0f);
    }

    private void StopShopBlendRoutine()
    {
        if (shopBlendRoutine == null)
        {
            return;
        }

        StopCoroutine(shopBlendRoutine);
        shopBlendRoutine = null;
    }

    private void PlayAmbience(string eventId)
    {
        AudioManager.PlayLoop(eventId, AmbienceChannel, ambienceVolumeScale);
    }

    private void StopAmbience()
    {
        AudioManager.StopLoop(AmbienceChannel);
    }

    private void PlayMusic(string eventId, float volumeScale)
    {
        AudioManager.PlayLoop(eventId, MusicChannel, volumeScale);
    }

    private static bool SupportsShopMode(GameState state)
    {
        return state == GameState.Expedition || state == GameState.ReturnChoice;
    }
}
