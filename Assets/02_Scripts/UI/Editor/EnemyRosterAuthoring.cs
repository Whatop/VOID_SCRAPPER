using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit saved authoring only. No runtime installation or fallback hierarchy.
public static class EnemyRosterAuthoring
{
    public const string BaseA = "Assets/03_Prefabs/Enemy/PF_FactionBase_A.prefab";
    public const string BaseB = "Assets/03_Prefabs/Enemy/PF_FactionBase_B.prefab";
    public const string PowerLink = "Assets/03_Prefabs/Enemy/PF_FieldBasePowerLink.prefab";
    private static void Edit(Object o, string field, Object value)
    { var s = new SerializedObject(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o); }
    private static void Array(Object o, string field, params Object[] values)
    {
        var s = new SerializedObject(o); var p = s.FindProperty(field); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        s.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(o);
    }
    public static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode before saved authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty)
            throw new InvalidOperationException("Save open scenes before saved authoring.");
        WireElite("Elite_MachineGun", "Enemy_Elite 2"); WireElite("Elite_Charging", "Enemy_Elite 1");
        AuthorMelee(); AuthorPowerLink(); AuthorBase();
        var scene = EditorSceneManager.OpenScene(EnemyRosterAudit.ScenePath, OpenSceneMode.Single);
        var generator = EnemyRosterAudit.Single<ExpeditionMapGenerator>(scene);
        Array(generator, "fieldBasePrefabs", AssetDatabase.LoadAssetAtPath<GameObject>(BaseA), AssetDatabase.LoadAssetAtPath<GameObject>(BaseB));
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); EnemyRosterAudit.Run();
        Debug.Log("ENEMY_ROSTER_AUTHORING: existing Elite variants, base captive/defender/power bindings and authored base pair saved.");
    }
    private static void WireElite(string definitionName, string prefabName)
    {
        string path = "Assets/03_Prefabs/Enemy/" + prefabName + ".prefab";
        var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/02_Scripts/Config/EnemyDefinition/" + definitionName + ".asset");
        Edit(definition, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(path));
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Edit(root.GetComponent<EnemyBaseAI>(), "enemyDefinition", definition);
            Edit(root.GetComponent<EnemyAttackController>(), "enemyDefinition", definition);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void AuthorPowerLink()
    {
        bool exists = File.Exists(PowerLink);
        var root = exists ? PrefabUtility.LoadPrefabContents(PowerLink) : new GameObject("PF_FieldBasePowerLink");
        try
        {
            var line = root.GetComponent<LineRenderer>(); if (line == null) line = root.AddComponent<LineRenderer>();
            var link = root.GetComponent<FieldBasePowerLink2D>(); if (link == null) link = root.AddComponent<FieldBasePowerLink2D>();
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.useWorldSpace = true; line.positionCount = 2; line.sortingOrder = 3;
            Edit(link, "lineRenderer", line);
            var s = new SerializedObject(link);
            s.FindProperty("autoCreateLineRenderer").boolValue = false;
            s.FindProperty("lineWidth").floatValue = .035f;
            s.FindProperty("waveAmplitude").floatValue = .008f;
            s.FindProperty("sortingOrder").intValue = 3;
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PowerLink);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }
    private static void AuthorMelee()
    {
        const string path = "Assets/03_Prefabs/Enemy/PF_Enemy_MeleeCharger_Common.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var melee = root.GetComponent<EnemyMeleeChargeController2D>();
            if (melee == null) melee = root.AddComponent<EnemyMeleeChargeController2D>();
            Edit(melee, "enemyAI", root.GetComponent<EnemyBaseAI>()); Edit(melee, "enemyHealth", root.GetComponent<EnemyHealth>());
            Edit(melee, "body", root.GetComponent<Rigidbody2D>()); Edit(melee, "bodySprite", root.GetComponent<SpriteRenderer>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void AuthorBase()
    {
        var root = PrefabUtility.LoadPrefabContents(BaseA);
        try
        {
            var controller = root.GetComponent<FieldBaseController>();
            var s = new SerializedObject(controller);
            var node = (FieldBaseSecurityNode)s.FindProperty("securityNodes").GetArrayElementAtIndex(0).objectReferenceValue;
            var turretRoot = root.transform.Find("Turrect");
            var points = turretRoot.Cast<Transform>().Where(t => t.name == "turretPoint").ToArray();
            if (points.Length != 4) throw new InvalidOperationException("Expected four existing turret anchors at " + BaseA);
            // Existing NPC prefab and existing captivity/portal authority. No additional rescue wave.
            var npc = root.transform.Find("CaptiveNPC")?.GetComponent<FieldNpcObjective>();
            if (npc == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/NPC/NPC_RescueContact.prefab"), root.transform);
                instance.name = "CaptiveNPC"; npc = instance.GetComponent<FieldNpcObjective>();
                // Root is scaled 2x; preserve the normal NPC's world footprint.
                instance.transform.localScale *= .5f;
            }
            npc.transform.localPosition = new Vector3(-.38f, 2.55f, 0);
            var npcData = new SerializedObject(npc); npcData.FindProperty("useBaseRescueFlow").boolValue = true;
            npcData.FindProperty("requiresRescue").boolValue = true; npcData.FindProperty("state").intValue = 0;
            npcData.ApplyModifiedPropertiesWithoutUndo();
            Edit(controller, "captiveNpc", npc);
            var portalPoint = root.transform.Find("NpcPortalInteraction");
            if (portalPoint == null) { portalPoint = new GameObject("NpcPortalInteraction").transform; portalPoint.SetParent(root.transform, false); }
            portalPoint.localPosition = new Vector3(-.38f, .9f, 0);
            Edit(controller, "npcPortalInteractionPoint", portalPoint);
            Array(controller, "defenders",
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Enemy_DefenderBasic.prefab").GetComponent<EnemyRoleController>(),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Enemy/PF_Enemy_DefenderShotgun.prefab").GetComponent<EnemyRoleController>());
            Edit(controller, "turretPowerLinkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(PowerLink));
            Edit(controller, "defaultTurretPowerSourcePoint", node.transform);
            // Three turrets / four mobile guards. B reverses that small composition emphasis.
            Array(controller, "turretSpawnPoints", points[0], points[1], points[3]);
            s.Update(); var links = s.FindProperty("turretPowerLinkEndpoints"); links.arraySize = 3;
            for (int i = 0; i < links.arraySize; i++)
            {
                links.GetArrayElementAtIndex(i).FindPropertyRelative("startPoint").objectReferenceValue = node.transform;
                links.GetArrayElementAtIndex(i).FindPropertyRelative("endPoint").objectReferenceValue = null;
            }
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BaseA);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        bool exists = File.Exists(BaseB);
        var variant = exists ? PrefabUtility.LoadPrefabContents(BaseB) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BaseA));
        try
        {
            variant.name = "PF_FactionBase_B";
            var controller = variant.GetComponent<FieldBaseController>();
            var points = variant.transform.Find("Turrect").Cast<Transform>().Where(t => t.name == "turretPoint").ToArray();
            Array(controller, "turretSpawnPoints", points);
            var s = new SerializedObject(controller);
            s.FindProperty("defenderZoneAnchors").arraySize = 3;
            s.FindProperty("turretPowerLinkEndpoints").arraySize = 0;
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(variant, BaseB);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(variant); else Object.DestroyImmediate(variant); }
    }
}
