using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SpaceBackgroundGenerator2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("비워두면 Main Camera를 따라갑니다. 플레이어를 직접 넣어도 됩니다.")]
    [SerializeField] private Transform followTargetOverride;

    [Header("Sprites")]
    [SerializeField] private Sprite starfieldSprite;
    [SerializeField] private Sprite[] dustCloudSprites;
    [SerializeField] private Sprite[] dustWispSprites;
    [SerializeField] private Sprite smallFlareSprite;
    [SerializeField] private Sprite starFlareSprite;

    [Header("Planets Layer")]
    [Tooltip("SpaceKit 예시처럼 행성 레이어를 배경에 포함할지 여부입니다.")]
    [SerializeField] private bool generatePlanets = true;

    [Tooltip("여러 행성 스프라이트 후보. 비어 있으면 아래 Single Planet Sprite를 사용합니다.")]
    [SerializeField] private Sprite[] planetSprites;

    [Tooltip("기존 단일 행성 스프라이트 호환용입니다.")]
    [SerializeField] private Sprite planetSprite;

    [SerializeField] private int planetCount = 1;
    [SerializeField] private bool randomizePlanetPosition = true;
    [SerializeField] private Vector2 manualPlanetPosition = new Vector2(-6f, 3f);
    [SerializeField] private Vector2 planetWorldSizeRange = new Vector2(8f, 15f);
    [SerializeField] private float planetAvoidCenterRadius = 4f;

    [Range(0f, 1f)]
    [SerializeField] private float planetAlpha = 0.42f;

    [SerializeField] private Color planetTint = new Color(0.72f, 0.82f, 1f, 1f);

    [Header("Background Area")]
    [Tooltip("맵 전체 크기가 아니라 카메라 화면 기준으로 배경을 생성합니다. SpaceKit 방식에는 이걸 켜두세요.")]
    [SerializeField] private bool useCameraViewArea = true;

    [Tooltip("카메라 화면보다 얼마나 크게 배경을 만들지 정합니다.")]
    [SerializeField] private float cameraViewMargin = 12f;

    [Tooltip("useCameraViewArea가 꺼졌을 때 사용하는 맵 영역입니다.")]
    [SerializeField] private Vector2 mapCenter = Vector2.zero;

    [SerializeField] private Vector2 mapSize = new Vector2(80f, 80f);

    [Tooltip("배경 스프라이트의 월드 Z 위치입니다. 2D Sorting Layer를 쓰면 보통 10 유지해도 됩니다.")]
    [SerializeField] private float backgroundZ = 10f;

    [Header("Generate")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool clearBeforeGenerate = true;
    [SerializeField] private bool randomizeSeed;
    [SerializeField] private int seed = 1207;

    [Header("Layer Option")]
    [SerializeField] private string sortingLayerName = "Background";
    [SerializeField] private int baseSortingOrder = -100;

    [Header("SpaceKit Style Follow")]
    [Tooltip("켜면 배경 레이어가 카메라/플레이어와 같이 이동합니다.")]
    [SerializeField] private bool moveWithFollowTarget = true;

    [Tooltip("켜면 레이어별 저항값을 적용합니다. 1에 가까울수록 카메라와 같이 움직입니다.")]
    [SerializeField] private bool useLayerMovementResistance = true;

    [Tooltip("1 = 카메라와 완전히 같이 이동. 0.98 = 아주 조금 느리게 이동.")]
    [Range(0f, 1f)]
    [SerializeField] private float starMovementResistance = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float planetMovementResistance = 0.97f;

    [Range(0f, 1f)]
    [SerializeField] private float cloudMovementResistance = 0.985f;

    [Range(0f, 1f)]
    [SerializeField] private float wispMovementResistance = 0.99f;

    [Range(0f, 1f)]
    [SerializeField] private float flareMovementResistance = 1f;

    [Header("Starfield")]
    [SerializeField] private bool useTiledStarfield = true;

    [Range(0f, 1f)]
    [SerializeField] private float starfieldAlpha = 0.85f;

    [SerializeField] private Color starfieldTint = new Color(0.72f, 0.78f, 1f, 1f);

    [Header("Dust Clouds")]
    [SerializeField] private int dustCloudCount = 7;
    [SerializeField] private Vector2 dustCloudWorldSizeRange = new Vector2(8f, 18f);

    [Range(0f, 1f)]
    [SerializeField] private float dustCloudMinAlpha = 0.035f;

    [Range(0f, 1f)]
    [SerializeField] private float dustCloudMaxAlpha = 0.09f;

    [SerializeField] private Color dustCloudTintA = new Color(0.32f, 0.12f, 0.55f, 1f);
    [SerializeField] private Color dustCloudTintB = new Color(0.07f, 0.18f, 0.30f, 1f);

    [Header("Dust Wisps")]
    [SerializeField] private int dustWispCount = 10;
    [SerializeField] private Vector2 dustWispWorldSizeRange = new Vector2(10f, 24f);

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
    private Transform planetLayer;
    private Transform cloudLayer;
    private Transform wispLayer;
    private Transform flareLayer;

    private System.Random random;

    private Vector3 followOriginPosition;
    private Vector3 starLayerOriginPosition;
    private Vector3 planetLayerOriginPosition;
    private Vector3 cloudLayerOriginPosition;
    private Vector3 wispLayerOriginPosition;
    private Vector3 flareLayerOriginPosition;

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
        UpdateLayerPositions();
    }

    [ContextMenu("Generate Background")]
    public void GenerateBackground()
    {
        ResolveTargetCamera();

        if (clearBeforeGenerate)
        {
            ClearGeneratedBackground();
        }

        int finalSeed = randomizeSeed
            ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
            : seed;

        random = new System.Random(finalSeed);

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        followOriginPosition = ResolveFollowPosition();

        starLayer = CreateLayer("00_Starfield");
        planetLayer = CreateLayer("01_Planets");
        cloudLayer = CreateLayer("02_DustClouds");
        wispLayer = CreateLayer("03_DustWisps");
        flareLayer = CreateLayer("04_Flares");

        CacheLayerOriginPositions();

        CreateStarfield();
        CreatePlanets();
        CreateDustClouds();
        CreateDustWisps();
        CreateSmallFlares();
        CreateLargeFlares();

        UpdateLayerPositions();

        Debug.Log($"Space background generated. Seed: {finalSeed}", this);
    }

    [ContextMenu("Clear Generated Background")]
    public void ClearGeneratedBackground()
    {
        Transform existing = transform.Find(GeneratedRootName);

        if (existing == null)
        {
            ClearCachedLayers();
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

        ClearCachedLayers();
    }

    public void SetMapArea(Vector2 center, Vector2 size)
    {
        mapCenter = center;
        mapSize = new Vector2(
            Mathf.Max(1f, size.x),
            Mathf.Max(1f, size.y)
        );
    }

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
    }

    public void SetFollowTarget(Transform target)
    {
        followTargetOverride = target;
    }

    public void RebaseAfterWorldWrap(Vector2 wrapDelta)
    {
        Vector3 delta = new Vector3(wrapDelta.x, wrapDelta.y, 0f);
        followOriginPosition += delta;

        starLayerOriginPosition += delta * starMovementResistance;
        planetLayerOriginPosition += delta * planetMovementResistance;
        cloudLayerOriginPosition += delta * cloudMovementResistance;
        wispLayerOriginPosition += delta * wispMovementResistance;
        flareLayerOriginPosition += delta * flareMovementResistance;
    }

    private void ClearCachedLayers()
    {
        generatedRoot = null;
        starLayer = null;
        planetLayer = null;
        cloudLayer = null;
        wispLayer = null;
        flareLayer = null;
    }

    private void ResolveTargetCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private Vector3 ResolveFollowPosition()
    {
        if (followTargetOverride != null)
        {
            Vector3 position = followTargetOverride.position;
            position.z = 0f;
            return position;
        }

        ResolveTargetCamera();

        if (targetCamera != null)
        {
            Vector3 position = targetCamera.transform.position;
            position.z = 0f;
            return position;
        }

        return new Vector3(mapCenter.x, mapCenter.y, 0f);
    }

    private Transform CreateLayer(string layerName)
    {
        GameObject layerObject = new GameObject(layerName);
        Transform layer = layerObject.transform;

        layer.SetParent(generatedRoot, false);

        Vector3 centerPosition = moveWithFollowTarget
            ? ResolveFollowPosition()
            : new Vector3(mapCenter.x, mapCenter.y, 0f);

        centerPosition.z = backgroundZ;

        layer.position = centerPosition;
        layer.localRotation = Quaternion.identity;
        layer.localScale = Vector3.one;

        return layer;
    }

    private void CacheLayerOriginPositions()
    {
        starLayerOriginPosition = starLayer != null ? starLayer.position : Vector3.zero;
        planetLayerOriginPosition = planetLayer != null ? planetLayer.position : Vector3.zero;
        cloudLayerOriginPosition = cloudLayer != null ? cloudLayer.position : Vector3.zero;
        wispLayerOriginPosition = wispLayer != null ? wispLayer.position : Vector3.zero;
        flareLayerOriginPosition = flareLayer != null ? flareLayer.position : Vector3.zero;
    }

    private void CacheGeneratedLayersIfNeeded()
    {
        if (generatedRoot == null)
        {
            generatedRoot = transform.Find(GeneratedRootName);
        }

        if (generatedRoot == null)
        {
            return;
        }

        if (starLayer == null)
        {
            starLayer = generatedRoot.Find("00_Starfield");
        }

        if (planetLayer == null)
        {
            planetLayer = generatedRoot.Find("01_Planets");
        }

        if (cloudLayer == null)
        {
            cloudLayer = generatedRoot.Find("02_DustClouds");
        }

        if (wispLayer == null)
        {
            wispLayer = generatedRoot.Find("03_DustWisps");
        }

        if (flareLayer == null)
        {
            flareLayer = generatedRoot.Find("04_Flares");
        }
    }

    private void CreateStarfield()
    {
        if (starfieldSprite == null)
        {
            Debug.LogWarning("Starfield Sprite가 비어 있습니다.", this);
            return;
        }

        Vector2 backgroundSize = GetBackgroundAreaSize();

        Color color = starfieldTint;
        color.a = starfieldAlpha;

        GameObject obj = CreateSpriteObjectNative(
            "Base_Starfield",
            starfieldSprite,
            starLayer,
            Vector2.zero,
            0f,
            color,
            baseSortingOrder,
            Vector3.one
        );

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();

        if (useTiledStarfield && renderer != null)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = backgroundSize;
            obj.transform.localScale = Vector3.one;
        }
        else
        {
            obj.transform.localScale = CalculateScale(starfieldSprite, backgroundSize);
        }
    }

    private void CreatePlanets()
    {
        if (!generatePlanets)
        {
            return;
        }

        if (!HasAnyPlanetSprite())
        {
            return;
        }

        int count = Mathf.Max(0, planetCount);

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = PickPlanetSprite();

            if (sprite == null)
            {
                continue;
            }

            Vector2 position = randomizePlanetPosition
                ? RandomAreaPositionAvoidingCenter(planetAvoidCenterRadius)
                : manualPlanetPosition;

            float size = RandomRange(planetWorldSizeRange.x, planetWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);

            Color color = planetTint;
            color.a = planetAlpha;

            CreateSpriteObjectWorldSize(
                $"Planet_{i:00}",
                sprite,
                planetLayer,
                position,
                new Vector2(size, size),
                rotation,
                color,
                baseSortingOrder + 5
            );
        }
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

            Vector2 position = RandomAreaPosition();
            float size = RandomRange(dustCloudWorldSizeRange.x, dustCloudWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(dustCloudMinAlpha, dustCloudMaxAlpha);

            Color color = Color.Lerp(dustCloudTintA, dustCloudTintB, Random01());
            color.a = alpha;

            GameObject cloud = CreateSpriteObjectWorldSize(
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

            Vector2 position = RandomAreaPosition();

            float width = RandomRange(dustWispWorldSizeRange.x, dustWispWorldSizeRange.y);
            float height = width * RandomRange(0.55f, 1.25f);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(dustWispMinAlpha, dustWispMaxAlpha);

            Color color = Color.Lerp(dustWispTintA, dustWispTintB, Random01());
            color.a = alpha;

            GameObject wisp = CreateSpriteObjectWorldSize(
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
            Vector2 position = RandomAreaPosition();
            float size = RandomRange(smallFlareWorldSizeRange.x, smallFlareWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(smallFlareMinAlpha, smallFlareMaxAlpha);

            Color color = smallFlareTint;
            color.a = alpha;

            GameObject flare = CreateSpriteObjectWorldSize(
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
            Vector2 position = RandomAreaPosition();
            float size = RandomRange(largeFlareWorldSizeRange.x, largeFlareWorldSizeRange.y);
            float rotation = RandomRange(0f, 360f);
            float alpha = RandomRange(largeFlareMinAlpha, largeFlareMaxAlpha);

            Color color = largeFlareTint;
            color.a = alpha;

            GameObject flare = CreateSpriteObjectWorldSize(
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

    private GameObject CreateSpriteObjectNative(
        string objectName,
        Sprite sprite,
        Transform parent,
        Vector2 localPosition,
        float zRotation,
        Color color,
        int sortingOrder,
        Vector3 localScale)
    {
        GameObject obj = new GameObject(objectName);
        Transform objTransform = obj.transform;

        objTransform.SetParent(parent, false);
        objTransform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        objTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        objTransform.localScale = localScale;

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

    private GameObject CreateSpriteObjectWorldSize(
        string objectName,
        Sprite sprite,
        Transform parent,
        Vector2 localPosition,
        Vector2 targetWorldSize,
        float zRotation,
        Color color,
        int sortingOrder)
    {
        GameObject obj = CreateSpriteObjectNative(
            objectName,
            sprite,
            parent,
            localPosition,
            zRotation,
            color,
            sortingOrder,
            Vector3.one
        );

        obj.transform.localScale = CalculateScale(sprite, targetWorldSize);

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

    private void UpdateLayerPositions()
    {
        CacheGeneratedLayersIfNeeded();

        if (generatedRoot == null)
        {
            return;
        }

        Vector3 followPosition = ResolveFollowPosition();

        if (!moveWithFollowTarget)
        {
            SetLayerPosition(starLayer, starLayerOriginPosition);
            SetLayerPosition(planetLayer, planetLayerOriginPosition);
            SetLayerPosition(cloudLayer, cloudLayerOriginPosition);
            SetLayerPosition(wispLayer, wispLayerOriginPosition);
            SetLayerPosition(flareLayer, flareLayerOriginPosition);
            return;
        }

        Vector3 delta = followPosition - followOriginPosition;
        delta.z = 0f;

        if (!useLayerMovementResistance)
        {
            SetLayerPosition(starLayer, starLayerOriginPosition + delta);
            SetLayerPosition(planetLayer, planetLayerOriginPosition + delta);
            SetLayerPosition(cloudLayer, cloudLayerOriginPosition + delta);
            SetLayerPosition(wispLayer, wispLayerOriginPosition + delta);
            SetLayerPosition(flareLayer, flareLayerOriginPosition + delta);
            return;
        }

        SetLayerPosition(starLayer, starLayerOriginPosition + delta * starMovementResistance);
        SetLayerPosition(planetLayer, planetLayerOriginPosition + delta * planetMovementResistance);
        SetLayerPosition(cloudLayer, cloudLayerOriginPosition + delta * cloudMovementResistance);
        SetLayerPosition(wispLayer, wispLayerOriginPosition + delta * wispMovementResistance);
        SetLayerPosition(flareLayer, flareLayerOriginPosition + delta * flareMovementResistance);
    }

    private void SetLayerPosition(Transform layer, Vector3 position)
    {
        if (layer == null)
        {
            return;
        }

        position.z = backgroundZ;
        layer.position = position;
    }

    private Vector2 GetBackgroundAreaSize()
    {
        if (useCameraViewArea)
        {
            Vector2 cameraSize = GetCameraViewSize();
            return cameraSize + Vector2.one * cameraViewMargin * 2f;
        }

        return mapSize;
    }

    private Vector2 GetCameraViewSize()
    {
        ResolveTargetCamera();

        if (targetCamera == null || !targetCamera.orthographic)
        {
            return new Vector2(24f, 14f);
        }

        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;

        return new Vector2(
            Mathf.Max(1f, width),
            Mathf.Max(1f, height)
        );
    }

    private Vector2 RandomAreaPosition()
    {
        Vector2 halfSize = GetBackgroundAreaSize() * 0.5f;

        float x = RandomRange(-halfSize.x, halfSize.x);
        float y = RandomRange(-halfSize.y, halfSize.y);

        return new Vector2(x, y);
    }

    private Vector2 RandomAreaPositionAvoidingCenter(float avoidRadius)
    {
        avoidRadius = Mathf.Max(0f, avoidRadius);

        for (int i = 0; i < 32; i++)
        {
            Vector2 position = RandomAreaPosition();

            if (position.sqrMagnitude >= avoidRadius * avoidRadius)
            {
                return position;
            }
        }

        Vector2 fallbackDirection = RandomInsideUnitCircle();

        if (fallbackDirection.sqrMagnitude <= 0.001f)
        {
            fallbackDirection = Vector2.right;
        }

        fallbackDirection.Normalize();

        return fallbackDirection * avoidRadius;
    }

    private bool HasAnyPlanetSprite()
    {
        if (planetSprite != null)
        {
            return true;
        }

        if (planetSprites == null || planetSprites.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < planetSprites.Length; i++)
        {
            if (planetSprites[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private Sprite PickPlanetSprite()
    {
        if (planetSprites != null && planetSprites.Length > 0)
        {
            int validCount = 0;

            for (int i = 0; i < planetSprites.Length; i++)
            {
                if (planetSprites[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount > 0)
            {
                int targetIndex = random.Next(0, validCount);
                int currentIndex = 0;

                for (int i = 0; i < planetSprites.Length; i++)
                {
                    if (planetSprites[i] == null)
                    {
                        continue;
                    }

                    if (currentIndex == targetIndex)
                    {
                        return planetSprites[i];
                    }

                    currentIndex++;
                }
            }
        }

        return planetSprite;
    }

    private Sprite PickSprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return null;
        }

        int validCount = 0;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return null;
        }

        int targetIndex = random.Next(0, validCount);
        int currentIndex = 0;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            if (currentIndex == targetIndex)
            {
                return sprites[i];
            }

            currentIndex++;
        }

        return null;
    }

    private float RandomRange(float min, float max)
    {
        if (random == null)
        {
            random = new System.Random(seed);
        }

        if (max < min)
        {
            float temp = min;
            min = max;
            max = temp;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        mapSize.x = Mathf.Max(1f, mapSize.x);
        mapSize.y = Mathf.Max(1f, mapSize.y);

        cameraViewMargin = Mathf.Max(0f, cameraViewMargin);
        backgroundZ = Mathf.Max(0f, backgroundZ);

        planetCount = Mathf.Max(0, planetCount);
        dustCloudCount = Mathf.Max(0, dustCloudCount);
        dustWispCount = Mathf.Max(0, dustWispCount);
        smallFlareCount = Mathf.Max(0, smallFlareCount);
        largeFlareCount = Mathf.Max(0, largeFlareCount);
    }
#endif
}