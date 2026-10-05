using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Explicit saved-state authoring/audit; never runs on import.
public static class CommonPresentationAuthoring
{
    public const string EnemyFlash = "Assets/03_Prefabs/VFX/EnemyLightMuzzle.prefab";
    public static void Apply()
    {
        ApprovedVisualIntegration.Guard();
        var changes = new StringBuilder();
        foreach (string name in new[] { "MachineGun", "Shotgun", "Sniper" })
        {
            string path = "Assets/02_Scripts/Resources/VFX/Approved/" + name + "Muzzle.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var flash = root.GetComponent<PooledMuzzleFlash>() ?? root.AddComponent<PooledMuzzleFlash>();
                var so = new SerializedObject(flash); so.FindProperty("lifetime").floatValue = name == "MachineGun" ? .09f : .12f;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (name == "Shotgun") root.transform.localScale = Vector3.one * .65f;
                if (name == "Sniper") root.transform.localScale = Vector3.one * .35f;
                PrefabUtility.SaveAsPrefabAsset(root, path); changes.AppendLine(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var enemyFlash = PrefabUtility.LoadPrefabContents("Assets/03_Prefabs/VFX/RaiderAssaultMuzzle.prefab");
        try
        {
            enemyFlash.name = "EnemyLightMuzzle"; enemyFlash.transform.localScale = Vector3.one * .18f;
            var flash = enemyFlash.AddComponent<PooledMuzzleFlash>();
            var so = new SerializedObject(flash); so.FindProperty("lifetime").floatValue = .07f; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(enemyFlash, EnemyFlash); changes.AppendLine(EnemyFlash);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemyFlash); }
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFlash).GetComponent<PooledMuzzleFlash>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/03_Prefabs/Enemy" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<EnemyBaseAI>() == null || asset.GetComponent<EnemyAttackController>() == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(root.GetComponent<EnemyAttackController>());
                so.FindProperty("muzzleFlashPrefab").objectReferenceValue = enemyPrefab; so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path); changes.AppendLine(path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string name in new[] { "Tutorial", "Expedition", "Settlement" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var hud in root.GetComponentsInChildren<ExpeditionHUD>(true))
                {
                    var so = new SerializedObject(hud);
                    var gauge = (GaugeBarUI)so.FindProperty("hpGauge").objectReferenceValue;
                    var track = (Image)so.FindProperty("hpTrackImage").objectReferenceValue;
                    Slice(gauge.FillImage, 1); track.sprite = gauge.FillImage.sprite; Slice(track, 2);
                    var armor = (Image)so.FindProperty("armorFillImage").objectReferenceValue;
                    Slice(armor, .5f);
                }
                foreach (var hud in root.GetComponentsInChildren<BossHealthBarUI>(true))
                {
                    var so = new SerializedObject(hud); var fill = (Image)so.FindProperty("fillImage").objectReferenceValue;
                    Slice(fill, 1);
                    var slider = (Slider)so.FindProperty("hpSlider").objectReferenceValue;
                    Slice(slider.transform.Find("Background").GetComponent<Image>(), 2);
                    // Odd-height bars use half-pixel centers to put both edges on integer pixels.
                    var r = (RectTransform)slider.transform; r.anchoredPosition = new Vector2(r.anchoredPosition.x, -4.5f);
                    var value = (TextMeshProUGUI)so.FindProperty("hpText").objectReferenceValue;
                    value.enableAutoSizing = true; value.fontSizeMin = 7; value.fontSizeMax = 10;
                    value.textWrappingMode = TextWrappingModes.NoWrap;
                    var parts = so.FindProperty("triadPartFills");
                    for (int i = 0; i < parts.arraySize; i++)
                    {
                        var partFill = (Image)parts.GetArrayElementAtIndex(i).objectReferenceValue;
                        var partTrack = partFill.transform.parent.GetComponent<Image>();
                        partFill.sprite = fill.sprite; partTrack.sprite = fill.sprite;
                        Slice(partFill, 1); Slice(partTrack, 2);
                        var partRect = partTrack.rectTransform;
                        partRect.anchoredPosition = new Vector2(partRect.anchoredPosition.x, -2.5f);
                    }
                }
                if (name == "Settlement")
                    foreach (var gauge in root.GetComponentsInChildren<GaugeBarUI>(true))
                    {
                        if (!PathOf(gauge.transform).Contains("CombatStatus/HP") || gauge.FillImage == null) continue;
                        Slice(gauge.FillImage, 2);
                        var track = gauge.transform.Find("Track")?.GetComponent<Image>();
                        if (track != null) { track.sprite = gauge.FillImage.sprite; Slice(track, 3); }
                    }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changes.AppendLine(scene.path);
        }
        AssetDatabase.SaveAssets(); File.WriteAllText("Logs/CommonPresentation/authored.txt", changes.ToString());
    }
    static void Slice(Image image, float cornerPixels)
    {
        if (image == null || image.sprite == null) return;
        image.type = Image.Type.Sliced; image.preserveAspect = false;
        float reference = image.canvas != null ? image.canvas.referencePixelsPerUnit : 100;
        image.pixelsPerUnitMultiplier = image.sprite.border.x * reference / (image.sprite.pixelsPerUnit * cornerPixels);
        image.rectTransform.localScale = Vector3.one;
    }
    public static void Audit()
    {
        ApprovedVisualIntegration.Guard();
        var log = new StringBuilder();
        foreach (string sceneName in new[] { "Tutorial", "Expedition", "Settlement" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/" + sceneName + ".unity");
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<CanvasScaler>(true))
                    log.AppendLine(sceneName + " Canvas " + c.name + " mode=" + c.uiScaleMode + " reference=" + c.referenceResolution);
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    string path = PathOf(image.transform);
                    if (!path.ToLowerInvariant().Contains("hp") && !path.Contains("Armor") && !path.Contains("Triad")) continue;
                    var r = image.rectTransform; var sp = image.sprite;
                    log.AppendLine(sceneName + "/" + path + " type=" + image.type + " pos=" + r.anchoredPosition + " size=" + r.sizeDelta + " rect=" + r.rect + " anchors=" + r.anchorMin + "/" + r.anchorMax + " scale=" + r.localScale + " ppm=" + image.pixelsPerUnitMultiplier + " mask=" + (image.GetComponentInParent<Mask>() != null) + " sprite=" + AssetDatabase.GetAssetPath(sp) + " rect=" + (sp != null ? sp.rect.ToString() : "null") + " border=" + (sp != null ? sp.border.ToString() : "null") + " ppu=" + (sp != null ? sp.pixelsPerUnit : 0));
                }
            }
        }
        File.WriteAllText("Logs/CommonPresentation/audit-before.txt", log.ToString());
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
