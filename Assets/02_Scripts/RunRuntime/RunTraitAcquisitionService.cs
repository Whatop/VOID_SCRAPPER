using UnityEngine;

/// <summary>
/// 런 중 Trait 획득/강화 경로를 하나로 통합한다.
/// 상점, 필드 픽업, 보상 선택이 동일한 저장소와 적용기를 사용하도록 보장한다.
/// </summary>
public static class RunTraitAcquisitionService
{
    public static bool MeetsOfferPrerequisites(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        var prerequisites = trait.Prerequisites;

        if (prerequisites == null)
        {
            return true;
        }

        for (int i = 0; i < prerequisites.Count; i++)
        {
            TraitPrerequisite prerequisite = prerequisites[i];

            if (prerequisite == null || prerequisite.Trait == null)
            {
                return false;
            }

            if (ResolveActiveLevel(prerequisite.Trait) < prerequisite.RequiredLevel)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryAcquire(
        TraitDefinition trait,
        GameObject playerObject,
        out int previousLevel,
        out int newLevel)
    {
        return TryAcquireInternal(
            trait,
            playerObject,
            false,
            out previousLevel,
            out newLevel
        );
    }

    public static bool TryAcquireForDebug(
        TraitDefinition trait,
        GameObject playerObject,
        out int previousLevel,
        out int newLevel)
    {
        return TryAcquireInternal(
            trait,
            playerObject,
            true,
            out previousLevel,
            out newLevel
        );
    }

    private static bool TryAcquireInternal(
        TraitDefinition trait,
        GameObject playerObject,
        bool bypassOfferPrerequisites,
        out int previousLevel,
        out int newLevel)
    {
        previousLevel = 0;
        newLevel = 0;

        if (trait == null || playerObject == null)
        {
            return false;
        }

        if (!bypassOfferPrerequisites && !MeetsOfferPrerequisites(trait))
        {
            return false;
        }

        if (trait.IsPersistentStoryTrait)
        {
            PermanentProgress progress = PermanentProgress.Instance;
            previousLevel = progress != null && progress.HasPersistentStoryTrait(trait) ? 1 : 0;
            bool acquired = TryAcquirePersistentStoryTrait(trait, playerObject);
            newLevel = progress != null && progress.HasPersistentStoryTrait(trait) ? 1 : 0;
            return acquired;
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        previousLevel = store.GetLevel(trait.TraitId);
        newLevel = store.AddOrUpgrade(trait);

        if (newLevel <= previousLevel)
        {
            return false;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CurrentRun.AddTrait(trait.TraitId);
        }

        PlayerHealth playerHealth = playerObject.GetComponentInParent<PlayerHealth>();
        GameObject playerRoot = playerHealth != null ? playerHealth.gameObject : playerObject;

        RunTraitEffectApplier applier = playerRoot.GetComponentInChildren<RunTraitEffectApplier>(true);

        if (applier == null)
        {
            applier = playerRoot.AddComponent<RunTraitEffectApplier>();
        }

        applier.ApplyTraitLevel(trait, newLevel);
        return true;
    }

    private static int ResolveActiveLevel(TraitDefinition trait)
    {
        if (trait == null)
        {
            return 0;
        }

        int level = RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId);
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return level;
        }

        if (trait.IsPersistentStoryTrait)
        {
            return progress.HasPersistentStoryTrait(trait) ? Mathf.Max(1, level) : level;
        }

        if (progress.IsTraitActive(trait.TraitId))
        {
            level = Mathf.Max(level, progress.GetTraitLevel(trait.TraitId));
        }

        return level;
    }

    public static bool TryAcquirePersistentStoryTrait(
        TraitDefinition trait,
        GameObject playerObject = null)
    {
        if (trait == null || !trait.IsPersistentStoryTrait)
        {
            return false;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            Debug.LogError($"Cannot acquire persistent story Trait '{trait.TraitId}': PermanentProgress is unavailable.");
            return false;
        }

        if (!progress.TryAcquirePersistentStoryTrait(trait))
        {
            return false;
        }

        ApplyAcquiredStoryTraitToPlayer(trait, playerObject);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save(progress);
        }
        else
        {
            Debug.LogWarning($"Persistent story Trait '{trait.TraitId}' was acquired in memory, but SaveManager is unavailable.");
        }

        return true;
    }

    private static void ApplyAcquiredStoryTraitToPlayer(TraitDefinition trait, GameObject playerObject)
    {
        if (trait == null || playerObject == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerObject.GetComponentInParent<PlayerHealth>();
        GameObject playerRoot = playerHealth != null ? playerHealth.gameObject : playerObject;
        RunTraitEffectApplier applier = playerRoot.GetComponentInChildren<RunTraitEffectApplier>(true);

        if (applier == null)
        {
            applier = playerRoot.AddComponent<RunTraitEffectApplier>();
        }

        applier.ApplyTraitLevel(trait, 1);
    }
}
