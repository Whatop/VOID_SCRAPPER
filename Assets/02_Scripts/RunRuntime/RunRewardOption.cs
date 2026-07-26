using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunRewardOptionType
{
    Trait,
    Reinforcement
}

public enum RunRewardRarity
{
    Common,
    Rare,
    Special
}

public enum RunRewardSource
{
    LegacyLevel,
    SpecialContainer,
    Boss,
    FieldTechnician,
    BlackMarket,
    Debug
}

public enum SpecialRewardMode
{
    TraitOnly,
    ReinforcementOnly,
    Mixed
}

[Serializable]
public sealed class RunRewardOption
{
    [SerializeField] private RunRewardOptionType optionType;
    [SerializeField] private RunRewardRarity rarity;
    [SerializeField] private TraitDefinition trait;
    [SerializeField] private ReinforcementDefinition reinforcement;

    public RunRewardOptionType OptionType => optionType;
    public RunRewardRarity Rarity => rarity;
    public TraitDefinition Trait => trait;
    public ReinforcementDefinition Reinforcement => reinforcement;

    public string Id => optionType == RunRewardOptionType.Trait
        ? (trait != null ? trait.TraitId : string.Empty)
        : (reinforcement != null ? reinforcement.EquipmentId : string.Empty);

    public string DisplayName => optionType == RunRewardOptionType.Trait
        ? (trait != null ? trait.DisplayName : "특성 없음")
        : (reinforcement != null ? reinforcement.DisplayName : "장비 없음");

    public string Description => optionType == RunRewardOptionType.Trait
        ? (trait != null ? trait.Description : string.Empty)
        : (reinforcement != null ? reinforcement.Description : string.Empty);

    public Sprite Icon => optionType == RunRewardOptionType.Trait
        ? (trait != null ? trait.Icon : null)
        : (reinforcement != null ? reinforcement.Icon : null);

    public string CategoryText => optionType == RunRewardOptionType.Trait
        ? (trait != null ? trait.GetCategoryText() : "특성")
        : (reinforcement != null ? reinforcement.GetUseTypeText() : "Reinforcement");

    public string RarityText => RunRewardRarityUtility.GetText(rarity);
    public Color RarityColor => RunRewardRarityUtility.GetColor(rarity);

    public static RunRewardOption FromTrait(TraitDefinition definition)
    {
        return new RunRewardOption
        {
            optionType = RunRewardOptionType.Trait,
            rarity = RunRewardRarityUtility.FromTrait(definition),
            trait = definition,
            reinforcement = null
        };
    }

    public static RunRewardOption FromReinforcement(ReinforcementDefinition definition)
    {
        return new RunRewardOption
        {
            optionType = RunRewardOptionType.Reinforcement,
            rarity = RunRewardRarityUtility.FromReinforcement(definition),
            trait = null,
            reinforcement = definition
        };
    }
}

public readonly struct RunRewardChoiceResult
{
    public bool Success { get; }
    public RunRewardOption Option { get; }
    public int TraitLevel { get; }

    public RunRewardChoiceResult(bool success, RunRewardOption option, int traitLevel)
    {
        Success = success;
        Option = option;
        TraitLevel = traitLevel;
    }
}

public static class RunRewardRarityUtility
{
    public static RunRewardRarity FromTrait(TraitDefinition definition)
    {
        if (definition == null)
        {
            return RunRewardRarity.Common;
        }

        return definition.Rarity switch
        {
            TraitRarity.Common => RunRewardRarity.Common,
            TraitRarity.Rare => RunRewardRarity.Rare,
            TraitRarity.Special => RunRewardRarity.Special,
            _ => RunRewardRarity.Common
        };
    }

    public static RunRewardRarity FromReinforcement(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return RunRewardRarity.Common;
        }

