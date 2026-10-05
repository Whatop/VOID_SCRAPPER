using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Read-only snapshots of resolved production assets. Does not install or save content.
public static class BossFinalPolishAudit
{
    public const string Output = "Logs/BossFinalPolish/";
    public static readonly string[] BossPrefabs = {
        "Assets/03_Prefabs/Enemy/Boss.prefab",
        "Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab",
        "Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab",
        "Assets/03_Prefabs/Enemy/PF_Boss_NullDispatcher.prefab",
        "Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab"
    };
    public static void Run()
    {
        Directory.CreateDirectory(Output + "Snapshots");
        string[] related = {
            "Assets/03_Prefabs/Object/Core.prefab", "Assets/03_Prefabs/Object/ReturnBeacon.prefab",
            "Assets/03_Prefabs/Object/WormholePortal.prefab",
            "Assets/03_Prefabs/Object/PF_Region2BossCorridorRuntime.prefab",
            "Assets/03_Prefabs/Object/PF_Region3_PhaseReflectorPlate.prefab",
            "Assets/03_Prefabs/Enemy/PF_NullDispatcher_HostilePacket.prefab",
            "Assets/03_Prefabs/Enemy/PF_NullDispatcher_SupportPacket.prefab",
            "Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_WarningArea.prefab",
            "Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_LaserHazard.prefab"
        };
        foreach (string path in BossPrefabs.Concat(related))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new InvalidOperationException("Missing production asset: " + path);
            File.WriteAllText(Output + "Snapshots/" + root.name + ".txt", Dump(root));
        }
        var assets = AssetDatabase.GetDependencies(BossPrefabs.Concat(related).ToArray(), true)
            .Where(p => p.EndsWith(".asset")).Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
            .Where(o => o is BossCampaignDefinition || o is ProjectileDefinition || o is RewardDefinition || o is EnemyDefinition).ToArray();
        foreach (var asset in assets)
            File.WriteAllText(Output + "Snapshots/" + asset.name + ".txt", AssetDatabase.GetAssetPath(asset) + "\n" + EnemyRosterAudit.Fields(asset));
        var scene = EditorSceneManager.OpenPreviewScene(EnemyRosterAudit.ScenePath);
        try
        {
            var text = new StringBuilder();
            foreach (var component in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
                if (component is ExpeditionMapGenerator || component is ExpeditionHUD || component is BossHealthBarUI || component is ExpeditionOperationController)
                    text.AppendLine(EnemyRosterAudit.PathOf(component.transform) + " [" + component.GetType().Name + "]\n" + EnemyRosterAudit.Fields(component));
            File.WriteAllText(Output + "Snapshots/Expedition-bindings.txt", text.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("BOSS_FINAL_AUDIT: production prefabs, hierarchy, campaign/projectile/reward assets and saved Expedition bindings recorded without asset mutation.");
    }
    private static string Dump(GameObject root)
    {
        var text = new StringBuilder(AssetDatabase.GetAssetPath(root) + "\n");
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            text.AppendLine("\n" + EnemyRosterAudit.PathOf(t) + " active=" + t.gameObject.activeSelf + " position=" + t.localPosition + " scale=" + t.localScale + " rotation=" + t.localEulerAngles);
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) throw new InvalidOperationException("Missing script at " + EnemyRosterAudit.PathOf(t));
                if (c is MonoBehaviour)
                    text.AppendLine(c.GetType().Name + "\n" + EnemyRosterAudit.Fields(c));
                else if (c is SpriteRenderer s)
                    text.AppendLine("Sprite=" + EnemyRosterAudit.Reference(s.sprite) + " color=" + s.color + " sorting=" + s.sortingOrder);
                else if (c is Collider2D collider)
                    text.AppendLine(c.GetType().Name + " trigger=" + collider.isTrigger + " enabled=" + collider.enabled + " offset=" + collider.offset);
            }
        }
        return text.ToString();
    }
}
