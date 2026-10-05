using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Narrow saved-prefab correction. Does not rebuild encounter hierarchy or change attack tuning.
public static class BossFinalPolishAuthoring
{
    public static void ApplyFrigateFraming()
    {
        if (EditorApplication.isPlaying || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before saved boss authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scenes before saved boss authoring.");
        string path = BossFinalPolishAudit.BossPrefabs[1];
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var controller = root.GetComponent<FrigateTriadBossController>();
            var data = new SerializedObject(controller);
            // Preserve the formation shape/X positions. Full 188px sprite at 100 PPU and 1.65 scale
            // needs 1.551 units above its pivot, inside the saved 5.0625-unit camera half-height.
            Set(data, "threeAliveLeftOffset", -2.55f, 2.70f);
            Set(data, "threeAliveCenterOffset", 0f, 3.05f);
            Set(data, "threeAliveRightOffset", 2.55f, 2.70f);
            Set(data, "twoAliveLeftOffset", -1.65f, 2.80f);
            Set(data, "twoAliveRightOffset", 1.65f, 2.80f);
            Set(data, "oneAliveOffset", 0f, 2.90f);
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var part in root.GetComponentsInChildren<FrigateBossPart>(true))
            {
                string field = part.PartId == FrigateBossPartId.Left ? "threeAliveLeftOffset" :
                    part.PartId == FrigateBossPartId.Center ? "threeAliveCenterOffset" : "threeAliveRightOffset";
                Vector2 p = data.FindProperty(field).vector2Value;
                part.transform.localPosition = new Vector3(p.x, p.y, part.transform.localPosition.z);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("BOSS_FINAL_AUTHORING: saved six formation offsets and three matching initial transforms; all combat values preserved.");
    }
    static void Set(SerializedObject data, string field, float x, float y) => data.FindProperty(field).vector2Value = new Vector2(x, y);
}