        return definition.Rarity switch
        {
            ReinforcementRarity.Common => RunRewardRarity.Common,
            ReinforcementRarity.Rare => RunRewardRarity.Rare,
            ReinforcementRarity.Epic => RunRewardRarity.Special,
            ReinforcementRarity.Legendary => RunRewardRarity.Special,
            _ => RunRewardRarity.Common
        };
    }

    public static string GetText(RunRewardRarity rarity)
    {
        return rarity switch
        {
            RunRewardRarity.Common => "일반",
            RunRewardRarity.Rare => "희귀",
            RunRewardRarity.Special => "특수",
            _ => "일반"
        };
    }

    public static Color GetColor(RunRewardRarity rarity)
    {
        return rarity switch
        {
            RunRewardRarity.Common => Color.white,
            RunRewardRarity.Rare => new Color(0.25f, 0.85f, 1f, 1f),
            RunRewardRarity.Special => new Color(1f, 0.55f, 0.12f, 1f),
            _ => Color.white
        };
    }

    public static bool IsAtLeast(RunRewardRarity value, RunRewardRarity minimum)
    {
        return (int)value >= (int)minimum;
    }
}

public static class RunRewardChoiceGenerator
{
    public static List<RunRewardOption> BuildOptions(
        SpecialRewardMode mode,
        int requestedCount,
        RunRewardRarity minimumRarity,
        TraitCatalog traitCatalog,
        ReinforcementCatalog reinforcementCatalog,
        WeaponTreeType selectedWeaponTree,
        bool forceAtLeastOneRareOrBetter,
        string excludedReinforcementId = null)
    {
        int count = Mathf.Max(1, requestedCount);
        List<TraitDefinition> traitCandidates = BuildTraitCandidates(traitCatalog, selectedWeaponTree);
        List<ReinforcementDefinition> reinforcementCandidates = BuildReinforcementCandidates(
            reinforcementCatalog,
            selectedWeaponTree,
            excludedReinforcementId
        );

        return BuildOptionsFromCandidateLists(
            mode,
            count,
            minimumRarity,
            forceAtLeastOneRareOrBetter,
            traitCandidates,
            reinforcementCandidates
        );
    }

    public static List<RunRewardOption> BuildOptionsFromDefinitions(
        SpecialRewardMode mode,
        int requestedCount,
        RunRewardRarity minimumRarity,
        IReadOnlyList<TraitDefinition> traitDefinitions,
        IReadOnlyList<ReinforcementDefinition> reinforcementDefinitions,
        WeaponTreeType selectedWeaponTree,
        bool forceAtLeastOneRareOrBetter,
        string excludedReinforcementId = null)
    {
        List<TraitDefinition> traitCandidates = new List<TraitDefinition>();
        List<ReinforcementDefinition> reinforcementCandidates = new List<ReinforcementDefinition>();
        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;

        if (traitDefinitions != null)
        {
            for (int i = 0; i < traitDefinitions.Count; i++)
            {
                TraitDefinition trait = traitDefinitions[i];

                if (trait == null ||
                    !trait.CanAppearAsLevelUpTrait ||
                    !trait.IsAvailableFor(selectedWeaponTree) ||
                    (store != null && !store.CanUpgrade(trait)))
                {
                    continue;
                }

                traitCandidates.Add(trait);
            }
        }

        if (reinforcementDefinitions != null)
        {
            for (int i = 0; i < reinforcementDefinitions.Count; i++)
            {
                ReinforcementDefinition definition = reinforcementDefinitions[i];

                if (definition == null ||
                    !definition.CanAppearAsRandomDrop ||
                    !definition.CanUseFor(selectedWeaponTree) ||
                    (!string.IsNullOrWhiteSpace(excludedReinforcementId) && definition.EquipmentId == excludedReinforcementId))
                {
                    continue;
                }

                reinforcementCandidates.Add(definition);
            }
        }

        return BuildOptionsFromCandidateLists(
            mode,
            Mathf.Max(1, requestedCount),
            minimumRarity,
            forceAtLeastOneRareOrBetter,
            traitCandidates,
            reinforcementCandidates
        );
    }

