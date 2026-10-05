using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FeedbackPolish2Authoring
{
    public const string Rival = "Assets/03_Prefabs/Enemy/PF_Enemy_RivalHarvester.prefab";
    public const string Scavenger = "Assets/03_Prefabs/Enemy/PF_Enemy_Scavenger.prefab";
    [MenuItem("Tools/VOID SCRAPPER/Author Theft Channel Feedback")]
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && (stage.assetPath == Rival || stage.assetPath == Scavenger))
            throw new InvalidOperationException("Close the thief prefab stage before authoring its saved asset.");
        AuthorOne(Rival, new Color(1, .8f, .25f, 1));
        AuthorOne(Scavenger, new Color(1, .45f, .85f, 1));
    }
    static void AuthorOne(string path, Color color)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var child = root.transform.Find("PickupCollection");
            if (child == null)
            {
                child = new GameObject("PickupCollection").transform;
                child.SetParent(root.transform, false);
            }
            var view = child.GetComponent<PickupCollectionPresentation>();
            if (view == null) view = child.gameObject.AddComponent<PickupCollectionPresentation>();
            var so = new SerializedObject(view);
            so.FindProperty("tether").objectReferenceValue = Line(child, "Tether", .045f);
            so.FindProperty("progressRing").objectReferenceValue = Line(child, "Progress", .055f);
            so.FindProperty("channelColor").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(root.GetComponent<EnemyRoleController>());
            so.FindProperty("pickupCollectionPresentation").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static LineRenderer Line(Transform parent, string name, float width)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        var line = child.GetComponent<LineRenderer>();
        if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        line.useWorldSpace = true; line.loop = false; line.positionCount = 0;
        line.widthMultiplier = width; line.sortingOrder = 15;
        line.numCapVertices = 0; line.numCornerVertices = 0; line.enabled = false;
        return line;
    }
}
