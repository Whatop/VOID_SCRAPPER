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

    [Header("Frontend Music")]
    [SerializeField, Range(0f, 1f)] private float mainMenuMusicVolumeScale = 0.75f;
    [SerializeField, Range(0f, 1f)] private float tutorialMusicVolumeScale = 0.55f;

    [Header("Tutorial Corruption")]
    [Min(0f)]
    [SerializeField] private float tutorialCorruptionMusicFadeDuration = 0.45f;

    [Header("Shop Crossfade")]
    [Min(0f)]
    [SerializeField] private float shopMusicCrossfadeDuration = 0.65f;
    [SerializeField] private bool useUnscaledTimeForCrossfade = true;

    [Header("Run End Music Transition")]
    [Min(0f)]
    [SerializeField] private float runEndMusicFadeDuration = 0.8f;

    private GameState currentState = GameState.Boot;
    private bool hasResolvedState;
    private int shopModeDepth;
    private bool environmentPausedForMenu;
    private bool bossIntroMusicOverride;

    private Coroutine shopBlendRoutine;
    private Coroutine runEndMusicFadeRoutine;
    private Coroutine tutorialCorruptionMusicFadeRoutine;
    private float shopBlend;
    private bool shopLoopPrepared;
    private bool runEndMusicFadeRequested;
    private bool tutorialCorruptionMusicSilenced;

    public bool IsShopModeActive => shopModeDepth > 0;
    public float ShopBlend => shopBlend;
    public static bool IsRunEndMusicTransitionPending =>
        Instance != null && Instance.runEndMusicFadeRequested;

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

    public static void EnterBossIntroMusic()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller == null)
        {
            return;
        }

        controller.bossIntroMusicOverride = true;
        controller.shopModeDepth = 0;
        controller.ApplyCurrentContext();
    }

    public static void CancelBossIntroMusic()
    {
        GameAudioLoopController controller = ResolveInstance();

        if (controller == null)
        {
            return;
        }

        controller.bossIntroMusicOverride = false;
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

    public static void BeginRunEndMusicTransition()
    {
        GameAudioLoopController controller = ResolveInstance();
        controller?.RequestRunEndMusicFade();
    }

    public static void BeginTutorialCorruptionMusicTransition()
    {
        GameAudioLoopController controller = ResolveInstance();
        controller?.RequestTutorialCorruptionMusicFade();
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
        StopTutorialCorruptionMusicFadeRoutine();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        shopModeDepth = 0;
        environmentPausedForMenu = false;
        bossIntroMusicOverride = false;
        shopBlend = 0f;
        shopLoopPrepared = false;
        tutorialCorruptionMusicSilenced = false;
        StopShopBlendRoutine();
        StopTutorialCorruptionMusicFadeRoutine();
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
        if (next != GameState.Tutorial)
        {
            tutorialCorruptionMusicSilenced = false;
            StopTutorialCorruptionMusicFadeRoutine();
        }

        if (!SupportsShopMode(next))
        {
            shopModeDepth = 0;
        }

        if (next == GameState.BossBattle || next == GameState.FinalBossBattle)
        {
            // 이제 실제 GameState가 보스 음악을 담당하므로 임시 오버라이드는 해제한다.
            bossIntroMusicOverride = false;
        }
        else if (!SupportsBossIntroOverride(next))
        {
            bossIntroMusicOverride = false;
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
        if (runEndMusicFadeRequested)
        {
            if (currentState == GameState.RunResult)
            {
                AudioManager.StopLoop(AmbienceChannel);
            }

            if (currentState == GameState.RunResult &&
                runEndMusicFadeRoutine == null)
            {
                CompleteRunEndMusicTransition();
            }

            return;
        }

        if (environmentPausedForMenu)
        {
            AudioManager.StopLoop(AmbienceChannel);
            return;
        }

        if (bossIntroMusicOverride && SupportsBossIntroOverride(currentState))
        {
            ApplyBossIntroAudioImmediate();
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

    private void ApplyBossIntroAudioImmediate()
    {
        StopShopBlendRoutine();
        shopBlend = 0f;
        shopLoopPrepared = false;
        AudioManager.StopLoop(ShopMusicChannel);

        PlayAmbience(SoundEventIds.AmbSpaceLoop);
        PlayMusic(SoundEventIds.MusicBossLoop, musicVolumeScale);
        AudioManager.SetLoopModulation(AmbienceChannel, 1f, 1f);
        AudioManager.SetLoopModulation(MusicChannel, 1f, 1f);
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

            case GameState.Tutorial:
                PlayAmbience(SoundEventIds.AmbTutorialLoop);
                AudioManager.SetLoopModulation(AmbienceChannel, 1f, baseVolume);

                if (tutorialCorruptionMusicSilenced)
                {
                    AudioManager.StopLoop(MusicChannel);
                    break;
                }

                PlayMusic(SoundEventIds.MusicTutorialLoop, tutorialMusicVolumeScale);
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
                AudioManager.StopLoop(MusicChannel);
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
                StopAmbience();
                PlayMusic(SoundEventIds.MusicMainMenuLoop, mainMenuMusicVolumeScale);
                AudioManager.SetLoopModulation(MusicChannel, 1f, baseVolume);
                break;

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

    private void StopTutorialCorruptionMusicFadeRoutine()
    {
        if (tutorialCorruptionMusicFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(tutorialCorruptionMusicFadeRoutine);
        tutorialCorruptionMusicFadeRoutine = null;
    }

    private void RequestRunEndMusicFade()
    {
        if (runEndMusicFadeRequested)
        {
            return;
        }

        runEndMusicFadeRequested = true;
        StopShopBlendRoutine();
        shopModeDepth = 0;
        shopBlend = 0f;
        shopLoopPrepared = false;
        AudioManager.StopLoop(ShopMusicChannel);

        if (runEndMusicFadeDuration <= 0f || !isActiveAndEnabled)
        {
            AudioManager.StopLoop(MusicChannel);
            return;
        }

        runEndMusicFadeRoutine = StartCoroutine(RunEndMusicFadeRoutine());
    }

    private void RequestTutorialCorruptionMusicFade()
    {
        if (tutorialCorruptionMusicSilenced || currentState != GameState.Tutorial)
        {
            return;
        }

        tutorialCorruptionMusicSilenced = true;
        StopTutorialCorruptionMusicFadeRoutine();

        if (tutorialCorruptionMusicFadeDuration <= 0f || !isActiveAndEnabled)
        {
            AudioManager.StopLoop(MusicChannel);
            return;
        }

        tutorialCorruptionMusicFadeRoutine = StartCoroutine(TutorialCorruptionMusicFadeRoutine());
    }

    private IEnumerator TutorialCorruptionMusicFadeRoutine()
    {
        float duration = Mathf.Max(0.01f, tutorialCorruptionMusicFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - (2f * t));
            AudioManager.SetLoopModulation(MusicChannel, 1f, 1f - smooth);
            yield return null;
        }

        AudioManager.StopLoop(MusicChannel);
        tutorialCorruptionMusicFadeRoutine = null;
    }

    private IEnumerator RunEndMusicFadeRoutine()
    {
        float duration = Mathf.Max(0.01f, runEndMusicFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - (2f * t));
            AudioManager.SetLoopModulation(MusicChannel, 1f, 1f - smooth);
            yield return null;
        }

        AudioManager.StopLoop(MusicChannel);
        runEndMusicFadeRoutine = null;

        if (currentState == GameState.RunResult)
        {
            CompleteRunEndMusicTransition();
        }
    }

    private void CompleteRunEndMusicTransition()
    {
        runEndMusicFadeRequested = false;
        environmentPausedForMenu = false;
        bossIntroMusicOverride = false;
        ApplyCurrentContext();
    }

    private void PlayAmbience(string eventId)
    {
        if (!AudioManager.PlayLoop(eventId, AmbienceChannel, ambienceVolumeScale))
        {
            AudioManager.StopLoop(AmbienceChannel);
        }
    }

    private void StopAmbience()
    {
        AudioManager.StopLoop(AmbienceChannel);
    }

    private void PlayMusic(string eventId, float volumeScale)
    {
        if (!AudioManager.PlayLoop(eventId, MusicChannel, volumeScale))
        {
            AudioManager.StopLoop(MusicChannel);
        }
    }

    private static bool SupportsShopMode(GameState state)
    {
        return state == GameState.Expedition || state == GameState.ReturnChoice;
    }

    private static bool SupportsBossIntroOverride(GameState state)
    {
        return state == GameState.Expedition || state == GameState.ReturnChoice;
    }
}
