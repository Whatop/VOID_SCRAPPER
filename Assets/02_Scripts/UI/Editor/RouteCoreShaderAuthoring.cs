using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Saved authoring only. Does not touch renderer assets, combat tuning or vendor content.</summary>
public static class RouteCoreShaderAuthoring
{
    public const string GraphPath = "Assets/02_Scripts/VFX/RouteCore/SG_RouteCoreCorruption.shadergraph";
    public const string MaterialPath = "Assets/02_Scripts/VFX/RouteCore/MAT_RouteCoreCorruption.mat";
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before saved authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
        EnsureMaterial();
        var scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath);
        Apply(RouteCoreDeckAuthoring.Single<SettlementDefensePurpleCore>(scene));
        Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("ROUTE_CORE_SHADER: Sprite Unlit overlay saved; original base sprite/material and all combat values preserved.");
    }

    private static void EnsureMaterial()
    {
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException(GraphPath + ": shader import failed.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        else if (material.shader != shader) throw new InvalidOperationException(MaterialPath + ": unexpected shader; refusing to overwrite authored material.");
        Debug.Log("ROUTE_CORE_SHADER: graph=" + shader.name + ", material=" + MaterialPath);
    }

    public static void Apply(SettlementDefensePurpleCore purple)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) throw new InvalidOperationException(MaterialPath + ": missing authored material. Run RouteCoreShaderAuthoring.Run.");
        var original = RouteCoreDeckAuthoring.Ref<SpriteRenderer>(purple, "visual");
        var child = purple.transform.Find("CorruptionOverlay");
        if (child == null)
        {
            var go = new GameObject("CorruptionOverlay");
            SceneManager.MoveGameObjectToScene(go, purple.gameObject.scene);
            child = go.transform; child.SetParent(purple.transform, false);
        }
        child.localPosition = original.transform.localPosition;
        child.localRotation = original.transform.localRotation;
        child.localScale = original.transform.localScale;
        child.gameObject.layer = original.gameObject.layer;
        var overlay = child.GetComponent<SpriteRenderer>();
        if (overlay == null) overlay = child.gameObject.AddComponent<SpriteRenderer>();
        overlay.sprite = original.sprite; overlay.sharedMaterial = material; overlay.color = Color.white;
        overlay.sortingLayerID = original.sortingLayerID; overlay.sortingOrder = original.sortingOrder + 1;
        overlay.enabled = false;
        var presentation = purple.GetComponent<RouteCoreCorruptionVisual>();
        if (presentation == null) presentation = purple.gameObject.AddComponent<RouteCoreCorruptionVisual>();
        RouteCoreCombatAuthoring.Set(presentation, "overlay", overlay);
        RouteCoreCombatAuthoring.Set(purple, "corruptionPresentation", presentation);
    }

    public static void Validate(Scene scene)
    {
        var purple = RouteCoreDeckAuthoring.Single<SettlementDefensePurpleCore>(scene);
        var presentation = RouteCoreDeckAuthoring.Ref<RouteCoreCorruptionVisual>(purple, "corruptionPresentation");
        if (presentation == null || !presentation.HasAuthoredOverlay)
            throw new InvalidOperationException("RouteCoreDeck/PurpleCorruptionTarget/CorruptionOverlay: invalid authored shader presentation.");
        var overlay = RouteCoreDeckAuthoring.Ref<SpriteRenderer>(presentation, "overlay");
        if (overlay.sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) || overlay.GetComponent<Collider2D>() != null)
            throw new InvalidOperationException("RouteCoreDeck/PurpleCorruptionTarget/CorruptionOverlay: expected dedicated material and no collider.");
    }
}
