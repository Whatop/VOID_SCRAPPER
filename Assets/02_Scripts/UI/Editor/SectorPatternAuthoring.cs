using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Region A-only saved authoring. Reuses existing art/materials without importer writes.
public static class SectorPatternAuthoring
{
    public const string Missile = "Assets/03_Prefabs/VFX/SectorGuidedMissile.prefab";
    public const string Zone = "Assets/03_Prefabs/VFX/SectorDropZone.prefab";
    public const string Counter = "Assets/03_Prefabs/VFX/SectorCounterLane.prefab";
    public const string Marker = "Assets/03_Prefabs/VFX/SectorSpawnMarker.prefab";
    public const string Pulse = "Assets/03_Prefabs/VFX/SectorPulse.prefab";
    static void Set(Object owner, string field, Object value)
    {
        var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Number(Object owner, string field, float value)
    {
        var so = new SerializedObject(owner); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static LineRenderer Line(Transform parent, string name, Material material, int points, bool loop, bool world)
    {
        var child = new GameObject(name); child.transform.SetParent(parent, false);
        var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material;
        line.useWorldSpace = world; line.loop = loop; line.positionCount = points;
        line.numCapVertices = 0; line.numCornerVertices = 0; line.sortingOrder = -2;
        line.widthMultiplier = .06f; line.enabled = false;
        if (loop) for (int i = 0; i < points; i++)
        {
            float angle = Mathf.PI * 2 * i / points;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
        }
        else { line.SetPosition(0, Vector3.zero); line.SetPosition(1, Vector3.right); }
        return line;
    }
    static SpriteRenderer Glyph(Transform parent, Sprite sprite)
    {
        var child = new GameObject("WarningGlyph"); child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = -1;
        child.transform.localScale = Vector3.one * (.4f / sprite.bounds.size.y); renderer.enabled = false;
        return renderer;
    }
    static void SaveNew(string path, Action<GameObject> build)
    {
        // Authoring may be rerun before testing. These task-owned prefabs are rebuilt
        // in place through Unity, preserving the asset GUIDs.
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try { build(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { Object.DestroyImmediate(root); }
    }
    public static void Author()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && (stage.assetPath == SectorAdministratorAuthoring.Boss || stage.assetPath.StartsWith("Assets/03_Prefabs/VFX/Sector")))
            throw new InvalidOperationException("The Region A prefab is open in Prefab Mode; preserve its Editor state before authoring.");
        // The legacy Beam texture has a narrow alpha core. A flat existing Unity
        // sprite material makes the purple ray's visible width match its collider.
        var material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        var glyph = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Dark UI/New Icons/White Warning.png");
        if (material == null || glyph == null) throw new InvalidOperationException("Existing Region A material or warning sprite missing.");
        SaveNew(Marker, root =>
        {
            var effect = root.AddComponent<SectorPulseVfx>();
            Set(effect, "ring", Line(root.transform, "Ring", material, 40, true, false));
            Set(effect, "glyph", Glyph(root.transform, glyph));
            var so = new SerializedObject(effect); so.FindProperty("holdOpacity").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
        });
        SaveNew(Pulse, root =>
        {
            var effect = root.AddComponent<SectorPulseVfx>();
            Set(effect, "ring", Line(root.transform, "Ring", material, 48, true, false));
        });
        SaveNew(Zone, root =>
        {
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var collider = root.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.enabled = false;
            var zone = root.AddComponent<SectorDropZone>(); Set(zone, "damageCollider", collider);
            Set(zone, "ring", Line(root.transform, "Ring", material, 48, true, false));
            Set(zone, "marker", Glyph(root.transform, glyph));
            var projection = Line(root.transform, "CoreProjection", material, 2, false, true);
            projection.widthMultiplier = .025f; projection.startColor = projection.endColor = new Color(1, .12f, .1f, .4f);
            Set(zone, "projection", projection);
        });
        SaveNew(Counter, root =>
        {
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var collider = root.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.enabled = false;
            var lane = root.AddComponent<SectorPartitionLane>(); Set(lane, "damageCollider", collider);
            Set(lane, "solidVisual", Line(root.transform, "PurpleBeam", material, 2, false, false));
        });
        var missile = PrefabUtility.LoadPrefabContents("Assets/02_Scripts/Resources/VFX/PF_Player_MG_DashMissile.prefab");
        try
        {
            missile.name = "SectorGuidedMissile";
            var bullet = missile.GetComponent<Bullet>(); Number(bullet, "fallbackLifeTime", 3.2f);
            var visual = missile.GetComponentInChildren<SpriteRenderer>();
            visual.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Space Kit/Missiles/Missile (1).png");
            visual.transform.localScale = Vector3.one * (.45f / visual.sprite.bounds.size.y);
            visual.color = new Color(1, .68f, .24f, 1); visual.sortingOrder = 5;
            missile.GetComponent<CircleCollider2D>().radius = .08f;
            var trail = missile.GetComponent<TrailRenderer>();
            if (trail != null) { trail.time = .12f; trail.widthMultiplier = .045f; trail.startColor = new Color(1, .5f, .12f, .8f); trail.endColor = new Color(1, .3f, .08f, 0); }
            PrefabUtility.SaveAsPrefabAsset(missile, Missile);
        }
        finally { PrefabUtility.UnloadPrefabContents(missile); }
        var boss = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var controller = boss.GetComponent<BossPatternController>();
            Set(controller, "sectorMissilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Missile).GetComponent<Bullet>());
            Set(controller, "sectorDropZonePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Zone).GetComponent<SectorDropZone>());
            Set(controller, "sectorSpawnMarkerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Marker).GetComponent<SectorPulseVfx>());
            Set(controller, "sectorPulsePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Pulse).GetComponent<SectorPulseVfx>());
            Number(controller, "sectorBurstGap", .32f);
            PrefabUtility.SaveAsPrefabAsset(boss, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        Directory.CreateDirectory("Logs/SectorPattern");
        File.WriteAllText("Logs/SectorPattern/authored.txt", "Region A boss + five pooled prefabs; existing sprite/material references only. No art/importer/shader/scene/shared projectile changes.");
    }
}
