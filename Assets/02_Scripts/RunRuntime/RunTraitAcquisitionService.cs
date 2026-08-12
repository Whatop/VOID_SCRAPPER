using UnityEngine;

/// <summary>
/// 런 중 Trait 획득/강화 경로를 하나로 통합한다.
/// 상점, 필드 픽업, 보상 선택이 동일한 저장소와 적용기를 사용하도록 보장한다.
/// </summary>
public static class RunTraitAcquisitionService
{
    public static bool TryAcquire(
        TraitDefinition trait,
        GameObject playerObject,
        out int previousLevel,
        out int newLevel)
    {
        previousLevel = 0;
        newLevel = 0;

        if (trait == null || playerObject == null)
        {
            return false;
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
}
