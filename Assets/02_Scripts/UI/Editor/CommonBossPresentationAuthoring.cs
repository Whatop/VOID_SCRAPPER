using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class CommonBossPresentationAuthoring
{
    public static void Apply()
    {
        ApprovedVisualIntegration.Guard();
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Expedition.unity");
        var hud = Object.FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
        var s = new SerializedObject(hud);
        var triad = (RectTransform)s.FindProperty("triadPresentationRoot").objectReferenceValue;
        File.WriteAllLines("Logs/CommonBossPresentation/authored-before.txt", triad.GetComponentsInChildren<RectTransform>(true).Select(r => r.name + " " + r.anchoredPosition + " " + r.sizeDelta));
        Layout(triad, Vector2.zero, new Vector2(196, 28));
        var title = (TextMeshProUGUI)s.FindProperty("triadBossNameText").objectReferenceValue;
        Layout(title.rectTransform, new Vector2(0, 12), new Vector2(196, 14));
        title.fontSize = 10; title.enableAutoSizing = false; title.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < 3; i++)
        {
            var fill = (Image)s.FindProperty("triadPartFills").GetArrayElementAtIndex(i).objectReferenceValue;
            var bar = (RectTransform)fill.transform.parent;
            Layout(bar, new Vector2((i - 1) * 64, -2), new Vector2(60, 5));
            var rect = fill.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(1, 0); rect.sizeDelta = new Vector2(58, 3); rect.localScale = Vector3.one;
            var label = (TextMeshProUGUI)s.FindProperty("triadPartLabels").GetArrayElementAtIndex(i).objectReferenceValue;
            Layout(label.rectTransform, new Vector2((i - 1) * 64, -10), new Vector2(58, 10));
            label.fontSize = 8; label.enableAutoSizing = false; label.alignment = TextAlignmentOptions.Center;
        }
        s.FindProperty("horizontalTriadBars").boolValue = true; s.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        string path = PhaseGatekeeperAuthoring.Boss;
        var boss = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(boss.GetComponent<PhaseGatekeeperBossController>());
            data.FindProperty("normalCombatCameraZoomMultiplier").floatValue = 1f;
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(boss, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Logs/CommonBossPresentation/authored.txt", "Existing triad HUD objects retained: three 58x3 horizontal fills with integer insets. Region C normal camera multiplier 1.25 -> 1.0. No collision/combat data edited.");
    }
    static void Layout(RectTransform rect, Vector2 position, Vector2 size)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; }
}
