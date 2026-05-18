using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SpaceBackgroundGenerator2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Sprites")]
    [SerializeField] private Sprite starfieldSprite;
    [SerializeField] private Sprite[] dustCloudSprites;
    [SerializeField] private Sprite[] dustWispSprites;
    [SerializeField] private Sprite smallFlareSprite;
    [SerializeField] private Sprite starFlareSprite;

    [Header("Map Area")]
    [SerializeField] private Vector2 mapCenter = Vector2.zero;
    [SerializeField] private Vector2 mapSize = new Vector2(80f, 80f);
    [SerializeField] private float extraMargin = 20f;
    [SerializeField] private float backgroundZ = 10f;

    [Header("Generate")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;
    [SerializeField] private bool randomizeSeed;
    [SerializeField] private int seed = 1207;

    [Header("Layer Option")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int baseSortingOrder = -100;
    [SerializeField] private bool useCameraParallax = true;

    [Header("Parallax")]
    [Range(0f, 1f)]
    [SerializeField] private float starParallax = 0.03f;
    [Range(0f, 1f)]
    [SerializeField] private float cloudParallax = 0.06f;
    [Range(0f, 1f)]
    [SerializeField] private float wispParallax = 0.09f;
    [Range(0f, 1f)]
    [SerializeField] private float flareParallax = 0.04f;

    [Header("Starfield")]
    [Range(0f, 1f)]
    [SerializeField] private float starfieldAlpha = 0.85f;
    [SerializeField] private Color starfieldTint = new Color(0.72f, 0.78f, 1f, 1f);

    [Header("Dust Clouds")]
    [SerializeField] private int dustCloudCount = 7;
    [SerializeField] private Vector2 dustCloudWorldSizeRange = new Vector2(14f, 28f);
    [Range(0f, 1f)]
    [SerializeField] private float dustCloudMinAlpha = 0.035f;
    [Range(0f, 1f)]
    [SerializeField] private float dustCloudMaxAlpha = 0.09f;
    [SerializeField] private Color dustCloudTintA = new Color(0.32f, 0.12f, 0.55f, 1f);
    [SerializeField] private Color dustCloudTintB = new Color(0.07f, 0.18f, 0.30f, 1f);

    [Header("Dust Wisps")]
    [SerializeField] private int dustWispCount = 10;
    [SerializeField] private Vector2 dustWispWorldSizeRange = new Vector2(18f, 38f);
    [Range(0f, 1f)]
    [SerializeField] private float dustWispMinAlpha = 0.018f;
    [Range(0f, 1f)]
    [SerializeField] private float dustWispMaxAlpha = 0.055f;
    [SerializeField] private Color dustWispTintA = new Color(0.25f, 0.10f, 0.38f, 1f);
    [SerializeField] private Color dustWispTintB = new Color(0.08f, 0.16f, 0.25f, 1f);

    [Header("Small Flares")]
    [SerializeField] private int smallFlareCount = 26;
    [SerializeField] private Vector2 smallFlareWorldSizeRange = new Vector2(0.12f, 0.32f);
    [Range(0f, 1f)]
    [SerializeField] private float smallFlareMinAlpha = 0.18f;
    [Range(0f, 1f)]
    [SerializeField] private float smallFlareMaxAlpha = 0.55f;
    [SerializeField] private Color smallFlareTint = new Color(0.78f, 0.88f, 1f, 1f);

    [Header("Large Flares")]
    [SerializeField] private int largeFlareCount = 3;
    [SerializeField] private Vector2 largeFlareWorldSizeRange = new Vector2(0.8f, 1.8f);
    [Range(0f, 1f)]
    [SerializeField] private float largeFlareMinAlpha = 0.08f;
    [Range(0f, 1f)]
    [SerializeField] private float largeFlareMaxAlpha = 0.28f;
    [SerializeField] private Color largeFlareTint = new Color(0.65f, 0.76f, 1f, 1f);

    [Header("Motion")]
    [SerializeField] private bool addDustDrift = true;
    [SerializeField] private Vector2 dustDriftDistanceRange = new Vector2(0.08f, 0.25f);
    [SerializeField] private Vector2 dustDriftSpeedRange = new Vector2(0.04f, 0.12f);

    [SerializeField] private bool addFlareTwinkle = true;
    [SerializeField] private Vector2 flareTwinkleSpeedRange = new Vector2(0.6f, 1.8f);
    [SerializeField] private Vector2 flareScalePulseRange = new Vector2(0.04f, 0.12f);

    private const string GeneratedRootName = "__Generated_SpaceBackground";

    private Transform generatedRoot;
    private Transform starLayer;
    private Transform cloudLayer;
    private Transform wispLayer;
    private Transform flareLayer;

    private System.Random random;

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (generateOnStart)
        {
            GenerateBackground();
        }
    }

    private void LateUpdate()
    {
        if (!useCameraParallax)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        UpdateLayerParallax();
    }

    [ContextMenu("Generate Background")]
    public void GenerateBackground()
    {
        if (clearBeforeGenerate)
        {
            ClearGeneratedBackground();
        }

        int finalSeed = randomizeSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : seed;
        random = new System.Random(finalSeed);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform);
        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        starLayer = CreateLayer("00_Starfield");
        cloudLayer = CreateLayer("01_DustClouds");
        wispLayer = CreateLayer("02_DustWisps");
        flareLayer = CreateLayer("03_Flares");

        CreateStarfield();
        CreateDustClouds();
        CreateDustWisps();
        CreateSmallFlares();
        CreateLargeFlares();

        UpdateLayerParallax();

        Debug.Log($"SpaceBackground 생성 완료. Seed: {finalSeed}", this);
    }

    [ContextMenu("Clear Generated Background")]
    public void ClearGeneratedBackground()
    {
        Transform existing = transform.Find(GeneratedRootName);

        if (existing == null)
        {
            generatedRoot = null;
            starLayer = null;
            cloudLayer = null;
            wispLayer = null;
            flareLayer = null;
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }

        generatedRoot = null;
        starLayer = null;
        cloudLayer = null;
        wispLayer = null;
        flareLayer = null;
    }

    private Transform CreateLayer(string layerName)
    {
        GameObject layerObject = new GameObject(layerName);
        Transform layer = layerObject.transform;
        layer.SetParent(generatedRoot);
        layer.localPosition = new Vector3(mapCenter.x, mapCenter.y, backgroundZ);
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;
        return layer;
    }

    private void CreateStarfield()
    {
        if (starfieldSprite == null)
        {
            Debug.LogWarning("Starfield Sprite가 비어 있습니다. 1.png를 연결하세요.", this);
            return;
        }

        Vector2 targetSize = mapSize + Vector2.one * extraMargin * 2f;

        Color color = starfieldTint;
        color.a = starfieldAlpha;

        CreateSpriteObject(
            "Base_Starfield",
            starfieldSprite,
            starLayer,
            Vector2.zero,
            targetSize,
            0f,
            color,
            baseSortingOrder
        );
    }

    private void CreateDustClouds()
    {
        if (dustCloudSprites == null || dustCloudSprites.Length == 0)
        {
            return;
        }

        for (int i = 0; i < dustCloudCount; i++)
        {
            Sprite sprite = PickSprite(dustCloudSprites);
            if (sprite == null)
            {
                continue;
            }

            Vector2 position = RandomMapPosition();
            float size = RandomRange(dustCloudWorldSizeRange.x, dustCloudWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(dustCloudMinAlpha, dustCloudMaxAlpha);
            Color color = Color.Lerp(dustCloudTintA, dustCloudTintB, Random01());
            color.a = alpha;

            GameObject cloud = CreateSpriteObject(
                $"DustCloud_{i:00}",
                sprite,
                cloudLayer,
                position,
                new Vector2(size, size),
                rotation,
                color,
                baseSortingOrder + 10
            );

            TryAddDustDrift(cloud);
        }
    }

    private void CreateDustWisps()
    {
        if (dustWispSprites == null || dustWispSprites.Length == 0)
        {
            return;
        }

        for (int i = 0; i < dustWispCount; i++)
        {
            Sprite sprite = PickSprite(dustWispSprites);
            if (sprite == null)
            {
                continue;
            }

            Vector2 position = RandomMapPosition();
            float width = RandomRange(dustWispWorldSizeRange.x, dustWispWorldSizeRange.y);
            float height = width * RandomRange(0.55f, 1.25f);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(dustWispMinAlpha, dustWispMaxAlpha);
            Color color = Color.Lerp(dustWispTintA, dustWispTintB, Random01());
            color.a = alpha;

            GameObject wisp = CreateSpriteObject(
                $"DustWisp_{i:00}",
                sprite,
                wispLayer,
                position,
                new Vector2(width, height),
                rotation,
                color,
                baseSortingOrder + 20
            );

            TryAddDustDrift(wisp);
        }
    }

    private void CreateSmallFlares()
    {
        if (smallFlareSprite == null)
        {
            return;
        }

        for (int i = 0; i < smallFlareCount; i++)
        {
            Vector2 position = RandomMapPosition();
            float size = RandomRange(smallFlareWorldSizeRange.x, smallFlareWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(smallFlareMinAlpha, smallFlareMaxAlpha);

            Color color = smallFlareTint;
            color.a = alpha;

            GameObject flare = CreateSpriteObject(
                $"SmallFlare_{i:00}",
                smallFlareSprite,
                flareLayer,
                position,
                new Vector2(size, size),
                rotation,
                color,
                baseSortingOrder + 30
            );

            TryAddTwinkle(flare, smallFlareMinAlpha, smallFlareMaxAlpha);
        }
    }

    private void CreateLargeFlares()
    {
        if (starFlareSprite == null)
        {
            return;
        }

        for (int i = 0; i < largeFlareCount; i++)
        {
            Vector2 position = RandomMapPosition();
            float size = RandomRange(largeFlareWorldSizeRange.x, largeFlareWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(largeFlareMinAlpha, largeFlareMaxAlpha);

            Color color = largeFlareTint;
            color.a = alpha;

            GameObject flare = CreateSpriteObject(
                $"LargeFlare_{i:00}",
                starFlareSprite,
                flareLayer,
                position,
                new Vector2(size, size),
                rotation,
                color,
                baseSortingOrder + 31
            );

            TryAddTwinkle(flare, largeFlareMinAlpha, largeFlareMaxAlpha);
        }
    }

    private GameObject CreateSpriteObject(
        string objectName,
        Sprite sprite,
        Transform parent,
        Vector2 localPosition,
        Vector2 targetWorldSize,
        float zRotation,
        Color color,
        int sortingOrder)
    {
        GameObject obj = new GameObject(objectName);
        Transform objTransform = obj.transform;
        objTransform.SetParent(parent);
        objTransform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        objTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        objTransform.localScale = CalculateScale(sprite, targetWorldSize);

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
        {
            renderer.sortingLayerName = sortingLayerName;
        }

        return obj;
    }

    private Vector3 CalculateScale(Sprite sprite, Vector2 targetWorldSize)
    {
        if (sprite == null)
        {
            return Vector3.one;
        }

        Vector2 spriteSize = sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return Vector3.one;
        }

        return new Vector3(
            Mathf.Max(0.001f, targetWorldSize.x / spriteSize.x),
            Mathf.Max(0.001f, targetWorldSize.y / spriteSize.y),
            1f
        );
    }

    private void TryAddDustDrift(GameObject target)
    {
        if (!addDustDrift || target == null)
        {
            return;
        }

        SpaceBackgroundDrift2D drift = target.AddComponent<SpaceBackgroundDrift2D>();

        Vector2 direction = RandomInsideUnitCircle();
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        direction.Normalize();

        float distance = RandomRange(dustDriftDistanceRange.x, dustDriftDistanceRange.y);
        float speed = RandomRange(dustDriftSpeedRange.x, dustDriftSpeedRange.y);
        float phase = RandomRange(0f, Mathf.PI * 2f);

        drift.Setup(direction, distance, speed, phase);
    }

    private void TryAddTwinkle(GameObject target, float minAlpha, float maxAlpha)
    {
        if (!addFlareTwinkle || target == null)
        {
            return;
        }

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        SpaceBackgroundTwinkle2D twinkle = target.AddComponent<SpaceBackgroundTwinkle2D>();

        float speed = RandomRange(flareTwinkleSpeedRange.x, flareTwinkleSpeedRange.y);
        float scalePulse = RandomRange(flareScalePulseRange.x, flareScalePulseRange.y);
        float phase = RandomRange(0f, Mathf.PI * 2f);

        twinkle.Setup(renderer, minAlpha, maxAlpha, speed, scalePulse, phase);
    }

    private void UpdateLayerParallax()
    {
        if (generatedRoot == null)
        {
            generatedRoot = transform.Find(GeneratedRootName);
        }

        if (generatedRoot == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        Vector3 cameraPosition = targetCamera != null ? targetCamera.transform.position : Vector3.zero;

        UpdateSingleLayer(starLayer, cameraPosition, starParallax);
        UpdateSingleLayer(cloudLayer, cameraPosition, cloudParallax);
        UpdateSingleLayer(wispLayer, cameraPosition, wispParallax);
        UpdateSingleLayer(flareLayer, cameraPosition, flareParallax);
    }

    private void UpdateSingleLayer(Transform layer, Vector3 cameraPosition, float parallax)
    {
        if (layer == null)
        {
            return;
        }

        Vector3 offset = useCameraParallax
            ? new Vector3(cameraPosition.x * parallax, cameraPosition.y * parallax, 0f)
            : Vector3.zero;

        layer.localPosition = new Vector3(
            mapCenter.x + offset.x,
            mapCenter.y + offset.y,
            backgroundZ
        );
    }

    private Vector2 RandomMapPosition()
    {
        Vector2 halfSize = (mapSize + Vector2.one * extraMargin * 2f) * 0.5f;

        float x = RandomRange(-halfSize.x, halfSize.x);
        float y = RandomRange(-halfSize.y, halfSize.y);

        return new Vector2(x, y);
    }

    private Sprite PickSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        int index = random.Next(0, sprites.Length);
        return sprites[index];
    }

    private float RandomRange(float min, float max)
    {
        if (random == null)
        {
            random = new System.Random(seed);
        }

        if (max < min)
        {
            (min, max) = (max, min);
        }

        return min + ((float)random.NextDouble() * (max - min));
    }

    private float Random01()
    {
        if (random == null)
        {
            random = new System.Random(seed);
        }

        return (float)random.NextDouble();
    }

    private Vector2 RandomInsideUnitCircle()
    {
        float angle = RandomRange(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random01());

        return new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius
        );
    }
}
