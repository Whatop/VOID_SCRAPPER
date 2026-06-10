using System;
using System.Collections.Generic;
using UnityEngine;

public class RunRuntimeTraitStore : MonoBehaviour
{
    public static RunRuntimeTraitStore Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RunRuntimeTraitStore>();

                if (instance == null)
                {
                    GameObject obj = new GameObject(nameof(RunRuntimeTraitStore));
                    instance = obj.AddComponent<RunRuntimeTraitStore>();
                }
            }

            return instance;
        }
    }

    private static RunRuntimeTraitStore instance;

    [Header("Runtime Trait Levels")]
    [SerializeField] private List<TraitLevelState> traitLevels = new List<TraitLevelState>();

    private RunManager subscribedRunManager;

    public IReadOnlyList<TraitLevelState> TraitLevels => traitLevels;

    public event Action Changed;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureRunManagerSubscription();
    }

    private void Update()
    {
        EnsureRunManagerSubscription();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            UnsubscribeRunManager();
            instance = null;
        }
    }

    public int GetLevel(string traitId)
    {
        TraitLevelState state = FindState(traitId);
        return state != null ? Mathf.Max(0, state.level) : 0;
    }

    public bool HasTrait(string traitId)
    {
        return GetLevel(traitId) > 0;
    }

    public bool CanUpgrade(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        int currentLevel = GetLevel(trait.TraitId);
        return currentLevel < trait.MaxLevel;
    }

    public int AddOrUpgrade(TraitDefinition trait)
    {
        if (trait == null)
        {
            return 0;
        }

        string traitId = trait.TraitId;

        if (string.IsNullOrWhiteSpace(traitId))
        {
            return 0;
        }

        int maxLevel = Mathf.Max(1, trait.MaxLevel);
        TraitLevelState state = FindState(traitId);

        if (state == null)
        {
            state = new TraitLevelState(traitId, 1);
            traitLevels.Add(state);
            Changed?.Invoke();
            return 1;
        }

        if (state.level >= maxLevel)
        {
            return state.level;
        }

        state.level = Mathf.Clamp(state.level + 1, 1, maxLevel);
        Changed?.Invoke();
        return state.level;
    }

    public void Clear()
    {
        if (traitLevels.Count <= 0)
        {
            return;
        }

        traitLevels.Clear();
        Changed?.Invoke();
    }

    private TraitLevelState FindState(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        for (int i = 0; i < traitLevels.Count; i++)
        {
            TraitLevelState state = traitLevels[i];

            if (state != null && state.traitId == traitId)
            {
                return state;
            }
        }

        return null;
    }

    private void EnsureRunManagerSubscription()
    {
        if (RunManager.Instance == null)
        {
            return;
        }

        if (subscribedRunManager == RunManager.Instance)
        {
            return;
        }

        UnsubscribeRunManager();

        subscribedRunManager = RunManager.Instance;
        subscribedRunManager.RunStarted += HandleRunStarted;
        subscribedRunManager.RunEnded += HandleRunEnded;
    }

    private void UnsubscribeRunManager()
    {
        if (subscribedRunManager == null)
        {
            return;
        }

        subscribedRunManager.RunStarted -= HandleRunStarted;
        subscribedRunManager.RunEnded -= HandleRunEnded;
        subscribedRunManager = null;
    }

    private void HandleRunStarted(RunContext runContext)
    {
        Clear();
    }

    private void HandleRunEnded(RunResultData resultData)
    {
        Clear();
    }
}