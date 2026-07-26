#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpaceBackgroundGenerator2D))]
public class SpaceBackgroundGenerator2DEditor : Editor
{
    private static readonly string[] IncludedStarfieldNames =
    {
        "Starfield_01",
        "Starfield_02",
        "Starfield_03",
        "Starfield_04",
        "Starfield_05",
        "Starfield_06",
        "Starfield_07",
        "Starfield_08"
    };

    private const string IncludedPlanetName = "Planet_Primordial";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("VOID SCRAPPER Background Tools", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "1~8번 성운 이미지는 seamless texture가 아닙니다. " +
            "Legacy Tiled로 반복하면 교차선이 보입니다. " +
            "Layered Cover 프리셋은 이미지를 화면보다 크게 크롭하고 겹쳐서 경계가 카메라에 들어오지 않게 합니다.",
            MessageType.Info
        );

        SpaceBackgroundGenerator2D generator = (SpaceBackgroundGenerator2D)target;

        DrawStarfieldQuickAdjust(generator);

        if (GUILayout.Button("Apply Included Non-Tiled Preset + Generate", GUILayout.Height(30f)))
        {
            ApplyIncludedPreset(generator, true);
        }

        if (GUILayout.Button("Apply Planet Stability Fix + Regenerate", GUILayout.Height(26f)))
        {
            ApplyPlanetStabilityFix(generator);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Regenerate Background"))
        {
            generator.GenerateBackground();
            EditorUtility.SetDirty(generator);
        }

        if (GUILayout.Button("Clear Generated"))
        {
            generator.ClearGeneratedBackground();
            EditorUtility.SetDirty(generator);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawStarfieldQuickAdjust(SpaceBackgroundGenerator2D generator)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Starfield Quick Adjust", EditorStyles.boldLabel);

        SerializedProperty brightness = serializedObject.FindProperty("starfieldBrightness");
        SerializedProperty overlayIntensity = serializedObject.FindProperty("starfieldOverlayIntensity");
        SerializedProperty zoom = serializedObject.FindProperty("starfieldZoom");
        SerializedProperty offset = serializedObject.FindProperty("starfieldOffset");
        SerializedProperty rotation = serializedObject.FindProperty("starfieldRotationDegrees");
        SerializedProperty randomBase = serializedObject.FindProperty("randomizeBaseStarfield");
        SerializedProperty manualIndex = serializedObject.FindProperty("manualBaseStarfieldIndex");
        SerializedProperty sprites = serializedObject.FindProperty("starfieldSprites");

        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        if (brightness != null)
        {
            EditorGUILayout.Slider(brightness, 0f, 1f, new GUIContent("Brightness", "Starfield만 어둡게/밝게 조절합니다."));
        }

        if (overlayIntensity != null)
        {
            EditorGUILayout.Slider(overlayIntensity, 0f, 1f, new GUIContent("Overlay Strength", "보조 Starfield 레이어의 강도입니다."));
        }

        if (zoom != null)
        {
            EditorGUILayout.Slider(zoom, 0.5f, 1.75f, new GUIContent("Zoom", "0.5~1.75 범위에서 Starfield 스케일을 조절합니다. 1보다 작으면 더 넓은 원본 영역이 보입니다."));
        }

        if (offset != null)
        {
            EditorGUILayout.PropertyField(offset, new GUIContent("Offset", "Starfield 이미지 위치를 이동합니다."));
        }

        if (rotation != null)
        {
            EditorGUILayout.Slider(rotation, -180f, 180f, new GUIContent("Rotation", "Starfield 이미지 전체 회전값입니다."));
        }

        if (randomBase != null)
        {
            EditorGUILayout.PropertyField(randomBase, new GUIContent("Random Base Image"));
        }

        if (randomBase != null && !randomBase.boolValue && manualIndex != null)
        {
            int maxIndex = sprites != null && sprites.isArray
                ? Mathf.Max(0, sprites.arraySize - 1)
                : 0;

            manualIndex.intValue = EditorGUILayout.IntSlider(
                new GUIContent("Base Image Index", "Starfield Sprites 배열에서 사용할 기본 이미지 인덱스입니다."),
                Mathf.Clamp(manualIndex.intValue, 0, maxIndex),
                0,
                maxIndex
            );

            if (sprites != null && sprites.isArray && sprites.arraySize > 0)
            {
                UnityEngine.Object selected = sprites.GetArrayElementAtIndex(manualIndex.intValue).objectReferenceValue;
                EditorGUILayout.LabelField("Selected", selected != null ? selected.name : "None");
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(generator);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Dark"))
        {
            ApplyQuickBrightness(generator, 0.55f, 0.45f);
        }

        if (GUILayout.Button("Balanced"))
        {
            ApplyQuickBrightness(generator, 0.72f, 0.65f);
        }

        if (GUILayout.Button("Full"))
        {
            ApplyQuickBrightness(generator, 1f, 1f);
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Apply Starfield Adjust + Regenerate", GUILayout.Height(26f)))
        {
            serializedObject.ApplyModifiedProperties();
            generator.GenerateBackground();
            EditorUtility.SetDirty(generator);
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox(
            "값을 바꾼 뒤 Apply Starfield Adjust + Regenerate를 누르세요. " +
            "Brightness는 Starfield에만 적용되고 Planet/Dust/Flare에는 적용되지 않습니다.",
            MessageType.None
        );
    }

    private void ApplyQuickBrightness(SpaceBackgroundGenerator2D generator, float brightness, float overlayStrength)
    {
        serializedObject.Update();

        SetFloat(serializedObject, "starfieldBrightness", brightness);
        SetFloat(serializedObject, "starfieldOverlayIntensity", overlayStrength);

        serializedObject.ApplyModifiedProperties();
        generator.GenerateBackground();
        EditorUtility.SetDirty(generator);
        SceneView.RepaintAll();
    }

    private static void ApplyPlanetStabilityFix(SpaceBackgroundGenerator2D generator)
    {
        if (generator == null)
        {
            return;
        }

        Undo.RecordObject(generator, "Apply Planet Stability Fix");

        SerializedObject serialized = new SerializedObject(generator);
        serialized.Update();

        SetObject(serialized, "followTargetOverride", null);
        SetBool(serialized, "moveWithFollowTarget", true);
        SetBool(serialized, "useLayerMovementResistance", true);
        SetFloat(serialized, "planetMovementResistance", 1f);
        SetBool(serialized, "syncBeforeCameraRender", true);
        SetBool(serialized, "stabilizePlanetForPixelPerfectCamera", true);
        SetBool(serialized, "randomizePlanetRotation", false);
        SetFloat(serialized, "fixedPlanetRotation", 0f);
        SetBool(serialized, "snapPlanetLayerToPixelGrid", false);

        Material unlitMaterial = FindMaterialByExactName("Sprite-Unlit-Default");
        if (unlitMaterial != null)
        {
            SetObject(serialized, "planetMaterialOverride", unlitMaterial);
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(generator);
        generator.GenerateBackground();
        SceneView.RepaintAll();
    }

    private static Material FindMaterialByExactName(string materialName)
    {
        string[] guids = AssetDatabase.FindAssets($"{materialName} t:Material");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null && string.Equals(material.name, materialName, StringComparison.OrdinalIgnoreCase))
            {
                return material;
            }
        }

        return null;
    }

    private static void ApplyIncludedPreset(SpaceBackgroundGenerator2D generator, bool generateAfterApply)
    {
        if (generator == null)
        {
            return;
        }

        Undo.RecordObject(generator, "Apply Space Background Preset");

        List<Sprite> starfields = new List<Sprite>();

        for (int i = 0; i < IncludedStarfieldNames.Length; i++)
        {
            Sprite sprite = FindAndPrepareSprite(IncludedStarfieldNames[i], false);

            if (sprite != null)
            {
                starfields.Add(sprite);
            }
        }

        Sprite planet = FindAndPrepareSprite(IncludedPlanetName, true);

        SerializedObject serialized = new SerializedObject(generator);
        serialized.Update();

        AssignSpriteArray(serialized.FindProperty("starfieldSprites"), starfields);

        if (starfields.Count > 0)
        {
            SetObject(serialized, "starfieldSprite", starfields[0]);
        }

        if (planet != null)
        {
            SetObject(serialized, "planetSprite", planet);
            AssignSpriteArray(serialized.FindProperty("planetSprites"), new List<Sprite> { planet });
        }

        SetEnum(serialized, "starfieldLayoutMode", (int)SpaceStarfieldLayoutMode.LayeredCover);
        SetBool(serialized, "useTiledStarfield", false);
        SetInt(serialized, "layeredBackdropCount", Mathf.Clamp(starfields.Count, 1, 2));
        SetVector2(serialized, "layeredBackdropAlphaRange", new Vector2(0.06f, 0.14f));
        SetVector2(serialized, "layeredBackdropScaleRange", new Vector2(1.08f, 1.18f));
        SetBool(serialized, "randomizeBackdropRotation", true);
        SetBool(serialized, "randomizeBackdropFlip", true);
        SetFloat(serialized, "starfieldCoverOverscan", 1.12f);
        SetBool(serialized, "preserveStarfieldAspect", true);
        SetFloat(serialized, "starfieldAlpha", 1f);
        SetColor(serialized, "starfieldTint", Color.white);
        SetColor(serialized, "starfieldOverlayTint", new Color(0.78f, 0.84f, 1f, 1f));
        SetFloat(serialized, "starfieldBrightness", 0.72f);
        SetFloat(serialized, "starfieldOverlayIntensity", 0.65f);
        SetFloat(serialized, "starfieldZoom", 1f);
        SetVector2(serialized, "starfieldOffset", Vector2.zero);
        SetFloat(serialized, "starfieldRotationDegrees", 0f);
        SetBool(serialized, "randomizeBaseStarfield", true);
        SetInt(serialized, "manualBaseStarfieldIndex", 0);

        SetBool(serialized, "useCameraViewArea", true);
        SetFloat(serialized, "cameraViewMargin", 8f);
        SetBool(serialized, "moveWithFollowTarget", true);
        SetBool(serialized, "useLayerMovementResistance", true);
        SetFloat(serialized, "starMovementResistance", 1f);
        SetFloat(serialized, "planetMovementResistance", 0.985f);
        SetFloat(serialized, "cloudMovementResistance", 0.992f);
        SetFloat(serialized, "wispMovementResistance", 0.995f);
        SetFloat(serialized, "flareMovementResistance", 1f);

        SetBool(serialized, "syncBeforeCameraRender", true);
        SetBool(serialized, "stabilizePixelArtParallax", false);
        SetBool(serialized, "snapStarfieldLayerToPixelGrid", false);
        SetBool(serialized, "snapPlanetLayerToPixelGrid", false);
        SetBool(serialized, "snapFlareLayerToPixelGrid", false);
        SetBool(serialized, "snapCloudLayersToPixelGrid", false);

        SetBool(serialized, "autoExpandStarfieldForCameraZoom", true);
        SetFloat(serialized, "expectedMaximumZoomMultiplier", 3.75f);
        SetFloat(serialized, "extraZoomCoverageMargin", 2.5f);
        SetBool(serialized, "neverShrinkStarfieldAtRuntime", true);

        SetBool(serialized, "generatePlanets", planet != null);
        SetInt(serialized, "planetCount", planet != null ? 1 : 0);
        SetBool(serialized, "randomizePlanetPosition", true);
        SetVector2(serialized, "planetWorldSizeRange", new Vector2(13f, 18f));
        SetFloat(serialized, "planetAvoidCenterRadius", 5f);
        SetFloat(serialized, "planetAlpha", 0.58f);
        SetColor(serialized, "planetTint", new Color(0.86f, 0.93f, 1f, 1f));

        SetBool(serialized, "randomizeSeed", true);

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(generator);

        if (generateAfterApply)
        {
            generator.GenerateBackground();
        }

        SceneView.RepaintAll();

        string result =
            $"Starfields: {starfields.Count}/{IncludedStarfieldNames.Length}\n" +
            $"Planet: {(planet != null ? "Found" : "Missing")}\n\n" +
            "이미지를 찾지 못했다면 패치의 Temp/SpaceBackgroundPresetArt 폴더가 Assets 아래에 들어왔는지 확인하세요.";

        EditorUtility.DisplayDialog("Space Background Preset", result, "OK");
    }

    private static Sprite FindAndPrepareSprite(string exactFileNameWithoutExtension, bool keepAlpha)
    {
        string[] guids = AssetDatabase.FindAssets($"{exactFileNameWithoutExtension} t:Texture2D");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (!string.Equals(fileName, exactFileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PrepareTextureImporter(path, keepAlpha);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return null;
    }

    private static void PrepareTextureImporter(string path, bool keepAlpha)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        bool changed = false;

        changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
        changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Single, value => importer.spriteImportMode = value);
        changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
        changed |= SetIfDifferent(importer.wrapMode, TextureWrapMode.Clamp, value => importer.wrapMode = value);
        changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
        changed |= SetIfDifferent(importer.alphaIsTransparency, keepAlpha, value => importer.alphaIsTransparency = value);
        changed |= SetIfDifferent(importer.maxTextureSize, 2048, value => importer.maxTextureSize = value);
        changed |= SetIfDifferent(importer.textureCompression, TextureImporterCompression.CompressedHQ, value => importer.textureCompression = value);

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
        {
            importer.spritePixelsPerUnit = 100f;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static bool SetIfDifferent<T>(T current, T desired, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired))
        {
            return false;
        }

        setter(desired);
        return true;
    }

    private static void AssignSpriteArray(SerializedProperty property, IList<Sprite> sprites)
    {
        if (property == null || !property.isArray)
        {
            return;
        }

        property.arraySize = sprites != null ? sprites.Count : 0;

        for (int i = 0; i < property.arraySize; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetVector2(SerializedObject serialized, string name, Vector2 value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.vector2Value = value;
    }

    private static void SetColor(SerializedObject serialized, string name, Color value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.colorValue = value;
    }

    private static void SetEnum(SerializedObject serialized, string name, int enumIndex)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.enumValueIndex = enumIndex;
    }

    private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }
}
#endif
