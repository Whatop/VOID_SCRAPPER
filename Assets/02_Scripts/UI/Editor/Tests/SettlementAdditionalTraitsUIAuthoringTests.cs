using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class SettlementAdditionalTraitsUIAuthoringTests
{
    private Scene scene;
    private ShipTraitTreePanel panel;
    private SettlementUIController ui;
    private PermanentProgress progress, priorProgress;
    private SaveManager priorSave, save;
    private GameStateManager priorState, state;
    private RunRuntimeTraitStore priorStore, store;
    private RunManager priorRun;
    private TraitCatalog catalog;
    private List<ShipDefinition> ships;
    private GameObject services;
    private string savePath;
    private readonly List<Object> transientAssets = new List<Object>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        priorProgress = PermanentProgress.Instance; priorSave = SaveManager.Instance; priorRun = RunManager.Instance;
        priorState = GameStateManager.Instance;
        priorStore = (RunRuntimeTraitStore)typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        scene = AuthoredRuntimeFixture.Open("Settlement");
        AuthoredRuntimeFixture.Group(scene);
        panel = AuthoredRuntimeFixture.Single<ShipTraitTreePanel>(scene);
        ui = AuthoredRuntimeFixture.Single<SettlementUIController>(scene);
        services = AuthoredRuntimeFixture.Create(scene, null, "EquipmentTestServices", false); services.SetActive(false);
        progress = services.AddComponent<PermanentProgress>(); store = services.AddComponent<RunRuntimeTraitStore>();
        save = services.AddComponent<SaveManager>(); state = services.AddComponent<GameStateManager>();
        savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "void-equipment-correction-" + Guid.NewGuid().ToString("N") + ".json");
        Set(save, "fileName", savePath); Set(save, "logSavePath", false); Set(state, "currentState", GameState.Settlement);
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, progress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, save);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, state);
        typeof(RunManager).GetProperty("Instance").SetValue(null, null);
        typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, store);
        catalog = AssetDatabase.LoadAssetAtPath<TraitCatalog>("Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset");
        ships = new[] { "01_basic_ship", "02_shotgun_ship", "03_sniper_ship" }.Select(n =>
            AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/" + n + ".asset")).ToList();
        Set(progress, "equipmentCatalog", catalog); Set(progress, "equipmentShips", ships);
        progress.LoadFromSave(new SaveData());
    }

    [TearDown]
    public void TearDown()
    {
        if (RunManager.Instance != null && RunManager.Instance != priorRun)
            RunManager.Instance.ReleaseRunEndingPresentationOwnership();
        if (panel != null) Call(panel, "OnDisable");
        AuthoredRuntimeFixture.Close(scene);
        foreach (Object asset in transientAssets) Object.DestroyImmediate(asset);
        transientAssets.Clear();
        typeof(PermanentProgress).GetProperty("Instance").SetValue(null, priorProgress);
        typeof(SaveManager).GetProperty("Instance").SetValue(null, priorSave);
        typeof(GameStateManager).GetProperty("Instance").SetValue(null, priorState);
        typeof(RunManager).GetProperty("Instance").SetValue(null, priorRun);
        typeof(RunRuntimeTraitStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, priorStore);
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".corrupt" })
            if (System.IO.File.Exists(savePath + suffix)) System.IO.File.Delete(savePath + suffix);
    }

    private static SaveData Checkpoint(SaveData source, int index) => (SaveData)typeof(DebugItemGrantUI)
        .GetMethod("CreateCampaignCheckpoint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { source, index });

    // Historical ownership fixture only; real manufacture transactions have separate tests below.
    private bool PrepareFixture(TraitDefinition trait)
    {
        SaveData data = progress.CreateSaveData();
        if (!data.manufacturedEquipmentIds.Contains(trait.TraitId)) data.manufacturedEquipmentIds.Add(trait.TraitId);
        if (!data.equipmentLoadoutTraitIds.Contains(trait.TraitId)) data.equipmentLoadoutTraitIds.Add(trait.TraitId);
        progress.LoadFromSave(data);
        return progress.IsEquipmentFitted(trait.TraitId);
    }

    private void ResearchAndResources(int checkpoint = 11)
    {
        SaveData data = Checkpoint(progress.CreateSaveData(), checkpoint);
        data.scrapParts = data.coreShards = data.stabilizedAlloy = 9999;
        progress.LoadFromSave(data);
    }

    private void ManufactureAndFit(TraitDefinition trait)
    {
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.Success), trait.TraitId);
        Assert.That(progress.TrySetEquipmentFitted(trait, true), Is.EqualTo(EquipmentDevelopmentResult.Success), trait.TraitId);
    }

    [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(2, 3)] [TestCase(3, 6)]
    [TestCase(4, 6)] [TestCase(5, 9)] [TestCase(6, 9)] [TestCase(7, 12)]
    [TestCase(8, 12)] [TestCase(9, 12)] [TestCase(10, 12)] [TestCase(11, 12)]
    public void AnalysisOpensBlueprintPositionsInEveryAvailableBranch(int checkpoint, int positions)
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 10));
        SaveData data = Checkpoint(progress.CreateSaveData(), checkpoint);
        data.buildingLevels.Add(new BuildingSaveData(BuildingType.Hangar, 99));
        progress.LoadFromSave(data);
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.Shared), Is.EqualTo(positions));
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.MachineGun), Is.EqualTo(positions));
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.Shotgun), Is.EqualTo(positions >= 6 ? positions : 0));
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.Sniper), Is.EqualTo(positions >= 9 ? positions : 0));
        Assert.That(progress.ManufacturedEquipmentIds, Is.Empty, "F10 research must not manufacture anything.");
        var controller = AuthoredRuntimeFixture.Single<SettlementController>(scene);
        Assert.That(controller.IsShipUnlocked(ships[0]), Is.True);
        Assert.That(controller.IsShipUnlocked(ships[1]), Is.EqualTo(positions >= 6));
        Assert.That(controller.IsShipUnlocked(ships[2]), Is.EqualTo(positions >= 9));
    }

    [Test]
    public void ThirdAnalysisBeforeAssemblyRemainsAnExactOnceTransaction()
    {
        progress.LoadFromSave(Checkpoint(new SaveData(), 6));
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.Shared), Is.EqualTo(9));
        int changes = 0; progress.Changed += () => changes++;
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.True);
        Assert.That(progress.TryCompleteFinalComponentAnalysis(), Is.False);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(progress.GetEquipmentResearchPositionCount(ShipTraitBranchKind.Shared), Is.EqualTo(12));
        Assert.That(progress.CurrentRouteCoreState, Is.EqualTo(RouteCoreState.ReadyToAssemble));
        Assert.That(progress.SettlementDefenseCleared, Is.False);
    }

    [TestCase("mg_guidance_control")] [TestCase("mg_stable_feed")]
    public void SweeperInitialBlueprintIsResearchedButNotManufactured(string id)
    {
        TraitDefinition trait = catalog.FindById(id);
        Assert.That(trait.DevelopmentResearchTier, Is.Zero);
        Assert.That(progress.IsEquipmentResearched(trait), Is.True);
        Assert.That(progress.IsEquipmentManufactured(id), Is.False);
        Assert.That(progress.TrySetEquipmentFitted(trait, true), Is.EqualTo(EquipmentDevelopmentResult.NotManufactured));
        Assert.That(store.GetLevel(id), Is.Zero);
    }

    [Test]
    public void ManufacturingChargesExactlyOnceAndDoesNotFitOrPermanentlyLevel()
    {
        TraitDefinition trait = catalog.FindById("mg_stable_feed");
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.InsufficientResources));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        ResearchAndResources(1);
        int changes = 0; progress.Changed += () => changes++;
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.ScrapParts, Is.EqualTo(9999 - trait.ManufacturingScrapCost));
        Assert.That(progress.CoreShards, Is.EqualTo(9999 - trait.ManufacturingCoreCost));
        Assert.That(progress.ManufacturedEquipmentIds, Is.EqualTo(new[] { trait.TraitId }));
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.Empty);
        Assert.That(progress.GetTraitLevel(trait.TraitId), Is.Zero);
        Assert.That(store.GetLevel(trait.TraitId), Is.Zero);
        before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(trait), Is.EqualTo(EquipmentDevelopmentResult.AlreadyManufactured));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void FailedSaveLeavesManufacturingResourcesAndOwnershipUnchanged()
    {
        ResearchAndResources();
        System.IO.File.WriteAllText(savePath, "blocked parent");
        Set(save, "fileName", System.IO.Path.Combine(savePath, "blocked.json"));
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Save write failed.*"));
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("mg_stable_feed")), Is.EqualTo(EquipmentDevelopmentResult.SaveFailed));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Set(save, "fileName", savePath);
    }

    [Test]
    public void ManufacturingRejectsLockedInvalidRecipeAndUnsafeState()
    {
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("sg_choke_barrel")), Is.EqualTo(EquipmentDevelopmentResult.ResearchLocked));
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("pixel_curse")), Is.EqualTo(EquipmentDevelopmentResult.InvalidDefinition));
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("shared_lightweight_cargo")), Is.EqualTo(EquipmentDevelopmentResult.InvalidDefinition));
        ResearchAndResources();
        Set(state, "currentState", GameState.Expedition);
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("mg_stable_feed")), Is.EqualTo(EquipmentDevelopmentResult.UnsafeState));
    }

    [Test]
    public void FittingIsFreeAndShipSwitchPreservesPreferencesAndSharedOwnership()
    {
        ResearchAndResources();
        TraitDefinition shared = catalog.FindById("shared_cargo_bay"), mg = catalog.FindById("mg_stable_feed"), sg = catalog.FindById("sg_choke_barrel");
        ManufactureAndFit(shared); ManufactureAndFit(mg); ManufactureAndFit(sg);
        int scrap = progress.ScrapParts, core = progress.CoreShards;
        progress.SetSelectedShipId(ships[1].ShipId);
        Assert.That(progress.IsEquipmentFitted(mg.TraitId), Is.True);
        Assert.That(progress.IsEquipmentPrepared(mg.TraitId), Is.False);
        var effective = new List<string>(); progress.AppendEffectiveEquipment(effective, WeaponTreeType.Shotgun);
        Assert.That(effective, Is.EquivalentTo(new[] { shared.TraitId, sg.TraitId }));
        Assert.That(progress.TryManufactureEquipment(shared), Is.EqualTo(EquipmentDevelopmentResult.AlreadyManufactured));
        Assert.That(progress.TrySetEquipmentFitted(shared, false), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.TrySetEquipmentFitted(shared, true), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.ScrapParts, Is.EqualTo(scrap)); Assert.That(progress.CoreShards, Is.EqualTo(core));
        Assert.That(progress.ManufacturedEquipmentIds.Count, Is.EqualTo(3));
        Assert.That(store.TraitLevels, Is.Empty);
        progress.LoadFromSave(save.LoadOrCreate());
        Assert.That(progress.IsEquipmentFitted(mg.TraitId), Is.True);
        Assert.That(progress.IsEquipmentFitted(sg.TraitId), Is.True);
    }

    [Test]
    public void InvalidRecipeAndInsufficientCoreLeaveTheWholeTransactionUnchanged()
    {
        ResearchAndResources();
        var malformed = Object.Instantiate(catalog.FindById("mg_stable_feed")); transientAssets.Add(malformed);
        Set(malformed, "traitId", "invalid_recipe_fixture"); Set(malformed, "manufacturingCoreCost", -1);
        var fixtureCatalog = Object.Instantiate(catalog); transientAssets.Add(fixtureCatalog);
        var definitions = fixtureCatalog.TraitDefinitions.ToList(); definitions.Add(malformed);
        Set(fixtureCatalog, "traitDefinitions", definitions); Set(progress, "equipmentCatalog", fixtureCatalog);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(malformed), Is.EqualTo(EquipmentDevelopmentResult.InvalidRecipe));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        SaveData poor = progress.CreateSaveData(); poor.coreShards = 0; progress.LoadFromSave(poor);
        before = JsonUtility.ToJson(progress.CreateSaveData());
        Assert.That(progress.TryManufactureEquipment(catalog.FindById("mg_terminal_guidance")), Is.EqualTo(EquipmentDevelopmentResult.InsufficientResources));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
    }

    [Test]
    public void AcceptedRunSnapshotIgnoresLiveFittingAndMaxedShopDoesNotCharge()
    {
        ResearchAndResources(); TraitDefinition fitted = catalog.FindById("mg_stable_feed"), later = catalog.FindById("mg_guidance_control");
        ManufactureAndFit(fitted); RunManager manager = StartRunFixture();
        var player = ReconstructPlayer(manager.CurrentRun);
        SaveData changed = progress.CreateSaveData(); changed.manufacturedEquipmentIds.Add(later.TraitId);
        changed.equipmentLoadoutTraitIds.Clear(); changed.equipmentLoadoutTraitIds.Add(later.TraitId); progress.LoadFromSave(changed);
        Assert.That(manager.CurrentRun.PreparedEquipmentIds, Is.EqualTo(new[] { fitted.TraitId }));
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(fitted), Is.True);
        Assert.That(RunTraitAcquisitionService.IsOrdinaryCandidate(later), Is.False);
        Assert.That(progress.TrySetEquipmentFitted(later, false), Is.EqualTo(EquipmentDevelopmentResult.UnsafeState));
        while (store.CanUpgrade(fitted)) Assert.That(RunTraitAcquisitionService.TryAcquire(fitted, player, out _, out _), Is.True);
        manager.AddCurrency(CurrencyType.Credits, 1000);
        var shop = AuthoredRuntimeFixture.Create(scene, services.transform, "MaxedShop", false).AddComponent<ShopStockController>();
        string before = JsonUtility.ToJson(manager.CurrentRun.Wallet);
        Assert.That(shop.TryBuyTrait(null, fitted, player), Is.False);
        Assert.That(shop.TryBuyTrait(null, later, player), Is.False);
        Assert.That(JsonUtility.ToJson(manager.CurrentRun.Wallet), Is.EqualTo(before));
    }

    [Test]
    public void AuthoredRosterPositionsRecipesAndEquipmentSerializedReferencesAreValid()
    {
        var positions = new HashSet<string>();
        foreach (TraitDefinition trait in catalog.TraitDefinitions.Where(t => t.CanAppearAsRandomDropTrait))
        {
            Assert.That(trait.HasValidDevelopmentMetadata, Is.True, trait.TraitId);
            if (!trait.IsDevelopmentRoster) continue;
            Assert.That(trait.HasValidManufacturingRecipe, Is.True, trait.TraitId);
            Assert.That(positions.Add(trait.DevelopmentBranch + ":" + trait.DevelopmentResearchTier + ":" + trait.DevelopmentDisplayOrder), Is.True, trait.TraitId);
        }
        Assert.That(positions.Count, Is.EqualTo(48));
        // The unrelated Repair/MainPanel preview sprites are already missing in the task baseline.
        // Validate this correction's authored hierarchy and owner bindings without rewriting those panels.
        var affected = Get<GameObject>(panel, "equipmentDevelopmentRoot").GetComponentsInChildren<Component>(true).ToList();
        affected.Add(panel);
        foreach (Component component in affected)
            {
                Assert.That(component, Is.Not.Null, "Settlement missing script");
                var data = new SerializedObject(component); var property = data.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceInstanceIDValue != 0)
                        Assert.That(property.objectReferenceValue, Is.Not.Null, component.name + "." + property.propertyPath);
            }
    }

    [Test]
    public void ResearchRollbackRetainsOwnedAndFittedItemsAndRestoresTheirUse()
    {
        ResearchAndResources();
        var trait = catalog.FindById("mg_terminal_guidance"); ManufactureAndFit(trait);
        progress.LoadFromSave(Checkpoint(progress.CreateSaveData(), 1));
        Assert.That(progress.IsEquipmentManufactured(trait.TraitId), Is.True);
        Assert.That(progress.IsEquipmentFitted(trait.TraitId), Is.True);
        Assert.That(progress.IsEquipmentPrepared(trait.TraitId), Is.False);
        progress.LoadFromSave(Checkpoint(progress.CreateSaveData(), 11));
        Assert.That(progress.IsEquipmentPrepared(trait.TraitId), Is.True);
    }

    [Test]
    public void LegacyMigrationIsIdempotentAndPreservesSurplusAndUnknownIdsSafely()
    {
        var data = new SaveData { version = 5, scrapParts = 73 };
        data.equipmentLoadoutTraitIds.AddRange(new[] { "mg_stable_feed", "mg_stable_feed", "", "legacy_missing" });
        data.traitLevels.Add(new TraitLevelSaveData("shared_lightweight_cargo", 3));
        data.traitLevels.Add(new TraitLevelSaveData("pixel_curse", 1));
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Saved equipment ID 'legacy_missing'.*"));
        progress.LoadFromSave(data);
        Assert.That(progress.ManufacturedEquipmentIds, Is.EquivalentTo(new[] { "mg_stable_feed", "shared_lightweight_cargo" }));
        Assert.That(progress.EquipmentLoadoutTraitIds, Is.EqualTo(new[] { "mg_stable_feed", "legacy_missing" }));
        Assert.That(progress.GetTraitLevel("shared_lightweight_cargo"), Is.EqualTo(3));
        Assert.That(store.TraitLevels, Is.Empty);
        Assert.That(progress.TrySetEquipmentFitted(catalog.FindById("shared_lightweight_cargo"), true), Is.EqualTo(EquipmentDevelopmentResult.Success));
        string before = string.Join("|", progress.ManufacturedEquipmentIds) + ";" + string.Join("|", progress.EquipmentLoadoutTraitIds);
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Saved equipment ID 'legacy_missing'.*"));
        progress.LoadFromSave(save.LoadOrCreate());
        Assert.That(string.Join("|", progress.ManufacturedEquipmentIds) + ";" + string.Join("|", progress.EquipmentLoadoutTraitIds), Is.EqualTo(before));
        SaveData migrated = progress.CreateSaveData();
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Saved equipment ID 'legacy_missing'.*"));
        progress.LoadFromSave(migrated);
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(JsonUtility.ToJson(migrated)));
        Assert.That(progress.ScrapParts, Is.EqualTo(73));
        OpenEquipment(); panel.SelectEquipmentBranch(ShipTraitBranchKind.Shared);
        Assert.That(Get<Button>(panel, "clearEquipmentButton").gameObject.activeSelf, Is.True);
        Get<Button>(panel, "clearEquipmentButton").onClick.Invoke();
        Assert.That(Get<PreparedEquipmentView[]>(panel, "equipmentCandidates").Single(v => v.definition?.TraitId == "shared_lightweight_cargo").button.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void VersionFiveFileMigrationGrandfathersOnlyRecordedOwnership()
    {
        System.IO.File.WriteAllText(savePath, "{\"version\":5,\"scrapParts\":73,\"equipmentLoadoutTraitIds\":[\"mg_stable_feed\"],\"traitLevels\":[{\"traitId\":\"shared_lightweight_cargo\",\"level\":2}]}");
        progress.LoadFromSave(save.LoadOrCreate()); save.Save(progress);
        SaveData loaded = save.LoadOrCreate();
        Assert.That(loaded.version, Is.EqualTo(8)); Assert.That(loaded.equipmentOwnershipMigrationPending, Is.False);
        Assert.That(loaded.manufacturedEquipmentIds, Is.EquivalentTo(new[] { "mg_stable_feed", "shared_lightweight_cargo" }));
        Assert.That(loaded.equipmentLoadoutTraitIds, Is.EqualTo(new[] { "mg_stable_feed" }));
        Assert.That(loaded.scrapParts, Is.EqualTo(73));
        progress.LoadFromSave(loaded); save.Save(progress);
        Assert.That(save.LoadOrCreate().manufacturedEquipmentIds.Count, Is.EqualTo(2));
    }

    [Test]
    public void ActualCatalogSupportsMoreThanTwelveCompatibleFittings()
    {
        ResearchAndResources();
        foreach (TraitDefinition trait in catalog.TraitDefinitions.Where(t => t != null && t.IsDevelopmentRoster && t.IsAvailableFor(WeaponTreeType.MachineGun)))
            ManufactureAndFit(trait);
        var run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        Assert.That(run.PreparedEquipmentIds.Count, Is.EqualTo(24));
        store.InitializeDeployment(run, catalog);
        Assert.That(store.TraitLevels.Count, Is.EqualTo(23), "Terminal Guidance is conditional, not a free Lv1 grant.");
    }

    [Test]
    public void FinalTwentyFourContractUsesTransientFixturesNotProductionAssets()
    {
        ResearchAndResources();
        var fixtureCatalog = ScriptableObject.CreateInstance<TraitCatalog>(); transientAssets.Add(fixtureCatalog);
        var definitions = new List<TraitDefinition>();
        for (int i = 0; i < 24; i++)
        {
            var trait = Object.Instantiate(catalog.FindById("shared_cargo_bay")); transientAssets.Add(trait);
            Set(trait, "traitId", "contract_fixture_" + i); Set(trait, "category", i < 12 ? TraitCategory.Shared : TraitCategory.WeaponSpecific);
            Set(trait, "weaponTreeType", WeaponTreeType.MachineGun); Set(trait, "developmentResearchTier", i % 12 / 3);
            Set(trait, "developmentDisplayOrder", i % 3); definitions.Add(trait);
        }
        Set(fixtureCatalog, "traitDefinitions", definitions); Set(progress, "equipmentCatalog", fixtureCatalog);
        foreach (TraitDefinition trait in definitions) ManufactureAndFit(trait);
        var run = new RunContext(WeaponTreeType.MachineGun, ExpeditionDepth.Normal);
        Assert.That(run.PreparedEquipmentIds.Count, Is.EqualTo(24));
        store.InitializeDeployment(run, fixtureCatalog);
        Assert.That(store.TraitLevels.Count, Is.EqualTo(24));
        Assert.That(store.TraitLevels.All(t => t.level == 1), Is.True);
    }

    private RunManager StartRunFixture()
    {
        var manager = services.AddComponent<RunManager>();
        typeof(RunManager).GetProperty("Instance").SetValue(null, manager);
        Call(store, "EnsureRunManagerSubscription");
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        return manager;
    }

    private GameObject ReconstructPlayer(RunContext run)
    {
        var player = AuthoredRuntimeFixture.Create(scene, services.transform, "EquipmentPlayer", false);
        player.AddComponent<PlayerHealth>(); player.AddComponent<PlayerArmor>(); player.AddComponent<PlayerWeaponModifiers>();
        var stats = player.AddComponent<PlayerRuntimeStatApplier>(); Set(stats, "logApplyResult", false);
        var effects = player.AddComponent<RunTraitEffectApplier>(); Set(effects, "traitCatalog", catalog);
        var bootstrap = player.AddComponent<ExpeditionBootstrap>();
        Set(bootstrap, "playerObject", player); Set(bootstrap, "statApplier", stats); Set(bootstrap, "runTraitEffectApplier", effects);
        Set(bootstrap, "traitCatalog", catalog); Set(bootstrap, "shipDefinitions", ships);
        Set(bootstrap, "applySeaRegionPlayerEffect", false); Set(bootstrap, "setGameStateToExpedition", false);
        Set(bootstrap, "equipDefaultReinforcementWhenMissing", false); Set(bootstrap, "logBootstrapResult", false);
        bootstrap.Initialize();
        return player;
    }

    [Test]
    public void DeploymentPortalAndNextRunPreserveActualEffectsAndVitalsWithoutReseeding()
    {
        ResearchAndResources();
        var guidance = catalog.FindById("mg_guidance_control");
        ManufactureAndFit(guidance); PrepareFixture(catalog.FindById("shared_reinforced_plating"));
        RunManager manager = StartRunFixture(); RunContext run = manager.CurrentRun;
        Assert.That(store.GetLevel(guidance.TraitId), Is.EqualTo(1));
        Assert.That(store.GetLevel("mg_stable_feed"), Is.Zero);
        GameObject player = ReconstructPlayer(run);
        Assert.That(player.GetComponent<PlayerWeaponModifiers>().HomingAngleBonus, Is.EqualTo(6));
        Assert.That(RunTraitAcquisitionService.TryAcquire(guidance, player, out _, out int upgraded), Is.True);
        Assert.That(upgraded, Is.EqualTo(2));
        player.GetComponent<PlayerRuntimeStatApplier>().RestoreCurrentVitals(3, 0);
        player.GetComponent<ExpeditionBootstrap>().Initialize();
        Assert.That(player.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(player.GetComponent<PlayerWeaponModifiers>().HomingAngleBonus, Is.EqualTo(14));
        run.CapturePlayerVitals(3, 0); run.PrepareNextRegion(ExpeditionDepth.DeepZone1, SeaRegionType.DenseDebris);
        GameObject next = ReconstructPlayer(run);
        Assert.That(store.GetLevel(guidance.TraitId), Is.EqualTo(2));
        Assert.That(next.GetComponent<PlayerHealth>().CurrentHp, Is.EqualTo(3));
        Assert.That(next.GetComponent<PlayerArmor>().CurrentArmor, Is.Zero);
        Assert.That(next.GetComponent<PlayerWeaponModifiers>().HomingAngleBonus, Is.EqualTo(14));
        manager.AbandonActiveRunWithoutRewards();
        Assert.That(store.TraitLevels, Is.Empty);
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        Assert.That(store.GetLevel(guidance.TraitId), Is.EqualTo(1));
        Assert.That(progress.IsEquipmentFitted(guidance.TraitId), Is.True);
    }

    [Test]
    public void ConditionalEvolutionStaysUnownedUntilItsNormalPrerequisiteAndAcquisition()
    {
        ResearchAndResources();
        var guidance = catalog.FindById("mg_guidance_control"); var terminal = catalog.FindById("mg_terminal_guidance");
        ManufactureAndFit(guidance); ManufactureAndFit(terminal);
        RunManager manager = StartRunFixture(); GameObject player = ReconstructPlayer(manager.CurrentRun);
        Assert.That(store.GetLevel(terminal.TraitId), Is.Zero);
        Assert.That(RunTraitAcquisitionService.TryAcquire(terminal, player, out _, out _), Is.False);
        while (store.GetLevel(guidance.TraitId) < guidance.MaxLevel)
            Assert.That(RunTraitAcquisitionService.TryAcquire(guidance, player, out _, out _), Is.True);
        Assert.That(RunTraitAcquisitionService.TryAcquire(terminal, player, out _, out int level), Is.True);
        Assert.That(level, Is.EqualTo(1));
    }

    [Test]
    public void FreeDeploymentProvenanceSurvivesFieldDropRepickAndCannotPayDismantleRefund()
    {
        ResearchAndResources(); var trait = catalog.FindById("mg_stable_feed"); ManufactureAndFit(trait);
        RunManager manager = StartRunFixture(); GameObject player = ReconstructPlayer(manager.CurrentRun);
        Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out _, out _), Is.True);
        Assert.That(store.TryRemoveLevel(trait.TraitId, out _, out _, out bool earned), Is.True);
        Assert.That(earned, Is.False, "Paid/earned upgrade keeps its normal refund.");
        Assert.That(store.TryRemoveLevel(trait.TraitId, out _, out _, out bool free), Is.True);
        Assert.That(free, Is.True);
        store.InitializeDeployment(manager.CurrentRun, catalog);
        Assert.That(store.GetLevel(trait.TraitId), Is.Zero, "Discarding does not reseed within the same run.");
        var drop = AuthoredRuntimeFixture.Create(scene, services.transform, "DropFixture", false);
        drop.AddComponent<CircleCollider2D>();
        var pickup = drop.AddComponent<TraitPickup>();
        pickup.Initialize(trait, 0); pickup.SetDeploymentGrantProvenance(free);
        Assert.That(pickup.CanDismantle, Is.True); Assert.That(pickup.DismantleRewardAmount, Is.Zero);
        Assert.That(RunTraitAcquisitionService.TryAcquireFieldPickup(trait, player, true, out _, out int level), Is.True);
        Assert.That(store.IsLevelNonRefundable(trait.TraitId, level), Is.True);
        pickup.Initialize(trait, 0);
        Assert.That(pickup.DismantleRewardAmount, Is.GreaterThan(0), "Pooled reuse resets provenance.");
    }

    [Test]
    public void EmptyAndMaxedFittedPoolsNeverOpenTheMasterCatalog()
    {
        RunManager manager = StartRunFixture();
        Assert.That(RunRewardChoiceGenerator.BuildOptionsFromDefinitions(SpecialRewardMode.TraitOnly, 3, RunRewardRarity.Common,
            catalog.TraitDefinitions, null, WeaponTreeType.MachineGun, false), Is.Empty);
        manager.AbandonActiveRunWithoutRewards(); manager.ReleaseRunEndingPresentationOwnership();
        Set(state, "currentState", GameState.Settlement);
        ResearchAndResources(); ManufactureAndFit(catalog.FindById("mg_stable_feed"));
        manager.StartNewRun(WeaponTreeType.MachineGun, ExpeditionDepth.Normal, ships[0].ShipId, SeaRegionType.DenseDebris);
        GameObject player = ReconstructPlayer(manager.CurrentRun); var trait = catalog.FindById("mg_stable_feed");
        while (store.CanUpgrade(trait)) Assert.That(RunTraitAcquisitionService.TryAcquire(trait, player, out _, out _), Is.True);
        Assert.That(RunRewardChoiceGenerator.BuildOwnedTraitUpgradeOptions(catalog, 3), Is.Empty);
        Assert.That(catalog.TraitDefinitions.Any(t => RunTraitAcquisitionService.IsOrdinaryCandidate(t)), Is.False);
    }

    [Test]
    public void AuthoredFourBranchGridShowsAllTwelveRealBlueprints()
    {
        OpenEquipment(); ResearchAndResources();
        var errors = new List<string>(); Assert.That(panel.ValidateEquipmentPresentation(errors), Is.True, string.Join("; ", errors));
        int[] before = panel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()).ToArray();
        string savedShip = progress.SelectedShipId;
        var counts = new Dictionary<ShipTraitBranchKind, int> { { ShipTraitBranchKind.Shared, 12 }, { ShipTraitBranchKind.MachineGun, 12 },
            { ShipTraitBranchKind.Shotgun, 12 }, { ShipTraitBranchKind.Sniper, 12 } };
        foreach (var pair in counts)
        {
            if (pair.Key != ShipTraitBranchKind.Shared)
                progress.SetSelectedShipId(ships.Single(s => s.DefaultWeaponTree == (pair.Key == ShipTraitBranchKind.MachineGun ? WeaponTreeType.MachineGun :
                    pair.Key == ShipTraitBranchKind.Shotgun ? WeaponTreeType.Shotgun : WeaponTreeType.Sniper)).ShipId);
            string beforeInspection = progress.SelectedShipId;
            panel.SelectEquipmentBranch(pair.Key);
            Assert.That(progress.SelectedShipId, Is.EqualTo(beforeInspection));
            var views = Get<PreparedEquipmentView[]>(panel, "equipmentSlots");
            Assert.That(views.Count(v => v.definition != null), Is.EqualTo(pair.Value));
            Assert.That(views.All(v => v.definition != null && !v.label.text.Contains("개발 예정")), Is.True);
            for (int i = 0; i < 12; i++)
            {
                var rect = (RectTransform)views[i].button.transform;
                Assert.That(rect.sizeDelta.x, Is.GreaterThan(40));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(-166 + i % 3 * 73));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(36 - i / 3 * 36));
            }
        }
        progress.SetSelectedShipId(savedShip);
        Assert.That(progress.ManufacturedEquipmentIds, Is.Empty);
        Assert.That(panel.GetComponentsInChildren<Transform>(true).Select(t => t.GetInstanceID()), Is.EqualTo(before));
        Assert.That(panel.TryUnlockSelectedTrait(), Is.False);
    }

    [Test]
    public void InspectHoverManufactureAndFitHaveDistinctAuthoredStates()
    {
        OpenEquipment(); var trait = catalog.FindById("mg_guidance_control"); panel.InspectEquipment(trait);
        var view = Get<PreparedEquipmentView[]>(panel, "equipmentSlots").Single(v => v.definition == trait);
        string before = JsonUtility.ToJson(progress.CreateSaveData());
        view.button.OnPointerEnter(new PointerEventData(EventSystem.current)); view.button.OnPointerExit(new PointerEventData(EventSystem.current));
        Assert.That(JsonUtility.ToJson(progress.CreateSaveData()), Is.EqualTo(before));
        Assert.That(view.button.colors.normalColor, Is.EqualTo(SettlementSelectionColors.SelectedBackground));
        Assert.That(view.button.colors.highlightedColor, Is.EqualTo(SettlementSelectionColors.HoverBackground));
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Contain("자원 부족"));
        Assert.That(Get<Button>(panel, "equipmentActivationButton").interactable, Is.False);
        ResearchAndResources(1); panel.RefreshPanel();
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.IsEquipmentFitted(trait.TraitId), Is.False);
        Assert.That(Get<TMP_Text>(panel, "equipmentCandidateState").text, Does.Contain("제작 완료"));
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Not.Contain("스크랩"));
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(view.fittedIndicator.text, Does.Contain("장착"));
        Assert.That(Get<TMP_Text>(panel, "equipmentHint").text, Does.Contain("총 1종").And.Not.Contain("/"));
        Assert.That(Get<Button>(panel, "equipmentActivationButton").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("해제"));
        Assert.That(panel.TryExecuteEquipmentAction(), Is.EqualTo(EquipmentDevelopmentResult.Success));
        Assert.That(progress.IsEquipmentManufactured(trait.TraitId), Is.True);
        Assert.That(store.GetLevel(trait.TraitId), Is.Zero);
        Assert.That(Get<Button>(ui, "traitActionButton").gameObject.activeSelf, Is.False);
        Assert.That(AuthoredRuntimeFixture.Single<SettlementController>(scene).TryUnlockOrUpgradeTrait(trait), Is.False);
    }

    [Test]
    public void LockedBlueprintShowsRequirementAndConditionalBlueprintWarnsMissingPrerequisite()
    {
        OpenEquipment(); panel.InspectEquipment(catalog.FindById("shared_radar_amplifier"));
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Contain("분석"));
        Assert.That(Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows").All(r => !r.root.activeSelf), Is.True);
        Assert.That(Get<Button>(panel, "equipmentActivationButton").gameObject.activeSelf, Is.False);
        ResearchAndResources(); panel.InspectEquipment(catalog.FindById("mg_terminal_guidance"));
        Assert.That(Get<TMP_Text>(panel, "equipmentRequirements").text, Does.Contain("선행 장비 장착 필요"));
        Assert.That(Get<TMP_Text>(panel, "equipmentMaxLevel").text, Does.Not.Contain("Lv.1"));
        var inspected = panel.InspectedEquipment;
        panel.InspectEquipment(catalog.FindById("sg_choke_barrel"));
        Assert.That(panel.InspectedEquipment, Is.EqualTo(inspected));
        Assert.That(progress.LastSelectedWeaponTree, Is.EqualTo(WeaponTreeType.MachineGun));
    }

    [TestCase("mg_guidance_control", "유도 각도", "유도 거리")]
    [TestCase("mg_stable_feed", "탄 퍼짐", "연사력")]
    public void GrowthRowsRemainAuthoritativeAndOrdinaryEquipmentDisplaysLv1Deployment(string id, string first, string second)
    {
        OpenEquipment(); TraitDefinition trait = catalog.FindById(id); panel.InspectEquipment(trait);
        var rows = Get<EquipmentGrowthRow[]>(panel, "equipmentGrowthRows");
        Assert.That(rows.Count(r => r.root.activeSelf), Is.EqualTo(trait.MaxLevel));
        for (int i = 0; i < trait.MaxLevel; i++)
        {
            Assert.That(rows[i].effects.text, Is.EqualTo(TraitEffectTextUtility.BuildRichEffectText(trait, i + 1)));
            Assert.That(rows[i].effects.text, Does.Contain(first).And.Contain(second).And.Not.Contain("최대 체력"));
            Assert.That(rows[i].heading.text.Contains("MAX"), Is.EqualTo(i + 1 == trait.MaxLevel));
        }
        Assert.That(Get<TMP_Text>(panel, "equipmentMaxLevel").text, Does.Contain("출격 시 Lv.1").And.Contain("최대 Lv." + trait.MaxLevel));
        Assert.That(Get<TMP_Text>(panel, "equipmentCompatibility").rectTransform.rect.height, Is.LessThanOrEqualTo(12));
        Assert.That(Get<TMP_Text>(panel, "equipmentDetails").text, Is.EqualTo(trait.Description));
    }

    [Test]
    public void ActivationKeepsFooterSpaceAndInspectionResetsScroll()
    {
        OpenEquipment();
        RectTransform action = Get<Button>(panel, "equipmentActivationButton").GetComponent<RectTransform>();
        RectTransform root = Get<GameObject>(panel, "equipmentDevelopmentRoot").GetComponent<RectTransform>();
        Assert.That(action.anchoredPosition.y - action.sizeDelta.y * .5f + root.sizeDelta.y * .5f, Is.GreaterThanOrEqualTo(20));
        var scroll = Get<ScrollRect>(panel, "equipmentGrowthScroll"); scroll.content.anchoredPosition = new Vector2(0, 90);
        panel.InspectEquipment(catalog.FindById("mg_guidance_control"));
        Assert.That(scroll.content.anchoredPosition, Is.EqualTo(Vector2.zero));
    }

    private void OpenEquipment()
    {
        for (Transform t = ui.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        for (Transform t = panel.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        Set(ui, "settlementInputRequested", true); Set(ui, "dialogueModalActive", false);
        Call(panel, "OnEnable"); Assert.That(panel.CanUseTraitInput, Is.True);
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
