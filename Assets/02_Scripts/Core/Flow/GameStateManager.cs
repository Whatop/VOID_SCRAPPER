using System;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private GameState currentState = GameState.Boot;

    public GameState CurrentState => currentState;

    public event Action<GameState, GameState> StateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameStateManager가 중복으로 존재합니다. 중복 인스턴스를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public void ChangeState(GameState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        GameState previousState = currentState;
        currentState = nextState;

        Debug.Log($"GameState: {previousState} -> {nextState}");
        StateChanged?.Invoke(previousState, nextState);
    }

    public bool IsGameplayState()
    {
        return currentState == GameState.Expedition ||
               currentState == GameState.BossBattle ||
               currentState == GameState.Tutorial;
    }

    public bool IsInteractionLocked()
    {
        return currentState == GameState.ExpeditionLoading ||
               currentState == GameState.BossBattle ||
               currentState == GameState.ReturnChoice ||
               currentState == GameState.RunResult;
    }
}