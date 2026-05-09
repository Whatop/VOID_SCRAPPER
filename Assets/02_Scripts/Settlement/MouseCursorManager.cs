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
    public GameCursorType cursorType = GameCursorType.Default;
    public Texture2D texture;
    public Vector2 hotspot;
    public CursorMode cursorMode = CursorMode.Auto;
}

public class MouseCursorManager : MonoBehaviour
{
    public static MouseCursorManager Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Scene Defaults")]
    [SerializeField] private bool changeCursorByGameState = true;
    [SerializeField] private GameCursorType defaultCursorType = GameCursorType.Default;
    [SerializeField] private GameCursorType settlementCursorType = GameCursorType.Default;
    [SerializeField] private GameCursorType expeditionCursorType = GameCursorType.Crosshair;
    [SerializeField] private GameCursorType loadingCursorType = GameCursorType.Disabled;

    [Header("Cursor Presets")]
    [SerializeField] private List<MouseCursorPreset> cursorPresets = new List<MouseCursorPreset>();

    private readonly Dictionary<GameCursorType, MouseCursorPreset> presetMap = new Dictionary<GameCursorType, MouseCursorPreset>();

    private GameCursorType currentCursorType;
    private GameCursorType sceneDefaultCursorType;
    private bool lockedByGameState;
    private bool subscribedToGameState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        BuildPresetMap();

        sceneDefaultCursorType = defaultCursorType;
        SetCursor(sceneDefaultCursorType, true);
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

    private void OnDisable()
    {
        UnsubscribeGameState();
    }

    private void BuildPresetMap()
    {
        presetMap.Clear();

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
            return;
        }

        currentCursorType = cursorType;

        if (!presetMap.TryGetValue(cursorType, out MouseCursorPreset preset))
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        if (preset.texture == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        Cursor.SetCursor(preset.texture, preset.hotspot, preset.cursorMode);
    }
}