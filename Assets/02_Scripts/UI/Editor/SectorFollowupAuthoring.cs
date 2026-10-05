using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SectorFollowupAuthoring
{
    public const string Muzzle = "Assets/02_Scripts/Resources/VFX/Approved/MachineGunMuzzle.prefab";
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var muzzle = PrefabUtility.LoadPrefabContents(Muzzle);
        try { muzzle.transform.localScale = Vector3.one * .55f; PrefabUtility.SaveAsPrefabAsset(muzzle, Muzzle); }
        finally { PrefabUtility.UnloadPrefabContents(muzzle); }
        var lane = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Lane);
        try
        {
            lane.GetComponentInChildren<SpriteRenderer>(true).sortingOrder = -2;
            PrefabUtility.SaveAsPrefabAsset(lane, SectorAdministratorAuthoring.Lane);
        }
        finally { PrefabUtility.UnloadPrefabContents(lane); }
        var boss = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var so = new SerializedObject(boss.GetComponent<BossPatternController>());
            so.FindProperty("sectorAngularSpeed").floatValue = 30;
            so.FindProperty("sectorEscalationWarningTime").floatValue = 1.2f;
            so.FindProperty("sectorBurstCadence").floatValue = .16f;
            so.FindProperty("sectorBurstGap").floatValue = .28f;
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(boss, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        foreach (string name in new[] { "Tutorial", "Expedition" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + name + ".unity");
            foreach (var hud in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExpeditionHUD>(true)))
            {
                var so = new SerializedObject(hud);
                var gauge = (GaugeBarUI)so.FindProperty("hpGauge").objectReferenceValue;
                if (gauge == null) continue;
                var area = (RectTransform)gauge.FillImage.transform.parent;
                Layout(area, new Vector2(0, -4.5f), new Vector2(84, 3));
                var track = (Image)so.FindProperty("hpTrackImage").objectReferenceValue;
                Layout(track.rectTransform, new Vector2(0, -4.5f), new Vector2(86, 5));
                var armorTrack = (Image)so.FindProperty("armorTrackImage").objectReferenceValue;
                var armorFill = (RectTransform)so.FindProperty("armorFillRect").objectReferenceValue;
                Layout(armorTrack.rectTransform, new Vector2(0, -8.5f), new Vector2(84, 1));
                armorFill.SetParent(armorTrack.transform, false);
                armorFill.anchorMin = Vector2.zero; armorFill.anchorMax = Vector2.one;
                armorFill.offsetMin = armorFill.offsetMax = Vector2.zero;
                armorFill.localScale = Vector3.one;
                EditorUtility.SetDirty(hud);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/SectorFollowup/authored.txt", "Muzzle root scale .55; HP inner 84x3; Armor 84x1 at panel-local y -8.5. Original sprite, prefab, scene IDs and event bindings retained.");
    }
    static void Layout(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }
}
