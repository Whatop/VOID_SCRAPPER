using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public static partial class EquipmentEconomyDiagnostics
{
    public const string SamplesPath = "Assets/02_Scripts/UI/Editor/Tests/Fixtures/EquipmentEconomyMaps.json";
    [Serializable] public sealed class Samples { public List<MapSample> maps = new List<MapSample>(); }
    [Serializable] public sealed class Estimates { public List<Estimate> rows = new List<Estimate>(); }
    public static void Run()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, "-equipmentEconomyOutput");
        if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("Supply -equipmentEconomyOutput <json path>.");
        Samples samples = JsonUtility.FromJson<Samples>(System.IO.File.ReadAllText(SamplesPath));
        Estimates estimates = Evaluate(samples);
        System.IO.File.WriteAllText(arguments[index + 1], JsonUtility.ToJson(estimates, true));
        Debug.Log("EQUIPMENT_ECONOMY_DIAGNOSTIC: " + samples.maps.Count + " captured maps, " + estimates.rows.Count + " scenarios, 512 deterministic trials each; production reward rolls, pickup bonuses, cargo and return policies.");
    }
    [Serializable]
    public sealed class Estimate
    {
        public int sample, trials, capacity, fittedCount;
        public string frame, profile;
        public bool bossCleared;
        public double theoreticalScrap, theoreticalCore, discoverableScrap, discoverableCore;
        public double safeScrap, safeCore, emergencyScrap, emergencyCore, deathScrap, deathCore, cargoLimited;
    }

    private static readonly MethodInfo Settlement = typeof(RunManager).GetMethod("CreateRunResult", BindingFlags.Instance | BindingFlags.NonPublic);

    // Calls the production settlement policy without completing a live run or changing progression.
    public static RunResultData Settle(RunManager manager, RunContext run, RunEndReason reason) =>
        (RunResultData)Settlement.Invoke(manager, new object[] { reason, run });

    public static Estimate EstimateMap(MapSample map, OperatingFrameType frame, float visited, string profile,
        int trials = 512, bool bossCleared = true)
    {
        Random.State originalRandom = Random.state;
        GameObject fixture = new GameObject("Editor economy diagnostic");
        fixture.SetActive(false); // No singleton Awake, player bootstrap, or active PreviewScene.
        var manager = fixture.AddComponent<RunManager>();
        var bonus = fixture.AddComponent<PlayerRuntimeBonusState>();
        try
        {
            string[] configs = AssetDatabase.FindAssets("t:GameBalanceConfig", new[] { "Assets/02_Scripts" });
            if (configs.Length != 1) throw new InvalidOperationException("Economy diagnostic requires the single authored BalanceConfig.");
            typeof(RunManager).GetField("balanceConfig", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager,
                AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(AssetDatabase.GUIDToAssetPath(configs[0])));
            var ship = AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/01_basic_ship.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
            int initialCargo = 0;
            foreach (TraitLevelEffect level in catalog.FindById("shared_cargo_bay").LevelEffects)
                if (level.Level == 1 && level.EffectType == TraitEffectType.CargoCapacityBonus) initialCargo += Mathf.RoundToInt(level.Value);
            int fittedCount = map.repeat && map.depth == 2 ? 24 : 6 * (map.depth + 1);
            var frameProfile = new OperatingFrameProfile(frame, fittedCount);
            int capacity = ship.CargoCapacity + initialCargo + frameProfile.CargoBonus;
            bonus.AddHarvestYieldPercent(frameProfile.HarvestYieldPercent);
            bonus.AddScrapGainPercent(SeaRegionCatalog.Get((SeaRegionType)map.weather).ScrapGainPercent);
            var definitions = new Dictionary<string, RewardDefinition>();
            var groups = new Dictionary<string, List<Source>>();
            foreach (Source source in map.sources)
            {
                if (!definitions.ContainsKey(source.definition))
                {
                    var definition = AssetDatabase.LoadAssetAtPath<RewardDefinition>(source.definition);
                    if (definition == null) throw new InvalidOperationException("Captured reward no longer resolves: " + source.definition);
                    definitions.Add(source.definition, definition);
                }
                if (!groups.TryGetValue(source.path, out List<Source> group)) groups.Add(source.path, group = new List<Source>());
                group.Add(source);
            }
            var result = new Estimate { sample = map.sample, trials = trials, frame = frame.ToString(), profile = profile,
                capacity = capacity, fittedCount = fittedCount, bossCleared = bossCleared };
            Random.InitState(9023 + map.sample);
            for (int trial = 0; trial < trials; trial++)
            {
                var found = new RunWallet();
                foreach (List<Source> group in groups.Values)
                {
                    float choice = Random.value, cumulative = 0;
                    Source chosen = group[group.Count - 1];
                    foreach (Source source in group)
                    {
                        cumulative += source.probability;
                        if (choice <= cumulative) { chosen = source; break; }
                    }
                    bool discover = Random.value < visited;
                    foreach (CurrencyAmount currency in definitions[chosen.definition].RollCurrencies())
                    {
                        CurrencyType type = currency.CurrencyType;
                        if (type == CurrencyType.StabilizedAlloy && map.depth != 0) continue;
                        int amount = Mathf.Max(0, Mathf.RoundToInt(currency.Amount * (type == CurrencyType.StabilizedAlloy ? 1f : chosen.multiplier)));
                        if (type == CurrencyType.ScrapParts) result.theoreticalScrap += amount;
                        if (type == CurrencyType.CoreShards) result.theoreticalCore += amount;
                        if (!discover || amount == 0) continue;
                        int shards = ShardCount(chosen, type, amount);
                        for (int i = 0; i < shards; i++)
                            found.Add(type, bonus.ApplyCurrencyGain(type, amount / shards + (i < amount % shards ? 1 : 0)));
                    }
                }
                // Normal operation grants Alloy only; its target's existing reward was already counted.
                if (map.depth == 0 && Random.value < visited) found.Add(CurrencyType.StabilizedAlloy, 1);
                int core = bossCleared ? map.bossCore : 0, guaranteed = bossCleared ? map.guaranteedCore : 0;
                result.theoreticalCore += map.bossCore;
                found.Add(CurrencyType.CoreShards, core - guaranteed);
                result.discoverableScrap += found.PendingScrapParts;
                result.discoverableCore += found.PendingCoreShards + guaranteed;
                var run = new RunContext();
                run.SetCargoRule(capacity, map.emergencyRatio, map.scrapWeight, map.coreWeight, map.alloyWeight);
                // Explicit estimate: player preserves Core, then Alloy, then Scrap using existing inventory triage.
                // This is not a pathfinding simulation; the report includes the lost Scrap opportunity cost.
                foreach (CurrencyType type in new[] { CurrencyType.CoreShards, CurrencyType.StabilizedAlloy, CurrencyType.ScrapParts })
                    run.Wallet.Add(type, run.GetAcceptedAmountByCargo(type, found.GetAmount(type)));
                if (run.CalculateCargoLoad(found.PendingScrapParts, found.PendingCoreShards, found.PendingStabilizedAlloy) > capacity)
                    result.cargoLimited++;
                if (guaranteed > 0)
                {
                    run.TryRegisterGuaranteedBossCoreReward(CampaignBossId.SalvageDevourer, guaranteed);
                    run.Wallet.Add(CurrencyType.CoreShards, guaranteed);
                }
                RunResultData safe = Settle(manager, run, RunEndReason.SafeReturn);
                RunResultData emergency = Settle(manager, run, RunEndReason.EmergencyReturn);
                RunResultData death = Settle(manager, run, RunEndReason.Death);
                result.safeScrap += safe.committedScrapParts; result.safeCore += safe.committedCoreShards;
                result.emergencyScrap += emergency.committedScrapParts; result.emergencyCore += emergency.committedCoreShards;
                result.deathScrap += death.committedScrapParts; result.deathCore += death.committedCoreShards;
            }
            foreach (FieldInfo field in typeof(Estimate).GetFields())
                if (field.FieldType == typeof(double)) field.SetValue(result, (double)field.GetValue(result) / trials);
            return result;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
            Random.state = originalRandom;
        }
    }

    public static int ShardCount(Source source, CurrencyType type, int amount)
    {
        if (amount <= 0) return 0;
        int size = type == CurrencyType.ScrapParts ? source.scrapShardSize : type == CurrencyType.CoreShards ? source.coreShardSize : 1;
        return Mathf.Clamp(Mathf.CeilToInt((float)amount / Mathf.Max(1, size)), 1, Mathf.Max(1, source.maximumShards));
    }

    public static Estimates Evaluate(Samples samples, int trials = 512)
    {
        var results = new Estimates();
        foreach (MapSample map in samples.maps)
            foreach (OperatingFrameType frame in Enum.GetValues(typeof(OperatingFrameType)))
                for (int profile = 0; profile < 3; profile++)
                    results.rows.Add(EstimateMap(map, frame, new[] { .4f, .65f, .9f }[profile], new[] { "Quick", "Normal", "Thorough" }[profile], trials));
        foreach (MapSample map in samples.maps)
            results.rows.Add(EstimateMap(map, OperatingFrameType.Standard, .65f, "Normal without boss", trials, false));
        // First Region-2 boss guarantee is not a renewable 2-Core stipend. Same-map sensitivity
        // uses the existing repeat Raider recipe, labelled separately from captured map inventories.
        var repeatBoss = AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(
            "Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region1_Repeat_Raider.asset");
        foreach (MapSample map in samples.maps)
            if (map.depth == 1 && !map.repeat)
            {
                MapSample repeat = JsonUtility.FromJson<MapSample>(JsonUtility.ToJson(map));
                repeat.repeat = true;
                repeat.bossCore = repeatBoss.CoreShardReward;
                repeat.guaranteedCore = 0;
                results.rows.Add(EstimateMap(repeat, OperatingFrameType.Standard, .65f, "Normal repeat estimate", trials));
            }
        return results;
    }
}
