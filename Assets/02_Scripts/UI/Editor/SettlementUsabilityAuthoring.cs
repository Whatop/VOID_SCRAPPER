using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Saved authoring only; never called from runtime or a missing-reference fallback.
public static class SettlementUsabilityAuthoring
{
    public const string Output = "Logs/SettlementUsability/";
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene work before authoring.");
        Scene scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath);
        EquipmentDevelopmentInstaller.AuthorInputAndSound(RouteCoreDeckAuthoring.Single<ShipTraitTreePanel>(scene));
        ApplyDeckHUD(scene);
        Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Audit();
    }

    public static void Audit()
    {
        Scene scene = EditorSceneManager.OpenPreviewScene(RouteCoreDeckAuthoring.ScenePath);
        try
        {
            var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
            var player = Ref<PlayerHealth>(encounter, "player");
            var movement = player.GetComponent<PlayerController2D>();
            var weapons = player.GetComponent<PlayerWeaponController>();
            var camera = Ref<Camera>(encounter, "deckCamera");
            var lines = new List<string>
            {
                "Saved Settlement bindings (no gameplay mutation)",
                "Aim pivot: " + movement.AimVisualRoot.name,
                "FirePoint parent: " + weapons.FirePoint.parent.name,
                "Deck camera tag: " + camera.tag,
                "MainCamera count: " + scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(c => c.CompareTag("MainCamera")),
                "Camera size: " + camera.orthographicSize,
                "Armor: " + (player.GetComponent<PlayerArmor>() != null)
            };
            foreach (var input in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EquipmentDevelopmentInput>(true)))
                lines.Add("Equipment input/audio: " + EnemyRosterAudit.PathOf(input.transform));
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + "authored.txt", lines);
            Debug.Log(string.Join("\n", lines.Take(7)));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static void ApplyDeckHUD(Scene scene)
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var player = Ref<PlayerHealth>(encounter, "player");
        var weapons = player.GetComponent<PlayerWeaponController>();
        var canvas = Ref<GameObject>(encounter, "deckCanvas").GetComponent<Canvas>();
        var camera = Ref<Camera>(encounter, "deckCamera");
        var vitals = Ref<TextMeshProUGUI>(encounter, "vitalsText");
        if (player.GetComponent<PlayerArmor>() == null || vitals == null)
            throw new InvalidOperationException("RouteCoreDeckHUD: authored player Armor / vitals reference missing.");
        var status = Rect(canvas.transform, "CombatStatus", Vector2.zero, new Vector2(480, 270));
        var hp = Gauge(status, "HP", new Vector2(-174, 101), new Vector2(100, 5), new Color(.95f, .24f, .28f));
        vitals.transform.SetParent(hp.transform, false);
        Layout(vitals.rectTransform, new Vector2(0, 9), new Vector2(100, 12));
        vitals.fontSize = 8; vitals.enableAutoSizing = false; vitals.alignment = TextAlignmentOptions.Left;
        vitals.raycastTarget = false;
        Bind(hp, "valueText", vitals); Bool(hp, "showValueText", true); String(hp, "valueFormat", "HP {0:0} / {1:0}");
        var armor = Gauge(status, "Armor", new Vector2(-174, 96), new Vector2(100, 2), Color.white);
        var reader = Component<RouteCoreCombatHUD>(status);
        Bind(reader, "playerHealth", player); Bind(reader, "playerArmor", player.GetComponent<PlayerArmor>());
        Bind(reader, "healthGauge", hp); Bind(reader, "armorGauge", armor);
        var heat = Gauge(status, "WeaponHeat", new Vector2(-174, 87), new Vector2(100, 3), new Color(.35f, .9f, 1));
        var heatUI = Component<WeaponHeatUI>(heat.transform);
        Bind(heatUI, "rootObject", heat.gameObject); Bind(heatUI, "canvasGroup", heat.GetComponent<CanvasGroup>());
        Bind(heatUI, "weaponController", weapons); Bind(heatUI, "heatGauge", heat);
        Bind(heatUI, "fillImage", Ref<Image>(heat, "fillImage"));
        Bind(heatUI, "trackImage", heat.transform.Find("Track").GetComponent<Image>());
        Bind(heatUI, "backgroundImage", heat.transform.Find("Track").GetComponent<Image>());

        var charge = Gauge(canvas.transform, "PlayerCharge", Vector2.zero, new Vector2(32, 3), new Color(.35f, .8f, 1));
        var chargeValue = Component<TextMeshProUGUI>(Rect(charge.transform, "Value", new Vector2(0, 7), new Vector2(32, 9)));
        chargeValue.font = vitals.font; chargeValue.fontSize = 6; chargeValue.alignment = TextAlignmentOptions.Center;
        chargeValue.raycastTarget = false; Bind(charge, "valueText", chargeValue); Bool(charge, "showValueText", true);
        var follower = Component<WorldGaugeFollower>(charge.transform);
        Bind(follower, "target", player.transform); Bind(follower, "canvas", canvas);
        Bind(follower, "worldCamera", camera); Bind(follower, "uiCamera", camera);
        // Match Expedition's fixed player offset. Child weapon lines/Curse effects
        // must not move the HUD by expanding the combined renderer bounds.
        Bool(follower, "anchorToTargetTop", false);
        Bool(follower, "includeChildRenderers", false); Bool(follower, "includeChildColliders", false);
        var followerData = new SerializedObject(follower);
        followerData.FindProperty("worldOffset").vector3Value = new Vector3(0, 1.1f, 0);
        followerData.ApplyModifiedPropertiesWithoutUndo();
        var chargeUI = Component<PlayerChargeGaugeUI>(charge.transform);
        Bind(chargeUI, "playerTarget", player.transform); Bind(chargeUI, "chargeGauge", charge);
        Bind(chargeUI, "follower", follower); Bind(chargeUI, "canvasGroup", charge.GetComponent<CanvasGroup>());
        Bind(chargeUI, "weaponController", weapons);
        var data = new SerializedObject(chargeUI); var sources = data.FindProperty("weaponSources");
        var all = player.GetComponentsInChildren<PlayerWeaponBase>(true); sources.arraySize = all.Length;
        for (int i = 0; i < all.Length; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Validate(Scene scene)
    {
        var panel = RouteCoreDeckAuthoring.Single<ShipTraitTreePanel>(scene);
        var data = new SerializedObject(panel);
        foreach (var input in panel.GetComponentsInChildren<EquipmentDevelopmentInput>(true))
        {
            if (Ref<ShipTraitTreePanel>(input, "owner") != panel || input.GetComponent<UISoundButton>() == null)
                throw new InvalidOperationException(EnemyRosterAudit.PathOf(input.transform) + ": missing input/audio owner.");
        }
        var errors = new List<string>(); panel.ValidateEquipmentPresentation(errors);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var canvas = Ref<GameObject>(encounter, "deckCanvas");
        if (canvas.GetComponentsInChildren<RouteCoreCombatHUD>(true).Length != 1 ||
            canvas.GetComponentsInChildren<WeaponHeatUI>(true).Length != 1 ||
            canvas.GetComponentsInChildren<PlayerChargeGaugeUI>(true).Length != 1)
            throw new InvalidOperationException("RouteCoreDeckHUD: expected one HP/Armor, Heat, and player charge reader.");
        foreach (var gauge in canvas.GetComponentsInChildren<GaugeBarUI>(true))
            if (Ref<Image>(gauge, "fillImage") == null || Ref<CanvasGroup>(gauge, "canvasGroup") == null)
                throw new InvalidOperationException(EnemyRosterAudit.PathOf(gauge.transform) + ": missing fill/visibility binding.");
    }

    private static T Ref<T>(Object owner, string field) where T : Object => RouteCoreDeckAuthoring.Ref<T>(owner, field);
    private static void Bind(Object owner, string field, Object value) { var s = new SerializedObject(owner); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Bool(Object owner, string field, bool value) { var s = new SerializedObject(owner); s.FindProperty(field).boolValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void String(Object owner, string field, string value) { var s = new SerializedObject(owner); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static T Component<T>(Transform t) where T : Component
    { var component = t.GetComponent<T>(); return component != null ? component : t.gameObject.AddComponent<T>(); }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = parent.Find(name) as RectTransform;
        if (rect == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        }
        rect.gameObject.layer = parent.gameObject.layer;
        Layout(rect, position, size); return rect;
    }
    private static void Layout(RectTransform r, Vector2 position, Vector2 size)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; r.localScale = Vector3.one; }
    private static GaugeBarUI Gauge(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var root = Rect(parent, name, position, size); var group = Component<CanvasGroup>(root);
        group.interactable = group.blocksRaycasts = false;
        var track = Component<Image>(Rect(root, "Track", Vector2.zero, size + new Vector2(2, 2)));
        track.color = new Color(.035f, .065f, .085f, .95f); track.raycastTarget = false;
        var fill = Component<Image>(Rect(root, "Fill", Vector2.zero, size));
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
        fill.color = color; fill.raycastTarget = false;
        // Unity's built-in white UI sprite supports a filled rectangle without new artwork.
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var gauge = Component<GaugeBarUI>(root);
        Bind(gauge, "fillImage", fill); Bind(gauge, "canvasGroup", group); Bind(gauge, "rootObject", root.gameObject);
        Bool(gauge, "showValueText", false); return gauge;
    }
}
