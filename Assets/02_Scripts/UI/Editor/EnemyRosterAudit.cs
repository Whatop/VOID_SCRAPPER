using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only inspection of the current saved assets. Never substitutes runtime owners or saves a scene.
public static class EnemyRosterAudit
{
    public const string Output = "Logs/EnemyRoster/";
    public const string ScenePath = "Assets/01_Scenes/Expedition.unity";
    public static EnemyDefinition[] Definitions => AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/02_Scripts" })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<EnemyDefinition>).ToArray();
    public static T Single<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();
    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    public static string Reference(UnityEngine.Object o)
    {
        if (o == null) return "NULL";
        string asset = AssetDatabase.GetAssetPath(o);
        return asset + " :: " + (o is Component c ? PathOf(c.transform) + " [" + c.GetType().Name + "]" : o is GameObject g ? PathOf(g.transform) : o.name);
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        var text = new StringBuilder();
        foreach (EnemyDefinition d in Definitions)
        {
            text.AppendLine(AssetDatabase.GetAssetPath(d));
            text.AppendLine($"{d.EnemyId} | {d.DisplayName} | {d.EnemyType} | HP={d.MaxHp} Move={d.MoveSpeed} Vision={d.VisionRange} Range={d.AttackRange} Interval={d.AttackInterval} Pattern={d.RangedAttackPattern} Shots={d.ProjectileCount} Spread={d.SpreadAngle} Charge={d.ChargeTime} Predictive={d.PredictiveShotChance}");
            text.AppendLine("Prefab: " + Reference(d.EnemyPrefab));
            text.AppendLine("Projectile: " + Reference(d.ProjectileDefinition) + " Split: " + Reference(d.SplitProjectileDefinition));
            text.AppendLine("Reward: " + Reference(d.RewardDefinition));
        }
        File.WriteAllText(Output + "definitions-unity.txt", text.ToString());
        foreach (var asset in Definitions.SelectMany(d => new UnityEngine.Object[] { d.ProjectileDefinition, d.SplitProjectileDefinition, d.RewardDefinition }).Where(a => a != null).Distinct())
            File.WriteAllText(Output + "data-" + asset.name + ".txt", Reference(asset) + "\n" + Fields(asset));
        string[] extra = { "Assets/03_Prefabs/Enemy/Enemy_Elite 1.prefab", "Assets/03_Prefabs/Enemy/Enemy_Elite 2.prefab",
            "Assets/03_Prefabs/Enemy/Enemy_Shotgun 1.prefab", "Assets/03_Prefabs/Enemy/Enemy_Shotgun 2.prefab",
            "Assets/03_Prefabs/Enemy/PF_Enemy_DefenderBasic.prefab", "Assets/03_Prefabs/Enemy/PF_Enemy_DefenderShotgun.prefab",
            "Assets/03_Prefabs/Enemy/PF_Enemy_MeleeCharger.prefab", "Assets/03_Prefabs/Enemy/PF_Enemy_MeleeCharger 1.prefab",
            "Assets/03_Prefabs/Enemy/PF_FactionBase_A.prefab", "Assets/03_Prefabs/Enemy/PF_FactionBase_B.prefab",
            "Assets/03_Prefabs/Enemy/PF_FieldBasePowerLink.prefab", "Assets/03_Prefabs/Enemy/PF_ShopStructure.prefab",
            "Assets/03_Prefabs/Turret.prefab", "Assets/03_Prefabs/NPC/NPC_RescueContact.prefab" };
        foreach (string path in Definitions.Where(d => d.EnemyPrefab != null).Select(d => AssetDatabase.GetAssetPath(d.EnemyPrefab)).Concat(extra).Distinct())
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing audit prefab: " + path);
            File.WriteAllText(Output + Path.GetFileNameWithoutExtension(path) + ".txt", Dump(prefab));
        }
        Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var generator = Single<ExpeditionMapGenerator>(scene);
            File.WriteAllText(Output + "generator-bindings.txt", Fields(generator));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("ENEMY_ROSTER_AUDIT: saved definitions, prefab hierarchy/bindings and Expedition generator inspected without asset mutation.");
    }
    public static string Fields(UnityEngine.Object owner)
    {
        var text = new StringBuilder();
        var property = new SerializedObject(owner).GetIterator();
        bool enter = true;
        while (property.Next(enter))
        {
            enter = property.propertyType == SerializedPropertyType.Generic;
            if (property.name.StartsWith("m_")) { enter = false; continue; }
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                text.AppendLine(property.propertyPath + " = " + Reference(property.objectReferenceValue));
            else if (property.propertyType == SerializedPropertyType.Boolean) text.AppendLine(property.propertyPath + " = " + property.boolValue);
            else if (property.propertyType == SerializedPropertyType.Integer || property.propertyType == SerializedPropertyType.Enum) text.AppendLine(property.propertyPath + " = " + property.intValue);
            else if (property.propertyType == SerializedPropertyType.Float) text.AppendLine(property.propertyPath + " = " + property.floatValue);
            else if (property.propertyType == SerializedPropertyType.String) text.AppendLine(property.propertyPath + " = " + property.stringValue);
            else if (property.propertyType == SerializedPropertyType.Vector2) text.AppendLine(property.propertyPath + " = " + property.vector2Value);
            else if (property.propertyType == SerializedPropertyType.Vector3) text.AppendLine(property.propertyPath + " = " + property.vector3Value);
            else if (property.propertyType == SerializedPropertyType.LayerMask) text.AppendLine(property.propertyPath + " = " + property.intValue);
            else if (property.propertyType == SerializedPropertyType.Color) text.AppendLine(property.propertyPath + " = " + property.colorValue);
        }
        return text.ToString();
    }
    private static string Dump(GameObject prefab)
    {
        var text = new StringBuilder();
        foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
        {
            text.AppendLine("\n" + PathOf(t) + " position=" + t.localPosition + " world=" + t.position + " scale=" + t.localScale + " rotation=" + t.localEulerAngles + " active=" + t.gameObject.activeSelf + " layer=" + LayerMask.LayerToName(t.gameObject.layer));
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) { text.AppendLine("MISSING SCRIPT"); continue; }
                if (c is Transform) continue;
                if (c is SpriteRenderer s)
                {
                    text.AppendLine("Sprite=" + Reference(s.sprite) + " order=" + s.sortingOrder + " color=" + s.color);
                    if (s.sprite != null)
                    {
                        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s.sprite)) as TextureImporter;
                        text.AppendLine("  rect=" + s.sprite.rect + " PPU=" + s.sprite.pixelsPerUnit + " filter=" + importer?.filterMode + " mipmaps=" + importer?.mipmapEnabled + " compression=" + importer?.textureCompression);
                    }
                }
                else if (c is Collider2D collider)
                    text.AppendLine(c.GetType().Name + " enabled=" + collider.enabled + " trigger=" + collider.isTrigger + " offset=" + collider.offset + " bounds=" + collider.bounds);
                else if (c is MonoBehaviour || c is Collider2D) { text.AppendLine(c.GetType().Name); text.Append(Fields(c)); }
            }
        }
        return text.ToString();
    }
}
