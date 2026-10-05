using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Narrow, idempotent saved-scene presentation pass. Existing components/listeners survive.
public static class HudHangarAuthoring
{
    public static void AuditBaseline()
    {
        var report = new System.Text.StringBuilder();
        foreach (string path in new[] { "Logs/HudHangar/Baseline/Settlement.unity", RouteCoreDeckAuthoring.ScenePath })
        {
            var snapshot = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var hud = RouteCoreDeckAuthoring.Single<SettlementHUD>(snapshot);
                var property = new SerializedObject(hud).FindProperty("cursedPreviewSprite");
                report.AppendLine(path + ": cursedPreviewSprite=" + (property.objectReferenceValue != null ? property.objectReferenceValue.name : "NULL"));
                var errors = new System.Collections.Generic.List<string>(); hud.ValidateHangarPresentation(errors);
                foreach (string error in errors) report.AppendLine(error);
            }
            finally { EditorSceneManager.ClosePreviewScene(snapshot); }
        }
        report.AppendLine("Sprite GUID path: " + AssetDatabase.GUIDToAssetPath("82aabd5b11a4e984db1e75bed82be3d8"));
        System.IO.File.WriteAllText("Logs/HudHangar/baseline-sprite-audit.txt", report.ToString());
        Debug.Log(report.ToString());
    }
    private static T Ref<T>(Object o, string field) where T : Object => RouteCoreDeckAuthoring.Ref<T>(o, field);
    private static void Bind(Object o, string field, Object value) => RouteCoreCombatAuthoring.Set(o, field, value);
    private static T Add<T>(Transform t) where T : Component => t.GetComponent<T>() ?? t.gameObject.AddComponent<T>();
    public static void Layout(RectTransform r, Vector2 position, Vector2 size)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size; r.localScale = Vector3.one; }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var t = parent.Find(name) as RectTransform;
        if (t == null) { t = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); t.SetParent(parent, false); t.gameObject.layer = parent.gameObject.layer; }
        Layout(t, position, size); return t;
    }
    private static void Skin(Image image, string role)
    {
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DarkUISettlementAuthoring.Root + role + ".png");
        if (image.sprite == null) throw new InvalidOperationException("Missing approved DarkUI role " + role);
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 8; image.preserveAspect = false;
        image.color = new Color(.055f, .09f, .13f, .97f); image.raycastTarget = false;
    }
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
        var scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath);
        Apply(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        LocalizationContentImporter.ImportDefaultCatalogFromMenu();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        AssetDatabase.SaveAssets();
        Debug.Log("HUD_HANGAR: existing Deck/Hangar bindings preserved; presentation saved.");
    }
    public static void Apply(Scene scene)
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var canvas = Ref<GameObject>(encounter, "deckCanvas").transform;
        var combat = (RectTransform)canvas.Find("CombatStatus");
        Layout(combat, new Vector2(-164, 88), new Vector2(112, 58));
        var background = Add<Image>(Rect(combat, "Background", Vector2.zero, combat.sizeDelta));
        Skin(background, "32"); background.transform.SetAsFirstSibling();
        var hp = (RectTransform)combat.Find("HP"); Layout(hp, new Vector2(0, 8), new Vector2(96, 5));
        var hpGauge = hp.GetComponent<GaugeBarUI>();
        Layout(hpGauge.ValueText.rectTransform, new Vector2(0, 12), new Vector2(96, 12));
        hpGauge.ValueText.alignment = TextAlignmentOptions.MidlineLeft; hpGauge.ValueText.fontSize = 9;
        hpGauge.ValueText.color = Color.white;
        var s = new SerializedObject(hpGauge); s.FindProperty("valueFormat").stringValue = "HP {0:0} / {1:0}"; s.ApplyModifiedPropertiesWithoutUndo();
        foreach (string name in new[] { "HP", "Armor", "WeaponHeat" })
        {
            var root = (RectTransform)combat.Find(name);
            float height = name == "HP" ? 5 : name == "Armor" ? 2 : 3;
            if (name != "HP") Layout(root, new Vector2(0, name == "Armor" ? 1 : -18), new Vector2(96, height));
            Layout((RectTransform)root.Find("Fill"), Vector2.zero, new Vector2(96, height));
            Layout((RectTransform)root.Find("Track"), Vector2.zero, new Vector2(98, height + 2));
        }
        ColorUtility.TryParseHtmlString(StatPresentation.Hex(StatCategory.Defense), out Color armorColor);
        combat.Find("Armor").GetComponent<GaugeBarUI>().SetFillColor(armorColor);
        var heat = combat.Find("WeaponHeat").GetComponent<WeaponHeatUI>();
        s = new SerializedObject(heat); s.FindProperty("hideAtZeroHeat").boolValue = false; s.ApplyModifiedPropertiesWithoutUndo();
        var label = Add<TextMeshProUGUI>(Rect(heat.transform, "HeatLabel", new Vector2(0, 7), new Vector2(96, 9)));
        label.font = hpGauge.ValueText.font; label.fontSize = 8; label.text = "HEAT";
        label.color = new Color(.55f, .77f, .84f); label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        Bind(heat, "stateText", label);
        s = new SerializedObject(heat); s.FindProperty("areaLabel").stringValue = "HEAT"; s.ApplyModifiedPropertiesWithoutUndo();
        // Child of the existing heat CanvasGroup: Shotgun/Sniper hide the label and track together.
        var objective = Ref<TMP_Text>(RouteCoreDeckAuthoring.Single<SettlementHUD>(scene), "campaignDeckMessageText");
        var panel = Rect(canvas, "ObjectiveMessage", new Vector2(0, 99), new Vector2(196, 30));
        Skin(Add<Image>(panel), "32"); objective.transform.SetParent(panel, false);
        Layout(objective.rectTransform, Vector2.zero, new Vector2(180, 22));
        objective.fontSize = 8; objective.enableAutoSizing = false; objective.alignment = TextAlignmentOptions.Center;
        var interaction = Ref<TMP_Text>(encounter, "interactionText");
        Layout(interaction.rectTransform, new Vector2(0, -91), new Vector2(276, 18));
        var back = Ref<GameObject>(encounter, "facilityNavigation").GetComponent<Button>();
        Layout((RectTransform)back.transform, new Vector2(-172, -108), new Vector2(96, 18));
        var backImage = (Image)back.targetGraphic; Skin(backImage, "Button_Small_A");
        backImage.color = Color.white; backImage.raycastTarget = true;
        back.transition = Selectable.Transition.ColorTint; back.spriteState = default;
        var colors = back.colors; colors.normalColor = new Color(.07f, .12f, .17f);
        colors.highlightedColor = colors.selectedColor = SettlementSelectionColors.HoverBackground;
        colors.pressedColor = SettlementSelectionColors.SelectedBackground;
        colors.disabledColor = new Color(.09f, .11f, .14f); back.colors = colors;
        Layout(back.GetComponentInChildren<TMP_Text>(true).rectTransform, Vector2.zero, new Vector2(88, 16));

        var controller = RouteCoreDeckAuthoring.Single<SettlementController>(scene);
        Bind(controller, "deploymentStatSource", Ref<PlayerRuntimeStatApplier>(encounter, "playerStats"));
        var hud = RouteCoreDeckAuthoring.Single<SettlementHUD>(scene);
        var body = Ref<TextMeshProUGUI>(hud, "shipBodyText");
        var title = Ref<TextMeshProUGUI>(hud, "shipTitleText");
        var card = (RectTransform)title.transform.parent;
        card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.pivot = new Vector2(.5f, 1);
        card.anchoredPosition = new Vector2(94, 94); card.sizeDelta = new Vector2(238, 194);
        Layout(title.rectTransform, new Vector2(0, 80), new Vector2(218, 20)); title.fontSize = 14;
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(.5f, 1); title.rectTransform.anchoredPosition = new Vector2(0, -17);
        var action = (RectTransform)card.Find("ship_choiceButton"); action.anchorMin = action.anchorMax = new Vector2(.5f, 1); action.anchoredPosition = new Vector2(-9, -210);
        var frame = (RectTransform)card.Find("Line"); frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
        var scrollRoot = Rect(card, "ShipDetailScroll", new Vector2(0, -8), new Vector2(220, 148));
        scrollRoot.anchorMin = scrollRoot.anchorMax = scrollRoot.pivot = new Vector2(.5f, 1); scrollRoot.anchoredPosition = new Vector2(0, -34);
        var scroll = Add<ScrollRect>(scrollRoot); scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 18; scroll.inertia = false;
        var viewport = Rect(scrollRoot, "Viewport", Vector2.zero, scrollRoot.sizeDelta);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(.5f, 1);
        Add<RectMask2D>(viewport);
        var hit = Add<Image>(viewport); hit.color = Color.clear; hit.raycastTarget = true;
        body.transform.SetParent(viewport, false); body.richText = true; body.fontSize = 9; body.enableAutoSizing = false;
        body.alignment = TextAlignmentOptions.TopLeft; body.overflowMode = TextOverflowModes.Overflow;
        body.rectTransform.anchorMin = body.rectTransform.anchorMax = body.rectTransform.pivot = new Vector2(.5f, 1);
        body.rectTransform.anchoredPosition = Vector2.zero; body.rectTransform.sizeDelta = new Vector2(220, 148);
        scroll.viewport = viewport; scroll.content = body.rectTransform; Bind(hud, "shipDetailScroll", scroll);
        Bind(hud, "shipDetailCard", card);
        var barRect = Rect(scrollRoot, "Scrollbar", new Vector2(114, 0), new Vector2(3, 0));
        barRect.anchorMin = new Vector2(.5f, 0); barRect.anchorMax = new Vector2(.5f, 1);
        var barImage = Add<Image>(barRect); Skin(barImage, "32"); barImage.raycastTarget = true;
        var bar = Add<Scrollbar>(barRect); bar.direction = Scrollbar.Direction.BottomToTop;
        var handle = Rect(barRect, "Handle", Vector2.zero, new Vector2(3, 24));
        var handleImage = Add<Image>(handle); Skin(handleImage, "32"); handleImage.color = new Color(.28f, .55f, .65f); handleImage.raycastTarget = true;
        bar.handleRect = handle; bar.targetGraphic = handleImage;
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        // Existing source fields are retained for serialized compatibility and Curse alpha.
        s = new SerializedObject(hud);
        foreach (var pair in new[] { ("machineGunAccentColor", WeaponTreeType.MachineGun), ("shotgunAccentColor", WeaponTreeType.Shotgun), ("sniperAccentColor", WeaponTreeType.Sniper) })
        { var prop = s.FindProperty(pair.Item1); Color c = ShipDefinition.WeaponAccent(pair.Item2); c.a = prop.colorValue.a; prop.colorValue = c; }
        s.ApplyModifiedPropertiesWithoutUndo();
    }
}
