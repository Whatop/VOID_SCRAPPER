using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class GameplayPauseManager : MonoBehaviour
{
    private struct CancelHandler
    {
        public object owner;
        public Action callback;

        public CancelHandler(object owner, Action callback)
        {
            this.owner = owner;
            this.callback = callback;
        }
    }

    private static GameplayPauseManager instance;

    private readonly Dictionary<object, string> pauseRequests = new Dictionary<object, string>();
    private readonly List<CancelHandler> cancelHandlers = new List<CancelHandler>();

    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool hasStoredTimeState;

    public static GameplayPauseManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GameplayPauseManager>();

                if (instance == null)
                {
                    GameObject obj = new GameObject(nameof(GameplayPauseManager));
                    instance = obj.AddComponent<GameplayPauseManager>();
                }
            }

            return instance;
        }
    }

    public static bool IsPaused => instance != null && instance.pauseRequests.Count > 0;
    public bool Paused => pauseRequests.Count > 0;
    public int PauseRequestCount => pauseRequests.Count;

    public event Action<bool> PauseStateChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            ResetAllPauses();
            instance = null;
        }
    }

    public void PushPause(object owner, string reason = "")
    {
        if (owner == null)
        {
            owner = this;
        }

        bool wasPaused = Paused;

        if (!pauseRequests.ContainsKey(owner))
        {
            pauseRequests.Add(owner, reason);
        }
        else
        {
            pauseRequests[owner] = reason;
        }

        ApplyPauseStateIfChanged(wasPaused);
    }

    public void PopPause(object owner)
    {
        if (owner == null)
        {
            owner = this;
        }

        bool wasPaused = Paused;

        if (pauseRequests.ContainsKey(owner))
        {
            pauseRequests.Remove(owner);
        }

        ApplyPauseStateIfChanged(wasPaused);
    }

    public bool IsPausedBy(object owner)
    {
        if (owner == null)
        {
            return false;
        }

        return pauseRequests.ContainsKey(owner);
    }

    public void ResetAllPauses()
    {
        bool wasPaused = Paused;

        pauseRequests.Clear();
        cancelHandlers.Clear();

        ApplyPauseStateIfChanged(wasPaused);
    }

    public void RegisterCancelHandler(object owner, Action callback)
    {
        if (owner == null || callback == null)
        {
            return;
        }

        UnregisterCancelHandler(owner);
        cancelHandlers.Add(new CancelHandler(owner, callback));
    }

    public void UnregisterCancelHandler(object owner)
    {
        if (owner == null)
        {
            return;
        }

        for (int i = cancelHandlers.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(cancelHandlers[i].owner, owner))
            {
                cancelHandlers.RemoveAt(i);
            }
        }
    }

    public bool TryHandleCancel()
    {
        for (int i = cancelHandlers.Count - 1; i >= 0; i--)
        {
            CancelHandler handler = cancelHandlers[i];

            if (handler.owner == null || handler.callback == null)
            {
                cancelHandlers.RemoveAt(i);
                continue;
            }

            handler.callback.Invoke();
            return true;
        }

        return false;
    }

    private void ApplyPauseStateIfChanged(bool wasPaused)
    {
        bool nowPaused = Paused;

        if (wasPaused == nowPaused)
        {
            return;
        }

        if (nowPaused)
        {
            if (!hasStoredTimeState)
            {
                previousTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
                previousFixedDeltaTime = Time.fixedDeltaTime;
                hasStoredTimeState = true;
            }

            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = Mathf.Max(0.0001f, previousTimeScale);
            Time.fixedDeltaTime = previousFixedDeltaTime;
            hasStoredTimeState = false;
        }

        PauseStateChanged?.Invoke(nowPaused);
    }
}