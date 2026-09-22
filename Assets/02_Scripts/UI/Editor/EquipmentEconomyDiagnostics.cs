using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only economy measurement. Samples production content, never grants progression.
public static partial class EquipmentEconomyDiagnostics
{
    [Serializable]
    public sealed class Source
    {
        public string path, definition, category;
        public float multiplier = 1f, probability = 1f;
        public int scrapShardSize = 1, coreShardSize = 1, maximumShards = 8;
    }

    [Serializable]
    public sealed class MapSample
    {
        public int sample, depth, weather, capacity, scrapWeight, coreWeight, alloyWeight;
        public bool repeat;
        public float emergencyRatio;
        public int bossCore, guaranteedCore;
        public List<Source> sources = new List<Source>();
        public List<string> excluded = new List<string>();
    }

    public static MapSample Capture(int sample)
    {
        RunContext run = RunManager.Instance.CurrentRun;
        var result = new MapSample { sample = sample, depth = (int)run.ExpeditionDepth, weather = (int)run.SeaRegionType,
            repeat = CampaignProgressionCatalog.ShouldUseRepeatBoss(run.ExpeditionDepth, PermanentProgress.Instance),
            capacity = run.MaxCargoCapacity, scrapWeight = run.ScrapCargoWeight, coreWeight = run.CoreShardCargoWeight,
            alloyWeight = run.StabilizedAlloyCargoWeight, emergencyRatio = run.EmergencyReturnCapacityRatio };
        CoreObject core = UnityEngine.Object.FindFirstObjectByType<CoreObject>();
        if (core != null)
        {
            var definition = (BossCampaignDefinition)typeof(CoreObject).GetMethod("ResolveBossCampaignDefinition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(core, null);
            result.bossCore = CampaignBossRewardService.ResolveCoreShardReward(definition, run.ExpeditionDepth);
            if ((bool)typeof(CoreObject).GetMethod("ShouldDeferSalvageDevourerCoreRewardToBossDeath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(core, null))
                result.guaranteedCore = result.bossCore;
        }
        foreach (RewardDropper drop in UnityEngine.Object.FindObjectsByType<RewardDropper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (drop.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (drop.GetComponentInParent<ShopStructure>() != null) { result.excluded.Add("Hostile-shop-only: " + Path(drop.transform)); continue; }
            var evt = drop.GetComponent<ExpeditionEventObject>();
            if (evt != null)
            {
                var type = Read<ExpeditionEventType>(evt, "eventType");
                if (type == ExpeditionEventType.UnknownDevice)
                {
                    float chance = Read<float>(evt, "unknownSafeChance");
                    Add(result, drop, Read<RewardDefinition>(evt, "unknownSafeReward"), "Event", chance);
                    Add(result, drop, Read<RewardDefinition>(evt, "unknownCombatReward"), "Event", 1f - chance);
                }
                else Add(result, drop, Read<RewardDefinition>(evt, type == ExpeditionEventType.RescueSignal ? "rescueReward" :
                    type == ExpeditionEventType.UnstableReactor ? "reactorReward" : "blackBoxReward"), "Event");
                continue;
            }
            var enemy = drop.GetComponent<EnemyHealth>();
            if (enemy != null && !Read<bool>(enemy, "dropRewardOnDeath"))
            { result.excluded.Add("Suppressed enemy: " + Path(drop.transform)); continue; }
            Add(result, drop, drop.RewardDefinition, drop.GetComponentInParent<FieldBaseController>() != null ? "Field base" :
                enemy != null ? "Enemy" : "Harvest");
        }
        return result;
    }

    private static void Add(MapSample result, RewardDropper drop, RewardDefinition definition, string category, float chance = 1f)
    {
        if (definition == null || !drop.CanSpawnCurrencyPickup)
        { result.excluded.Add("No currency source: " + Path(drop.transform)); return; }
        bool compact = Read<bool>(drop, "compactCurrencyDrops"), perCurrency = Read<bool>(drop, "usePerCurrencyShardSize");
        result.sources.Add(new Source { path = Path(drop.transform), definition = AssetDatabase.GetAssetPath(definition),
            category = category, multiplier = drop.RuntimeCurrencyMultiplier, probability = chance,
            scrapShardSize = compact && perCurrency ? Read<int>(drop, "scrapAmountPerShard") : Read<int>(drop, compact ? "compactAmountPerShard" : "amountPerShard"),
            coreShardSize = compact && perCurrency ? Read<int>(drop, "coreAmountPerShard") : Read<int>(drop, compact ? "compactAmountPerShard" : "amountPerShard"),
            maximumShards = Read<int>(drop, compact ? "compactMaxShardsPerCurrency" : "maxShardsPerCurrency") });
    }

    private static string Path(Transform t) => t.parent != null ? Path(t.parent) + "/" + t.name : t.name;
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
}
