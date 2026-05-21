using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameCursorType
{
    Default,
    Hover,
    Pressed,
    Disabled,
    Upgrade,
    Select,
    Launch,
    Crosshair
}

[Serializable]
public class MouseCursorPreset
{
    [Header("Identity")]
    public GameCursorType cursorType = GameCursorType.Default;

    [Header("Static Cursor")]
    public Texture2D texture;
    public Vector2 hotspot;
    public CursorMode cursorMode = CursorMode.Auto;

    [Header("Animation - Settlement Only")]
    [Tooltip("정착지에서 사용할 커서 애니메이션 프레임입니다. 비어 있으면 texture를 사용합니다.")]
    public Texture2D[] animationFrames;

    [Min(1f)]
    public float animationFrameRate = 10f;

    public bool loopAnimation = true;

    [Tooltip("켜두면 Settlement 상태에서만 애니메이션 커서를 사용합니다. Expedition에서는 정적 커서로 처리됩니다.")]
    public bool animateOnlyInSettlement = true;

    public bool HasAnimation =>
        animationFrames != null &&
        animationFrames.Length > 0;
}

public class MouseCursorManager : MonoBehaviour
{
    public static MouseCursorManager Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("DontDestroyOnLoad는 루트 오브젝트에서만 동작하므로, 부모가 있으면 자동으로 루트로 분리합니다.")]
    [SerializeField] private bool detachToRootBeforeDontDestroy = true;

    [Header("Scene Defaults")]
    [SerializeField] private bool changeCursorByGameState = true;
    [SerializeField] private GameCursorType defaultCursorType = GameCursorType.Default;
    [SerializeField] private GameCursorType settlementCursorType = GameCursorType.Default;
    [SerializeField] private GameCursorType expeditionCursorType = GameCursorType.Crosshair;
    [SerializeField] private GameCursorType loadingCursorType = GameCursorType.Disabled;

    [Header("Animation Option")]
    [Tooltip("GameStateManager가 없는 테스트 씬에서도 커서 애니메이션을 재생할지 여부입니다.")]
    [SerializeField] private bool allowAnimationWhenGameStateMissing = true;

    [Header("Cursor Presets")]
    [SerializeField] private List<MouseCursorPreset> cursorPresets = new List<MouseCursorPreset>();

    private readonly Dictionary<GameCursorType, MouseCursorPreset> presetMap =
        new Dictionary<GameCursorType, MouseCursorPreset>();

    private GameCursorType currentCursorType;
    private GameCursorType sceneDefaultCursorType;

    private MouseCursorPreset currentPreset;
    private int currentAnimationFrameIndex;
    private float animationTimer;

    private bool lockedByGameState;
    private bool subscribedToGameState;
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        MakePersistentIfNeeded();

        BuildPresetMap();

        sceneDefaultCursorType = defaultCursorType;
        SetCursor(sceneDefaultCursorType, true);

        initialized = true;
    }

    private void OnEnable()
    {
        TrySubscribeGameState();
    }

    private void Start()
    {
        TrySubscribeGameState();

        if (changeCursorByGameState && GameStateManager.Instance != null)
        {
            ApplyCursorForGameState(GameStateManager.Instance.CurrentState);
        }
    }

    private void Update()
    {
        if (changeCursorByGameState && !subscribedToGameState)
        {
            TrySubscribeGameState();

            if (subscribedToGameState && GameStateManager.Instance != null)
            {
                ApplyCursorForGameState(GameStateManager.Instance.CurrentState);
            }
        }

        UpdateAnimatedCursor();
    }

    private void OnDisable()
    {
        UnsubscribeGameState();
    }

    private void OnDestroy()
    {
        UnsubscribeGameState();

        if (Instance == this)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Instance = null;
        }
    }

    private void MakePersistentIfNeeded()
    {
        if (!dontDestroyOnLoad)
        {
            return;
        }

        if (transform.parent != null)
        {
            if (detachToRootBeforeDontDestroy)
            {
                Debug.LogWarning(
                    "MouseCursorManager가 자식 오브젝트에 붙어 있어서 루트로 분리한 뒤 DontDestroyOnLoad를 적용합니다.",
                    this
                );

                transform.SetParent(null);
            }
            else
            {
                Debug.LogWarning(
                    "MouseCursorManager는 루트 오브젝트에 있어야 DontDestroyOnLoad가 정상 동작합니다. 현재는 DontDestroyOnLoad를 적용하지 않습니다.",
                    this
                );

                return;
            }
        }

        DontDestroyOnLoad(gameObject);
    }

    private void BuildPresetMap()
    {
        presetMap.Clear();

        if (cursorPresets == null)
        {
            return;
        }

        foreach (MouseCursorPreset preset in cursorPresets)
        {
            if (preset == null)
            {
                continue;
            }

            presetMap[preset.cursorType] = preset;
        }
    }

    private void TrySubscribeGameState()
    {
        if (!changeCursorByGameState)
        {
            return;
        }

        if (subscribedToGameState)
        {
            return;
        }

        if (GameStateManager.Instance == null)
        {
            return;
        }

        GameStateManager.Instance.StateChanged += HandleGameStateChanged;
        subscribedToGameState = true;
    }

    private void UnsubscribeGameState()
    {
        if (!subscribedToGameState)
        {
            return;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.StateChanged -= HandleGameStateChanged;
        }

        subscribedToGameState = false;
    }

    private void HandleGameStateChanged(GameState previousState, GameState nextState)
    {
        ApplyCursorForGameState(nextState);
    }

    private void ApplyCursorForGameState(GameState state)
    {
        if (state == GameState.ExpeditionLoading)
        {
            lockedByGameState = true;
            SetCursor(loadingCursorType, true);
            return;
        }

        lockedByGameState = false;

        GameCursorType nextSceneDefault = state switch
        {
            GameState.Settlement => settlementCursorType,
            GameState.Expedition => expeditionCursorType,
            GameState.BossBattle => expeditionCursorType,
            _ => defaultCursorType
        };

        SetSceneDefaultCursor(nextSceneDefault, true);
    }

    public void SetSceneDefaultCursor(GameCursorType cursorType, bool applyNow)
    {
        sceneDefaultCursorType = cursorType;

        if (applyNow)
        {
            SetCursor(sceneDefaultCursorType, true);
        }
    }

    public void ResetToSceneDefault()
    {
        SetCursor(sceneDefaultCursorType);
    }

    public void SetCursor(GameCursorType cursorType)
    {
        SetCursor(cursorType, false);
    }

    public void SetCursor(GameCursorType cursorType, bool force)
    {
        if (!force && lockedByGameState && cursorType != loadingCursorType)
        {
            return;
        }

        if (!force && currentCursorType == cursorType)
        {
            ApplyCurrentCursorFrame();
            return;
        }

        currentCursorType = cursorType;
        currentAnimationFrameIndex = 0;
        animationTimer = 0f;

        if (!presetMap.TryGetValue(cursorType, out MouseCursorPreset preset))
        {
            currentPreset = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        currentPreset = preset;
        ApplyCurrentCursorFrame();
    }

    public void RebuildPresetsAndApply()
    {
        BuildPresetMap();
        SetCursor(currentCursorType, true);
    }

    private void UpdateAnimatedCursor()
    {
        if (!initialized)
        {
            return;
        }

        if (currentPreset == null)
        {
            return;
        }

        if (!currentPreset.HasAnimation)
        {
            return;
        }

        if (!CanPlayAnimation(currentPreset))
        {
            return;
        }

        float frameRate = Mathf.Max(1f, currentPreset.animationFrameRate);
        float frameDuration = 1f / frameRate;

        animationTimer += Time.unscaledDeltaTime;

        if (animationTimer < frameDuration)
        {
            return;
        }

        animationTimer -= frameDuration;
        currentAnimationFrameIndex++;

        if (currentAnimationFrameIndex >= currentPreset.animationFrames.Length)
        {
            if (currentPreset.loopAnimation)
            {
                currentAnimationFrameIndex = 0;
            }
            else
            {
                currentAnimationFrameIndex = currentPreset.animationFrames.Length - 1;
            }
        }

        ApplyCurrentCursorFrame();
    }

    private void ApplyCurrentCursorFrame()
    {
        if (currentPreset == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        Texture2D cursorTexture = GetCurrentTexture(currentPreset);

        if (cursorTexture == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        Cursor.SetCursor(
            cursorTexture,
            currentPreset.hotspot,
            currentPreset.cursorMode
        );
    }

    private Texture2D GetCurrentTexture(MouseCursorPreset preset)
    {
        if (preset == null)
        {
            return null;
        }

        if (preset.HasAnimation && CanPlayAnimation(preset))
        {
            int frameIndex = Mathf.Clamp(
                currentAnimationFrameIndex,
                0,
                preset.animationFrames.Length - 1
            );

            Texture2D frameTexture = preset.animationFrames[frameIndex];

            if (frameTexture != null)
            {
                return frameTexture;
            }
        }

        return preset.texture;
    }

    private bool CanPlayAnimation(MouseCursorPreset preset)
    {
        if (preset == null)
        {
            return false;
        }

        if (!preset.HasAnimation)
        {
            return false;
        }

        if (!preset.animateOnlyInSettlement)
        {
            return true;
        }

        if (GameStateManager.Instance == null)
        {
            return allowAnimationWhenGameStateMissing;
        }

        return GameStateManager.Instance.CurrentState == GameState.Settlement;
    }
}