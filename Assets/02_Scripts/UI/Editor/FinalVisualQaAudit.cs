using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FinalVisualQaAudit
{
    const string Dir = "Logs/FinalVisualQA/";
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Expedition.unity", OpenSceneMode.Single);
        var warning = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WarningMessageUI>(true)).Single();
        var rect = (RectTransform)warning.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f); rect.anchoredPosition = new Vector2(0, -32);
        rect.sizeDelta = new Vector2(220, 24);
        var so = new SerializedObject(warning);
        so.FindProperty("maximumHeight").floatValue = 24;
        var text = (TextMeshProUGUI)so.FindProperty("messageText").objectReferenceValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.enableAutoSizing = true; text.fontSizeMin = 5.5f; text.fontSizeMax = 7;
        text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis;
        text.maxVisibleLines = 3; text.alignment = TextAlignmentOptions.Center;
        var bossHud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BossHealthBarUI>(true)).Single();
        var bossRoot = (GameObject)new SerializedObject(bossHud).FindProperty("rootObject").objectReferenceValue;
        ((RectTransform)bossRoot.transform).anchoredPosition = new Vector2(0, 26);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        NullArtAuthoring.RepairCoreFrames();
        File.WriteAllText(Dir + "authored.txt", "Expedition existing WarningMessage and child text: top-center (0,-32), 220x24. BossHealthBarRoot: y44 -> y26 to clear bottom rain lane. Same bindings, priorities, durations and colors; no new scene objects. Purple Core relay/ending sheets sliced to authored cells with stable asset GUIDs.\n");
    }
    public static void Run()
    {
        ApprovedVisualIntegration.Guard();
        var lines = new List<string>(); int missingScripts = 0, spriteCount = 0;
        foreach (string file in Directory.GetFiles("Assets/03_Prefabs", "*.prefab", SearchOption.AllDirectories))
        {
            var root = PrefabUtility.LoadPrefabContents(file);
            try { Inspect(file, root, lines, ref missingScripts, ref spriteCount); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string name in new[] { "Boot", "Tutorial", "Settlement", "Expedition" })
        {
            string path = "Assets/01_Scenes/" + name + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects()) Inspect(path, root, lines, ref missingScripts, ref spriteCount);
        }
        lines.Insert(0, "Missing scripts=" + missingScripts + "; SpriteRenderer components inspected=" + spriteCount);
        File.WriteAllLines(Dir + "unity-reference-audit.txt", lines);
        if (missingScripts > 0) throw new Exception("Missing scripts: " + missingScripts);
        Debug.Log("FINAL_VISUAL_QA_AUDIT PASS: zero missing scripts; null renderer roles listed for context.");
    }
    static void Inspect(string file, GameObject root, List<string> lines, ref int scripts, ref int sprites)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            scripts += count;
            if (count > 0) lines.Add("MISSING SCRIPT " + file + " / " + ApprovedVisualIntegration.PathOf(t));
        }
        foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sprites++;
            if (r.sprite == null) lines.Add("NULL RENDERER " + file + " / " + ApprovedVisualIntegration.PathOf(r.transform));
        }
    }
}
