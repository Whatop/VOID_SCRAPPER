using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StatusEffectHUDPresenter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform statusRowRoot;
    [SerializeField] private StatusEffectSlotUI slotPrefab;
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private PlayerReinforcementController reinforcementController;

    [Header("Capacity")]
    [Min(1)]
    [SerializeField] private int prewarmSlotCount = 6;
    [Min(1)]
    [SerializeField] private int maxVisibleStatuses = 8;

    [Header("Refresh")]
    [Min(0.02f)]
    [SerializeField] private float durationRefreshInterval = 0.1f;

    private readonly List<StatusEffectSlotUI> slots = new List<StatusEffectSlotUI>();
    private readonly List<StatusEntry> statusEntries = new List<StatusEntry>();
    private readonly List<TraitDefinition> traitBuffer = new List<TraitDefinition>();

    private PermanentProgress subscribedProgress;
    private PlayerReinforcementController subscribedReinforcementController;
    private float nextDurationRefreshTime;
    private bool externalVisible = true;

    private struct StatusEntry
    {
        public string statusId;
        public string displayName;
        public string description;
        public Sprite icon;
        public Color accentColor;
        public bool negative;
        public bool timed;
        public int priority;
        public int stackCount;
        public float remainingSeconds;
        public float durationSeconds;
    }

    private void Awake()
    {
        ResolveReferences();
        PrewarmSlots();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        PrewarmSlots();
        RefreshStatuses();
    }

    private void Start()
    {
        Unsubscribe();
        ResolveReferences();
        Subscribe();
        RefreshStatuses();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!externalVisible || Time.unscaledTime < nextDurationRefreshTime)
        {
            return;
        }

        nextDurationRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, durationRefreshInterval);
        UpdateTimedSlots();
    }

    public void SetExternalVisible(bool visible)
    {
        externalVisible = visible;

        if (statusRowRoot != null && statusRowRoot.gameObject.activeSelf != visible)
        {
            statusRowRoot.gameObject.SetActive(visible);
        }

        if (visible)
        {
            RefreshStatuses();
        }
    }

    public void RefreshStatuses()
    {
        ResolveReferences();
        statusEntries.Clear();
        AppendPersistentStoryTraits();
        AppendActiveReinforcementStatuses();
        SortEntriesByPriority();

        int visibleCount = Mathf.Min(statusEntries.Count, Mathf.Max(1, maxVisibleStatuses));
        EnsureSlotCount(visibleCount);

        for (int i = 0; i < slots.Count; i++)
        {
            StatusEffectSlotUI slot = slots[i];

            if (slot == null)
            {
                continue;
            }

            if (i >= visibleCount)
            {
                slot.Release();
                continue;
            }

            StatusEntry entry = statusEntries[i];
            slot.Bind(
                entry.statusId,
                entry.displayName,
                entry.description,
                entry.icon,
                entry.accentColor,
                entry.negative,
                entry.stackCount,
                entry.timed,
                entry.remainingSeconds,
                entry.durationSeconds
            );
        }
    }

    private void ResolveReferences()
    {
        statusRowRoot ??= transform as RectTransform;
        reinforcementController ??= FindFirstObjectByType<PlayerReinforcementController>();
    }

    private void Subscribe()
    {
        subscribedProgress = PermanentProgress.Instance;
        if (subscribedProgress != null)
        {
            subscribedProgress.Changed += RefreshStatuses;
        }

        subscribedReinforcementController = reinforcementController;
        if (subscribedReinforcementController != null)
        {
            subscribedReinforcementController.ActiveTimedStatusesChanged += RefreshStatuses;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedProgress != null)
        {
            subscribedProgress.Changed -= RefreshStatuses;
            subscribedProgress = null;
        }

        if (subscribedReinforcementController != null)
        {
            subscribedReinforcementController.ActiveTimedStatusesChanged -= RefreshStatuses;
            subscribedReinforcementController = null;
        }
    }

    private void AppendPersistentStoryTraits()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || traitCatalog == null)
        {
            return;
        }

        traitBuffer.Clear();
        traitCatalog.AppendAllTo(traitBuffer);

        for (int i = 0; i < traitBuffer.Count; i++)
        {
            TraitDefinition trait = traitBuffer[i];

            if (trait == null ||
                !trait.IsPersistentStoryTrait ||
                !progress.HasPersistentStoryTrait(trait) ||
                ContainsStatusId(trait.TraitId))
            {
                continue;
            }

            statusEntries.Add(new StatusEntry
            {
                statusId = trait.TraitId,
                displayName = trait.DisplayName,
                description = trait.Description,
                icon = trait.Icon,
                accentColor = trait.GetRarityColor(),
                negative = trait.IsNegativeStatus,
                timed = false,
                priority = 0,
                stackCount = 1,
                remainingSeconds = 0f,
                durationSeconds = 0f
            });
        }
    }

    private void AppendActiveReinforcementStatuses()
    {
        if (reinforcementController == null)
        {
            return;
        }

        int count = reinforcementController.ActiveTimedStatusCount;

        for (int i = 0; i < count; i++)
        {
            if (!reinforcementController.TryGetActiveTimedStatus(
                    i,
                    out string statusId,
                    out ReinforcementDefinition definition,
                    out float remainingSeconds,
                    out float durationSeconds) ||
                definition == null ||
                ContainsStatusId(statusId))
            {
                continue;
            }

            statusEntries.Add(new StatusEntry
            {
                statusId = statusId,
                displayName = definition.DisplayName,
                description = definition.Description,
                icon = definition.Icon,
                accentColor = definition.GetRarityColor(),
                negative = false,
                timed = true,
                priority = IsDefensiveReinforcement(definition) ? 2 : 3,
                stackCount = 1,
                remainingSeconds = remainingSeconds,
                durationSeconds = durationSeconds
            });
        }
    }

    private void UpdateTimedSlots()
    {
        bool needsRefresh = false;
        int count = Mathf.Min(statusEntries.Count, slots.Count);

        for (int i = 0; i < count; i++)
        {
            StatusEntry entry = statusEntries[i];

            if (!entry.timed)
            {
                continue;
            }

            if (reinforcementController == null ||
                !reinforcementController.TryGetActiveTimedStatus(
                    entry.statusId,
                    out _,
                    out float remainingSeconds,
                    out float durationSeconds))
            {
                needsRefresh = true;
                continue;
            }

            entry.remainingSeconds = remainingSeconds;
            entry.durationSeconds = durationSeconds;
            statusEntries[i] = entry;
            slots[i]?.UpdateDuration(remainingSeconds, durationSeconds);
        }

        if (needsRefresh)
        {
            RefreshStatuses();
        }
    }

    private void PrewarmSlots()
    {
        EnsureSlotCount(Mathf.Min(Mathf.Max(1, prewarmSlotCount), Mathf.Max(1, maxVisibleStatuses)));

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i]?.Release();
        }
    }

    private void EnsureSlotCount(int requiredCount)
    {
        if (slotPrefab == null || statusRowRoot == null)
        {
            return;
        }

        int safeCount = Mathf.Min(Mathf.Max(0, requiredCount), Mathf.Max(1, maxVisibleStatuses));

        while (slots.Count < safeCount)
        {
            StatusEffectSlotUI slot = Instantiate(slotPrefab, statusRowRoot);
            slot.Release();
            slots.Add(slot);
        }
    }

    private bool ContainsStatusId(string statusId)
    {
        if (string.IsNullOrWhiteSpace(statusId))
        {
            return true;
        }

        for (int i = 0; i < statusEntries.Count; i++)
        {
            if (string.Equals(statusEntries[i].statusId, statusId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SortEntriesByPriority()
    {
        for (int i = 1; i < statusEntries.Count; i++)
        {
            StatusEntry entry = statusEntries[i];
            int index = i - 1;

            while (index >= 0 && CompareEntries(entry, statusEntries[index]) < 0)
            {
                statusEntries[index + 1] = statusEntries[index];
                index--;
            }

            statusEntries[index + 1] = entry;
        }
    }

    private static int CompareEntries(StatusEntry left, StatusEntry right)
    {
        int priorityComparison = left.priority.CompareTo(right.priority);
        return priorityComparison != 0
            ? priorityComparison
            : string.Compare(left.statusId, right.statusId, StringComparison.Ordinal);
    }

    private static bool IsDefensiveReinforcement(ReinforcementDefinition definition)
    {
        return definition != null &&
               (definition.HasEffect(ReinforcementEffectType.AddInvincibleTime) ||
                definition.HasEffect(ReinforcementEffectType.TemporaryEnemyRadarJamming));
    }
}
