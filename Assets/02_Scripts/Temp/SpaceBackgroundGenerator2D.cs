using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum SpaceStarfieldLayoutMode
{
    LayeredCover = 0,
    SingleCover = 1,
    LegacyTiled = 2
}

[ExecuteAlways]
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class SpaceBackgroundGenerator2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("비워두면 Main Camera를 따라갑니다. 플레이어를 직접 넣어도 됩니다.")]
    [SerializeField] private Transform followTargetOverride;

    [Header("Sprites")]
    [Tooltip("기존 단일 배경 호환용입니다. Starfield Sprites가 비어 있을 때 사용합니다.")]
    [SerializeField] private Sprite starfieldSprite;

    [Tooltip("1~8번처럼 타일링되지 않는 큰 우주 이미지를 여러 장 넣습니다. 실행할 때 기본 1장과 보조 레이어를 선택합니다.")]
    [SerializeField] private Sprite[] starfieldSprites;

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

    [Header("Planet Render Stabilization")]
    [Tooltip("Pixel Perfect Camera가 실제 렌더 위치를 픽셀 격자로 보정할 때 행성 레이어도 같은 격자로 따라가게 합니다.")]
    [SerializeField] private bool stabilizePlanetForPixelPerfectCamera = true;

    [Tooltip("행성 생성 시 임의 각도를 사용합니다. 고해상도 배경 행성은 임의 회전이 텍스처 흔들림을 키울 수 있어 기본값은 끕니다.")]
    [SerializeField] private bool randomizePlanetRotation;

    [Tooltip("Randomize Planet Rotation이 꺼져 있을 때 사용할 각도입니다.")]
    [Range(-180f, 180f)]
    [SerializeField] private float fixedPlanetRotation;

    [Tooltip("행성 SpriteRenderer에 사용할 재질입니다. URP에서는 Sprite-Unlit-Default 권장입니다.")]
    [SerializeField] private Material planetMaterialOverride;

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

    [Header("Starfield - Non Tiled Recommended")]
    [Tooltip("Layered Cover는 비타일 이미지 여러 장을 화면보다 크게 겹쳐 사용합니다. 교차선이 생기지 않는 권장 모드입니다.")]
    [SerializeField] private SpaceStarfieldLayoutMode starfieldLayoutMode = SpaceStarfieldLayoutMode.LayeredCover;

    [Tooltip("Legacy Tiled 모드에서만 사용합니다. 원본 이미지가 완전한 seamless texture일 때만 켜세요.")]
    [SerializeField] private bool useTiledStarfield = true;

    [Min(1)]
    [SerializeField] private int layeredBackdropCount = 2;

    [Tooltip("보조 배경 레이어의 알파 범위입니다. 원본이 불투명 이미지여도 낮은 알파로 자연스럽게 혼합됩니다.")]
    [SerializeField] private Vector2 layeredBackdropAlphaRange = new Vector2(0.08f, 0.18f);

    [Tooltip("보조 레이어마다 크롭이 조금씩 달라지도록 추가 배율을 줍니다.")]
    [SerializeField] private Vector2 layeredBackdropScaleRange = new Vector2(1.05f, 1.20f);

    [SerializeField] private bool randomizeBackdropRotation = true;
    [SerializeField] private bool randomizeBackdropFlip = true;

    [Tooltip("카메라 영역보다 추가로 크게 덮는 비율입니다. 1.08이면 각 방향으로 충분한 여유를 둡니다.")]
    [Min(1f)]
    [SerializeField] private float starfieldCoverOverscan = 1.08f;

    [Tooltip("켜면 배경 비율을 유지하고 화면을 Cover 방식으로 크롭합니다. 이미지가 찌그러지지 않습니다.")]
    [SerializeField] private bool preserveStarfieldAspect = true;

    [Range(0f, 1f)]
    [SerializeField] private float starfieldAlpha = 1f;

    [SerializeField] private Color starfieldTint = Color.white;
    [SerializeField] private Color starfieldOverlayTint = new Color(0.78f, 0.84f, 1f, 1f);

    [Header("Starfield Quick Adjust")]
    [Tooltip("Starfield 전체 밝기입니다. 행성, 성운 파티클, 플레어에는 영향을 주지 않습니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float starfieldBrightness = 0.72f;

    [Tooltip("Layered Cover의 보조 Starfield 레이어 강도입니다. 0이면 기본 이미지 한 장만 보입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float starfieldOverlayIntensity = 0.65f;

    [Tooltip("Starfield 이미지 확대 비율입니다. 값을 높이면 더 확대되어 이미지 일부가 크롭됩니다.")]
    [Range(0.5f, 1.75f)]
    [SerializeField] private float starfieldZoom = 1f;

    [Tooltip("카메라 기준 Starfield 이미지 위치 보정입니다.")]
    [SerializeField] private Vector2 starfieldOffset = Vector2.zero;

    [Tooltip("Starfield 이미지 전체 회전 보정값입니다.")]
    [Range(-180f, 180f)]
    [SerializeField] private float starfieldRotationDegrees;

    [Tooltip("켜면 매 생성마다 기본 Starfield 이미지를 랜덤 선택합니다. 끄면 Manual Base Starfield Index를 사용합니다.")]
    [SerializeField] private bool randomizeBaseStarfield = true;

    [Tooltip("Randomize Base Starfield가 꺼졌을 때 사용할 Starfield Sprites 배열 인덱스입니다.")]
    [Min(0)]
    [SerializeField] private int manualBaseStarfieldIndex;

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

    [Header("Runtime Stability")]
    [Tooltip("Cinemachine이 카메라를 최종 갱신한 뒤 렌더 직전에 배경 위치를 한 번 더 맞춥니다.")]
    [SerializeField] private bool syncBeforeCameraRender = true;

    [Tooltip("픽셀 아트 레이어의 화면 상대 위치를 픽셀 그리드에 맞춰 행성/별이 미세하게 떨리는 현상을 줄입니다.")]
    [SerializeField] private bool stabilizePixelArtParallax = true;

    [SerializeField] private bool snapStarfieldLayerToPixelGrid = true;
    [SerializeField] private bool snapPlanetLayerToPixelGrid = true;
    [SerializeField] private bool snapFlareLayerToPixelGrid = true;
    [SerializeField] private bool snapCloudLayersToPixelGrid;
    [SerializeField] private int assetsPixelsPerUnit = 32;

    [Header("Camera Zoom Coverage")]
    [Tooltip("보스 컷신처럼 카메라가 크게 줌아웃될 때 타일 배경을 자동 확장해 검은 화면이 보이지 않게 합니다.")]
    [SerializeField] private bool autoExpandStarfieldForCameraZoom = true;

    [Tooltip("시작 시 미리 확보할 최대 줌 배율입니다. 보스 연출의 Wide Zoom Multiplier보다 같거나 크게 두세요.")]
    [SerializeField] private float expectedMaximumZoomMultiplier = 3.75f;

    [SerializeField] private float extraZoomCoverageMargin = 2f;
    [SerializeField] private bool neverShrinkStarfieldAtRuntime = true;

    private const string GeneratedRootName = "__Generated_SpaceBackground";

    private Transform generatedRoot;
    private Transform starLayer;
    private Transform planetLayer;
    private Transform cloudLayer;
    private Transform wispLayer;
    private Transform flareLayer;

    private System.Random random;

    private Vector3 followOriginPosition;
    private Vector3 planetFollowOriginPosition;
    private Vector3 starLayerOriginPosition;
    private Vector3 planetLayerOriginPosition;
    private Vector3 cloudLayerOriginPosition;
    private Vector3 wispLayerOriginPosition;
    private Vector3 flareLayerOriginPosition;

    private SpriteRenderer starfieldRenderer;
    private readonly List<SpriteRenderer> starfieldRenderers = new List<SpriteRenderer>();
    private readonly List<float> starfieldCoverageMultipliers = new List<float>();

    // 보스 카메라 줌 동안 Starfield를 필요한 크기까지 부드럽게 확장하고,
    // 연출 종료 시 사용자가 설정한 기존 Starfield Zoom 상태로 되돌리기 위한 캐시입니다.
    private readonly List<Vector3> cameraZoomBaseScales = new List<Vector3>();
    private readonly List<Vector3> cameraZoomWideScales = new List<Vector3>();
    private readonly List<Vector2> cameraZoomBaseSizes = new List<Vector2>();
    private readonly List<Vector2> cameraZoomWideSizes = new List<Vector2>();
    private readonly List<bool> cameraZoomUsesTiledSize = new List<bool>();
    private bool cameraZoomTransitionActive;
    private float cameraZoomTransitionProgress;

    private int lastRenderSyncFrame = -1;
    private int lastRenderSyncCameraId;
    private bool cameraCallbacksRegistered;

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        RegisterCameraCallbacks();
    }

    private void OnDisable()
    {
        UnregisterCameraCallbacks();
    }

    private void OnDestroy()
    {
        UnregisterCameraCallbacks();
    }

    private void RegisterCameraCallbacks()
    {
        if (cameraCallbacksRegistered)
        {
            return;
        }

        Camera.onPreCull += HandleCameraPreCull;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        cameraCallbacksRegistered = true;
    }

    private void UnregisterCameraCallbacks()
    {
        if (!cameraCallbacksRegistered)
        {
            return;
        }

        Camera.onPreCull -= HandleCameraPreCull;
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        cameraCallbacksRegistered = false;
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
        // When render-time synchronization is enabled, the camera callbacks below
        // are the single presentation sample. Updating here as well can sample the
        // pre-Cinemachine camera and produce a one-frame planet/starfield pop.
        if (!syncBeforeCameraRender)
        {
            UpdateLayerPositions();
        }

        if (cameraZoomTransitionActive)
        {
            ApplyCameraZoomTransitionProgress(cameraZoomTransitionProgress);
        }
        else
        {
            EnsureStarfieldCoverage(1f);
        }
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
        planetFollowOriginPosition = ResolvePlanetFollowPosition(followOriginPosition);

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

    public bool IsCameraZoomTransitionActive => cameraZoomTransitionActive;

    /// <summary>
    /// 기존 API 호환용입니다. 이제 즉시 Starfield 크기를 바꾸지 않고,
    /// 현재 사용자 Zoom 상태와 보스 줌용 목표 상태를 캐시합니다.
    /// </summary>
    public void PrepareForCameraZoom(float zoomMultiplier)
    {
        BeginCameraZoomTransition(zoomMultiplier);
    }

    public void BeginCameraZoomTransition(float zoomMultiplier)
    {
        CacheGeneratedLayersIfNeeded();

        if (starfieldRenderers.Count == 0)
        {
            cameraZoomTransitionActive = false;
            return;
        }

        if (cameraZoomTransitionActive)
        {
            EndCameraZoomTransition(true);
        }

        cameraZoomBaseScales.Clear();
        cameraZoomWideScales.Clear();
        cameraZoomBaseSizes.Clear();
        cameraZoomWideSizes.Clear();
        cameraZoomUsesTiledSize.Clear();

        for (int i = 0; i < starfieldRenderers.Count; i++)
        {
            SpriteRenderer renderer = starfieldRenderers[i];

            if (renderer == null)
            {
                cameraZoomBaseScales.Add(Vector3.one);
                cameraZoomBaseSizes.Add(Vector2.zero);
                cameraZoomUsesTiledSize.Add(false);
                continue;
            }

            cameraZoomBaseScales.Add(renderer.transform.localScale);
            cameraZoomBaseSizes.Add(renderer.size);
            cameraZoomUsesTiledSize.Add(renderer.drawMode == SpriteDrawMode.Tiled);
        }

        // 기존 상태에서 보스 줌에 필요한 실제 목표 크기를 계산합니다.
        EnsureStarfieldCoverage(Mathf.Max(1f, zoomMultiplier));

        for (int i = 0; i < starfieldRenderers.Count; i++)
        {
            SpriteRenderer renderer = starfieldRenderers[i];

            if (renderer == null)
            {
                cameraZoomWideScales.Add(Vector3.one);
                cameraZoomWideSizes.Add(Vector2.zero);
                continue;
            }

            cameraZoomWideScales.Add(renderer.transform.localScale);
            cameraZoomWideSizes.Add(renderer.size);
        }

        // 준비 단계에서 화면이 갑자기 커지는 것을 막기 위해 즉시 원래 상태로 복원합니다.
        RestoreCameraZoomBaseState();

        cameraZoomTransitionProgress = 0f;
        cameraZoomTransitionActive = true;
        UpdateLayerPositions();
    }

    public void SetCameraZoomTransitionProgress(float normalizedProgress)
    {
        if (!cameraZoomTransitionActive)
        {
            return;
        }

        cameraZoomTransitionProgress = Mathf.Clamp01(normalizedProgress);
        ApplyCameraZoomTransitionProgress(cameraZoomTransitionProgress);
        UpdateLayerPositions();
    }

    public void EndCameraZoomTransition(bool restoreOriginalZoom)
    {
        if (!cameraZoomTransitionActive)
        {
            if (restoreOriginalZoom)
            {
                ForceSyncNow();
            }

            return;
        }

        if (restoreOriginalZoom)
        {
            RestoreCameraZoomBaseState();
        }
        else
        {
            ApplyCameraZoomTransitionProgress(1f);
        }

        cameraZoomTransitionActive = false;
        cameraZoomTransitionProgress = restoreOriginalZoom ? 0f : 1f;

        cameraZoomBaseScales.Clear();
        cameraZoomWideScales.Clear();
        cameraZoomBaseSizes.Clear();
        cameraZoomWideSizes.Clear();
        cameraZoomUsesTiledSize.Clear();

        UpdateLayerPositions();

        if (restoreOriginalZoom)
        {
            EnsureStarfieldCoverage(1f);
        }
    }

    public void ForceSyncNow()
    {
        UpdateLayerPositions();

        if (cameraZoomTransitionActive)
        {
            ApplyCameraZoomTransitionProgress(cameraZoomTransitionProgress);
        }
        else
        {
            EnsureStarfieldCoverage(1f);
        }
    }

    public void RebaseAfterWorldWrap(Vector2 wrapDelta)
    {
        if (!moveWithFollowTarget)
        {
            return;
        }

        Vector3 delta = new Vector3(wrapDelta.x, wrapDelta.y, 0f);
        followOriginPosition += delta;
        planetFollowOriginPosition += delta;

        // 화면 상대 위치를 유지하려면 레이어 원점도 저항값이 아니라 실제 랩 이동량만큼 옮겨야 합니다.
        starLayerOriginPosition += delta;
        planetLayerOriginPosition += delta;
        cloudLayerOriginPosition += delta;
        wispLayerOriginPosition += delta;
        flareLayerOriginPosition += delta;

        ForceSyncNow();
    }

    private void ClearCachedLayers()
    {
        generatedRoot = null;
        starLayer = null;
        planetLayer = null;
        cloudLayer = null;
        wispLayer = null;
        flareLayer = null;
        starfieldRenderer = null;
        starfieldRenderers.Clear();
        starfieldCoverageMultipliers.Clear();

        cameraZoomTransitionActive = false;
        cameraZoomTransitionProgress = 0f;
        cameraZoomBaseScales.Clear();
        cameraZoomWideScales.Clear();
        cameraZoomBaseSizes.Clear();
        cameraZoomWideSizes.Clear();
        cameraZoomUsesTiledSize.Clear();
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

    private Vector3 ResolvePlanetFollowPosition(Vector3 followPosition)
    {
        if (!stabilizePlanetForPixelPerfectCamera || assetsPixelsPerUnit <= 0)
        {
            return followPosition;
        }

        float pixelStep = 1f / Mathf.Max(1, assetsPixelsPerUnit);
        followPosition.x = Mathf.Round(followPosition.x / pixelStep) * pixelStep;
        followPosition.y = Mathf.Round(followPosition.y / pixelStep) * pixelStep;
        followPosition.z = 0f;
        return followPosition;
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

        if (starLayer != null && starfieldRenderers.Count == 0)
        {
            SpriteRenderer[] renderers = starLayer.GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || !renderer.name.StartsWith("Base_Starfield"))
                {
                    continue;
                }

                starfieldRenderers.Add(renderer);
                starfieldCoverageMultipliers.Add(1f);
            }

            starfieldRenderers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            starfieldRenderer = starfieldRenderers.Count > 0 ? starfieldRenderers[0] : null;
        }
    }

    private void CreateStarfield()
    {
        List<Sprite> validSprites = GetValidStarfieldSprites();

        if (validSprites.Count == 0)
        {
            Debug.LogWarning("Starfield Sprite가 비어 있습니다.", this);
            return;
        }

        starfieldRenderers.Clear();
        starfieldCoverageMultipliers.Clear();

        Vector2 backgroundSize = GetInitialStarfieldSize() * Mathf.Max(1f, starfieldCoverOverscan);

        if (starfieldLayoutMode == SpaceStarfieldLayoutMode.LegacyTiled)
        {
            CreateLegacyTiledStarfield(validSprites[0], backgroundSize);
            return;
        }

        int requestedCount = starfieldLayoutMode == SpaceStarfieldLayoutMode.SingleCover
            ? 1
            : Mathf.Max(1, layeredBackdropCount);

        List<Sprite> selectedSprites = PickStarfieldSprites(validSprites, requestedCount);

        for (int i = 0; i < selectedSprites.Count; i++)
        {
            Sprite sprite = selectedSprites[i];
            bool isBase = i == 0;

            float coverageMultiplier = isBase
                ? 1f
                : RandomRange(layeredBackdropScaleRange.x, layeredBackdropScaleRange.y);

            float alpha = isBase
                ? starfieldAlpha
                : RandomRange(layeredBackdropAlphaRange.x, layeredBackdropAlphaRange.y);

            float layerIntensity = isBase ? 1f : starfieldOverlayIntensity;
            Color color = isBase ? starfieldTint : starfieldOverlayTint;
            color.a *= Mathf.Clamp01(alpha * starfieldBrightness * layerIntensity);

            float rotation = starfieldRotationDegrees;

            if (!isBase && randomizeBackdropRotation)
            {
                rotation += random.Next(0, 4) * 90f;
            }
            else if (isBase && randomizeBackdropRotation && Random01() < 0.5f)
            {
                rotation += 180f;
            }

            GameObject obj = CreateSpriteObjectNative(
                $"Base_Starfield_{i:00}",
                sprite,
                starLayer,
                starfieldOffset,
                rotation,
                color,
                baseSortingOrder + i,
                Vector3.one
            );

            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            Vector3 coverScale = CalculateCoverScale(
                sprite,
                backgroundSize * coverageMultiplier,
                preserveStarfieldAspect
            ) * Mathf.Clamp(starfieldZoom, 0.5f, 1.75f) * GetRotationCoverageMultiplier(rotation);

            if (randomizeBackdropFlip)
            {
                if (Random01() < 0.5f)
                {
                    coverScale.x *= -1f;
                }

                if (!isBase && Random01() < 0.25f)
                {
                    coverScale.y *= -1f;
                }
            }

            obj.transform.localScale = coverScale;

            if (renderer != null)
            {
                renderer.drawMode = SpriteDrawMode.Simple;
                starfieldRenderers.Add(renderer);
                starfieldCoverageMultipliers.Add(coverageMultiplier);
            }
        }

        starfieldRenderer = starfieldRenderers.Count > 0 ? starfieldRenderers[0] : null;
    }

    private void CreateLegacyTiledStarfield(Sprite sprite, Vector2 backgroundSize)
    {
        Color color = starfieldTint;
        color.a *= Mathf.Clamp01(starfieldAlpha * starfieldBrightness);

        GameObject obj = CreateSpriteObjectNative(
            "Base_Starfield_00",
            sprite,
            starLayer,
            starfieldOffset,
            starfieldRotationDegrees,
            color,
            baseSortingOrder,
            Vector3.one
        );

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        starfieldRenderer = renderer;

        if (renderer == null)
        {
            return;
        }

        if (useTiledStarfield)
        {
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = backgroundSize;
            obj.transform.localScale = Vector3.one;
        }
        else
        {
            renderer.drawMode = SpriteDrawMode.Simple;
            obj.transform.localScale = CalculateCoverScale(sprite, backgroundSize, preserveStarfieldAspect)
                * Mathf.Clamp(starfieldZoom, 0.5f, 1.75f)
                * GetRotationCoverageMultiplier(starfieldRotationDegrees);
        }

        starfieldRenderers.Add(renderer);
        starfieldCoverageMultipliers.Add(1f);
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
            float rotation = randomizePlanetRotation
                ? RandomRange(0f, 360f)
                : fixedPlanetRotation;

            Color color = planetTint;
            color.a = planetAlpha;

            GameObject planetObject = CreateSpriteObjectWorldSize(
                $"Planet_{i:00}",
                sprite,
                planetLayer,
                position,
                new Vector2(size, size),
                rotation,
                color,
                baseSortingOrder + 5
            );

            if (planetMaterialOverride != null && planetObject != null)
            {
                SpriteRenderer renderer = planetObject.GetComponent<SpriteRenderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterial = planetMaterialOverride;
                }
            }
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

    private Vector3 CalculateCoverScale(Sprite sprite, Vector2 targetWorldSize, bool preserveAspect)
    {
        if (!preserveAspect)
        {
            return CalculateScale(sprite, targetWorldSize);
        }

        if (sprite == null)
        {
            return Vector3.one;
        }

        Vector2 spriteSize = sprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return Vector3.one;
        }

        float uniformScale = Mathf.Max(
            targetWorldSize.x / spriteSize.x,
            targetWorldSize.y / spriteSize.y
        );

        uniformScale = Mathf.Max(0.001f, uniformScale);
        return new Vector3(uniformScale, uniformScale, 1f);
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
        Vector3 planetFollowPosition = ResolvePlanetFollowPosition(followPosition);

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
        Vector3 planetDelta = planetFollowPosition - planetFollowOriginPosition;
        delta.z = 0f;
        planetDelta.z = 0f;

        if (!useLayerMovementResistance)
        {
            SetLayerPosition(starLayer, starLayerOriginPosition + delta);
            SetLayerPosition(planetLayer, planetLayerOriginPosition + planetDelta);
            SetLayerPosition(cloudLayer, cloudLayerOriginPosition + delta);
            SetLayerPosition(wispLayer, wispLayerOriginPosition + delta);
            SetLayerPosition(flareLayer, flareLayerOriginPosition + delta);
            return;
        }

        SetLayerPosition(starLayer, starLayerOriginPosition + delta * starMovementResistance);
        SetLayerPosition(planetLayer, planetLayerOriginPosition + planetDelta * planetMovementResistance);
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

        if (ShouldSnapLayerToPixelGrid(layer))
        {
            position = SnapScreenRelativePosition(position);
            position.z = backgroundZ;
        }

        layer.position = position;
    }

    private bool ShouldSnapLayerToPixelGrid(Transform layer)
    {
        if (!stabilizePixelArtParallax || layer == null || assetsPixelsPerUnit <= 0)
        {
            return false;
        }

        if (layer == starLayer)
        {
            return snapStarfieldLayerToPixelGrid;
        }

        if (layer == planetLayer)
        {
            // 카메라 추적 좌표 자체를 렌더 픽셀 격자에 맞추므로 이중 스냅을 피합니다.
            return !stabilizePlanetForPixelPerfectCamera && snapPlanetLayerToPixelGrid;
        }

        if (layer == flareLayer)
        {
            return snapFlareLayerToPixelGrid;
        }

        if (layer == cloudLayer || layer == wispLayer)
        {
            return snapCloudLayersToPixelGrid;
        }

        return false;
    }

    private Vector3 SnapScreenRelativePosition(Vector3 worldPosition)
    {
        ResolveTargetCamera();

        if (targetCamera == null)
        {
            return worldPosition;
        }

        float pixelStep = 1f / Mathf.Max(1, assetsPixelsPerUnit);
        Vector3 cameraPosition = targetCamera.transform.position;
        cameraPosition.x = Mathf.Round(cameraPosition.x / pixelStep) * pixelStep;
        cameraPosition.y = Mathf.Round(cameraPosition.y / pixelStep) * pixelStep;

        float relativeX = worldPosition.x - cameraPosition.x;
        float relativeY = worldPosition.y - cameraPosition.y;

        relativeX = Mathf.Round(relativeX / pixelStep) * pixelStep;
        relativeY = Mathf.Round(relativeY / pixelStep) * pixelStep;

        worldPosition.x = cameraPosition.x + relativeX;
        worldPosition.y = cameraPosition.y + relativeY;

        return worldPosition;
    }

    private void HandleCameraPreCull(Camera camera)
    {
        SyncForCameraRender(camera);
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        SyncForCameraRender(camera);
    }

    private void SyncForCameraRender(Camera camera)
    {
        if (!syncBeforeCameraRender || camera == null)
        {
            return;
        }

        ResolveTargetCamera();

        if (targetCamera == null || camera != targetCamera)
        {
            return;
        }

        int cameraId = camera.GetInstanceID();

        if (lastRenderSyncFrame == Time.frameCount && lastRenderSyncCameraId == cameraId)
        {
            return;
        }

        lastRenderSyncFrame = Time.frameCount;
        lastRenderSyncCameraId = cameraId;

        UpdateLayerPositions();

        if (cameraZoomTransitionActive)
        {
            ApplyCameraZoomTransitionProgress(cameraZoomTransitionProgress);
        }
        else
        {
            EnsureStarfieldCoverage(1f);
        }
    }

    private void ApplyCameraZoomTransitionProgress(float normalizedProgress)
    {
        if (!cameraZoomTransitionActive)
        {
            return;
        }

        int count = Mathf.Min(
            starfieldRenderers.Count,
            Mathf.Min(cameraZoomBaseScales.Count, cameraZoomWideScales.Count)
        );

        float progress = Mathf.Clamp01(normalizedProgress);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = starfieldRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            renderer.transform.localScale = Vector3.LerpUnclamped(
                cameraZoomBaseScales[i],
                cameraZoomWideScales[i],
                progress
            );

            bool useTiledSize = i < cameraZoomUsesTiledSize.Count && cameraZoomUsesTiledSize[i];

            if (useTiledSize &&
                i < cameraZoomBaseSizes.Count &&
                i < cameraZoomWideSizes.Count)
            {
                renderer.size = Vector2.LerpUnclamped(
                    cameraZoomBaseSizes[i],
                    cameraZoomWideSizes[i],
                    progress
                );
            }
        }
    }

    private void RestoreCameraZoomBaseState()
    {
        int count = Mathf.Min(starfieldRenderers.Count, cameraZoomBaseScales.Count);

        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = starfieldRenderers[i];

            if (renderer == null)
            {
                continue;
            }

            renderer.transform.localScale = cameraZoomBaseScales[i];

            bool useTiledSize = i < cameraZoomUsesTiledSize.Count && cameraZoomUsesTiledSize[i];

            if (useTiledSize && i < cameraZoomBaseSizes.Count)
            {
                renderer.size = cameraZoomBaseSizes[i];
            }
        }
    }

    private Vector2 GetInitialStarfieldSize()
    {
        if (useCameraViewArea)
        {
            float multiplier = Mathf.Max(1f, expectedMaximumZoomMultiplier);
            Vector2 cameraSize = GetCameraViewSize() * multiplier;
            float margin = Mathf.Max(0f, cameraViewMargin + extraZoomCoverageMargin);
            Vector2 offsetCoverage = new Vector2(
                Mathf.Abs(starfieldOffset.x) * 2f,
                Mathf.Abs(starfieldOffset.y) * 2f
            );
            return cameraSize + Vector2.one * margin * 2f + offsetCoverage;
        }

        Vector2 viewPadding = GetCameraViewSize() * Mathf.Max(1f, expectedMaximumZoomMultiplier);
        float extra = Mathf.Max(0f, extraZoomCoverageMargin);
        return mapSize + viewPadding + Vector2.one * extra * 2f;
    }

    private void EnsureStarfieldCoverage(float requestedZoomMultiplier)
    {
        if (!autoExpandStarfieldForCameraZoom)
        {
            return;
        }

        CacheGeneratedLayersIfNeeded();

        if (starfieldRenderers.Count == 0)
        {
            return;
        }

        float multiplier = Mathf.Max(1f, requestedZoomMultiplier);
        Vector2 requiredSize = GetCameraViewSize() * multiplier;
        float margin = Mathf.Max(0f, cameraViewMargin + extraZoomCoverageMargin);
        requiredSize += Vector2.one * margin * 2f;
        requiredSize.x += Mathf.Abs(starfieldOffset.x) * 2f;
        requiredSize.y += Mathf.Abs(starfieldOffset.y) * 2f;

        ResolveTargetCamera();

        if (targetCamera != null && starLayer != null)
        {
            Vector3 cameraPosition = targetCamera.transform.position;
            Vector3 layerPosition = starLayer.position;

            requiredSize.x += Mathf.Abs(cameraPosition.x - layerPosition.x) * 2f;
            requiredSize.y += Mathf.Abs(cameraPosition.y - layerPosition.y) * 2f;
        }

        if (!useCameraViewArea && !moveWithFollowTarget)
        {
            requiredSize = new Vector2(
                Mathf.Max(requiredSize.x, mapSize.x + GetCameraViewSize().x),
                Mathf.Max(requiredSize.y, mapSize.y + GetCameraViewSize().y)
            );
        }

        requiredSize *= Mathf.Max(1f, starfieldCoverOverscan);

        if (starfieldLayoutMode == SpaceStarfieldLayoutMode.LegacyTiled &&
            useTiledStarfield &&
            starfieldRenderer != null)
        {
            Vector2 targetSize = requiredSize;

            if (neverShrinkStarfieldAtRuntime)
            {
                targetSize.x = Mathf.Max(starfieldRenderer.size.x, targetSize.x);
                targetSize.y = Mathf.Max(starfieldRenderer.size.y, targetSize.y);
            }

            if ((starfieldRenderer.size - targetSize).sqrMagnitude > 0.0001f)
            {
                starfieldRenderer.drawMode = SpriteDrawMode.Tiled;
                starfieldRenderer.size = targetSize;
                starfieldRenderer.transform.localScale = Vector3.one;
            }

            return;
        }

        for (int i = 0; i < starfieldRenderers.Count; i++)
        {
            SpriteRenderer renderer = starfieldRenderers[i];

            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            float coverageMultiplier = i < starfieldCoverageMultipliers.Count
                ? Mathf.Max(1f, starfieldCoverageMultipliers[i])
                : 1f;

            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 currentScale = renderer.transform.localScale;
            Vector2 currentWorldSize = new Vector2(
                spriteSize.x * Mathf.Abs(currentScale.x),
                spriteSize.y * Mathf.Abs(currentScale.y)
            );

            Vector2 targetWorldSize = requiredSize * coverageMultiplier;

            if (neverShrinkStarfieldAtRuntime)
            {
                targetWorldSize.x = Mathf.Max(currentWorldSize.x, targetWorldSize.x);
                targetWorldSize.y = Mathf.Max(currentWorldSize.y, targetWorldSize.y);
            }

            Vector3 targetScale = CalculateCoverScale(
                renderer.sprite,
                targetWorldSize,
                preserveStarfieldAspect
            );

            targetScale.x *= currentScale.x < 0f ? -1f : 1f;
            targetScale.y *= currentScale.y < 0f ? -1f : 1f;

            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.transform.localScale = targetScale;
        }
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

    private List<Sprite> GetValidStarfieldSprites()
    {
        List<Sprite> result = new List<Sprite>();

        if (starfieldSprites != null)
        {
            for (int i = 0; i < starfieldSprites.Length; i++)
            {
                Sprite sprite = starfieldSprites[i];

                if (sprite != null && !result.Contains(sprite))
                {
                    result.Add(sprite);
                }
            }
        }

        if (starfieldSprite != null && !result.Contains(starfieldSprite))
        {
            result.Add(starfieldSprite);
        }

        return result;
    }

    private List<Sprite> PickStarfieldSprites(List<Sprite> candidates, int count)
    {
        List<Sprite> result = new List<Sprite>();

        if (candidates == null || candidates.Count == 0)
        {
            return result;
        }

        count = Mathf.Max(1, count);
        List<Sprite> pool = new List<Sprite>(candidates);

        if (!randomizeBaseStarfield)
        {
            int index = Mathf.Clamp(manualBaseStarfieldIndex, 0, candidates.Count - 1);
            Sprite baseSprite = candidates[index];
            result.Add(baseSprite);
            pool.Remove(baseSprite);
        }

        while (result.Count < count && pool.Count > 0)
        {
            int index = random.Next(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        while (result.Count < count)
        {
            result.Add(candidates[random.Next(0, candidates.Count)]);
        }

        return result;
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

    private float GetRotationCoverageMultiplier(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float coverage = Mathf.Abs(Mathf.Cos(radians)) + Mathf.Abs(Mathf.Sin(radians));
        return Mathf.Max(1f, coverage);
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
        assetsPixelsPerUnit = Mathf.Max(1, assetsPixelsPerUnit);
        expectedMaximumZoomMultiplier = Mathf.Max(1f, expectedMaximumZoomMultiplier);
        extraZoomCoverageMargin = Mathf.Max(0f, extraZoomCoverageMargin);
        layeredBackdropCount = Mathf.Max(1, layeredBackdropCount);
        starfieldCoverOverscan = Mathf.Max(1f, starfieldCoverOverscan);
        starfieldBrightness = Mathf.Clamp01(starfieldBrightness);
        starfieldOverlayIntensity = Mathf.Clamp01(starfieldOverlayIntensity);
        starfieldZoom = Mathf.Clamp(starfieldZoom, 0.5f, 1.75f);
        starfieldRotationDegrees = Mathf.Clamp(starfieldRotationDegrees, -180f, 180f);
        manualBaseStarfieldIndex = Mathf.Max(0, manualBaseStarfieldIndex);

        if (layeredBackdropAlphaRange.y < layeredBackdropAlphaRange.x)
        {
            float temp = layeredBackdropAlphaRange.x;
            layeredBackdropAlphaRange.x = layeredBackdropAlphaRange.y;
            layeredBackdropAlphaRange.y = temp;
        }

        if (layeredBackdropScaleRange.y < layeredBackdropScaleRange.x)
        {
            float temp = layeredBackdropScaleRange.x;
            layeredBackdropScaleRange.x = layeredBackdropScaleRange.y;
            layeredBackdropScaleRange.y = temp;
        }

        layeredBackdropAlphaRange.x = Mathf.Clamp01(layeredBackdropAlphaRange.x);
        layeredBackdropAlphaRange.y = Mathf.Clamp01(layeredBackdropAlphaRange.y);
        layeredBackdropScaleRange.x = Mathf.Max(1f, layeredBackdropScaleRange.x);
        layeredBackdropScaleRange.y = Mathf.Max(1f, layeredBackdropScaleRange.y);

        planetCount = Mathf.Max(0, planetCount);
        dustCloudCount = Mathf.Max(0, dustCloudCount);
        dustWispCount = Mathf.Max(0, dustWispCount);
        smallFlareCount = Mathf.Max(0, smallFlareCount);
        largeFlareCount = Mathf.Max(0, largeFlareCount);
    }
#endif
}