    public static List<RunRewardOption> BuildOwnedTraitUpgradeOptions(
        TraitCatalog traitCatalog,
        int requestedCount)
    {
        List<RunRewardOption> result = new List<RunRewardOption>();

        if (traitCatalog == null || RunRuntimeTraitStore.Instance == null)
        {
            return result;
        }

        List<TraitDefinition> definitions = new List<TraitDefinition>();
        traitCatalog.AppendAllTo(definitions);

        for (int i = 0; i < definitions.Count; i++)
        {
            TraitDefinition trait = definitions[i];

            if (trait == null)
            {
                continue;
            }

            int currentLevel = RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId);

            if (currentLevel <= 0 || currentLevel >= trait.MaxLevel)
            {
                continue;
            }

            result.Add(RunRewardOption.FromTrait(trait));
        }

        Shuffle(result);

        if (result.Count > requestedCount)
        {
            result.RemoveRange(requestedCount, result.Count - requestedCount);
        }

        return result;
    }

    public static ReinforcementDefinition PickSameRarityReplacement(
        ReinforcementCatalog catalog,
        ReinforcementDefinition current,
        WeaponTreeType selectedWeaponTree)
    {
        if (catalog == null || current == null)
        {
            return null;
        }

        List<ReinforcementDefinition> candidates = new List<ReinforcementDefinition>();
        catalog.AppendAllTo(candidates);

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            ReinforcementDefinition candidate = candidates[i];

            if (candidate == null ||
                candidate == current ||
                candidate.Rarity != current.Rarity ||
                !candidate.CanAppearAsRandomDrop ||
                !candidate.CanUseFor(selectedWeaponTree))
            {
                candidates.RemoveAt(i);
            }
        }

        if (candidates.Count <= 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    public static bool ContainsRareOrBetter(IReadOnlyList<RunRewardOption> options)
    {
        if (options == null)
        {
            return false;
        }

        for (int i = 0; i < options.Count; i++)
        {
            RunRewardOption option = options[i];

            if (option != null && RunRewardRarityUtility.IsAtLeast(option.Rarity, RunRewardRarity.Rare))
            {
                return true;
            }
        }

        return false;
    }

    private static List<RunRewardOption> BuildOptionsFromCandidateLists(
        SpecialRewardMode mode,
        int count,
        RunRewardRarity minimumRarity,
        bool forceAtLeastOneRareOrBetter,
        List<TraitDefinition> traitCandidates,
        List<ReinforcementDefinition> reinforcementCandidates)
    {
        List<RunRewardOption> result = new List<RunRewardOption>(count);
        HashSet<string> usedKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < count; i++)
        {
            RunRewardRarity targetRarity = RollRarity(minimumRarity);

            if (forceAtLeastOneRareOrBetter && i == 0 && targetRarity == RunRewardRarity.Common)
            {
                targetRarity = RunRewardRarity.Rare;
            }

            RunRewardOption option = PickOption(
                mode,
                targetRarity,
                minimumRarity,
                traitCandidates,
                reinforcementCandidates,
                usedKeys
            );

            if (option == null)
            {
                break;
            }

            result.Add(option);
            usedKeys.Add(BuildUniqueKey(option));
        }

        return result;
    }

    private static List<TraitDefinition> BuildTraitCandidates(TraitCatalog catalog, WeaponTreeType selectedTree)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (catalog == null)
        {
            return result;
        }

        catalog.AppendAllTo(result);
        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;

        for (int i = result.Count - 1; i >= 0; i--)
        {
            TraitDefinition trait = result[i];

            if (trait == null ||
                !trait.CanAppearAsLevelUpTrait ||
                !trait.IsAvailableFor(selectedTree) ||
                (store != null && !store.CanUpgrade(trait)))
            {
                result.RemoveAt(i);
            }
        }

        return result;
    }

    private static List<ReinforcementDefinition> BuildReinforcementCandidates(
        ReinforcementCatalog catalog,
        WeaponTreeType selectedTree,
        string excludedId)
    {
        List<ReinforcementDefinition> result = new List<ReinforcementDefinition>();

        if (catalog == null)
        {
            return result;
        }

        catalog.AppendAllTo(result);

        for (int i = result.Count - 1; i >= 0; i--)
        {
            ReinforcementDefinition definition = result[i];

            if (definition == null ||
                !definition.CanAppearAsRandomDrop ||
                !definition.CanUseFor(selectedTree) ||
                (!string.IsNullOrWhiteSpace(excludedId) && definition.EquipmentId == excludedId))
            {
                result.RemoveAt(i);
            }
        }

        return result;
    }

    private static RunRewardOption PickOption(
        SpecialRewardMode mode,
        RunRewardRarity targetRarity,
        RunRewardRarity minimumRarity,
        List<TraitDefinition> traitCandidates,
        List<ReinforcementDefinition> reinforcementCandidates,
        HashSet<string> usedKeys)
    {
        bool canUseTrait = mode != SpecialRewardMode.ReinforcementOnly && HasUnusedTrait(traitCandidates, usedKeys);
        bool canUseReinforcement = mode != SpecialRewardMode.TraitOnly && HasUnusedReinforcement(reinforcementCandidates, usedKeys);

        if (!canUseTrait && !canUseReinforcement)
        {
            return null;
        }

        bool preferTrait = mode == SpecialRewardMode.TraitOnly ||
                           (mode == SpecialRewardMode.Mixed && canUseTrait && (!canUseReinforcement || UnityEngine.Random.value < 0.5f));

        RunRewardOption option = preferTrait
            ? PickTrait(targetRarity, minimumRarity, traitCandidates, usedKeys)
            : PickReinforcement(targetRarity, minimumRarity, reinforcementCandidates, usedKeys);

        if (option != null)
        {
            return option;
        }

        return preferTrait
            ? PickReinforcement(targetRarity, minimumRarity, reinforcementCandidates, usedKeys)
            : PickTrait(targetRarity, minimumRarity, traitCandidates, usedKeys);
    }

    private static RunRewardOption PickTrait(
        RunRewardRarity targetRarity,
        RunRewardRarity minimumRarity,
        List<TraitDefinition> candidates,
        HashSet<string> usedKeys)
    {
        TraitDefinition picked = PickCandidate(
            candidates,
            candidate => candidate != null ? RunRewardRarityUtility.FromTrait(candidate) : RunRewardRarity.Common,
            candidate => candidate != null ? $"T:{candidate.TraitId}" : string.Empty,
            targetRarity,
            minimumRarity,
            usedKeys
        );

        return picked != null ? RunRewardOption.FromTrait(picked) : null;
    }

    private static RunRewardOption PickReinforcement(
        RunRewardRarity targetRarity,
        RunRewardRarity minimumRarity,
        List<ReinforcementDefinition> candidates,
        HashSet<string> usedKeys)
    {
        ReinforcementDefinition picked = PickCandidate(
            candidates,
            candidate => candidate != null ? RunRewardRarityUtility.FromReinforcement(candidate) : RunRewardRarity.Common,
            candidate => candidate != null ? $"R:{candidate.EquipmentId}" : string.Empty,
            targetRarity,
            minimumRarity,
            usedKeys
        );

        return picked != null ? RunRewardOption.FromReinforcement(picked) : null;
    }

    private static T PickCandidate<T>(
        List<T> candidates,
        Func<T, RunRewardRarity> raritySelector,
        Func<T, string> keySelector,
        RunRewardRarity targetRarity,
        RunRewardRarity minimumRarity,
        HashSet<string> usedKeys)
        where T : UnityEngine.Object
    {
        List<T> exact = new List<T>();
        List<T> above = new List<T>();
        List<T> fallback = new List<T>();

        for (int i = 0; i < candidates.Count; i++)
        {
            T candidate = candidates[i];

            if (candidate == null)
            {
                continue;
            }

            string key = keySelector(candidate);

            if (usedKeys.Contains(key))
            {
                continue;
            }

            RunRewardRarity rarity = raritySelector(candidate);

            if (!RunRewardRarityUtility.IsAtLeast(rarity, minimumRarity))
            {
                continue;
            }

            fallback.Add(candidate);

            if (rarity == targetRarity)
            {
                exact.Add(candidate);
            }
            else if ((int)rarity > (int)targetRarity)
            {
                above.Add(candidate);
            }
        }

        List<T> source = exact.Count > 0 ? exact : (above.Count > 0 ? above : fallback);

        if (source.Count <= 0)
        {
            return null;
        }

        return source[UnityEngine.Random.Range(0, source.Count)];
    }

    private static RunRewardRarity RollRarity(RunRewardRarity minimumRarity)
    {
        float roll = UnityEngine.Random.value;

        if (minimumRarity == RunRewardRarity.Special)
        {
            return RunRewardRarity.Special;
        }

        if (minimumRarity == RunRewardRarity.Rare)
        {
            return roll < 0.85f ? RunRewardRarity.Rare : RunRewardRarity.Special;
        }

        if (roll < 0.65f)
        {
            return RunRewardRarity.Common;
        }

        return roll < 0.95f ? RunRewardRarity.Rare : RunRewardRarity.Special;
    }

    private static bool HasUnusedTrait(List<TraitDefinition> candidates, HashSet<string> usedKeys)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            TraitDefinition trait = candidates[i];

            if (trait != null && !usedKeys.Contains($"T:{trait.TraitId}"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnusedReinforcement(List<ReinforcementDefinition> candidates, HashSet<string> usedKeys)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            ReinforcementDefinition definition = candidates[i];

            if (definition != null && !usedKeys.Contains($"R:{definition.EquipmentId}"))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildUniqueKey(RunRewardOption option)
    {
        return option.OptionType == RunRewardOptionType.Trait
            ? $"T:{option.Id}"
            : $"R:{option.Id}";
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int index = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[index];
            list[index] = temp;
        }
    }
}

