using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private RunContext deploymentRun;
    private readonly List<TraitLevelState> nonRefundableLevels = new List<TraitLevelState>();

    public IReadOnlyList<TraitLevelState> TraitLevels => traitLevels;

    public event Action Changed;

    public void InitializeDeployment(RunContext run, TraitCatalog catalog)
    {
        if (run == null || !run.IsActive || ReferenceEquals(deploymentRun, run)) return;
        if (catalog == null && run.PreparedEquipmentIds.Count > 0)
        {
            Debug.LogError("Run equipment initialization requires PermanentProgress.equipmentCatalog. No starting levels were granted.", this);
            return;
        }
        deploymentRun = run;
        foreach (string id in run.PreparedEquipmentIds)
        {
            TraitDefinition trait = catalog != null ? catalog.FindById(id) : null;
            if (trait == null || !trait.CanAppearAsRandomDropTrait || !trait.IsAvailableFor(run.SelectedWeaponTree) ||
                trait.HasRuntimePrerequisites || GetLevel(id) > 0) continue;
            AddOrUpgrade(trait);
            MarkLevelNonRefundable(id, 1);
            run.AddTrait(id);
        }
    }

    public bool IsLevelNonRefundable(string id, int level)
    {
        for (int i = 0; i < nonRefundableLevels.Count; i++)
            if (nonRefundableLevels[i].traitId == id && nonRefundableLevels[i].level == level) return true;
        return false;
    }

    public void MarkLevelNonRefundable(string id, int level)
    {
        if (level > 0 && GetLevel(id) >= level && !IsLevelNonRefundable(id, level))
            nonRefundableLevels.Add(new TraitLevelState(id, level));
    }

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // 모든 Awake가 끝난 뒤 RunManager를 한 번 더 확인한다.
        EnsureRunManagerSubscription();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
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

    public bool TryRemoveLevel(string traitId, out int previousLevel, out int remainingLevel)
    {
        return TryRemoveLevel(traitId, out previousLevel, out remainingLevel, out _);
    }

    public bool TryRemoveLevel(string traitId, out int previousLevel, out int remainingLevel, out bool nonRefundable)
    {
        previousLevel = 0;
        remainingLevel = 0;
        nonRefundable = false;

        TraitLevelState state = FindState(traitId);

        if (state == null || state.level <= 0)
        {
            return false;
        }

        previousLevel = state.level;
        nonRefundable = IsLevelNonRefundable(traitId, previousLevel);
        for (int i = nonRefundableLevels.Count - 1; i >= 0; i--)
            if (nonRefundableLevels[i].traitId == traitId && nonRefundableLevels[i].level == previousLevel)
                nonRefundableLevels.RemoveAt(i);
        state.level = Mathf.Max(0, state.level - 1);
        remainingLevel = state.level;

        if (state.level <= 0)
        {
            traitLevels.Remove(state);
        }

        Changed?.Invoke();
        return true;
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
        deploymentRun = null;
        nonRefundableLevels.Clear();
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

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureRunManagerSubscription();
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
