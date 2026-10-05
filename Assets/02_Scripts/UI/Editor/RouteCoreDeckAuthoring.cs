using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit saved-scene authoring. No runtime builder or additional progression owner.
public static class RouteCoreDeckAuthoring
{
    public const string ScenePath = "Assets/01_Scenes/Settlement.unity";
    public const string ArtRoot = "Assets/Space Kit/";
    public static readonly Vector3 Start = new Vector3(-3.75f, -2.375f, 0);
    public static readonly Vector3 Center = new Vector3(0, .375f, 0);
    public static readonly Vector3[] Stations = { new Vector3(-3.5f, 1.25f), new Vector3(3.5f, 1.5f), new Vector3(2, -1.875f) };
    public static readonly Color[] Identities = { new Color(1, .5f, .12f), new Color(.25f, .65f, 1), new Color(.2f, 1, .35f) };
    public static readonly string[] StationNames = { "SectorStabilizer", "PhaseNavigationLens", "MatterCompressor" };

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play/Prefab Mode before deck authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes before deck authoring.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Apply(scene); Validate(scene);
            // The old amber-filled core5 suppresses green/blue tint. The existing white-hot
            // core1 lets the encounter's authoritative identity color read on the same renderer.
            string prefabPath = AssetDatabase.GetAssetPath(Ref<SettlementDefenseCorruptedCore>(Single<SettlementDefenseEncounterController>(scene), "corePrefab"));
            GameObject prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Ref<SpriteRenderer>(prefab.GetComponent<SettlementDefenseCorruptedCore>(), "visual").sprite = Asset("Core/core1.png");
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        LocalizationContentImporter.ValidateDefaultCatalogFromMenu();
        Debug.Log("ROUTE_CORE_DECK: authored world saved; references, camera-safe layout and collider policy validated.");
    }

    public static T Single<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<T>(true)).Single();
    public static T Ref<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object =>
        new SerializedObject(owner).FindProperty(name).objectReferenceValue as T ??
        throw new InvalidOperationException(owner.name + "/" + name + " has no authored reference.");
    private static Transform Required(Transform parent, string path) => parent.Find(path) ??
        throw new InvalidOperationException("Missing authored deck hierarchy: " + parent.name + "/" + path);
    private static Transform Node(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
        return child;
    }
    private static Sprite Asset(string path, string sub = null)
    {
        Sprite sprite = sub == null ? AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + path) :
            AssetDatabase.LoadAllAssetsAtPath(ArtRoot + path).OfType<Sprite>().SingleOrDefault(s => s.name == sub);
        return sprite ?? throw new InvalidOperationException("Missing world sprite: " + path + "/" + sub);
    }
    private static SpriteRenderer Art(Transform parent, string name, Sprite sprite, Vector2 position,
        Vector2 size, Color color, int order, float angle = 0)
    {
        Transform t = Node(parent, name);
        t.localPosition = position; t.localRotation = Quaternion.Euler(0, 0, angle);
        t.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        SpriteRenderer renderer = t.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = t.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
        return renderer;
    }

    public static void Apply(Scene scene)
    {
        var encounter = Single<SettlementDefenseEncounterController>(scene);
        var route = Single<SettlementRouteCoreController>(scene);
        Transform deck = Ref<GameObject>(encounter, "deckRoot").transform;
        route.transform.localPosition = Center;
        Ref<Transform>(encounter, "playerStart").localPosition = Start;
        Ref<PlayerHealth>(encounter, "player").transform.localPosition = Start;
        var serialized = new SerializedObject(encounter);
        var cores = serialized.FindProperty("componentCores");
        for (int i = 0; i < 3; i++)
        {
            var core = cores.GetArrayElementAtIndex(i);
            ((Transform)core.FindPropertyRelative("combatPoint").objectReferenceValue).localPosition = Stations[i];
            core.FindPropertyRelative("color").colorValue = Identities[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Sprite floor = Asset("Tile/Tile.png", "Tile_0");
        Sprite rail = Asset("Walls/wall9.png");
        Sprite conduit = Asset("Tile/Tile.png", "Tile_2");
        Sprite socket = Asset("Walls/DIx4.png");
        Sprite mount = Asset("Ships and Stations/Station 2.png");
        Sprite glow = Asset("Particles (Sprites)/Small Flare.png");
        Transform environment = Node(deck, "DeckEnvironment");
        Transform flooring = Node(environment, "Floor");
        // Native 32 px tiles at 32 px/world unit. Cover the complete camera, including HUD gutters.
        for (int y = 0; y < 9; y++) for (int x = 0; x < 15; x++)
            Art(flooring, "Tile_" + x + "_" + y, floor, new Vector2(x - 7, y - 4), Vector2.one,
                new Color(.09f, .115f, .145f), -30);
        Transform boundary = Node(environment, "MaintenanceRails");
        for (int x = 0; x < 15; x++) foreach (int side in new[] { -1, 1 })
            Art(boundary, "Horizontal_" + side + "_" + x, rail, new Vector2(x - 7, side * 3.90625f),
                new Vector2(1, 1), new Color(.45f, .5f, .58f), -20);
        for (int y = 0; y < 7; y++) foreach (int side in new[] { -1, 1 })
            Art(boundary, "Vertical_" + side + "_" + y, rail, new Vector2(side * 7.375f, y - 3),
                new Vector2(1, 1), new Color(.45f, .5f, .58f), -20, 90);
        Art(environment, "CentralMount", mount, Center, new Vector2(2.5f, 2.5f), new Color(.5f, .48f, .62f), -10);

        Transform stations = Node(environment, "ComponentStations");
        for (int i = 0; i < 3; i++)
        {
            Transform station = Node(stations, StationNames[i]); station.localPosition = Stations[i];
            Art(station, "Socket", socket, Vector2.zero, new Vector2(1.5f, 1.5f), new Color(.4f, .45f, .52f), -10);
            // Small identity lamps stay outside the combat sprite; these are support sockets, never extra cores.
            Art(station, "IdentityLampLeft", glow, new Vector2(-.65625f, -.4375f), Vector2.one * .25f, Identities[i], -8);
            Art(station, "IdentityLampRight", glow, new Vector2(.65625f, -.4375f), Vector2.one * .25f, Identities[i], -8);
        }
        Transform entry = Node(environment, "FacilityEntry");
        Art(entry, "EntrySocket", floor, new Vector2(-3.75f, -3.125f), new Vector2(1.5f, .5f), new Color(.27f, .42f, .48f), -24);
        for (int i = 0; i < 3; i++)
            Art(entry, "GuideLamp_" + i, glow, new Vector2(-4.5f + i * .75f, -3.3125f), new Vector2(.1875f, .1875f), new Color(.3f, .7f, .8f), -8);

        string[] states = { "Missing", "Ready", "Assembled", "Activated" };
        string[] coreArt = { "core7", "core6", "core5", "core9" };
        float[] power = { .08f, .25f, .4f, .85f };
        for (int state = 0; state < states.Length; state++)
        {
            Transform root = Required(route.transform, states[state]);
            root.localScale = Vector3.one; root.localPosition = Vector3.zero;
            var core = root.GetComponent<SpriteRenderer>(); core.sprite = Asset("Core/" + coreArt[state] + ".png");
            core.color = state == 0 ? new Color(.4f, .4f, .48f) : new Color(.75f, .3f, 1);
            // The nonblocking center must not visually hide enemy projectiles (authored order 2).
            core.sortingOrder = 1;
            // Central sprite and all powered decorations remain owned by the original progression root.
            Art(root, "Energy", glow, Vector2.zero, Vector2.one * (state == 3 ? 1.5f : .875f),
                new Color(.67f, .22f, 1, power[state]), 1);
            Transform links = Node(root, "Conduits");
            for (int i = 0; i < 3; i++)
            {
                Vector2 delta = Stations[i] - Center;
                Vector2 direction = delta.normalized;
                Art(root, "Coupling_" + StationNames[i], socket, direction * .8125f, Vector2.one * .4375f,
                    state == 0 ? new Color(.25f, .28f, .34f) : Identities[i] * (state == 3 ? .85f : .6f), 0);
                if (state > 0)
                    Art(root, "ReadyLamp_" + StationNames[i], glow, direction * .8125f, Vector2.one * .25f,
                        new Color(Identities[i].r, Identities[i].g, Identities[i].b, state == 3 ? .9f : .5f), 1);
                int count = Mathf.FloorToInt((delta.magnitude - 1.625f) / .25f);
                for (int n = 0; n < count; n++)
                {
                    // Incomplete roots visibly leave a gap at the central connector.
                    float distance = 1.0625f + n * .25f;
                    if (state < 2 && n < 2) continue;
                    Color c = Color.Lerp(new Color(.18f, .22f, .29f), Identities[i], state == 3 ? .4f : state == 2 ? .2f : .07f);
                    Art(links, StationNames[i] + "_" + n, conduit, direction * distance, new Vector2(.1875f, .25f),
                        c, -15, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
                }
            }
        }
    }

    public static void Validate(Scene scene)
    {
        var encounter = Single<SettlementDefenseEncounterController>(scene);
        var route = Single<SettlementRouteCoreController>(scene);
        var deck = Ref<GameObject>(encounter, "deckRoot");
        var camera = Ref<Camera>(encounter, "deckCamera");
        if (deck.activeSelf || camera.transform.position.x != 0 || camera.transform.position.y != 0)
            throw new InvalidOperationException("Deck must be authored inactive around the existing camera center.");
        if (!route.GetComponent<Collider2D>().isTrigger) throw new InvalidOperationException("RouteCoreRoot interaction must remain a trigger.");
        Transform environment = Required(deck.transform, "DeckEnvironment");
        if (environment.GetComponentsInChildren<Collider2D>(true).Length != 0)
            throw new InvalidOperationException("DeckEnvironment decoration must not introduce blocking colliders.");
        foreach (SpriteRenderer renderer in deck.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.transform.IsChildOf(environment) && renderer.sprite == null)
                throw new InvalidOperationException("Missing world sprite at " + renderer.name);
        Vector3[] points = Stations.Concat(new[] { Start, Center }).ToArray();
        foreach (Vector3 point in points)
            if (Mathf.Abs(point.x) > 6 || Mathf.Abs(point.y) > 2.75f)
                throw new InvalidOperationException("Deck point exceeds the padded 480x270 play area: " + point);
        if (Ref<GameObject>(encounter, "facilityNavigation").name != "ReturnToFacilities")
            throw new InvalidOperationException("Existing ReturnToFacilities navigation owner must be preserved.");
    }
}
