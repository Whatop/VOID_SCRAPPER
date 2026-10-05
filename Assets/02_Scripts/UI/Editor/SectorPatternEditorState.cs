using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only state snapshot after import; never authors or runs without an explicit request.
[InitializeOnLoad]
public static class SectorPatternEditorState
{
    static SectorPatternEditorState() { EditorApplication.delayCall += Snapshot; }
    public static void Snapshot()
    {
        Directory.CreateDirectory("Logs/SectorPattern");
        var lines = new List<string> { "Playing=" + EditorApplication.isPlayingOrWillChangePlaymode };
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            lines.Add("Scene=" + scene.path + "; Dirty=" + scene.isDirty);
        }
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        lines.Add(stage == null ? "PrefabStage=none" : "PrefabStage=" + stage.assetPath + "; Dirty=" + stage.scene.isDirty);
        File.WriteAllLines("Logs/SectorPattern/editor-state.txt", lines);
    }
}
