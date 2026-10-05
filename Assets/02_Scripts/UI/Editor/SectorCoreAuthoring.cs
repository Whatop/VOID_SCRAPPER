using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SectorCoreAuthoring
{
    public const string Rectangle = "Assets/03_Prefabs/VFX/SectorRectangularAoE.prefab";
    public const string PlayerMissile = "Assets/02_Scripts/Resources/VFX/PF_Player_MG_DashMissile.prefab";
    static void Set(Object owner, string field, Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var t = parent.Find(name); if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        t.localPosition = position; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; return t;
    }
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        var root = new GameObject("SectorRectangularAoE");
        try
        {
            var effect = root.AddComponent<SectorRectangularAoE>(); var so = new SerializedObject(effect);
            foreach (string field in new[] { "borders", "fills" })
            {
                int count = field == "borders" ? 8 : 16;
                var array = so.FindProperty(field); array.arraySize = count;
                for (int i = 0; i < count; i++)
                {
                    var line = Child(root.transform, field + i, Vector3.zero).gameObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = material; line.useWorldSpace = false; line.loop = field == "borders";
                    line.positionCount = line.loop ? 4 : 2; line.numCapVertices = 0; line.numCornerVertices = 0;
                    line.sortingOrder = line.loop ? -1 : -3; line.enabled = false;
                    array.GetArrayElementAtIndex(i).objectReferenceValue = line;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, Rectangle);
        }
        finally { Object.DestroyImmediate(root); }

        // Edit only the enemy variant; the player's sprite, prefab and values are read-only.
        var missile = PrefabUtility.LoadPrefabContents(SectorPatternAuthoring.Missile);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerMissile).GetComponentInChildren<SpriteRenderer>();
            var visual = missile.GetComponentInChildren<SpriteRenderer>();
            visual.sprite = source.sprite; visual.color = Color.white; visual.sortingOrder = 5;
            visual.transform.localScale = source.transform.localScale * 1.6f;
            // The source sprite is cyan: multiplying it by red produces a dark rim.
            // A tiny authored oval line follows the same silhouette without a shader
            // or a generated texture. Geometry is saved once, never rebuilt in combat.
            var oldOutline = visual.transform.Find("HostileRedOutline");
            if (oldOutline != null) Object.DestroyImmediate(oldOutline.gameObject);
            var outline = Child(missile.transform, "HostileRedOutline", Vector3.zero);
            var line = outline.GetComponent<LineRenderer>(); if (line == null) line = outline.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = true;
            line.positionCount = 12; line.numCapVertices = 0; line.numCornerVertices = 0;
            for (int i = 0; i < 12; i++)
            {
                float angle = Mathf.PI * 2 * i / 12;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * .085f, Mathf.Sin(angle) * .17f, 0));
            }
            line.widthMultiplier = .055f; line.startColor = line.endColor = new Color(1, .035f, .02f, 1);
            line.sortingOrder = 6;
            var trail = missile.GetComponent<TrailRenderer>();
            trail.time = .16f; trail.widthMultiplier = .075f; trail.sortingOrder = 3;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1, .055f, .025f), 0), new GradientColorKey(new Color(1, .03f, .02f), 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = gradient;
            PrefabUtility.SaveAsPrefabAsset(missile, SectorPatternAuthoring.Missile);
        }
        finally { PrefabUtility.UnloadPrefabContents(missile); }

        var boss = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var c = boss.GetComponent<BossPatternController>(); var visual = boss.transform.Find("BossVisualRoot");
            Set(c, "sectorLeftMissileLauncher", Child(visual, "LeftMissileLauncher", new Vector3(-.57f, .06f, 0)));
            Set(c, "sectorRightMissileLauncher", Child(visual, "RightMissileLauncher", new Vector3(.57f, .06f, 0)));
            Set(c, "sectorRearMissileLauncher", Child(visual, "RearMissileLauncher", new Vector3(0, -.78f, 0)));
            Set(c, "sectorRectangularPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Rectangle).GetComponent<SectorRectangularAoE>());
            PrefabUtility.SaveAsPrefabAsset(boss, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        Directory.CreateDirectory("Logs/SectorCoreRework");
        File.WriteAllText("Logs/SectorCoreRework/authored.txt", "Boss: three missile attachments + rectangular prefab binding. Enemy missile: player's same sprite + red outline/trail. New bounded pooled rectangular presentation. No scenes, player assets or shaders modified.");
        AssetDatabase.SaveAssets();
    }
}