public static class RunRewardChoiceApplier
{
    public static RunRewardChoiceResult Apply(RunRewardOption option, Vector2 sourcePosition)
    {
        if (option == null)
        {
            return new RunRewardChoiceResult(false, option, 0);
        }

        switch (option.OptionType)
        {
            case RunRewardOptionType.Trait:
                return ApplyTrait(option);

            case RunRewardOptionType.Reinforcement:
                return ApplyReinforcement(option, sourcePosition);

            default:
                return new RunRewardChoiceResult(false, option, 0);
        }
    }

    private static RunRewardChoiceResult ApplyTrait(RunRewardOption option)
    {
        TraitDefinition trait = option.Trait;

        if (trait == null)
        {
            return new RunRewardChoiceResult(false, option, 0);
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int previousLevel = store.GetLevel(trait.TraitId);
        int newLevel = store.AddOrUpgrade(trait);

        if (newLevel <= previousLevel)
        {
            return new RunRewardChoiceResult(false, option, previousLevel);
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CurrentRun.AddTrait(trait.TraitId);
        }

        RunTraitEffectApplier applier = UnityEngine.Object.FindFirstObjectByType<RunTraitEffectApplier>();

        if (applier != null)
        {
            applier.ApplyTraitLevel(trait, newLevel);
        }

        AudioManager.Play(SoundEventIds.TraitSelect);
        ShowWarning($"{trait.DisplayName} Lv{newLevel} 적용");
        return new RunRewardChoiceResult(true, option, newLevel);
    }

    private static RunRewardChoiceResult ApplyReinforcement(RunRewardOption option, Vector2 sourcePosition)
    {
        ReinforcementDefinition definition = option.Reinforcement;

        if (definition == null)
        {
            return new RunRewardChoiceResult(false, option, 0);
        }

        PlayerReinforcementController controller = UnityEngine.Object.FindFirstObjectByType<PlayerReinforcementController>();

        if (controller == null)
        {
            return new RunRewardChoiceResult(false, option, 0);
        }

        bool equipped = controller.EquipFromPickup(definition, -1, sourcePosition);

        if (equipped)
        {
            AudioManager.Play(SoundEventIds.ReinforcementPickup);
            ShowWarning($"장비 획득: {definition.DisplayName}");
        }

        return new RunRewardChoiceResult(equipped, option, 0);
    }

    private static void ShowWarning(string message)
    {
        ExpeditionHUD hud = UnityEngine.Object.FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowWarning(message);
        }
    }
}
