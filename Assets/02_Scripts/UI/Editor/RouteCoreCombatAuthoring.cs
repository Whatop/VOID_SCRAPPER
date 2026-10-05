using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit saved authoring only. Re-running preserves existing objects, IDs and gameplay owners.
public static class RouteCoreCombatAuthoring
{
    public const string CorePrefab = "Assets/03_Prefabs/Enemy/PF_SettlementCorruptedCore.prefab";
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before authoring.");
        AuthorComponentPrefab();
        Scene scene = EditorSceneManager.OpenScene(RouteCoreDeckAuthoring.ScenePath);
        Apply(scene); Validate(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        LocalizationContentImporter.ImportDefaultCatalogFromMenu();
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        AssetDatabase.SaveAssets();
        Debug.Log("ROUTE_CORE_COMBAT: saved three weapon identities, Purple target and bound Deck Radar; original HP/damage preserved.");
    }

    public static void Set(Object owner, string field, Object value)
    { var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Bool(Object owner, string field, bool value)
    { var so = new SerializedObject(owner); so.FindProperty(field).boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Float(Object owner, string field, float value)
    { var so = new SerializedObject(owner); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static T Ref<T>(Object owner, string field) where T : Object => RouteCoreDeckAuthoring.Ref<T>(owner, field);
    private static T Component<T>(Transform root) where T : Component
    { var c = root.GetComponent<T>(); return c != null ? c : root.gameObject.AddComponent<T>(); }
    private static Transform Node(Transform parent, string name, bool ui = false)
    {
        var child = parent.Find(name);
        if (child == null)
        {
            var go = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, parent.gameObject.scene);
            child = go.transform; child.SetParent(parent, false);
        }
        child.gameObject.layer = parent.gameObject.layer;
        return child;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var r = (RectTransform)Node(parent, name, true);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position; r.sizeDelta = size; r.localScale = Vector3.one;
        return r;
    }
    private static TMP_Text Label(Transform parent, string name, Vector2 position, Vector2 size, TMP_FontAsset font)
    {
        var text = Component<TextMeshProUGUI>(Rect(parent, name, position, size));
        text.font = font; text.fontSize = 8; text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.color = new Color(.8f, .88f, 1);
        return text;
    }

    public static void AuthorComponentPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(CorePrefab);
        try
        {
            var core = root.GetComponent<SettlementDefenseCorruptedCore>();
            var line = Component<LineRenderer>(Node(root.transform, "WeaponTelegraph"));
            line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            line.useWorldSpace = true; line.positionCount = 2; line.enabled = false;
            line.startWidth = line.endWidth = .04f; line.sortingOrder = 1;
            line.numCapVertices = 0; line.numCornerVertices = 0;
            Set(core, "telegraph", line);
            PrefabUtility.SaveAsPrefabAsset(root, CorePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Apply(Scene scene)
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var deck = Ref<GameObject>(encounter, "deckRoot").transform;
        var route = Ref<SettlementRouteCoreController>(encounter, "routeCore");
        var player = Ref<PlayerHealth>(encounter, "player");
        var scanner = player.GetComponent<PlayerRadarScanner>();
        if (scanner == null) throw new InvalidOperationException("SettlementDefensePlayer is missing its existing PlayerRadarScanner.");
        var central = Ref<SpriteRenderer>(encounter, "centralVisual");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefab).GetComponent<SettlementDefenseCorruptedCore>();
        var projectile = Ref<ProjectileDefinition>(prefab, "projectile");
        var purpleRoot = Node(deck, "PurpleCorruptionTarget");
        purpleRoot.gameObject.SetActive(false); purpleRoot.position = route.transform.position;
        var purple = Component<SettlementDefensePurpleCore>(purpleRoot);
        var damage = Node(purpleRoot, "Damage"); damage.gameObject.layer = LayerMask.NameToLayer("Enemy");
        var rb = Component<Rigidbody2D>(damage); rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeAll; rb.gravityScale = 0;
        var collider = Component<CircleCollider2D>(damage); collider.radius = .6f; collider.isTrigger = true; collider.enabled = false;
        var health = Component<EnemyHealth>(damage);
        Float(health, "maxHp", 50); Bool(health, "dropRewardOnDeath", false); Bool(health, "releaseOnDeath", false);
        Bool(health, "autoCreateSpriteHitFlash", false); Bool(health, "useSmoothKnockback", false);
        Float(health, "knockbackDistanceMultiplier", 0); Bool(health, "useProceduralDeathEffectWhenPrefabMissing", false);
        Float(health, "hitShakeAmplitude", 0); Float(health, "deathShakeAmplitude", 0);
        var hs = new SerializedObject(health); var colliders = hs.FindProperty("collidersToDisableOnDeath");
        colliders.arraySize = 1; colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider; hs.ApplyModifiedPropertiesWithoutUndo();
        var visual = Component<SpriteRenderer>(Node(purpleRoot, "CorruptionVisual"));
        visual.sprite = Ref<SpriteRenderer>(prefab, "visual").sprite;
        visual.sharedMaterial = central.sharedMaterial; visual.sortingOrder = 1;
        visual.transform.localScale = Vector3.one * 2; visual.color = new Color(.8f, .3f, 1, 1);
        // Radar sibling has no EnemyHealth ancestor: its trigger cannot absorb player bullets.
        var radarRoot = Node(purpleRoot, "RadarDetection"); radarRoot.gameObject.layer = LayerMask.NameToLayer("Enemy");
        var detection = Component<CircleCollider2D>(radarRoot); detection.radius = .7f; detection.isTrigger = true; detection.enabled = false;
        var target = Component<RadarTarget>(radarRoot); target.SetMarkerType(RadarMarkerType.Core);
        target.SetShowOnMap(false); target.SetVisible(false); Bool(target, "allowShotgunTaunt", false);
        var ts = new SerializedObject(target); ts.FindProperty("markerColor").colorValue = new Color(.8f, .3f, 1);
        ts.ApplyModifiedPropertiesWithoutUndo(); target.enabled = false;
        Set(purple, "health", health); Set(purple, "damageCollider", collider); Set(purple, "radarCollider", detection);
        Set(purple, "radarTarget", target); Set(purple, "visual", visual); Set(purple, "projectile", projectile);
        Set(encounter, "purpleCore", purple); Set(encounter, "deckRadar", scanner);

        var canvas = Ref<GameObject>(encounter, "deckCanvas").transform;
        var shell = Rect(canvas, "PurpleRadar", Vector2.zero, new Vector2(480, 270));
        shell.gameObject.SetActive(false);
        var panelRect = Rect(shell, "RadarPanel", new Vector2(197, 90), new Vector2(64, 64));
        var animator = Component<RadarPanelAnimator>(panelRect);
        var group = Component<CanvasGroup>(panelRect);
        var functional = Rect(panelRect, "Contacts", Vector2.zero, new Vector2(64, 64));
        var scope = Component<RadarScopeGraphic>(Rect(functional, "Scope", Vector2.zero, new Vector2(64, 64)));
        scope.raycastTarget = false;
        var sweep = Component<Image>(Rect(scope.transform, "ScanSweep", Vector2.zero, new Vector2(64, 64)));
        sweep.sprite = null; sweep.raycastTarget = false; sweep.color = new Color(.15f, .8f, 1, .12f);
        var radar = Component<RadarHUD>(functional);
        Set(radar, "radarArea", scope.rectTransform); Set(radar, "scopeGraphic", scope);
        Set(radar, "markerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/MarkerPrefab.prefab").GetComponent<RadarMarkerUI>());
        Set(radar, "defaultCenter", player.transform); Set(radar, "playerVisualState", player.GetComponent<PlayerVisualStateController>());
        Float(radar, "scanRadius", 22);
        // Deck-local core hexagon preserves the target identity without a tiny unknown-text glyph.
        var rs = new SerializedObject(radar); rs.FindProperty("coreColor").colorValue = new Color(.8f, .3f, 1);
        rs.ApplyModifiedPropertiesWithoutUndo();
        Set(animator, "panelRoot", panelRect.gameObject); Set(animator, "canvasGroup", group);
        Set(animator, "functionalRadarRoot", functional.gameObject); Set(animator, "scopeGraphic", scope);
        Set(animator, "scanSweep", sweep); Bool(animator, "closeOnStart", true);
        var font = Ref<TMP_Text>(encounter, "interactionText").font;
        var hint = Label(shell, "InputHint", new Vector2(183, 43), new Vector2(110, 27), font);
        hint.text = "[Q] Radar\n[Mouse 4] Scan";
        // The existing top objective already receives SettlementHUD.SetMessage; avoid a duplicate readout.
        var duplicate = shell.Find("SignalStatus");
        if (duplicate != null) Object.DestroyImmediate(duplicate.gameObject);
        Set(scanner, "radarPanelAnimator", animator); Set(scanner, "radarHUD", radar);
        // No passive discovery during the Deck encounter. Expedition/Tutorial scanner settings are untouched.
        Bool(scanner, "enablePassiveRadar", false); scanner.enabled = true;
        player.GetComponent<PlayerRadarVFXController>().enabled = true;
        Set(encounter, "radarPresentation", shell.gameObject); Set(encounter, "radarPanel", animator);
        Set(encounter, "radarHint", hint);
        var environment = deck.Find("DeckEnvironment").GetComponentsInChildren<SpriteRenderer>(true)
            .Concat(route.GetComponentsInChildren<SpriteRenderer>(true)).ToArray();
        var es = new SerializedObject(encounter); var array = es.FindProperty("blackoutRenderers");
        array.arraySize = environment.Length;
        for (int i = 0; i < environment.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = environment[i];
        es.ApplyModifiedPropertiesWithoutUndo();
        RouteCoreShaderAuthoring.Apply(purple);
    }

    public static void Validate(Scene scene)
    {
        var encounter = RouteCoreDeckAuthoring.Single<SettlementDefenseEncounterController>(scene);
        var purple = Ref<SettlementDefensePurpleCore>(encounter, "purpleCore");
        if (!purple.HasAuthoredBindings || purple.gameObject.activeSelf || purple.GetComponentInChildren<RewardDropper>(true) != null)
            throw new InvalidOperationException("RouteCoreDeck/PurpleCorruptionTarget: bindings=" + purple.HasAuthoredBindings +
                ", active=" + purple.gameObject.activeSelf + ", reward=" + purple.GetComponentInChildren<RewardDropper>(true));
        if (!Ref<RadarPanelAnimator>(encounter, "radarPanel").TryValidateAuthoredPresentation(out string reason))
            throw new InvalidOperationException("RouteCoreDeckHUD/PurpleRadar: " + reason);
        if (!Ref<PlayerRadarScanner>(encounter, "deckRadar").enabled)
            throw new InvalidOperationException("SettlementDefensePlayer/PlayerRadarScanner must be enabled and controller-locked outside Purple.");
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefab).GetComponent<SettlementDefenseCorruptedCore>().HasAuthoredBindings)
            throw new InvalidOperationException(CorePrefab + "/WeaponTelegraph is missing.");
    }
}
