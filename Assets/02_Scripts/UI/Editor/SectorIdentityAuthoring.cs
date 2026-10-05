using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Saved Region A assets only; no texture/importer, shader or scene authoring.
public static class SectorIdentityAuthoring
{
    static readonly string Charger = "Assets/02_Scripts/Config/EnemyDefinition/Charge_enemy.asset";
    static LineRenderer Line(Transform parent, string name, int points)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        line.useWorldSpace = false; line.positionCount = points; line.numCornerVertices = line.numCapVertices = 0;
        line.startColor = line.endColor = BossPatternController.SectorPurple; line.enabled = false;
        return line;
    }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var boss = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var c = boss.GetComponent<BossPatternController>();
            var visual = boss.transform.Find("BossVisualRoot"); var body = visual.GetComponent<SpriteRenderer>();
            var cannon = visual.Find("CenterChargeCannon");
            float ppu = body.sprite.pixelsPerUnit;
            var accents = new List<LineRenderer>();
            // Simple saved strips over the EXISTING energy positions. White armor is untouched.
            void Strip(string name, float x, float y, float x2, float y2, float width, bool onCannon = false)
            {
                var line = Line(onCannon ? cannon : visual, "Energy_" + name, 2);
                float cy = onCannon ? 32 : 64;
                line.SetPosition(0, new Vector3((x - 64) / ppu, (cy - y) / ppu, 0));
                line.SetPosition(1, new Vector3((x2 - 64) / ppu, (cy - y2) / ppu, 0));
                line.widthMultiplier = width / ppu; line.sortingOrder = body.sortingOrder + 3;
                accents.Add(line);
            }
            Strip("LeftBarrel", 21, 14, 21, 44, 2.4f);
            Strip("RightBarrel", 107, 14, 107, 44, 2.4f);
            Strip("CannonTip", 60, 29, 68, 29, 4, true);
            Strip("CannonNeck", 64, 42, 64, 44, 4, true);
            Strip("LeftShoulder", 34, 47, 43, 47, 2.4f);
            Strip("RightShoulder", 85, 47, 94, 47, 2.4f);
            Strip("LeftCore", 48, 64, 52, 64, 4);
            Strip("RightCore", 76, 64, 80, 64, 4);
            Strip("LowerCore", 64, 76, 64, 80, 4);
            Strip("LeftReturn", 34, 85, 43, 85, 2.4f);
            Strip("RightReturn", 85, 85, 94, 85, 2.4f);
            Strip("RearEnergy", 58, 93, 70, 93, 2.4f);
            Strip("RearTip", 60, 111, 68, 111, 2.4f);
            Strip("LeftPod", 18, 90, 18, 94, 4);
            Strip("RightPod", 110, 90, 110, 94, 4);
            var core = Line(visual, "Energy_CoreDiamond", 4); core.loop = true;
            core.SetPositions(new[] { new Vector3(0, 5/ppu), new Vector3(5/ppu, 0), new Vector3(0, -5/ppu), new Vector3(-5/ppu, 0) });
            core.widthMultiplier = 4/ppu; core.sortingOrder = body.sortingOrder + 3; accents.Add(core);
            var so = new SerializedObject(c); var array = so.FindProperty("sectorEnergyAccents"); array.arraySize = accents.Count;
            for (int i = 0; i < accents.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = accents[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(boss, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        var lane = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Lane);
        try
        {
            var line = Line(lane.transform, "EmpoweredEnergy", 2); line.sortingOrder = -2;
            line.SetPosition(0, Vector3.zero); line.SetPosition(1, Vector3.right);
            var so = new SerializedObject(lane.GetComponent<SectorPartitionLane>());
            so.FindProperty("solidVisual").objectReferenceValue = line; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(lane, SectorAdministratorAuthoring.Lane);
        }
        finally { PrefabUtility.UnloadPrefabContents(lane); }
        var missile = PrefabUtility.LoadPrefabContents(SectorPatternAuthoring.Missile);
        try
        {
            // +25% over the accepted enemy visual. Root and radius .08 stay identical.
            var visual = missile.GetComponentInChildren<SpriteRenderer>();
            visual.transform.localScale = new Vector3(.32f, .6f, 2f);
            missile.transform.Find("HostileRedOutline").localScale = Vector3.one * 1.25f;
            PrefabUtility.SaveAsPrefabAsset(missile, SectorPatternAuthoring.Missile);
        }
        finally { PrefabUtility.UnloadPrefabContents(missile); }
        var charger = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Charger);
        var definition = new SerializedObject(charger);
        definition.FindProperty("predictiveShotChance").floatValue = 0;
        definition.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(charger);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/SectorPhaseIdentity");
        File.WriteAllText("Logs/SectorPhaseIdentity/authored.txt", "Region A energy accents, same-lane phase visual, enemy missile +25% visual only; ordinary Charge_enemy prediction chance zero. No scenes/textures/shaders authored.");
    }
}
