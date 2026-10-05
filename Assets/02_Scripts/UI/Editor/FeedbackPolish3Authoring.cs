using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FeedbackPolish3Authoring
{
    [MenuItem("Tools/VOID SCRAPPER/Author Dash Teaching Feedback")]
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var scene = EditorSceneManager.OpenScene("Assets/01_Scenes/Tutorial.unity");
        TutorialFlowController flow = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            flow = root.GetComponentInChildren<TutorialFlowController>(true);
            if (flow != null) break;
        }
        if (flow == null) throw new InvalidOperationException("Tutorial flow missing");
        var child = flow.transform.Find("DashPracticeZone");
        if (child == null)
        {
            child = new GameObject("DashPracticeZone").transform;
            child.SetParent(flow.transform, false);
        }
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        line.useWorldSpace = false; line.loop = true; line.positionCount = 48;
        line.widthMultiplier = .055f; line.sortingOrder = 25;
        line.startColor = line.endColor = new Color(1f, .55f, .22f, .9f);
        for (int i = 0; i < 48; i++)
        {
            float angle = i * Mathf.PI * 2 / 48;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .8f);
        }
        line.enabled = false;
        var so = new SerializedObject(flow);
        so.FindProperty("dashPracticeMarker").objectReferenceValue = line;
        var entries = so.FindProperty("stepPresentations");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("step").intValue != (int)TutorialStep.Dash) continue;
            entry.FindPropertyRelative("instructionTemplate").stringValue =
                "표시된 위험 구역에서 {0} 대시로 벗어나세요.\n대시 중 잠깐 무적입니다.";
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
