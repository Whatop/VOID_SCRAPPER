using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

public enum SpaceStarfieldLayoutMode
{
    LayeredCover = 0,
    SingleCover = 1,
    LegacyTiled = 2
}

public enum NormalSpaceBackgroundSource
{
    LegacyGenerated = 0,
    DynamicSpaceBackgroundLite = 1
}

[ExecuteAlways]
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class SpaceBackgroundGenerator2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Optional scene-authored normal background. When assigned, this root replaces runtime normal-background generation while retaining the shared Pixel Curse transition/minimal background path.")]
    [SerializeField] private Transform authoredNormalBackgroundRoot;

    [Header("Normal Background Source")]
    [SerializeField] private NormalSpaceBackgroundSource normalBackgroundSource =
        NormalSpaceBackgroundSource.LegacyGenerated;
    [SerializeField] private GameObject dynamicSpaceBackgroundLitePrefab;
    [SerializeField, Min(0f)] private float dynamicLiteCoveragePadding = 4f;

    [Header("Temporary Boss Background")]
    [SerializeField, Range(0f, 1f)] private float bossFarLayerAlphaMultiplier = 0.12f;
    [SerializeField, Min(0.05f)] private float bossBackgroundFadeDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float explorationBackgroundRestoreDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float coreActivationOverlayAlpha = 0.42f;
    [SerializeField, Min(0.01f)] private float coreActivationOverlayFadeInDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float coreActivationOverlayFadeOutDuration = 0.3f;
    [SerializeField, Range(0f, 1f)] private float bossRecoveryOverlayAlpha = 0.12f;
    [SerializeField, Min(0.01f)] private float bossRecoveryOverlayFadeInDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float bossRecoveryOverlayFadeOutDuration = 0.35f;

    [Tooltip("비워두면 Main Camera를 따라갑니다. 플레이어를 직접 넣어도 됩니다.")]
    [SerializeField] private Transform followTargetOverride;

    [Header("Sprites")]
    [Tooltip("기존 단일 배경 호환용입니다. Starfield Sprites가 비어 있을 때 사용합니다.")]
    [SerializeField] private Sprite starfieldSprite;

    [Tooltip("1~8번처럼 타일링되지 않는 큰 우주 이미지를 여러 장 넣습니다. 실행할 때 기본 1장과 보조 레이어를 선택합니다.")]
    [SerializeField] private Sprite[] starfieldSprites;

    [SerializeField] private Sprite[] dustCloudSprites;
    [SerializeField] private Sprite[] dustWispSprites;
    [SerializeField] private Sprite[] distantNebulaSprites;
    [SerializeField] private Sprite[] coverageSpeckSprites;
    [SerializeField] private Sprite smallFlareSprite;
    [SerializeField] private Sprite starFlareSprite;

    [Header("Normal Expedition Composition")]
    [SerializeField] private Color farColorTint = new Color(0.035f, 0.055f, 0.07f, 1f);
    [SerializeField] private Material staticStarParticleMaterial;
    [SerializeField] private int backgroundSeedOffset = 7919;
    [SerializeField, Min(0f)] private float staticParticleBoundsPadding = 2f;
    [SerializeField] private bool logBackgroundDiagnostics;

    [Header("Planets Layer")]
    [Tooltip("SpaceKit 예시처럼 행성 레이어를 배경에 포함할지 여부입니다.")]
    [SerializeField] private bool generatePlanets = true;

    [Tooltip("여러 행성 스프라이트 후보. 비어 있으면 아래 Single Planet Sprite를 사용합니다.")]
    [SerializeField] private Sprite[] planetSprites;

    [Tooltip("기존 단일 행성 스프라이트 호환용입니다.")]
    [SerializeField] private Sprite planetSprite;

    [SerializeField] private int planetCount = 1;
    [SerializeField] private Vector2Int planetCountRange = new Vector2Int(0, 2);
    [SerializeField] private bool randomizePlanetPosition = true;
    [SerializeField] private Vector2 manualPlanetPosition = new Vector2(-6f, 3f);
    [SerializeField] private Vector2 planetWorldSizeRange = new Vector2(3.5f, 5.5f);
    [SerializeField] private float planetAvoidCenterRadius = 14f;

    [Range(0f, 1f)]
    [SerializeField] private float planetAlpha = 0.3f;

    [SerializeField] private Color planetTint = new Color(0.58f, 0.68f, 0.78f, 1f);

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
    [SerializeField] private bool moveWithFollowTarget;

    [Tooltip("켜면 레이어별 저항값을 적용합니다. 1에 가까울수록 카메라와 같이 움직입니다.")]
    [SerializeField] private bool useLayerMovementResistance;

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

    [Header("Distant Nebula Coverage")]
    [SerializeField] private bool generateDistantNebulae = true;
    [SerializeField, Min(0)] private int distantNebulaCount = 8;
    [SerializeField] private Vector2 distantNebulaWorldSizeRange = new Vector2(24f, 44f);
    [SerializeField] private Vector2 distantNebulaAlphaRange = new Vector2(0.025f, 0.075f);
    [SerializeField] private Color distantNebulaTintA = new Color(0.15f, 0.2f, 0.3f, 1f);
    [SerializeField] private Color distantNebulaTintB = new Color(0.24f, 0.12f, 0.3f, 1f);

    [Header("Stratified Background Coverage")]
    [SerializeField] private bool generateStratifiedCoverage = true;
    [SerializeField, Min(2f)] private float coverageCellSize = 9f;
    [SerializeField] private Vector2Int starsPerCellRange = new Vector2Int(1, 3);
    [SerializeField, HideInInspector] private int coverageSpecksPerCell = 1;
    [SerializeField, Range(0f, 0.5f)] private float sparseCellChance = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float rareStarChance = 0.08f;
    [SerializeField] private Vector2 coverageSpeckWorldSizeRange = new Vector2(0.06f, 0.14f);
    [SerializeField] private Vector2 coverageSpeckAlphaRange = new Vector2(0.12f, 0.28f);
    [SerializeField] private Color coverageSpeckTintA = new Color(0.62f, 0.72f, 0.86f, 1f);
    [SerializeField] private Color coverageSpeckTintB = new Color(0.48f, 0.38f, 0.72f, 1f);
    [SerializeField] private Vector2 rareStarWorldSizeRange = new Vector2(0.16f, 0.32f);
    [SerializeField] private Vector2 rareStarAlphaRange = new Vector2(0.28f, 0.5f);

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
    [SerializeField] private bool syncBeforeCameraRender;

    [Tooltip("픽셀 아트 레이어의 화면 상대 위치를 픽셀 그리드에 맞춰 행성/별이 미세하게 떨리는 현상을 줄입니다.")]
    [SerializeField] private bool stabilizePixelArtParallax;

    [SerializeField] private bool snapStarfieldLayerToPixelGrid;
    [SerializeField] private bool snapPlanetLayerToPixelGrid;
    [SerializeField] private bool snapFlareLayerToPixelGrid;
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
    private const string MinimalRootName = "__Minimal_ExpeditionBackground";
    private const string CurseTransitionOverlayName = "CurseTransitionOverlay";
    private const int MinimalStarCount = 48;
    private const int MinimalCurseFragmentCount = 10;

    [Header("References")]
    [SerializeField] private PlayerVisualStateController playerVisualState;
    [Tooltip("When enabled, the player's persistent Curse state replaces the normal background. Disable this in normal Expedition so story progression does not permanently suppress generated space.")]
    [SerializeField] private bool replaceNormalBackgroundWhenPlayerCursed = true;
    [SerializeField] private float curseToCursedTransitionDuration = 0.6f;
    [SerializeField] private float curseToNormalTransitionDuration = 0.35f;
    [SerializeField] private Color curseTransitionColor = new Color(0.65f, 0.2f, 1f, 1f);

    private Transform generatedRoot;
    private Transform minimalBackgroundRoot;
    private Transform dynamicLiteRoot;
    private SpriteRenderer dynamicLiteFarRenderer;
    private SpriteRenderer dynamicLiteNebulaRenderer;
    private SpriteRenderer dynamicLiteDetailRenderer;
    private Color dynamicLiteFarBaseColor;
    private Color dynamicLiteNebulaBaseColor;
    private Color dynamicLiteDetailBaseColor;
    private bool dynamicLiteBaseColorsCaptured;
    private Tween temporaryBossBackgroundTween;
    private ScreenFader bossScreenFader;
    private CanvasGroup bossScreenOverlayCanvasGroup;
    private Tween bossScreenOverlayTween;
    private bool ownsBossScreenOverlay;
    private Transform farColorLayer;
    private Transform starLayer;
    private Transform planetLayer;
    private Transform cloudLayer;
    private Transform wispLayer;
    private Transform flareLayer;

    private System.Random random;

    private Vector3 followOriginPosition;
    private Vector3 planetFollowOriginPosition;
    private Vector3 farColorLayerOriginPosition;
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
    private Vector3 layerCameraSample;
    private bool hasLayerCameraSample;
    private Tween curseTransitionTween;
    private bool isCurseBackgroundActive;
    private PlayerVisualStateController subscribedPlayerVisualState;
    private readonly List<SpriteRenderer> generatedBackgroundRenderers = new List<SpriteRenderer>();
    private readonly List<float> generatedBackgroundRendererAlphas = new List<float>();
    private readonly List<SpriteRenderer> minimalBackgroundRenderers = new List<SpriteRenderer>();
    private readonly List<float> minimalBackgroundRendererAlphas = new List<float>();
    private readonly Dictionary<int, float> rendererBaseAlphaByInstanceId = new Dictionary<int, float>();
    private SpriteRenderer curseTransitionRenderer;
    private Sprite generatedFarColorSprite;
    private int generatedMainStarCount;
    private int generatedRareStarCount;
    private Bounds generatedBackgroundCoverageBounds;

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        RegisterCameraCallbacks();
        ResolvePlayerVisualState();
        SubscribeCurseState();

        if (Application.isPlaying)
        {
            isCurseBackgroundActive = ResolveAutomaticCurseBackgroundState();
            ApplyBackgroundVisibility(isCurseBackgroundActive);
        }
    }

    private void OnDisable()
    {
        UnregisterCameraCallbacks();
        UnsubscribeCurseState();
        ResetCurseTransition();
        ResetTemporaryBossBackgroundPresentation();
    }

    private void OnDestroy()
    {
        UnregisterCameraCallbacks();
        UnsubscribeCurseState();
        ResetCurseTransition();
        KillTemporaryBossBackgroundTween();
    }

    private void RegisterCameraCallbacks()
    {
        if (cameraCallbacksRegistered || !moveWithFollowTarget || !syncBeforeCameraRender)
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

        if (generateOnStart && generatedRoot == null)
        {
            GenerateBackground();
        }
        else if (authoredNormalBackgroundRoot != null)
        {
            PrepareAuthoredBackground();
        }
        else
        {
            CacheBackgroundRendererLists();
        }

        ResolvePlayerVisualState();
        SubscribeCurseState();

        bool curseActive = ResolveAutomaticCurseBackgroundState();
        isCurseBackgroundActive = curseActive;
        ApplyCurseBackgroundState(curseActive, true);
    }

    private void LateUpdate()
    {
        if (generatedRoot == null || !generatedRoot.gameObject.activeSelf)
        {
            return;
        }

        if (!moveWithFollowTarget)
        {
            return;
        }

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

        int finalSeed = ResolveBackgroundSeed();

        random = new System.Random(finalSeed);
        generatedMainStarCount = 0;
        generatedRareStarCount = 0;
        generatedBackgroundCoverageBounds = default;

        generatedRoot = new GameObject(GeneratedRootName).transform;
        generatedRoot.SetParent(transform, false);
        generatedRoot.localPosition = Vector3.zero;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        followOriginPosition = ResolveFollowPosition();
        planetFollowOriginPosition = ResolvePlanetFollowPosition(followOriginPosition);

        bool usesDynamicLite = normalBackgroundSource ==
            NormalSpaceBackgroundSource.DynamicSpaceBackgroundLite;

        if (usesDynamicLite && TryCreateDynamicSpaceBackgroundLite())
        {
            CacheLayerOriginPositions();
        }
        else
        {
            if (usesDynamicLite)
            {
                Debug.LogWarning(
                    "Dynamic Space Background Lite prefab is unavailable. " +
                    "Falling back to the legacy generated normal background.",
                    this
                );
            }

            CreateLegacyNormalBackground();
        }

        CreateMinimalBackground();

        UpdateLayerPositions();
        CacheBackgroundRendererLists();
        ApplyBackgroundVisibility(isCurseBackgroundActive);
        ApplyCurseBackgroundState(isCurseBackgroundActive, true);
        ApplyCurrentEncounterBackgroundImmediate();
        LogGeneratedBackgroundDiagnostics();

        Debug.Log($"Space background generated. Seed: {finalSeed}", this);
    }

    private void CreateLegacyNormalBackground()
    {
        farColorLayer = CreateLayer("00_FarColorBase");
        starLayer = CreateLayer("01_StaticStarField");
        cloudLayer = CreateLayer("02_NebulaPatches");
        planetLayer = CreateLayer("03_DistantPlanets");
        flareLayer = CreateLayer("04_DistantFlares");
        wispLayer = null;

        CacheLayerOriginPositions();

        CreateStarfield();
        CreateStratifiedCoverage();
        CreateDistantNebulae();
        CreatePlanets();
        CreateLargeFlares();
    }

    private bool TryCreateDynamicSpaceBackgroundLite()
    {
        if (dynamicSpaceBackgroundLitePrefab == null)
        {
            return false;
        }

        GameObject instance = Instantiate(dynamicSpaceBackgroundLitePrefab);
        instance.name = "DynamicLiteBackdrop";

        dynamicLiteRoot = instance.transform;
        dynamicLiteRoot.SetParent(generatedRoot, false);
        dynamicLiteRoot.position = new Vector3(mapCenter.x, mapCenter.y, backgroundZ);
        dynamicLiteRoot.localRotation = Quaternion.identity;

        Vector2 coverageSize = GetStaticCoverageSize() +
            Vector2.one * Mathf.Max(0f, dynamicLiteCoveragePadding) * 2f;
        SpriteRenderer[] renderers = dynamicLiteRoot.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            Transform rendererTransform = renderer.transform;
            Vector3 localScale = rendererTransform.localScale;
            Vector3 localPosition = rendererTransform.localPosition;
            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(localScale.x));
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(localScale.y));
            float offsetX = Mathf.Abs(localPosition.x * localScale.x);
            float offsetY = Mathf.Abs(localPosition.y * localScale.y);

            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = new Vector2(
                (coverageSize.x + offsetX * 2f) / scaleX,
                (coverageSize.y + offsetY * 2f) / scaleY
            );
        }

        farColorLayer = dynamicLiteRoot;
        starLayer = null;
        planetLayer = null;
        cloudLayer = null;
        wispLayer = null;
        flareLayer = null;
        CacheDynamicLiteRenderers(true);
        return true;
    }

    private void PrepareAuthoredBackground()
    {
        ResolveTargetCamera();

        generatedRoot = authoredNormalBackgroundRoot;
        generatedRoot.gameObject.name = GeneratedRootName;

        int finalSeed = ResolveBackgroundSeed();

        random = new System.Random(finalSeed);

        Transform existingMinimalRoot = transform.Find(MinimalRootName);
        if (existingMinimalRoot == null)
        {
            CreateMinimalBackground();
        }
        else
        {
            minimalBackgroundRoot = existingMinimalRoot;
        }

        CacheGeneratedLayersIfNeeded();
        CacheLayerOriginPositions();
        CacheBackgroundRendererLists();
    }

    [ContextMenu("Clear Generated Background")]
    public void ClearGeneratedBackground()
    {
        KillTemporaryBossBackgroundTween();
        ClearBossScreenOverlayImmediate();

        if (curseTransitionTween != null)
        {
            curseTransitionTween.Kill(false);
            curseTransitionTween = null;
        }

        if (authoredNormalBackgroundRoot == null)
        {
            DestroyGeneratedObject(transform.Find(GeneratedRootName));
        }
        DestroyGeneratedObject(transform.Find(MinimalRootName));
        DestroyGeneratedObject(transform.Find(CurseTransitionOverlayName));

        if (generatedFarColorSprite != null)
        {
            if (Application.isPlaying)
            {
                Destroy(generatedFarColorSprite);
            }
            else
            {
                DestroyImmediate(generatedFarColorSprite);
            }

            generatedFarColorSprite = null;
        }

        ClearCachedLayers();
    }

    public Tween BeginBossEncounterPresentation(float duration = -1f)
    {
        if (!TryPrepareDynamicLitePresentation())
        {
            return null;
        }

        KillTemporaryBossBackgroundTween();

        float resolvedDuration = duration >= 0f
            ? Mathf.Max(0.05f, duration)
            : Mathf.Max(0.05f, bossBackgroundFadeDuration);
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);

        AppendRendererColorTween(
            sequence,
            dynamicLiteFarRenderer,
            WithAlpha(
                dynamicLiteFarBaseColor,
                dynamicLiteFarBaseColor.a * Mathf.Clamp01(bossFarLayerAlphaMultiplier)
            ),
            resolvedDuration
        );
        AppendRendererColorTween(
            sequence,
            dynamicLiteNebulaRenderer,
            WithAlpha(dynamicLiteNebulaBaseColor, 0f),
            resolvedDuration
        );
        AppendRendererColorTween(
            sequence,
            dynamicLiteDetailRenderer,
            WithAlpha(dynamicLiteDetailBaseColor, 0f),
            resolvedDuration
        );

        temporaryBossBackgroundTween = sequence;
        sequence.OnComplete(() => temporaryBossBackgroundTween = null);
        BeginCoreActivationScreenOverlay();
        return sequence;
    }

    public Tween RestoreExplorationPresentation(float duration = -1f, bool playRecoveryOverlay = true)
    {
        if (!TryPrepareDynamicLitePresentation())
        {
            ClearBossScreenOverlayImmediate();
            return null;
        }

        KillTemporaryBossBackgroundTween();

        float resolvedDuration = duration >= 0f
            ? Mathf.Max(0.05f, duration)
            : Mathf.Max(0.05f, explorationBackgroundRestoreDuration);
        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);

        AppendRendererColorTween(
            sequence,
            dynamicLiteFarRenderer,
            dynamicLiteFarBaseColor,
            resolvedDuration
        );
        AppendRendererColorTween(
            sequence,
            dynamicLiteNebulaRenderer,
            dynamicLiteNebulaBaseColor,
            resolvedDuration
        );
        AppendRendererColorTween(
            sequence,
            dynamicLiteDetailRenderer,
            dynamicLiteDetailBaseColor,
            resolvedDuration
        );

        temporaryBossBackgroundTween = sequence;
        sequence.OnComplete(() => temporaryBossBackgroundTween = null);

        if (playRecoveryOverlay)
        {
            PlayBossRecoveryScreenOverlay();
        }
        else
        {
            ClearBossScreenOverlayImmediate();
        }

        return sequence;
    }

    public void ReleaseCoreActivationScreenOverlay()
    {
        if (!ownsBossScreenOverlay || bossScreenOverlayCanvasGroup == null)
        {
            return;
        }

        KillBossScreenOverlayTween();
        bossScreenOverlayTween = bossScreenOverlayCanvasGroup
            .DOFade(0f, Mathf.Max(0.01f, coreActivationOverlayFadeOutDuration))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                bossScreenOverlayTween = null;
                ClearBossScreenOverlayImmediate();
            });
    }

    public void ApplyBossEncounterPresentationImmediate()
    {
        if (!TryPrepareDynamicLitePresentation())
        {
            return;
        }

        KillTemporaryBossBackgroundTween();
        SetRendererColor(
            dynamicLiteFarRenderer,
            WithAlpha(
                dynamicLiteFarBaseColor,
                dynamicLiteFarBaseColor.a * Mathf.Clamp01(bossFarLayerAlphaMultiplier)
            )
        );
        SetRendererColor(
            dynamicLiteNebulaRenderer,
            WithAlpha(dynamicLiteNebulaBaseColor, 0f)
        );
        SetRendererColor(
            dynamicLiteDetailRenderer,
            WithAlpha(dynamicLiteDetailBaseColor, 0f)
        );
    }

    public void ApplyExplorationPresentationImmediate()
    {
        if (!TryPrepareDynamicLitePresentation())
        {
            return;
        }

        KillTemporaryBossBackgroundTween();
        SetRendererColor(dynamicLiteFarRenderer, dynamicLiteFarBaseColor);
        SetRendererColor(dynamicLiteNebulaRenderer, dynamicLiteNebulaBaseColor);
        SetRendererColor(dynamicLiteDetailRenderer, dynamicLiteDetailBaseColor);
        ClearBossScreenOverlayImmediate();
    }

    private bool TryPrepareDynamicLitePresentation()
    {
        if (normalBackgroundSource != NormalSpaceBackgroundSource.DynamicSpaceBackgroundLite ||
            isCurseBackgroundActive)
        {
            return false;
        }

        CacheGeneratedLayersIfNeeded();
        CacheDynamicLiteRenderers(false);
        return dynamicLiteBaseColorsCaptured && dynamicLiteFarRenderer != null;
    }

    private void CacheDynamicLiteRenderers(bool forceRecapture)
    {
        if (dynamicLiteRoot == null)
        {
            return;
        }

        SpriteRenderer farRenderer = dynamicLiteRoot.Find("FarSpaceLayer")
            ?.GetComponent<SpriteRenderer>();
        SpriteRenderer nebulaRenderer = dynamicLiteRoot.Find("NebulaOverlay")
            ?.GetComponent<SpriteRenderer>();
        SpriteRenderer detailRenderer = dynamicLiteRoot.Find("OptionalDetailOverlay")
            ?.GetComponent<SpriteRenderer>();

        bool rendererSetChanged = farRenderer != dynamicLiteFarRenderer ||
                                  nebulaRenderer != dynamicLiteNebulaRenderer ||
                                  detailRenderer != dynamicLiteDetailRenderer;

        dynamicLiteFarRenderer = farRenderer;
        dynamicLiteNebulaRenderer = nebulaRenderer;
        dynamicLiteDetailRenderer = detailRenderer;

        if (!forceRecapture && dynamicLiteBaseColorsCaptured && !rendererSetChanged)
        {
            return;
        }

        dynamicLiteFarBaseColor = dynamicLiteFarRenderer != null
            ? dynamicLiteFarRenderer.color
            : Color.white;
        dynamicLiteNebulaBaseColor = dynamicLiteNebulaRenderer != null
            ? dynamicLiteNebulaRenderer.color
            : Color.clear;
        dynamicLiteDetailBaseColor = dynamicLiteDetailRenderer != null
            ? dynamicLiteDetailRenderer.color
            : Color.clear;
        dynamicLiteBaseColorsCaptured = dynamicLiteFarRenderer != null;
    }

    private void ApplyCurrentEncounterBackgroundImmediate()
    {
        if (isCurseBackgroundActive)
        {
            return;
        }

        GameState currentState = GameStateManager.Instance != null
            ? GameStateManager.Instance.CurrentState
            : GameState.Expedition;

        if (currentState == GameState.BossBattle || currentState == GameState.FinalBossBattle)
        {
            ApplyBossEncounterPresentationImmediate();
            return;
        }

        ApplyExplorationPresentationImmediate();
    }

    private void ResetTemporaryBossBackgroundPresentation()
    {
        KillTemporaryBossBackgroundTween();
        ClearBossScreenOverlayImmediate();

        if (dynamicLiteBaseColorsCaptured)
        {
            SetRendererColor(dynamicLiteFarRenderer, dynamicLiteFarBaseColor);
            SetRendererColor(dynamicLiteNebulaRenderer, dynamicLiteNebulaBaseColor);
            SetRendererColor(dynamicLiteDetailRenderer, dynamicLiteDetailBaseColor);
        }
    }

    private void KillTemporaryBossBackgroundTween()
    {
        if (temporaryBossBackgroundTween == null)
        {
            return;
        }

        temporaryBossBackgroundTween.Kill(false);
        temporaryBossBackgroundTween = null;
    }

    private void BeginCoreActivationScreenOverlay()
    {
        if (!TryResolveBossScreenOverlay())
        {
            return;
        }

        if (!ownsBossScreenOverlay && bossScreenFader.IsFading)
        {
            return;
        }

        KillBossScreenOverlayTween();
        bossScreenFader.SetClearImmediate();
        ownsBossScreenOverlay = true;
        bossScreenOverlayTween = bossScreenOverlayCanvasGroup
            .DOFade(
                Mathf.Clamp01(coreActivationOverlayAlpha),
                Mathf.Max(0.01f, coreActivationOverlayFadeInDuration)
            )
            .SetUpdate(true)
            .OnComplete(() => bossScreenOverlayTween = null);
    }

    private void PlayBossRecoveryScreenOverlay()
    {
        if (!TryResolveBossScreenOverlay())
        {
            return;
        }

        if (!ownsBossScreenOverlay &&
            (bossScreenFader.IsFading || bossScreenFader.Alpha > 0.001f))
        {
            return;
        }

        KillBossScreenOverlayTween();
        bossScreenFader.SetClearImmediate();
        ownsBossScreenOverlay = true;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Append(
            bossScreenOverlayCanvasGroup.DOFade(
                Mathf.Clamp01(bossRecoveryOverlayAlpha),
                Mathf.Max(0.01f, bossRecoveryOverlayFadeInDuration)
            )
        );
        sequence.Append(
            bossScreenOverlayCanvasGroup.DOFade(
                0f,
                Mathf.Max(0.01f, bossRecoveryOverlayFadeOutDuration)
            )
        );
        sequence.OnComplete(() =>
        {
            bossScreenOverlayTween = null;
            ClearBossScreenOverlayImmediate();
        });
        bossScreenOverlayTween = sequence;
    }

    private bool TryResolveBossScreenOverlay()
    {
        if (bossScreenFader == null)
        {
            bossScreenFader = ScreenFader.Instance;
        }

        if (bossScreenFader == null)
        {
            return false;
        }

        if (bossScreenOverlayCanvasGroup == null)
        {
            bossScreenOverlayCanvasGroup = bossScreenFader.GetComponent<CanvasGroup>();
        }

        return bossScreenOverlayCanvasGroup != null;
    }

    private void ClearBossScreenOverlayImmediate()
    {
        KillBossScreenOverlayTween();

        if (ownsBossScreenOverlay && bossScreenFader != null && !bossScreenFader.IsFading)
        {
            bossScreenFader.SetClearImmediate();
        }

        ownsBossScreenOverlay = false;
    }

    private void KillBossScreenOverlayTween()
    {
        if (bossScreenOverlayTween == null)
        {
            return;
        }

        bossScreenOverlayTween.Kill(false);
        bossScreenOverlayTween = null;
    }

    private static void AppendRendererColorTween(
        Sequence sequence,
        SpriteRenderer renderer,
        Color targetColor,
        float duration)
    {
        if (sequence == null || renderer == null)
        {
            return;
        }

        sequence.Join(renderer.DOColor(targetColor, duration));
    }

    private static void SetRendererColor(SpriteRenderer renderer, Color color)
    {
        if (renderer != null)
        {
            renderer.color = color;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void DestroyGeneratedObject(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target.gameObject);
        }
        else
        {
            DestroyImmediate(target.gameObject);
        }
    }

    private void ApplyBackgroundVisibility(bool curseActive)
    {
        CacheBackgroundRendererLists();

        SetRootRenderersAlpha(
            generatedBackgroundRenderers,
            generatedBackgroundRendererAlphas,
            curseActive ? 0f : 1f
        );
        SetRootRenderersAlpha(
            minimalBackgroundRenderers,
            minimalBackgroundRendererAlphas,
            curseActive ? 1f : 0f
        );

        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(!curseActive);
        }

        if (minimalBackgroundRoot != null)
        {
            minimalBackgroundRoot.gameObject.SetActive(curseActive);
        }

        SetSingleRendererAlpha(curseTransitionRenderer, 0f);
        isCurseBackgroundActive = curseActive;
    }

    private void ResolvePlayerVisualState()
    {
        if (playerVisualState == null)
        {
            playerVisualState = FindFirstObjectByType<PlayerVisualStateController>(FindObjectsInactive.Include);
        }
    }

    private void SubscribeCurseState()
    {
        ResolvePlayerVisualState();

        if (!replaceNormalBackgroundWhenPlayerCursed)
        {
            UnsubscribeCurseState();
            return;
        }

        if (subscribedPlayerVisualState == playerVisualState)
        {
            return;
        }

        UnsubscribeCurseState();
        subscribedPlayerVisualState = playerVisualState;

        if (subscribedPlayerVisualState != null)
        {
            subscribedPlayerVisualState.CurseStateChanged += HandlePlayerCurseStateChanged;
        }
    }

    private void UnsubscribeCurseState()
    {
        if (subscribedPlayerVisualState == null)
        {
            return;
        }

        subscribedPlayerVisualState.CurseStateChanged -= HandlePlayerCurseStateChanged;
        subscribedPlayerVisualState = null;
    }

    private void HandlePlayerCurseStateChanged(bool cursed)
    {
        if (!replaceNormalBackgroundWhenPlayerCursed)
        {
            return;
        }

        if (cursed)
        {
            KillTemporaryBossBackgroundTween();
            ClearBossScreenOverlayImmediate();
        }

        ApplyCurseBackgroundState(cursed, false);
    }

    private bool ResolveAutomaticCurseBackgroundState()
    {
        return replaceNormalBackgroundWhenPlayerCursed &&
               playerVisualState != null &&
               playerVisualState.IsCursed;
    }

    private void ApplyCurseBackgroundState(bool cursed, bool immediate)
    {
        CacheBackgroundRendererLists();

        if (generatedRoot == null || minimalBackgroundRoot == null ||
            generatedBackgroundRenderers.Count == 0 || minimalBackgroundRenderers.Count == 0)
        {
            isCurseBackgroundActive = cursed;
            ApplyBackgroundVisibility(cursed);
            return;
        }

        if (curseTransitionTween != null)
        {
            curseTransitionTween.Kill(false);
            curseTransitionTween = null;
        }

        bool previousCurseState = isCurseBackgroundActive;
        isCurseBackgroundActive = cursed;

        if (immediate || previousCurseState == cursed)
        {
            ApplyBackgroundVisibility(cursed);

            if (!cursed)
            {
                ApplyCurrentEncounterBackgroundImmediate();
            }

            return;
        }

        generatedRoot.gameObject.SetActive(true);
        minimalBackgroundRoot.gameObject.SetActive(true);

        float generatedStartAlpha = previousCurseState ? 0f : 1f;
        float minimalStartAlpha = previousCurseState ? 1f : 0f;
        float generatedTargetAlpha = cursed ? 0f : 1f;
        float minimalTargetAlpha = cursed ? 1f : 0f;

        SetRootRenderersAlpha(
            generatedBackgroundRenderers,
            generatedBackgroundRendererAlphas,
            generatedStartAlpha
        );
        SetRootRenderersAlpha(
            minimalBackgroundRenderers,
            minimalBackgroundRendererAlphas,
            minimalStartAlpha
        );
        SetSingleRendererAlpha(curseTransitionRenderer, 0f);

        float transitionDuration = cursed
            ? Mathf.Max(0.01f, curseToCursedTransitionDuration)
            : Mathf.Max(0.01f, curseToNormalTransitionDuration);
        float flashInDuration = Mathf.Min(0.12f, transitionDuration * 0.25f);
        float flashOutDuration = Mathf.Min(0.16f, transitionDuration * 0.3f);
        float flashPeak = cursed ? 0.4f : 0.22f;

        Sequence sequence = DOTween.Sequence();
        sequence.SetUpdate(true);
        sequence.Append(
            DOVirtual.Float(
                0f,
                flashPeak,
                Mathf.Max(0.01f, flashInDuration),
                value => SetSingleRendererAlpha(curseTransitionRenderer, value)
            )
        );
        sequence.Join(
            DOVirtual.Float(
                generatedStartAlpha,
                generatedTargetAlpha,
                transitionDuration,
                value => SetRootRenderersAlpha(
                    generatedBackgroundRenderers,
                    generatedBackgroundRendererAlphas,
                    value
                )
            )
        );
        sequence.Join(
            DOVirtual.Float(
                minimalStartAlpha,
                minimalTargetAlpha,
                transitionDuration,
                value => SetRootRenderersAlpha(
                    minimalBackgroundRenderers,
                    minimalBackgroundRendererAlphas,
                    value
                )
            )
        );
        sequence.Append(
            DOVirtual.Float(
                flashPeak,
                0f,
                Mathf.Max(0.01f, flashOutDuration),
                value => SetSingleRendererAlpha(curseTransitionRenderer, value)
            )
        );
        sequence.OnComplete(() =>
        {
            curseTransitionTween = null;
            ApplyBackgroundVisibility(cursed);

            if (!cursed)
            {
                ApplyCurrentEncounterBackgroundImmediate();
            }
        });

        curseTransitionTween = sequence;
    }

    private void CacheBackgroundRendererLists()
    {
        CacheGeneratedLayersIfNeeded();

        if (minimalBackgroundRoot == null)
        {
            minimalBackgroundRoot = transform.Find(MinimalRootName);
        }

        if (curseTransitionRenderer == null)
        {
            Transform overlay = transform.Find(CurseTransitionOverlayName);
            curseTransitionRenderer = overlay != null ? overlay.GetComponent<SpriteRenderer>() : null;
        }

        CacheRootRenderers(generatedRoot, generatedBackgroundRenderers, generatedBackgroundRendererAlphas);
        CacheRootRenderers(minimalBackgroundRoot, minimalBackgroundRenderers, minimalBackgroundRendererAlphas);
    }

    private void CacheRootRenderers(
        Transform root,
        List<SpriteRenderer> renderers,
        List<float> baseAlphas)
    {
        renderers.Clear();
        baseAlphas.Clear();

        if (root == null)
        {
            return;
        }

        SpriteRenderer[] children = root.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < children.Length; i++)
        {
            SpriteRenderer renderer = children[i];

            if (renderer == null)
            {
                continue;
            }

            int instanceId = renderer.GetInstanceID();
            if (!rendererBaseAlphaByInstanceId.TryGetValue(instanceId, out float baseAlpha))
            {
                baseAlpha = renderer.color.a;
                rendererBaseAlphaByInstanceId.Add(instanceId, baseAlpha);
            }

            renderers.Add(renderer);
            baseAlphas.Add(baseAlpha);
        }
    }

    private void SetRootRenderersAlpha(List<SpriteRenderer> renderers, List<float> baseAlphas, float normalizedAlpha)
    {
        normalizedAlpha = Mathf.Clamp01(normalizedAlpha);

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            float baseAlpha = i < baseAlphas.Count ? baseAlphas[i] : 1f;
            Color color = renderer.color;
            color.a = baseAlpha * normalizedAlpha;
            renderer.color = color;
        }
    }

    private void SetSingleRendererAlpha(SpriteRenderer renderer, float targetAlpha)
    {
        if (renderer == null)
        {
            return;
        }

        Color color = renderer.color;
        color.a = Mathf.Clamp01(targetAlpha) * Mathf.Clamp01(curseTransitionColor.a);
        renderer.color = color;
    }

    private void ResetCurseTransition()
    {
        if (curseTransitionTween != null)
        {
            curseTransitionTween.Kill(false);
            curseTransitionTween = null;
        }

        ApplyBackgroundVisibility(isCurseBackgroundActive);
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
        farColorLayerOriginPosition += delta;
        planetLayerOriginPosition += delta;
        cloudLayerOriginPosition += delta;
        wispLayerOriginPosition += delta;
        flareLayerOriginPosition += delta;

        ForceSyncNow();
    }

    private void ClearCachedLayers()
    {
        generatedRoot = null;
        minimalBackgroundRoot = null;
        dynamicLiteRoot = null;
        dynamicLiteFarRenderer = null;
        dynamicLiteNebulaRenderer = null;
        dynamicLiteDetailRenderer = null;
        dynamicLiteBaseColorsCaptured = false;
        curseTransitionRenderer = null;
        farColorLayer = null;
        starLayer = null;
        planetLayer = null;
        cloudLayer = null;
        wispLayer = null;
        flareLayer = null;
        starfieldRenderer = null;
        starfieldRenderers.Clear();
        starfieldCoverageMultipliers.Clear();
        generatedBackgroundRenderers.Clear();
        generatedBackgroundRendererAlphas.Clear();
        minimalBackgroundRenderers.Clear();
        minimalBackgroundRendererAlphas.Clear();
        rendererBaseAlphaByInstanceId.Clear();

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
        farColorLayerOriginPosition = farColorLayer != null ? farColorLayer.position : Vector3.zero;
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

        if (normalBackgroundSource == NormalSpaceBackgroundSource.DynamicSpaceBackgroundLite)
        {
            if (dynamicLiteRoot == null)
            {
                dynamicLiteRoot = generatedRoot.Find("DynamicLiteBackdrop");
            }

            if (dynamicLiteRoot != null)
            {
                farColorLayer = dynamicLiteRoot;
                starLayer = null;
                planetLayer = null;
                cloudLayer = null;
                wispLayer = null;
                flareLayer = null;
                CacheDynamicLiteRenderers(false);
                return;
            }
        }

        if (farColorLayer == null)
        {
            farColorLayer = generatedRoot.Find("00_FarColorBase");
        }

        if (starLayer == null)
        {
            starLayer = generatedRoot.Find("01_StaticStarField");
        }

        if (cloudLayer == null)
        {
            cloudLayer = generatedRoot.Find("02_NebulaPatches");
        }

        if (planetLayer == null)
        {
            planetLayer = generatedRoot.Find("03_DistantPlanets");
        }

        if (flareLayer == null)
        {
            flareLayer = generatedRoot.Find("04_DistantFlares");
        }

        if (farColorLayer != null && starfieldRenderers.Count == 0)
        {
            SpriteRenderer[] renderers = farColorLayer.GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || !renderer.name.StartsWith("FarColorBase"))
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
        if (farColorLayer == null)
        {
            return;
        }

        starfieldRenderers.Clear();
        starfieldCoverageMultipliers.Clear();

        generatedFarColorSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );
        generatedFarColorSprite.name = "GeneratedFarColorSprite";
        generatedFarColorSprite.hideFlags = HideFlags.DontSave;

        Vector2 backgroundSize = GetStaticCoverageSize() * Mathf.Max(1f, starfieldCoverOverscan);
        GameObject backdrop = CreateSpriteObjectNative(
            "FarColorBase_00",
            generatedFarColorSprite,
            farColorLayer,
            Vector2.zero,
            random.Next(0, 2) * 180f,
            farColorTint,
            baseSortingOrder - 20,
            Vector3.one
        );

        SpriteRenderer renderer = backdrop.GetComponent<SpriteRenderer>();
        backdrop.transform.localScale = CalculateCoverScale(
            generatedFarColorSprite,
            backgroundSize,
            true
        );

        if (renderer != null)
        {
            renderer.drawMode = SpriteDrawMode.Simple;
            starfieldRenderers.Add(renderer);
            starfieldCoverageMultipliers.Add(1f);
        }

        starfieldRenderer = renderer;
    }

    private void CreateLegacyStarfieldComposition()
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

    private void CreateMinimalBackground()
    {
        minimalBackgroundRoot = new GameObject(MinimalRootName).transform;
        minimalBackgroundRoot.SetParent(transform, false);
        minimalBackgroundRoot.localPosition = new Vector3(0f, 0f, backgroundZ);
        minimalBackgroundRoot.localRotation = Quaternion.identity;
        minimalBackgroundRoot.localScale = Vector3.one;

        Vector2 coverage = GetInitialStarfieldSize() * Mathf.Max(1f, starfieldCoverOverscan);
        coverage.x = Mathf.Max(24f, coverage.x);
        coverage.y = Mathf.Max(14f, coverage.y);

        Sprite overlaySprite = ResolveMinimalBackgroundSprite();
        if (overlaySprite != null)
        {
            Color voidColor = new Color(0.02f, 0.008f, 0.035f, 0.3f);
            GameObject backdrop = CreateSpriteObjectNative(
                "MinimalVoidBackdrop",
                overlaySprite,
                minimalBackgroundRoot,
                mapCenter,
                0f,
                voidColor,
                baseSortingOrder - 140,
                Vector3.one
            );
            backdrop.transform.localScale = CalculateCoverScale(overlaySprite, coverage, true);
        }

        if (smallFlareSprite != null)
        {
            Vector2 half = coverage * 0.5f;

            for (int i = 0; i < MinimalStarCount; i++)
            {
                Vector2 position = mapCenter + new Vector2(
                    RandomRange(-half.x, half.x),
                    RandomRange(-half.y, half.y)
                );
                float size = RandomRange(0.045f, 0.095f);
                float purpleMix = RandomRange(0f, 1f);
                Color color = Color.Lerp(
                    new Color(0.42f, 0.58f, 0.82f, 1f),
                    new Color(0.68f, 0.34f, 0.92f, 1f),
                    purpleMix
                );
                color.a = RandomRange(0.11f, 0.23f);

                CreateSpriteObjectWorldSize(
                    $"MinimalStar_{i:00}",
                    smallFlareSprite,
                    minimalBackgroundRoot,
                    position,
                    new Vector2(size, size),
                    0f,
                    color,
                    baseSortingOrder - 120
                );
            }

            for (int i = 0; i < MinimalCurseFragmentCount; i++)
            {
                Vector2 position = mapCenter + new Vector2(
                    RandomRange(-half.x, half.x),
                    RandomRange(-half.y, half.y)
                );
                float width = RandomRange(0.08f, 0.2f);
                float height = RandomRange(0.06f, 0.16f);
                Color color = new Color(0.72f, 0.2f, 1f, RandomRange(0.12f, 0.24f));

                CreateSpriteObjectWorldSize(
                    $"CurseFragment_{i:00}",
                    smallFlareSprite,
                    minimalBackgroundRoot,
                    position,
                    new Vector2(width, height),
                    random.Next(0, 4) * 90f,
                    color,
                    baseSortingOrder - 110
                );
            }
        }

        CreateCurseTransitionOverlay(overlaySprite, coverage);
    }

    private Sprite ResolveMinimalBackgroundSprite()
    {
        List<Sprite> validSprites = GetValidStarfieldSprites();

        if (validSprites.Count > 0)
        {
            return validSprites[0];
        }

        if (starFlareSprite != null)
        {
            return starFlareSprite;
        }

        return smallFlareSprite;
    }

    private void CreateCurseTransitionOverlay(Sprite overlaySprite, Vector2 coverage)
    {
        if (overlaySprite == null)
        {
            curseTransitionRenderer = null;
            return;
        }

        Transform existing = transform.Find(CurseTransitionOverlayName);
        if (existing != null)
        {
            DestroyGeneratedObject(existing);
        }

        Color overlayColor = curseTransitionColor;
        overlayColor.a = 0f;

        GameObject overlay = CreateSpriteObjectNative(
            CurseTransitionOverlayName,
            overlaySprite,
            transform,
            mapCenter,
            0f,
            overlayColor,
            baseSortingOrder + 80,
            Vector3.one
        );
        overlay.transform.localPosition = new Vector3(mapCenter.x, mapCenter.y, backgroundZ - 0.25f);
        overlay.transform.localScale = CalculateCoverScale(overlaySprite, coverage, true);

        curseTransitionRenderer = overlay.GetComponent<SpriteRenderer>();
        SetSingleRendererAlpha(curseTransitionRenderer, 0f);
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

        int minimumCount = Mathf.Clamp(planetCountRange.x, 0, 2);
        int maximumCount = Mathf.Clamp(planetCountRange.y, minimumCount, 2);
        int count = random.Next(minimumCount, maximumCount + 1);

        for (int i = 0; i < count; i++)
        {
            Sprite sprite = PickPlanetSprite();

            if (sprite == null)
            {
                continue;
            }

            Vector2 position = randomizePlanetPosition
                ? RandomPlanetPosition()
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
                baseSortingOrder - 5
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

    private void CreateDistantNebulae()
    {
        if (!generateDistantNebulae || distantNebulaCount <= 0 || cloudLayer == null)
        {
            return;
        }

        Vector2 coverageSize = GetStaticCoverageSize();
        Vector2 coverageCenter = mapCenter;

        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(distantNebulaCount)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)distantNebulaCount / columns));
        Vector2 cellSize = new Vector2(coverageSize.x / columns, coverageSize.y / rows);
        Vector2 coverageMin = coverageCenter - coverageSize * 0.5f;
        int cellCount = columns * rows;
        int[] cellOrder = new int[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            cellOrder[i] = i;
        }

        for (int i = cellCount - 1; i > 0; i--)
        {
            int swapIndex = random.Next(0, i + 1);
            (cellOrder[i], cellOrder[swapIndex]) = (cellOrder[swapIndex], cellOrder[i]);
        }

        for (int i = 0; i < distantNebulaCount; i++)
        {
            Sprite sprite = PickSprite(distantNebulaSprites);
            if (sprite == null)
            {
                return;
            }

            int cellIndex = cellOrder[i % cellCount];
            int column = cellIndex % columns;
            int row = cellIndex / columns;
            Vector2 position = new Vector2(
                coverageMin.x + (column + RandomRange(0.15f, 0.85f)) * cellSize.x,
                coverageMin.y + (row + RandomRange(0.15f, 0.85f)) * cellSize.y
            );
            float minimumCoverageWidth = Mathf.Min(cellSize.x, cellSize.y) * 0.78f;
            float width = Mathf.Max(
                minimumCoverageWidth,
                RandomRange(distantNebulaWorldSizeRange.x, distantNebulaWorldSizeRange.y)
            );
            float height = width * RandomRange(0.65f, 1.15f);
            Color color = Color.Lerp(distantNebulaTintA, distantNebulaTintB, Random01());
            color.a = RandomRange(distantNebulaAlphaRange.x, distantNebulaAlphaRange.y);

            CreateSpriteObjectWorldSize(
                $"DistantNebula_{i:00}",
                sprite,
                cloudLayer,
                position,
                new Vector2(width, height),
                RandomRange(0f, 360f),
                color,
                baseSortingOrder - 15
            );
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

    private void CreateStratifiedCoverage()
    {
        if (!generateStratifiedCoverage || starLayer == null)
        {
            return;
        }

        Sprite fallbackSprite = smallFlareSprite != null ? smallFlareSprite : starFlareSprite;
        if (!HasValidSprite(coverageSpeckSprites) && fallbackSprite == null)
        {
            return;
        }

        Vector2 coverageSize = GetStaticCoverageSize();
        Vector2 halfSize = coverageSize * 0.5f;
        float cellSize = Mathf.Max(2f, coverageCellSize);
        int columns = Mathf.Max(1, Mathf.CeilToInt(coverageSize.x / cellSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(coverageSize.y / cellSize));
        int minimumPerCell = Mathf.Clamp(starsPerCellRange.x, 1, 3);
        int maximumPerCell = Mathf.Clamp(starsPerCellRange.y, minimumPerCell, 3);

        List<ParticleSystem.Particle> faintStars = new List<ParticleSystem.Particle>(
            columns * rows * maximumPerCell
        );
        List<ParticleSystem.Particle> rareStars = new List<ParticleSystem.Particle>(
            Mathf.CeilToInt(columns * rows * rareStarChance)
        );

        for (int row = 0; row < rows; row++)
        {
            float cellMinY = mapCenter.y - halfSize.y + row * cellSize;
            float cellHeight = Mathf.Min(cellSize, mapCenter.y + halfSize.y - cellMinY);

            for (int column = 0; column < columns; column++)
            {
                float cellMinX = mapCenter.x - halfSize.x + column * cellSize;
                float cellWidth = Mathf.Min(cellSize, mapCenter.x + halfSize.x - cellMinX);
                int starCount = Random01() < sparseCellChance
                    ? 1
                    : random.Next(minimumPerCell, maximumPerCell + 1);

                for (int i = 0; i < starCount; i++)
                {
                    Vector3 position = new Vector3(
                        cellMinX + cellWidth * RandomRange(0.08f, 0.92f),
                        cellMinY + cellHeight * RandomRange(0.08f, 0.92f),
                        backgroundZ
                    );
                    Color color = Color.Lerp(coverageSpeckTintA, coverageSpeckTintB, Random01());
                    color.a = RandomRange(coverageSpeckAlphaRange.x, coverageSpeckAlphaRange.y);
                    faintStars.Add(CreateStaticStarParticle(
                        position,
                        coverageSpeckWorldSizeRange,
                        color
                    ));
                }

                if (Random01() < rareStarChance)
                {
                    Vector3 position = new Vector3(
                        cellMinX + cellWidth * RandomRange(0.15f, 0.85f),
                        cellMinY + cellHeight * RandomRange(0.15f, 0.85f),
                        backgroundZ
                    );
                    Color color = Color.Lerp(coverageSpeckTintA, Color.white, 0.35f);
                    color.a = RandomRange(rareStarAlphaRange.x, rareStarAlphaRange.y);
                    rareStars.Add(CreateStaticStarParticle(position, rareStarWorldSizeRange, color));
                }
            }
        }

        generatedMainStarCount = CreateStaticStarParticleSystem(
            "StaticStarField",
            faintStars,
            baseSortingOrder - 10
        );
        generatedRareStarCount = CreateStaticStarParticleSystem(
            "RareBrightStars",
            rareStars,
            baseSortingOrder - 9
        );

        generatedBackgroundCoverageBounds = new Bounds(
            new Vector3(mapCenter.x, mapCenter.y, backgroundZ),
            new Vector3(coverageSize.x, coverageSize.y, 1f)
        );
    }

    private void CreateLegacyStratifiedCoverage()
    {
        if (!generateStratifiedCoverage || starLayer == null)
        {
            return;
        }

        Sprite fallbackCoverageSprite = smallFlareSprite != null ? smallFlareSprite : starFlareSprite;
        if (!HasValidSprite(coverageSpeckSprites) && fallbackCoverageSprite == null)
        {
            return;
        }

        Vector2 coverageSize = GetBackgroundAreaSize();
        Vector2 coverageCenter = Vector2.zero;

        if (!moveWithFollowTarget)
        {
            Vector2 cameraSize = GetCameraViewSize();
            coverageSize = new Vector2(
                Mathf.Max(coverageSize.x, mapSize.x + cameraSize.x),
                Mathf.Max(coverageSize.y, mapSize.y + cameraSize.y)
            );
            coverageCenter = mapCenter;
        }

        float cellSize = Mathf.Max(2f, coverageCellSize);
        int columns = Mathf.Max(1, Mathf.CeilToInt(coverageSize.x / cellSize));
        int rows = Mathf.Max(1, Mathf.CeilToInt(coverageSize.y / cellSize));
        int specksPerCell = Mathf.Clamp(coverageSpecksPerCell, 1, 2);
        Vector2 halfSize = coverageSize * 0.5f;
        int speckIndex = 0;

        for (int row = 0; row < rows; row++)
        {
            float cellMinY = coverageCenter.y - halfSize.y + row * cellSize;
            float cellHeight = Mathf.Min(cellSize, coverageCenter.y + halfSize.y - cellMinY);

            for (int column = 0; column < columns; column++)
            {
                float cellMinX = coverageCenter.x - halfSize.x + column * cellSize;
                float cellWidth = Mathf.Min(cellSize, coverageCenter.x + halfSize.x - cellMinX);

                for (int i = 0; i < specksPerCell; i++)
                {
                    Sprite coverageSprite = PickSprite(coverageSpeckSprites);
                    coverageSprite ??= fallbackCoverageSprite;

                    Vector2 position = new Vector2(
                        cellMinX + cellWidth * RandomRange(0.18f, 0.82f),
                        cellMinY + cellHeight * RandomRange(0.18f, 0.82f)
                    );
                    float size = RandomRange(
                        coverageSpeckWorldSizeRange.x,
                        coverageSpeckWorldSizeRange.y
                    );
                    Color color = Color.Lerp(coverageSpeckTintA, coverageSpeckTintB, Random01());
                    color.a = RandomRange(coverageSpeckAlphaRange.x, coverageSpeckAlphaRange.y);

                    CreateSpriteObjectWorldSize(
                        $"CoverageSpeck_{speckIndex++:000}",
                        coverageSprite,
                        starLayer,
                        position,
                        new Vector2(size, size),
                        RandomRange(0f, 360f),
                        color,
                        baseSortingOrder + 4
                    );
                }
            }
        }
    }

    private ParticleSystem.Particle CreateStaticStarParticle(
        Vector3 position,
        Vector2 sizeRange,
        Color color)
    {
        const float lifetime = 100000f;

        return new ParticleSystem.Particle
        {
            position = position,
            rotation = RandomRange(0f, 360f),
            startSize = RandomRange(sizeRange.x, sizeRange.y),
            startColor = color,
            startLifetime = lifetime,
            remainingLifetime = lifetime,
            randomSeed = (uint)random.Next(1, int.MaxValue)
        };
    }

    private int CreateStaticStarParticleSystem(
        string objectName,
        List<ParticleSystem.Particle> particles,
        int sortingOrder)
    {
        if (particles == null || particles.Count == 0 || starLayer == null)
        {
            return 0;
        }

        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(starLayer, false);
        particleObject.transform.localPosition = Vector3.zero;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        main.startLifetime = 100000f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = particles.Count;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = false;

        ParticleSystem.TextureSheetAnimationModule textureSheet = particleSystem.textureSheetAnimation;
        textureSheet.enabled = false;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = sortingOrder;
        particleRenderer.sharedMaterial = staticStarParticleMaterial;
        particleRenderer.localBounds = ResolveStaticParticleLocalBounds(particleRenderer.transform);

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
        {
            particleRenderer.sortingLayerName = sortingLayerName;
        }

        particleSystem.Play(false);
        particleSystem.SetParticles(particles.ToArray(), particles.Count);
        particleSystem.Pause(false);
        return particleSystem.particleCount;
    }

    private void CreateLargeFlares()
    {
        if (starFlareSprite == null)
        {
            return;
        }

        for (int i = 0; i < largeFlareCount; i++)
        {
            Vector2 position = RandomPlanetPosition();
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
                baseSortingOrder - 4
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

        ResolveTargetCamera();
        hasLayerCameraSample = targetCamera != null;
        if (hasLayerCameraSample)
        {
            layerCameraSample = targetCamera.transform.position;
        }

        if (generatedRoot == null)
        {
            return;
        }

        Vector3 followPosition = ResolveFollowPosition();
        Vector3 planetFollowPosition = ResolvePlanetFollowPosition(followPosition);

        if (!moveWithFollowTarget)
        {
            SetLayerPosition(farColorLayer, farColorLayerOriginPosition);
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
            SetLayerPosition(farColorLayer, farColorLayerOriginPosition + delta);
            SetLayerPosition(starLayer, starLayerOriginPosition + delta);
            SetLayerPosition(planetLayer, planetLayerOriginPosition + planetDelta);
            SetLayerPosition(cloudLayer, cloudLayerOriginPosition + delta);
            SetLayerPosition(wispLayer, wispLayerOriginPosition + delta);
            SetLayerPosition(flareLayer, flareLayerOriginPosition + delta);
            return;
        }

        SetLayerPosition(farColorLayer, farColorLayerOriginPosition + delta * starMovementResistance);
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
        if (!moveWithFollowTarget ||
            !stabilizePixelArtParallax ||
            layer == null ||
            assetsPixelsPerUnit <= 0)
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
        Vector3 cameraPosition = hasLayerCameraSample
            ? layerCameraSample
            : targetCamera.transform.position;
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
        if (!syncBeforeCameraRender || !moveWithFollowTarget || camera == null)
        {
            return;
        }

        CacheGeneratedLayersIfNeeded();

        if (generatedRoot == null || !generatedRoot.gameObject.activeSelf)
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
        if (!autoExpandStarfieldForCameraZoom || !moveWithFollowTarget)
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

    private Vector2 GetStaticCoverageSize()
    {
        Vector2 maximumCameraSize = GetCameraViewSize() * Mathf.Max(1f, expectedMaximumZoomMultiplier);
        float margin = Mathf.Max(0f, cameraViewMargin + extraZoomCoverageMargin);

        return new Vector2(
            Mathf.Max(mapSize.x + maximumCameraSize.x, maximumCameraSize.x) + margin * 2f,
            Mathf.Max(mapSize.y + maximumCameraSize.y, maximumCameraSize.y) + margin * 2f
        );
    }

    private Bounds ResolveStaticParticleLocalBounds(Transform rendererTransform)
    {
        Vector2 coverageSize = GetStaticCoverageSize();
        float padding = Mathf.Max(0f, staticParticleBoundsPadding);
        Vector3 worldCenter = new Vector3(mapCenter.x, mapCenter.y, backgroundZ);
        Vector3 localCenter = rendererTransform != null
            ? rendererTransform.InverseTransformPoint(worldCenter)
            : worldCenter;

        return new Bounds(
            localCenter,
            new Vector3(
                coverageSize.x + padding * 2f,
                coverageSize.y + padding * 2f,
                Mathf.Max(4f, padding * 2f + 1f)
            )
        );
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogGeneratedBackgroundDiagnostics()
    {
        if (!logBackgroundDiagnostics)
        {
            return;
        }

        ParticleSystemRenderer mainRenderer = starLayer != null
            ? starLayer.Find("StaticStarField")?.GetComponent<ParticleSystemRenderer>()
            : null;
        ParticleSystemRenderer rareRenderer = starLayer != null
            ? starLayer.Find("RareBrightStars")?.GetComponent<ParticleSystemRenderer>()
            : null;
        ParticleSystem mainSystem = mainRenderer != null
            ? mainRenderer.GetComponent<ParticleSystem>()
            : null;
        string materialDescription = staticStarParticleMaterial != null
            ? $"{staticStarParticleMaterial.name}/{staticStarParticleMaterial.shader.name}"
            : "missing";

        Debug.Log(
            $"Background generated. Main stars: {generatedMainStarCount}, " +
            $"Rare stars: {generatedRareStarCount}, Coverage: {generatedBackgroundCoverageBounds}, " +
            $"Main bounds: {(mainRenderer != null ? mainRenderer.localBounds.ToString() : "missing")}, " +
            $"Rare bounds: {(rareRenderer != null ? rareRenderer.localBounds.ToString() : "missing")}, " +
            $"Simulation: {(mainSystem != null ? mainSystem.main.simulationSpace.ToString() : "missing")}, " +
            $"Render: {(mainRenderer != null ? mainRenderer.renderMode.ToString() : "missing")}, " +
            $"Sorting: {(mainRenderer != null ? $"{mainRenderer.sortingLayerName}/{mainRenderer.sortingOrder}" : "missing")}, " +
            $"Size: {coverageSpeckWorldSizeRange}, Alpha: {coverageSpeckAlphaRange}, " +
            $"Material: {materialDescription}, " +
            $"Normal root active: {(generatedRoot != null && generatedRoot.gameObject.activeInHierarchy)}",
            this
        );
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

    private Vector2 RandomPlanetPosition()
    {
        Vector2 halfMap = mapSize * 0.5f;
        float minimumRadius = Mathf.Max(
            planetAvoidCenterRadius,
            Mathf.Min(halfMap.x, halfMap.y) * 0.45f
        );

        for (int i = 0; i < 32; i++)
        {
            Vector2 position = new Vector2(
                RandomRange(-halfMap.x, halfMap.x),
                RandomRange(-halfMap.y, halfMap.y)
            );

            if (position.sqrMagnitude >= minimumRadius * minimumRadius)
            {
                return position;
            }
        }

        Vector2 direction = RandomInsideUnitCircle();
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        return direction.normalized * minimumRadius;
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

    private static bool HasValidSprite(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return false;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                return true;
            }
        }

        return false;
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

    private int ResolveBackgroundSeed()
    {
        if (!randomizeSeed)
        {
            return unchecked(seed + backgroundSeedOffset);
        }

        long ticks = System.DateTime.UtcNow.Ticks;
        return unchecked(
            seed * 397 ^
            backgroundSeedOffset * 31 ^
            GetInstanceID() ^
            (int)ticks ^
            (int)(ticks >> 32)
        );
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
        curseToCursedTransitionDuration = Mathf.Max(0.01f, curseToCursedTransitionDuration);
        curseToNormalTransitionDuration = Mathf.Max(0.01f, curseToNormalTransitionDuration);
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
        planetCountRange.x = Mathf.Clamp(planetCountRange.x, 0, 2);
        planetCountRange.y = Mathf.Clamp(planetCountRange.y, planetCountRange.x, 2);
        planetWorldSizeRange.x = Mathf.Max(0.1f, planetWorldSizeRange.x);
        planetWorldSizeRange.y = Mathf.Max(planetWorldSizeRange.x, planetWorldSizeRange.y);
        dustCloudCount = Mathf.Max(0, dustCloudCount);
        dustWispCount = Mathf.Max(0, dustWispCount);
        smallFlareCount = Mathf.Max(0, smallFlareCount);
        largeFlareCount = Mathf.Max(0, largeFlareCount);
        distantNebulaCount = Mathf.Max(0, distantNebulaCount);
        distantNebulaWorldSizeRange.x = Mathf.Max(0.1f, distantNebulaWorldSizeRange.x);
        distantNebulaWorldSizeRange.y = Mathf.Max(
            distantNebulaWorldSizeRange.x,
            distantNebulaWorldSizeRange.y
        );
        distantNebulaAlphaRange.x = Mathf.Clamp01(distantNebulaAlphaRange.x);
        distantNebulaAlphaRange.y = Mathf.Clamp(
            distantNebulaAlphaRange.y,
            distantNebulaAlphaRange.x,
            1f
        );
        coverageCellSize = Mathf.Max(2f, coverageCellSize);
        starsPerCellRange.x = Mathf.Clamp(starsPerCellRange.x, 1, 3);
        starsPerCellRange.y = Mathf.Clamp(starsPerCellRange.y, starsPerCellRange.x, 3);
        coverageSpecksPerCell = Mathf.Clamp(coverageSpecksPerCell, 1, 2);
        coverageSpeckWorldSizeRange.x = Mathf.Max(0.01f, coverageSpeckWorldSizeRange.x);
        coverageSpeckWorldSizeRange.y = Mathf.Max(
            coverageSpeckWorldSizeRange.x,
            coverageSpeckWorldSizeRange.y
        );
        coverageSpeckAlphaRange.x = Mathf.Clamp01(coverageSpeckAlphaRange.x);
        coverageSpeckAlphaRange.y = Mathf.Clamp(
            coverageSpeckAlphaRange.y,
            coverageSpeckAlphaRange.x,
            1f
        );
        rareStarWorldSizeRange.x = Mathf.Max(0.01f, rareStarWorldSizeRange.x);
        rareStarWorldSizeRange.y = Mathf.Max(
            rareStarWorldSizeRange.x,
            rareStarWorldSizeRange.y
        );
        rareStarAlphaRange.x = Mathf.Clamp01(rareStarAlphaRange.x);
        rareStarAlphaRange.y = Mathf.Clamp(rareStarAlphaRange.y, rareStarAlphaRange.x, 1f);
        dynamicLiteCoveragePadding = Mathf.Max(0f, dynamicLiteCoveragePadding);
        bossFarLayerAlphaMultiplier = Mathf.Clamp01(bossFarLayerAlphaMultiplier);
        bossBackgroundFadeDuration = Mathf.Max(0.05f, bossBackgroundFadeDuration);
        explorationBackgroundRestoreDuration = Mathf.Max(
            0.05f,
            explorationBackgroundRestoreDuration
        );
        coreActivationOverlayAlpha = Mathf.Clamp01(coreActivationOverlayAlpha);
        coreActivationOverlayFadeInDuration = Mathf.Max(0.01f, coreActivationOverlayFadeInDuration);
        coreActivationOverlayFadeOutDuration = Mathf.Max(0.01f, coreActivationOverlayFadeOutDuration);
        bossRecoveryOverlayAlpha = Mathf.Clamp01(bossRecoveryOverlayAlpha);
        bossRecoveryOverlayFadeInDuration = Mathf.Max(0.01f, bossRecoveryOverlayFadeInDuration);
        bossRecoveryOverlayFadeOutDuration = Mathf.Max(0.01f, bossRecoveryOverlayFadeOutDuration);
    }
#endif
}
