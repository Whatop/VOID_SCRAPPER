using System.IO;
using UnityEditor;
using UnityEngine;

// Saved Region A prefab only. No texture/importer, scene or shared VFX writes.
public static class SectorRefineAuthoring
{
    public const string Cannon = "Assets/Art/SectorAdministrator/SectorCenterCannon.asset";
    public static void Author()
    {
        ApprovedVisualIntegration.Guard();
        var source = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ApprovedIntegration/World/SectorAdministrator.png");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Cannon);
        if (sprite == null)
        {
            // The existing upper center diamond/barrel, referenced from the approved texture.
            sprite = Sprite.Create(source.texture, new Rect(49, 80, 30, 32), new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "SectorCenterCannon";
            AssetDatabase.CreateAsset(sprite, Cannon);
        }
        var root = PrefabUtility.LoadPrefabContents(SectorAdministratorAuthoring.Boss);
        try
        {
            var boss = root.GetComponent<BossPatternController>();
            // Facing belongs to BossVisualRoot. Contact impulses must not turn
            // the physics root after the intro hands the dynamic body back.
            root.GetComponent<Rigidbody2D>().constraints |= RigidbodyConstraints2D.FreezeRotation;
            var body = root.transform.Find("BossVisualRoot");
            float ppu = source.pixelsPerUnit;
            var left = Child(body, "LeftSuppressionMuzzle", new Vector3(-43 / ppu, 50 / ppu, 0));
            var right = Child(body, "RightSuppressionMuzzle", new Vector3(43 / ppu, 50 / ppu, 0));
            var center = Child(body, "CenterChargeCannon", new Vector3(0, 32 / ppu, 0));
            center.localScale = Vector3.one * 1.25f;
            var renderer = center.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = center.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = body.GetComponent<SpriteRenderer>().sharedMaterial;
            renderer.sortingOrder = body.GetComponent<SpriteRenderer>().sortingOrder + 1;
            var muzzle = Child(center, "CenterChargeMuzzle", new Vector3(0, 15 / ppu, 0));
            var so = new SerializedObject(boss);
            so.FindProperty("sectorLeftMuzzle").objectReferenceValue = left;
            so.FindProperty("sectorRightMuzzle").objectReferenceValue = right;
            so.FindProperty("sectorChargeMuzzle").objectReferenceValue = muzzle;
            so.FindProperty("sectorChargeCannon").objectReferenceValue = renderer;
            so.FindProperty("firePoint").objectReferenceValue = muzzle;
            so.FindProperty("sectorSideFlashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/VFX/EnemyLightMuzzle.prefab");
            so.FindProperty("sectorChargeFlashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02_Scripts/Resources/VFX/Approved/SniperMuzzle.prefab");
            so.FindProperty("sectorBurstCadence").floatValue = .08f;
            so.FindProperty("sectorBurstGap").floatValue = .28f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SectorAdministratorAuthoring.Boss);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/SectorRefine");
        File.WriteAllText("Logs/SectorRefine/authored.txt", "Region A prefab authored with side and center muzzle attachments; existing texture slice at 1.25 scale. No scene/shared VFX/artwork changes.");
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        child.localPosition = position; child.localRotation = Quaternion.identity;
        return child;
    }
}
