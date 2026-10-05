using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Edits the saved UI only. No gameplay assets or runtime hierarchy fallback.
public static class InventoryPolishAuthoring
{
    public const string PrefabPath = "Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab";
    [MenuItem("Tools/VOID SCRAPPER/Polish Inventory Presentation")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before inventory authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before inventory authoring.");
        AuthorSlot();
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try { Polish(prefab.GetComponentInChildren<PlayerBuildStatusPanelUI>(true)); PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        Scene original = SceneManager.GetActiveScene();
        const string path = "Assets/01_Scenes/Expedition.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            Polish(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBuildStatusPanelUI>(true)).Single());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        if (!LocalizationContentImporter.Import(LocalizationContentImporter.DefaultSourceAssetPath,
            LocalizationContentImporter.DefaultCatalogAssetPath, true)) throw new InvalidOperationException("Localization import failed.");
        AssetDatabase.SaveAssets();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("INVENTORY POLISH: saved shared prefab and unpacked Expedition; Tutorial inherits shared prefab.");
    }

    private static T Ref<T>(SerializedObject data, string field) where T : UnityEngine.Object =>
        data.FindProperty(field).objectReferenceValue as T ?? throw new InvalidOperationException("Missing inventory binding: " + field);
    private static Transform Find(Transform parent, string path) => parent.Find(path) ?? throw new InvalidOperationException("Missing authored UI: " + path);
    private static void Rect(Transform target, float x, float y, float w, float h)
    {
        var r = (RectTransform)target;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.localScale = Vector3.one; r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
    }
    private static void Text(TMP_Text t, float size, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        t.fontSize = size; t.enableAutoSizing = false; t.textWrappingMode = TextWrappingModes.Normal;
        t.alignment = alignment; t.raycastTarget = false; t.color = Color.white;
        t.margin = Vector4.zero; t.overflowMode = TextOverflowModes.Overflow;
    }
    private static Outline Border(GameObject go, bool enabled = true)
    {
        var border = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        border.effectColor = new Color(1, .82f, .3f, 1); border.effectDistance = new Vector2(.65f, -.65f);
        border.useGraphicAlpha = false; border.enabled = enabled; return border;
    }

    public static void Polish(PlayerBuildStatusPanelUI panel)
    {
        var data = new SerializedObject(panel);
        Transform inventory = Ref<GameObject>(data, "root").transform;
        Transform equipment = Ref<GameObject>(data, "equipmentContentRoot").transform;
        Transform cargo = Ref<GameObject>(data, "cargoContentRoot").transform;
        foreach (string path in new[] { "CurrentActivePanel", "EquipmentStoragePanel", "SelectedEquipmentDetailPanel" })
        {
            Transform box = Find(equipment, path);
            box.GetComponent<Image>().sprite = null;
            Outline edge = Border(box.gameObject); edge.effectColor = new Color(.22f, .34f, .4f, 1);
            foreach (Transform child in box)
                if (child.name == "Line" && child.GetComponent<Image>() != null) child.GetComponent<Image>().enabled = false;
        }
        foreach (string field in new[] { "equipmentTabButton", "cargoTabButton", "storyProgressInspectButton" })
        {
            Button b = Ref<Button>(data, field); Image image = b.GetComponent<Image>();
            image.color = Color.white; image.sprite = null; b.targetGraphic = image;
            var colors = b.colors; colors.normalColor = new Color(.055f, .12f, .16f);
            colors.highlightedColor = colors.selectedColor = new Color(.08f, .32f, .48f); b.colors = colors;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true); Text(label, 9, TextAlignmentOptions.Center);
            label.text = field == "equipmentTabButton" ? "장비" : field == "cargoTabButton" ? "적재 자원" : "회수 기록 0/3";
            if (field != "storyProgressInspectButton") Border(b.gameObject, field == "equipmentTabButton");
        }
        TMP_Text frame = Ref<TMP_Text>(data, "shipNameText");
        Rect(frame.transform, -118, 0, 178, 17); Text(frame, 7.5f); frame.raycastTarget = true;
        // The old header object was unbound; reusing it avoids a second resource readout.
        TMP_Text resources = Find(inventory, "Header/Text (TMP) (2)").GetComponent<TMP_Text>();
        Rect(resources.transform, 93, 0, 226, 17); Text(resources, 7, TextAlignmentOptions.MidlineRight);
        resources.gameObject.SetActive(true); data.FindProperty("runResourcesText").objectReferenceValue = resources;
        // Keep only the bound contextual hint. Retire static G hints without deleting their IDs.
        TMP_Text hint = Ref<TMP_Text>(data, "fieldDropHintText");
        foreach (TMP_Text label in inventory.GetComponentsInChildren<TMP_Text>(true))
            if (label != hint && label.text.Contains("[G]")) label.gameObject.SetActive(false);
        Text(hint, 7); Rect(hint.transform, 0, -122, 406, 12);

        Transform active = Find(equipment, "CurrentActivePanel");
        TMP_Text activeHeading = Find(active, "Text (TMP) (2)").GetComponent<TMP_Text>();
        Rect(activeHeading.transform, -29, 31, 132, 11); Text(activeHeading, 8);
        data.FindProperty("activeHeadingText").objectReferenceValue = activeHeading;
        Rect(Find(active, "Tier"), 69, 31, 58, 11);
        Text(Ref<TMP_Text>(data, "activeCooldownText"), 6.5f, TextAlignmentOptions.Center);
        Rect(Find(active, "CurItem"), -82, 10, 25, 25);
        Rect(Find(active, "CurItem/Line"), 0, 0, 26, 26);
        Rect(Ref<Image>(data, "activeIconImage").transform, 0, 0, 22, 22);
        TMP_Text activeName = Ref<TMP_Text>(data, "activeNameText");
        Rect(activeName.transform, 16, 14, 155, 17); Text(activeName, 9);
        Rect(Find(active, "ActiveEffectViewport"), 0, -7, 190, 23);
        Text(Ref<TMP_Text>(data, "activeEffectText"), 8.5f, TextAlignmentOptions.TopLeft);
        Rect(Ref<TMP_Text>(data, "activeChargeText").transform, -61, -29, 68, 12);
        Rect(Ref<TMP_Text>(data, "activeStateText").transform, 10, -29, 64, 12);
        Button activeDrop = Ref<Button>(data, "activeFieldDropButton");
        Rect(activeDrop.transform, 74, -29, 43, 15);
        Rect(activeDrop.GetComponentInChildren<TMP_Text>(true).transform, 0, 0, 39, 14);
        activeDrop.GetComponentInChildren<TMP_Text>(true).text = "드랍";

        Transform storage = Find(equipment, "EquipmentStoragePanel");
        TMP_Text storageHeading = Find(storage, "Text (TMP) (2)").GetComponent<TMP_Text>();
        Rect(storageHeading.transform, -30, 41, 132, 12); Text(storageHeading, 8.5f);
        data.FindProperty("storageHeadingText").objectReferenceValue = storageHeading;
        Rect(Ref<TMP_Text>(data, "passiveCountText").transform, 71, 41, 49, 12);
        Text(Ref<TMP_Text>(data, "passiveCountText"), 7, TextAlignmentOptions.MidlineRight);
        var scroll = Ref<ScrollRect>(data, "passiveScrollRect");
        Rect(scroll.transform, -3, -7, 188, 78);
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        var bar = scroll.verticalScrollbar;
        var barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(1, .5f); barRect.anchoredPosition = new Vector2(5, 0); barRect.sizeDelta = new Vector2(4, 0);
        Transform slide = bar.handleRect.parent;
        var slideRect = (RectTransform)slide; slideRect.anchorMin = Vector2.zero; slideRect.anchorMax = Vector2.one;
        slideRect.offsetMin = slideRect.offsetMax = Vector2.zero;
        bar.handleRect.sizeDelta = Vector2.zero;
        bar.navigation = new Navigation { mode = Navigation.Mode.None };
        GridLayoutGroup grid = Ref<GridLayoutGroup>(data, "passiveGridLayoutGroup");
        grid.cellSize = new Vector2(43, 34); grid.spacing = new Vector2(4, 4);
        grid.padding = new RectOffset(1, 1, 2, 3); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        data.FindProperty("passiveColumnCount").intValue = 4;

        TMP_Text name = Ref<TMP_Text>(data, "selectedPassiveNameText");
        Rect(name.transform, -16, 81, 162, 23); Text(name, 10);
        TMP_Text level = Ref<TMP_Text>(data, "selectedPassiveLevelText");
        Rect(level.transform, -34, 61, 126, 13); Text(level, 7.5f);
        Text(Ref<TMP_Text>(data, "selectedPassiveRarityText"), 7, TextAlignmentOptions.Center);
        Transform detail = Find(equipment, "SelectedEquipmentDetailPanel");
        Rect(Find(detail, "CurItem"), 82, 82, 24, 24);
        Rect(Find(detail, "CurItem/Line"), 0, 0, 26, 26);
        Rect(Ref<Image>(data, "selectedPassiveIconImage").transform, 0, 0, 22, 22);
        var detailScroll = Ref<ScrollRect>(data, "equipmentDetailScrollRect");
        Rect(detailScroll.transform, 0, -10, 192, 120);
        var layout = detailScroll.content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6; layout.padding = new RectOffset(2, 2, 3, 5);
        Text(Ref<TMP_Text>(data, "selectedPassiveDescriptionText"), 8.5f, TextAlignmentOptions.TopLeft);
        Text(Ref<TMP_Text>(data, "selectedPassiveEffectText"), 9, TextAlignmentOptions.TopLeft);
        Ref<TMP_Text>(data, "selectedPassiveEffectText").lineSpacing = 8;
        Text(Ref<TMP_Text>(data, "selectedPassiveFlavorText"), 7, TextAlignmentOptions.TopLeft);
        Ref<Button>(data, "passiveFieldDropButton").GetComponentInChildren<TMP_Text>(true).text = "장비 필드 드랍";

        TMP_Text cargoHeading = Find(cargo, "CargoSectionTitle").GetComponent<TMP_Text>();
        Text(cargoHeading, 8); data.FindProperty("cargoHeadingText").objectReferenceValue = cargoHeading;
        Ref<TMP_Text>(data, "cargoShowAllText").text = "[보유만 보기]";
        Text(Ref<TMP_Text>(data, "cargoShowAllText"), 8, TextAlignmentOptions.MidlineRight);
        Ref<TMP_Text>(data, "cargoShowAllText").raycastTarget = true;
        Ref<TMP_Text>(data, "selectedCargoEmptyText").gameObject.SetActive(false);
        var gauge = Ref<Slider>(data, "cargoLoadSlider");
        gauge.transition = Selectable.Transition.None;
        gauge.GetComponent<Image>().sprite = null;
        gauge.GetComponent<Image>().color = new Color(.22f, .28f, .32f, 1);
        var rows = data.FindProperty("cargoManifestRows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var background = (Image)row.FindPropertyRelative("backgroundImage").objectReferenceValue;
            row.FindPropertyRelative("selectionOutline").objectReferenceValue = Border(background.gameObject, i == 0);
            var primary = (TMP_Text)row.FindPropertyRelative("primaryText").objectReferenceValue;
            var secondary = (TMP_Text)row.FindPropertyRelative("secondaryText").objectReferenceValue;
            Text(primary, 8); Text(secondary, 7.5f);
            primary.text = new[] { "스크랩  0", "코어  0", "안정화 합금  0" }[i];
            secondary.text = "개당 0   적재 0\n자동 회수 ON";
            Rect(primary.transform, 13, 12, 162, 14); Rect(secondary.transform, 13, -8, 162, 24);
        }
        Transform cargoDetail = Ref<GameObject>(data, "cargoDetailActionsRoot").transform;
        Rect(cargoDetail, 108, -24, 198, 142);
        cargoDetail.GetComponent<Image>().color = new Color(.035f, .075f, .105f, 1);
        Rect(Ref<TMP_Text>(data, "selectedCargoNameText").transform, 14, 53, 158, 20);
        Text(Ref<TMP_Text>(data, "selectedCargoNameText"), 9);
        Rect(Ref<Image>(data, "selectedCargoIconImage").transform, -81, 53, 22, 22);
        Rect(Ref<TMP_Text>(data, "selectedCargoStatsText").transform, 0, 25, 184, 28);
        Text(Ref<TMP_Text>(data, "selectedCargoStatsText"), 8);
        Rect(Ref<Slider>(data, "cargoQuantitySlider").transform, -15, 0, 144, 10);
        Rect(Ref<TMP_Text>(data, "selectedCargoQuantityText").transform, 77, 0, 32, 13);

        Transform overlay = Ref<GameObject>(data, "storyProgressInspectionRoot").transform;
        overlay.GetComponent<Image>().color = new Color(.005f, .015f, .025f, .88f);
        Transform section = Find(overlay, "StoryRecoverySection");
        Rect(section, 0, 0, 316, 220);
        Image panelImage = section.GetComponent<Image>() ?? section.gameObject.AddComponent<Image>();
        panelImage.color = new Color(.035f, .07f, .1f, 1); panelImage.raycastTarget = true;
        TMP_Text title = Ref<TMP_Text>(data, "storyRecoveryTitle");
        Rect(title.transform, 0, 92, 288, 18); Text(title, 10); title.text = "회수 기록";
        var slots = data.FindProperty("storyRecoverySlots");
        Sprite fallback = FindStoryFallback();
        Color[] accents = { new Color(1, .62f, .27f), new Color(.43f, .88f, .53f), new Color(.4f, .72f, 1) };
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i);
            var row = (RectTransform)slot.FindPropertyRelative("root").objectReferenceValue;
            Rect(row, 0, 60 - i * 42, 288, 36);
            var label = (TMP_Text)slot.FindPropertyRelative("nameText").objectReferenceValue;
            Text(label, 9); label.color = accents[i];
            var icon = (Image)slot.FindPropertyRelative("iconImage").objectReferenceValue;
            slot.FindPropertyRelative("fallbackIcon").objectReferenceValue = fallback;
            icon.sprite = fallback; icon.color = accents[i];
            Image highlight = (Image)slot.FindPropertyRelative("acquiredHighlight").objectReferenceValue;
            Color tint = accents[i]; tint.a = .12f; highlight.color = tint;
            Rect(highlight.transform, 0, 0, 288, 36);
        }
        TMP_Text summary = Ref<TMP_Text>(data, "storyProgressSummaryText");
        summary.transform.SetParent(section, false); Rect(summary.transform, 0, -58, 288, 16); Text(summary, 8); summary.text = "분석 완료 0/3";
        Button close = Ref<Button>(data, "storyProgressCloseButton");
        close.transform.SetParent(section, false); Rect(close.transform, 0, -88, 108, 20);
        TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>(true); Text(closeLabel, 8, TextAlignmentOptions.Center); closeLabel.text = "닫기";
        close.navigation = new Navigation { mode = Navigation.Mode.None };
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite FindStoryFallback()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("CoreReward")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var presentation = prefab.GetComponentInChildren<Region2BossCoreRewardPresentation>(true);
            if (presentation != null) return presentation.GetComponentInChildren<SpriteRenderer>(true).sprite;
        }
        throw new InvalidOperationException("Established story-part fallback is missing.");
    }

    private static void AuthorSlot()
    {
        const string path = "Assets/03_Prefabs/TestSlotButton.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var data = new SerializedObject(root.GetComponent<BuildStatusSlotButtonUI>());
            Rect(root.transform, 0, 0, 43, 34);
            var icon = Ref<Image>(data, "iconImage"); Rect(icon.transform, 0, 7, 18, 18);
            TMP_Text amount = Ref<TMP_Text>(data, "amountText"); Rect(amount.transform, 0, -13, 41, 9); Text(amount, 7, TextAlignmentOptions.Center);
            Transform kind = root.transform.Find("Kind");
            if (kind == null) { var go = new GameObject("Kind", typeof(RectTransform), typeof(TextMeshProUGUI)); kind = go.transform; kind.SetParent(root.transform, false); }
            Rect(kind, 0, -5, 41, 9);
            var kindText = kind.GetComponent<TextMeshProUGUI>(); kindText.font = amount.font; Text(kindText, 7, TextAlignmentOptions.Center); kindText.text = "일반";
            data.FindProperty("kindText").objectReferenceValue = kindText;
            Transform selected = root.transform.Find("Selected");
            if (selected == null) { var go = new GameObject("Selected", typeof(RectTransform), typeof(Image)); selected = go.transform; selected.SetParent(root.transform, false); }
            Rect(selected, 0, 0, 45, 36);
            selected.GetComponent<Image>().enabled = false;
            for (int i = 0; i < 4; i++)
            {
                Transform edge = selected.Find("Edge" + i);
                if (edge == null) { var go = new GameObject("Edge" + i, typeof(RectTransform), typeof(Image)); edge = go.transform; edge.SetParent(selected, false); }
                Rect(edge, i < 2 ? 0 : i == 2 ? -22 : 22, i >= 2 ? 0 : i == 0 ? 17 : -17, i < 2 ? 45 : 1, i < 2 ? 1 : 35);
                var image = edge.GetComponent<Image>(); image.color = new Color(1, .82f, .3f, 1); image.raycastTarget = false;
            }
            data.FindProperty("selectedRoot").objectReferenceValue = selected.gameObject; selected.gameObject.SetActive(false);
            data.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
