using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit, saved-state visual authoring. Never runs at runtime or on asset import.
public static class ApprovedVisualIntegration
{
    internal const string LogRoot = "Logs/VisualIntegration/";
    internal const string ArtRoot = "Assets/Art/ApprovedIntegration/";
    private const string Source = "ArtTools/Aseprite/Output/";
    private static readonly List<string> changes = new List<string>();
    private static readonly Dictionary<string, Sprite> imports = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> changedAssets = new HashSet<string>();
    private static readonly HashSet<string> allowed = new HashSet<string>();
    private static Sprite player, ring, scrap, credit, shard, chip, heal;

    // New copies only: source PNGs and existing importer GUIDs are never rewritten.
    private static Sprite Import(string source, string role, Vector2 size)
    {
        if (imports.TryGetValue(role, out var cached)) return cached;
        string path = ArtRoot + role + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        byte[] bytes = File.ReadAllBytes(source);
        if (File.Exists(path) && !File.ReadAllBytes(path).SequenceEqual(bytes))
            throw new InvalidOperationException("Refusing to overwrite different artwork: " + path);
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Mathf.Max(width / size.x, height / size.y);
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(.5f, .5f); importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + path);
        imports.Add(role, sprite);
        changes.Add("IMPORT\t" + path + "\t" + source + "\tPPU=" + importer.spritePixelsPerUnit);
        return sprite;
    }
    private static Sprite Art(string source, string role, Vector2 size) => Import(Source + source + ".png", role, size);
    private static Sprite Existing(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    private static string Key(Object o, string property) => o.GetInstanceID() + ":" + property;
    private static void Set(Object o, string field, Object value)
    {
        var so = new SerializedObject(o); var p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
            throw new InvalidOperationException(o.name + " missing reference field " + field);
        if (p.objectReferenceValue == value && p.objectReferenceInstanceIDValue == (value ? value.GetInstanceID() : 0)) return;
        allowed.Add(Key(o, field)); p.objectReferenceValue = value;
        if(o is SpriteRenderer && field=="m_Sprite") allowed.Add(Key(o,"m_WasSpriteAssigned"));
        if(o is SpriteRenderer renderer && field=="m_Sprite" && renderer.drawMode==SpriteDrawMode.Simple)
            allowed.Add(Key(o,"m_Size")); // cached native sprite size; unused by Simple rendering
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
        changes.Add("BIND\t" + o.GetType().Name + "\t" + (o is Component c ? PathOf(c.transform) : o.name) + "\t" + field + "\t" + AssetDatabase.GetAssetPath(value));
    }
    private static T Ref<T>(Object o, string field) where T : Object => (T)new SerializedObject(o).FindProperty(field).objectReferenceValue;
    private static string Relative(Transform t, Transform root) => t == root ? "" : AnimationUtility.CalculateTransformPath(t, root);
    private static SpriteRenderer Renderer(GameObject root, string path) =>
        (string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path))?.GetComponent<SpriteRenderer>()
        ?? throw new InvalidOperationException(root.name + " missing renderer " + path);
    private static Sprite Fit(SpriteRenderer r, string source, string role, Vector2 fallback = default)
    {
        Vector2 size = r.sprite != null ? r.sprite.bounds.size : fallback == default ? Vector2.one : fallback;
        // A square replacement is inscribed into the previous sprite bounds. No transforms, anchors or colliders move.
        var sprite = Art(source, role, size); Set(r, "m_Sprite", sprite); return sprite;
    }
    private static Dictionary<string, string> Snapshot(IEnumerable<GameObject> roots)
    {
        var result = new Dictionary<string, string>();
        foreach (var root in roots)
        foreach (var o in root.GetComponentsInChildren<Component>(true).Where(c => c != null).Cast<Object>().Concat(root.GetComponentsInChildren<Transform>(true).Select(t => (Object)t.gameObject)))
        {
            var it = new SerializedObject(o).GetIterator(); bool enterChildren = true;
            while (it.Next(enterChildren))
            {
                enterChildren = it.propertyType == SerializedPropertyType.Generic;
                if (it.propertyType == SerializedPropertyType.Generic) continue;
                // Unity refreshes this sprite-bound cache on non-tiling colliders. Actual
                // collider size, offset, paths, triggers and every physics setting remain checked.
                if(o is Collider2D && it.propertyPath.StartsWith("m_SpriteTilingProperty."))
                {
                    var tiling=new SerializedObject(o).FindProperty("m_AutoTiling");
                    if(tiling==null||!tiling.boolValue) continue;
                }
                string value = it.propertyType == SerializedPropertyType.ObjectReference ? it.objectReferenceInstanceIDValue.ToString() : it.propertyType == SerializedPropertyType.ManagedReference ? it.managedReferenceFullTypename : it.type + ":" + it.contentHash;
                result[Key(o, it.propertyPath)] = value;
            }
        }
        return result;
    }
    private static void AssertPreserved(Dictionary<string, string> before, IEnumerable<GameObject> roots)
    {
        var after = Snapshot(roots);
        foreach (var item in before)
            if (!allowed.Contains(item.Key) && (!after.TryGetValue(item.Key, out string v) || v != item.Value))
                throw new InvalidOperationException("Non-visual serialized property changed: " + item.Key + " owner=" + EditorUtility.InstanceIDToObject(int.Parse(item.Key.Split(':')[0])));
        if (after.Count != before.Count) throw new InvalidOperationException("Hierarchy/component/property count changed.");
    }
    private static void Prefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            allowed.Clear(); var before = Snapshot(new[] { root }); int count = changes.Count;
            edit(root); AssertPreserved(before, new[] { root });
            if (changes.Count != count) { PrefabUtility.SaveAsPrefabAsset(root, path); changedAssets.Add(path); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Run()
    {
        Guard(); changes.Clear(); imports.Clear(); changedAssets.Clear();
        Directory.CreateDirectory(LogRoot);
        // This already-approved square is the surviving original player silhouette, not new art.
        player = Import("ArtTools/Aseprite/Input/01_Player/Px_Player.png", "Player/CurseBase", new Vector2(.5f, .5f));
        ring = Existing("Assets/Dark UI/Free/CIRCLE2PXLAR.png");
        credit = Art("19_WorldPickups/Pickup_Credit", "UI/Credit", Vector2.one);
        scrap = Art("19_WorldPickups/Pickup_Scrap", "UI/Scrap", Vector2.one);
        shard = Art("19_WorldPickups/Pickup_CoreShard", "UI/CoreShard", Vector2.one);
        chip = Art("19_WorldPickups/Pickup_TuningChip", "UI/TuningChip", Vector2.one);
        heal = Art("19_WorldPickups/Pickup_Heal", "UI/Heal", Vector2.one);
        AuthorTraits(); AuthorShips(); AuthorEnemies(); AuthorWorld(); AuthorScenes();
        AssetDatabase.SaveAssets();
        File.WriteAllLines(LogRoot + "bindings.tsv", changes);
        File.WriteAllLines(LogRoot + "changed-assets.txt", changedAssets.OrderBy(p => p));
        Validate();
        Debug.Log("APPROVED_VISUAL_AUTHOR PASS; " + changedAssets.Count + " assets; all non-visual serialized properties preserved.");
    }
    private static void AuthorTraits()
    {
        var map = new Dictionary<string, string> {
            {"shared_periodic_reflector","Reinforcement/Common/04_rf_burst_barrier"},
            {"mg_terminal_guidance","Trait/MachineGun/27_mg_guidance_control"},
            {"mg_dash_missile_salvo","Reinforcement/MachineGun/27_rf_mg_guidance_drone"},
            {"mg_traverse_servo","Trait/Common/13_shared_combat_gyro"},
            {"mg_heat_exchanger","Reinforcement/MachineGun/26_rf_mg_cooling_core"},
            {"mg_line_penetrator","Trait/Sniper/37_sn_piercing_amplifier"},
            {"mg_target_distributor","Reinforcement/MachineGun/30_rf_mg_lockon_array"},
            {"mg_twin_feed","Trait/MachineGun/28_mg_stable_feed"},
            {"sg_close_quarters_overpressure","Reinforcement/Shotgun/34_rf_sg_impact_booster"},
            {"sg_cycle_actuator","Trait/Common/12_shared_rapid_feed"},
            {"sg_pellet_penetrator","Trait/Sniper/37_sn_piercing_amplifier"},
            {"sg_impact_ejector","Trait/Shotgun/33_sg_breaching_drive"},
            {"sg_breach_compensator","Trait/Common/13_shared_combat_gyro"},
            {"sg_slug_coupler","Trait/Shotgun/31_sg_choke_barrel"},
            {"sg_breach_sequencer","Reinforcement/Shotgun/32_rf_sg_cone_emp"},
            {"sn_dash_echo_shot","Trait/Common/06_shared_dash_capacitor"},
            {"sn_ballistic_alignment","Trait/Common/09_shared_targeting_bus"},
            {"sn_mobile_charge_coupler","Trait/Common/07_shared_vector_thruster"},
            {"sn_charge_aperture","Trait/Sniper/36_sn_charge_accelerator"},
            {"sn_anchor_optics","Reinforcement/Sniper/39_rf_sn_stasis_anchor"},
            {"sn_reserve_capacitor","Reinforcement/MachineGun/29_rf_mg_ammo_capacitor"},
            {"boss_sector_barrier","Trait/Common/04_shared_reinforced_plating"},
            {"boss_matter_reconstructor","Trait/Common/01_shared_cargo_bay"},
            {"boss_phase_afterimage","Trait/Sniper/40_sn_stealth_scan"}
        };
        foreach (string path in Directory.GetFiles("Assets/02_Scripts/Config/TraitDefinition", "*.asset", SearchOption.AllDirectories))
        {
            var trait = AssetDatabase.LoadAssetAtPath<TraitDefinition>(path.Replace('\\','/'));
            if (trait != null && map.TryGetValue(trait.TraitId, out string icon))
            { Set(trait, "icon", Existing("Assets/02_Scripts/Config/Icons/" + icon + ".png")); changedAssets.Add(path.Replace('\\','/')); }
        }
        foreach (string region in new[] {"1", "2", "3"})
        {
            string path = "Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region" + region + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<BossCampaignDefinition>(path);
            string family = region == "1" ? "green" : region == "2" ? "orange" : "blue";
            Set(def, "storyPartSprite", CoreIcon(family)); changedAssets.Add(path);
        }
    }
    private static Sprite CoreIcon(string family) => Art("02_Core/" + family + "/core_" + family + "_icon", "UI/Core_" + family, Vector2.one);
    private static void AuthorShips()
    {
        foreach (string path in new[] {"01_basic_ship", "02_shotgun_ship", "03_sniper_ship"})
        {
            string asset = "Assets/02_Scripts/Settlement/" + path + ".asset";
            var ship = AssetDatabase.LoadAssetAtPath<ShipDefinition>(asset);
            Set(ship, "previewSprite", ship.GetWeaponSprite(ship.DefaultWeaponTree)); changedAssets.Add(asset);
        }
    }
    private static void AuthorEnemies()
    {
        var map = new Dictionary<string,string> {
            {"Enemy_Basic","basic"}, {"Enemy_Shotgun","shotgun"}, {"Enemy_Charge","sniper_charging"},
            {"PF_Enemy_MeleeCharger_Common","basic"}, {"PF_Enemy_DefenderBasic","basic"},
            {"PF_Enemy_DefenderShotgun","shotgun"}, {"PF_Enemy_RivalHarvester","basic"}, {"PF_Enemy_Scavenger","basic"},
            {"Enemy_Elite","elite"}, {"Enemy_Elite 1","elite"}, {"Enemy_Elite 2","elite"}
        };
        foreach (var pair in map)
            Prefab("Assets/03_Prefabs/Enemy/" + pair.Key + ".prefab", root => Fit(Renderer(root, ""), "03_RaiderEnemy/raider_" + pair.Value, "World/" + pair.Key, Vector2.one));
        Prefab("Assets/03_Prefabs/Enemy/Boss.prefab", root => Fit(Renderer(root,"BossVisualRoot"), "06_SectorAdministrator/sector_administrator_idle", "World/SectorAdministrator"));
        Prefab("Assets/03_Prefabs/Enemy/LaserManagerShip.prefab", root => Fit(Renderer(root,""), "04_SystemSupport/system_support_green", "World/GreenSupport"));
        Prefab("Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab", root => {
            foreach (string part in new[] {"FrigateLeft", "FrigateCenter", "FrigateRight"})
                Fit(Renderer(root,"FormationRoot/"+part+"/VisualRoot"), "07_DefenseOverseer/defense_overseer_idle", "World/DefenseOverseer");
        });
        Prefab("Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab", root => {
            var r = Renderer(root,"BossVisualRoot"); Vector2 size = r.sprite.bounds.size;
            var idle = Fit(r,"08_PhaseGatekeeper/phase_gatekeeper_idle","World/PhaseGatekeeper");
            var charged = Art("08_PhaseGatekeeper/phase_gatekeeper_phase_lock","World/PhaseGatekeeperCharge",size);
            var controller = root.GetComponent<PhaseGatekeeperBossController>(); Set(controller,"idleSprite",idle);
            var p = new SerializedObject(controller).FindProperty("chargeSprites");
            for (int i=0;i<p.arraySize;i++) Set(controller,"chargeSprites.Array.data["+i+"]",charged);
        });
        Prefab("Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab", root => Fit(Renderer(root,"BossVisualRoot/Sprite"),"09_RaiderAssaultCommander/raider_assault_commander_idle","World/RaiderCommander"));
        Prefab("Assets/03_Prefabs/Enemy/PF_Raider_BarricadeCarrier.prefab", root => Fit(Renderer(root,"VisualRoot"),"10_RaiderSalvageCarrier/raider_salvage_carrier_idle","World/RaiderCarrier"));
    }
    private static void AuthorWorld()
    {
        Prefab("Assets/03_Prefabs/Enemy/PF_ShopStructure.prefab", root => Fit(Renderer(root,"Visual"),"12_SalvageTradingStation/salvage_trading_station_neutral","World/NeutralShop"));
        Prefab("Assets/03_Prefabs/Enemy/PF_FactionBase_A.prefab", root => {
            Fit(Renderer(root,"Visual/Walls_Trigger/Center"),"13_RaiderBase/raider_outpost_normal","World/RaiderOutpost");
            Fit(Renderer(root,"Visual/Object/FIXED_MUCHINE"),"13_RaiderBase/raider_power_node_normal","World/RaiderPowerNode");
            Fit(Renderer(root,"Visual/Object/FIXED_MUCHINE/VisualRoot"),"13_RaiderBase/raider_power_node_active","World/RaiderPowerNodeActive");
            Fit(Renderer(root,"Visual/Object/CoinBox"),"13_RaiderBase/raider_storage_normal","World/RaiderStorage");
        }); // Base B inherits these visual references; do not flatten or rewrite its variant.
        Prefab("Assets/03_Prefabs/Turret.prefab", root => Fit(Renderer(root,"HeadPivot"),"13_RaiderBase/raider_turret_normal","World/RaiderTurret"));
        var events = new Dictionary<string,string> {
            {"Event_UnstableReactor","unstable_reactor_dormant"}, {"Event_BlackBoxRecovery","black_box_dormant"},
            {"Event_RescueSignal","rescue_signal_idle"}, {"Event_UnknownDevice","unknown_device_dormant"}
        };
        var progressRing = Import("Assets/Dark UI/Free/CIRCLE2PXLAR.png","World/EventProgressRing",new Vector2(.32f,.32f));
        foreach (var pair in events)
            Prefab("Assets/03_Prefabs/Event/"+pair.Key+".prefab",root => {
                Fit(Renderer(root,"VisualRoot/CoreSprite"),"14_FieldEvents/"+pair.Value,"World/"+pair.Key);
                var r = Renderer(root,"VisualRoot/ProgressPulse"); if (r.sprite == null) Set(r,"m_Sprite",progressRing);
            });
        foreach (string name in new[] {"PF_PlayerAttractionBeacon","PF_PlayerAreaControlField"})
            Prefab("Assets/03_Prefabs/Reinforcement/"+name+".prefab",root => {
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (r.sprite == null) Set(r,"m_Sprite",r.name.Contains("Radius")
                        ? Import("Assets/Dark UI/Free/CIRCLE2PXLAR.png","World/BeaconPulse",new Vector2(.5f,.5f)) : player);
            });
        Prefab("Assets/02_Scripts/Resources/VFX/PF_Player_Sniper_DashEcho.prefab",root => Set(Renderer(root,""),"m_Sprite",player));
        Prefab("Assets/03_Prefabs/Object/RewardPickup.prefab",root => {
            var pickup = root.GetComponent<RewardPickup>();
            var map = new Dictionary<string,string> {{"creditsSprite","Credit"},{"scrapSprite","Scrap"},{"coreShardSprite","CoreShard"},{"tuningChipSprite","TuningChip"},{"healSprite","Heal"}};
            foreach(var pair in map) {
                var old = Ref<Sprite>(pickup,pair.Key); Vector2 size = old != null ? old.bounds.size : new Vector2(.32f,.32f);
                Set(pickup,pair.Key,Art("19_WorldPickups/Pickup_"+pair.Value,"World/Pickup_"+pair.Value,size));
            }
            var oldScrap = Existing("Assets/Space Kit/UI/uis (21).png");
            Set(pickup,"stabilizedAlloySprite",Import("Assets/02_Scripts/Config/Icons/Trait/Common/04_shared_reinforced_plating.png","World/AlloyPlate",oldScrap.bounds.size));
            Set(Renderer(root,"VisualRoot"),"m_Sprite",Ref<Sprite>(pickup,"creditsSprite"));
        });
        Prefab("Assets/03_Prefabs/Object/TraitPickup.prefab",root => Set(root.GetComponent<TraitPickup>(),"fallbackSprite",Existing("Assets/02_Scripts/Config/Icons/Trait/Common/01_shared_cargo_bay.png")));
        Prefab("Assets/03_Prefabs/Object/ReinforcementPickup.prefab",root => Set(root.GetComponent<ReinforcementPickup>(),"fallbackSprite",Existing("Assets/02_Scripts/Config/Icons/Reinforcement/Common/01_rf_emergency_repair_kit.png")));
        Prefab("Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab",AuthorUI);
        Prefab("Assets/03_Prefabs/ShipTraitNodeButton.prefab",root => {
            foreach (var i in root.GetComponentsInChildren<Image>(true))
                if (i.name=="Icon" && i.sprite==null) Set(i,"m_Sprite",Existing("Assets/02_Scripts/Config/Icons/Trait/Common/01_shared_cargo_bay.png"));
        });
    }
    private static readonly Dictionary<string, Sprite> fragments = new Dictionary<string, Sprite>();
    private static Sprite Fragment(string name)
    {
        if (fragments.Count == 0)
        {
            string path = ArtRoot + "Player/DeathFragments.png";
            if (!File.Exists(path)) File.Copy("ArtTools/Aseprite/Input/01_Player/Px_Player.png",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.spritePixelsPerUnit=32;
            // Slicing the surviving approved texture changes no source pixels or animation paths.
            var factories=new SpriteDataProviderFactories(); factories.Init();
            var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var existing=provider.GetSpriteRects();
            string[] names={"UpperLeft","UpperRight","LowerLeft","LowerRight"};
            var rects=names.Select((n,i)=>new SpriteRect {name=n,rect=new Rect(i%2*8,i<2?8:0,8,8),
                alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),
                spriteID=existing.FirstOrDefault(r=>r.name==n)?.spriteID??GUID.Generate()}).ToArray();
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) fragments.Add(s.name,s);
            changes.Add("IMPORT\t"+path+"\tArtTools/Aseprite/Input/01_Player/Px_Player.png\t4 existing texture quadrants");
        }
        return fragments[name.Trim().Replace("Part_","")];
    }
    private static void AuthorScenes()
    {
        foreach (string name in new[] {"Boot","Tutorial","Settlement","Expedition"})
        {
            string path = "Assets/01_Scenes/"+name+".unity";
            var scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Single); var roots=scene.GetRootGameObjects();
            allowed.Clear(); var before=Snapshot(roots); int count=changes.Count;
            foreach (var root in roots)
            {
                AuthorUI(root);
                foreach (var state in root.GetComponentsInChildren<PlayerVisualStateController>(true)) Set(state,"cursedBaseSprite",player);
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    string p=PathOf(r.transform);
                    if (r.sprite==null && p.Contains("/DeathParts/")) Set(r,"m_Sprite",Fragment(r.name));
                    else if (r.sprite==null && (r.name=="Body_StaticSprite" || r.name=="WeaponAccent") && p.Contains("Player/"))
                        Set(r,"m_Sprite",r.name=="Body_StaticSprite" ? Existing("Assets/Space Kit/Player/MuchineGun.png") : player);
                    if (p.Contains("/ComponentStations/") && r.name=="Socket")
                    {
                        string family=p.Contains("SectorStabilizer")?"green_control":p.Contains("MatterCompressor")?"orange_defense":"blue_navigation";
                        Fit(r,"05_SystemStructures/system_"+family+"_node","World/System_"+family);
                    }
                }
            }
            AssertPreserved(before,roots);
            if(changes.Count!=count) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changedAssets.Add(path); }
        }
    }
    private static void AuthorUI(GameObject root)
    {
        var basic=AssetDatabase.LoadAssetAtPath<ShipDefinition>("Assets/02_Scripts/Settlement/01_basic_ship.asset").PreviewSprite;
        foreach(var hud in root.GetComponentsInChildren<SettlementHUD>(true))
        {
            Set(hud,"cursedPreviewSprite",player); Set(hud,"repairScrapCostIconSprite",scrap); Set(hud,"repairCoreShardCostIconSprite",shard);
        }
        foreach(var panel in root.GetComponentsInChildren<PlayerBuildStatusPanelUI>(true)) Set(panel,"fallbackShipIcon",basic);
        foreach(var tree in root.GetComponentsInChildren<ShipTraitTreePanel>(true))
        { Set(tree,"scrapCostIconSprite",scrap); Set(tree,"coreShardCostIconSprite",shard); }
        foreach(var i in root.GetComponentsInChildren<Image>(true))
        {
            string path=PathOf(i.transform); string old=AssetDatabase.GetAssetPath(i.sprite);
            if(old=="Assets/Space Kit/UI/uis (21).png") Set(i,"m_Sprite",path.Contains("Alloy") ? Existing("Assets/02_Scripts/Config/Icons/Trait/Common/04_shared_reinforced_plating.png") : scrap);
            else if(old=="Assets/Space Kit/UI/uis (18).png") Set(i,"m_Sprite",credit);
            else if(old=="Assets/Space Kit/UI/PF_RewardCurrency.png") Set(i,"m_Sprite",chip);
            else if(old=="Assets/Space Kit/UI/Heal.png") Set(i,"m_Sprite",heal);
            else if(old.StartsWith("Assets/Space Kit/Core/core") && !path.Contains("Curse"))
                Set(i,"m_Sprite",path.Contains("CoreSignal") ? CoreIcon("green") : shard);
            if ((i.sprite==null || old.Contains("/Editor/")) && (path.Contains("HPGauge") && (i.name=="Fill" || i.name=="ArmorFill")))
                Set(i,"m_Sprite",AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));
            if(i.sprite==null && path.Contains("SettlementHUD/MainPanel") && i.name=="PreViewImage") Set(i,"m_Sprite",basic);
            if(i.sprite==null && (path.EndsWith("ShipTraitTreePanel/LeftPanel/Image ") || path.EndsWith("ShipTraitTreePanel/TraitEntryTemplate/Icon")))
                Set(i,"m_Sprite",Existing("Assets/02_Scripts/Config/Icons/Trait/Common/01_shared_cargo_bay.png"));
            if(i.name=="Image" && path.Contains("/CargoManifestList/CargoRow_"))
            {
                Sprite icon=path.Contains("CargoRow_Credits/")?credit:path.Contains("CargoRow_Scrap/")?scrap:
                    path.Contains("CargoRow_Core/")?shard:path.Contains("CargoRow_TuningChips/")?chip:
                    path.Contains("CargoRow_Alloy/")?Existing("Assets/02_Scripts/Config/Icons/Trait/Common/04_shared_reinforced_plating.png"):null;
                if(icon!=null) Set(i,"m_Sprite",icon);
            }
        }
    }
    public static void Validate()
    {
        var errors=new List<string>();
        foreach (string path in Directory.GetFiles("Assets/02_Scripts/Config/TraitDefinition","*.asset",SearchOption.AllDirectories))
        {
            var t=AssetDatabase.LoadAssetAtPath<TraitDefinition>(path.Replace('\\','/'));
            if(t!=null && t.Icon==null) errors.Add("Missing trait icon "+path);
        }
        foreach (string path in File.ReadAllLines(LogRoot+"changed-assets.txt").Where(p=>p.EndsWith(".prefab")))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                foreach(var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                    if(r.sprite==null && !r.name.Contains("SupportDrone")) errors.Add(path+" missing renderer "+PathOf(r.transform));
            } finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach(string name in new[]{"Tutorial","Settlement","Expedition"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/01_Scenes/"+name+".unity",OpenSceneMode.Single);
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var state in root.GetComponentsInChildren<PlayerVisualStateController>(true))
                    if(Ref<Sprite>(state,"cursedBaseSprite")==null) errors.Add(name+" missing Curse sprite");
                foreach(var hud in root.GetComponentsInChildren<SettlementHUD>(true)) hud.ValidateHangarPresentation(errors);
                foreach(var i in root.GetComponentsInChildren<Image>(true))
                    if(PathOf(i.transform).Contains("HPGauge") && (i.name=="Fill"||i.name=="ArmorFill") && i.sprite==null) errors.Add(name+" missing gauge fill");
            }
        }
        File.WriteAllLines(LogRoot+"validation.txt",errors.Count==0?new[]{"PASS: all catalog trait icons, affected prefab renderers, Hangar/Curse bindings and HP/Armor fill references resolve."}:errors);
        if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
        Debug.Log("APPROVED_VISUAL_VALIDATION PASS");
    }
    internal static string PathOf(Transform t)
    {
        return t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
    internal static void Guard()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close the current prefab stage before saved authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save open scenes before saved authoring.");
    }
    public static void Inspect()
    {
        Guard(); Directory.CreateDirectory(LogRoot);
        var lines = new List<string>();
        foreach (string file in Directory.GetFiles("Assets/03_Prefabs", "*.prefab", SearchOption.AllDirectories))
        {
            var root = PrefabUtility.LoadPrefabContents(file);
            try { InspectRoot(file, root, lines); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string file in new[] {"Assets/01_Scenes/Boot.unity", "Assets/01_Scenes/Tutorial.unity", "Assets/01_Scenes/Settlement.unity", "Assets/01_Scenes/Expedition.unity"})
        {
            var scene = EditorSceneManager.OpenScene(file, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) InspectRoot(file, root, lines);
        }
        File.WriteAllLines(LogRoot + "before-renderers.tsv", lines);
        Debug.Log("APPROVED_VISUAL_INSPECT " + lines.Count + " renderer records; no assets saved.");
    }
    private static void InspectRoot(string file, GameObject root, List<string> lines)
    {
        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Sprite s = r.sprite;
            lines.Add(string.Join("\t", file.Replace('\\','/'), PathOf(r.transform),
                s != null ? AssetDatabase.GetAssetPath(s) + ":" + s.name : "NULL",
                s != null ? s.bounds.size.ToString("F4") : "NULL", r.transform.localScale.ToString("F4"),
                string.Join(",", r.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))));
        }
    }
}
