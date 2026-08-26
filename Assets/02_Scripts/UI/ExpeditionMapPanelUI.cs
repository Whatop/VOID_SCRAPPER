using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ExpeditionMapPanelUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
{
    [Header("References")]
    [SerializeField] private GameObject rootObject;
    [SerializeField] private RectTransform mapArea;
    [SerializeField] private RawImage discoveryMaskImage;
    [SerializeField] private RadarMarkerUI markerPrefab;
    [SerializeField] private RectTransform markerRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private Transform player;
    [SerializeField] private MapDiscoveryController discoveryController;
    [SerializeField] private ExpeditionRoutePlanner routePlanner;
    [SerializeField] private ExpeditionOperationController operationController;

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset mapFontAsset;

    [Header("Map View")]
    [SerializeField] private RectTransform mapViewport;
    [SerializeField] private RectTransform mapContentRoot;
    [SerializeField, Min(1f)] private float minimumZoom = 1f;
    [SerializeField, Min(1f)] private float maximumZoom = 2.25f;
    [SerializeField, Min(0.01f)] private float zoomStep = 0.15f;
    [SerializeField, Min(1f)] private float dragThresholdPixels = 6f;

    [Header("Map Controls")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string mapActionMapName = "Map";
    [SerializeField] private string routeAddActionName = "MapRouteAdd";
    [SerializeField] private string routeRemoveActionName = "MapRouteRemove";
    [SerializeField] private string routeClearActionName = "MapRouteClear";
    [SerializeField] private string uiActionMapName = "UI";
    [SerializeField] private string cancelActionName = "Cancel";
    [SerializeField] private RectTransform controlHintsRoot;
    [SerializeField] private TextMeshProUGUI controlHintsText;

    [Header("Operation Information")]
    [SerializeField] private RectTransform operationInfoRoot;
    [SerializeField] private TextMeshProUGUI operationInfoText;

    [Header("Grid")]
    [SerializeField] private MapGridGraphic mapGridGraphic;
    [SerializeField] private bool createGridIfMissing = true;
    [SerializeField, Min(0.25f)] private float gridWorldCellSize = 10f;

    [Header("Legend")]
    [SerializeField] private RectTransform legendRoot;
    [SerializeField] private TextMeshProUGUI legendText;
    [SerializeField] private bool createLegendIfMissing = true;

    [Header("Marker Colors")]
    [SerializeField] private Color enemyColor = new Color(1f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color rewardColor = new Color(1f, 0.78f, 0.22f, 1f);
    [SerializeField] private Color meteorColor = new Color(0.68f, 0.78f, 0.9f, 1f);
    [SerializeField] private Color eventColor = new Color(0.25f, 0.82f, 1f, 1f);
    [SerializeField] private Color specialColor = new Color(0.75f, 0.25f, 1f, 1f);
    [SerializeField] private Color coreColor = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private Color playerColor = new Color(0.36f, 1f, 0.48f, 1f);

    [Header("Marker Sizes")]
    [SerializeField, Min(1f)] private float playerMarkerSize = 8f;
    [SerializeField, Range(0.25f, 1f)] private float enemyMarkerScaleMultiplier = 0.9f;
    [SerializeField, Range(0.25f, 1f)] private float meteorMarkerScaleMultiplier = 0.65f;

    [Header("Marker Rules")]
    [SerializeField] private bool showMeteorsOnFullMap = true;
    [SerializeField] private bool showRewardObjectsOnFullMap = true;
    [SerializeField] private bool autoRefreshWhileVisible = true;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

    [Header("Route Planner")]
    [SerializeField] private MapRouteGraphic mapRouteGraphic;
    [SerializeField] private RectTransform routeMarkerRoot;
    [SerializeField] private Color routeMarkerColor = new Color(0.55f, 0.95f, 1f, 0.88f);
    [SerializeField, Min(2f)] private float routeMarkerHitRadius = 8f;

    [Header("Operation Search Region")]
    [SerializeField] private OperationSearchRegionGraphic operationSearchRegionGraphic;
    [SerializeField] private TextMeshProUGUI operationSearchRegionText;
    [SerializeField] private Color operationSearchRegionColor = new Color(1f, 0.72f, 0.22f, 1f);

    private bool hasExternalObjective;
    private string externalObjectiveTitle;
    private string externalObjectiveDetail;
    private string externalObjectiveState;
    private bool showExternalSearchRegion;
    private Vector2 externalSearchRegionCenter;
    private float externalSearchRegionRadius;

    private readonly Dictionary<RadarTarget, RadarMarkerUI> markerMap = new Dictionary<RadarTarget, RadarMarkerUI>();
    private readonly List<RadarTarget> removeBuffer = new List<RadarTarget>();
    private readonly GameObject[] routeMarkerObjects = new GameObject[ExpeditionRoutePlanner.MaxWaypoints];
    private readonly RectTransform[] routeMarkerRects = new RectTransform[ExpeditionRoutePlanner.MaxWaypoints];
    private readonly Vector2[] routeMapPointBuffer = new Vector2[ExpeditionRoutePlanner.MaxWaypoints + 1];

    private bool visible;
    private float refreshTimer;
    private bool legendBuilt;
    private RectTransform mapUiRoot;
    private InputActionMap mapInputMap;
    private InputAction routeAddAction;
    private InputAction routeRemoveAction;
    private InputAction routeClearAction;
    private float currentZoom = 1f;
    private Vector2 pointerDownScreenPosition;
    private bool pointerGestureActive;
    private bool pointerGestureDragged;

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (mapArea == null)
        {
            mapArea = transform as RectTransform;
        }

        if (markerRoot == null)
        {
            markerRoot = mapArea;
        }

        mapUiRoot = mapArea != null ? mapArea.parent as RectTransform : null;
        EnsureMapViewHierarchy();
        BindMapInput();

        if (discoveryController == null)
        {
            discoveryController = FindFirstObjectByType<MapDiscoveryController>();
        }

        if (routePlanner == null)
        {
            routePlanner = FindFirstObjectByType<ExpeditionRoutePlanner>();
        }

        if (operationController == null)
        {
            operationController = FindFirstObjectByType<ExpeditionOperationController>();
        }

        ResolvePlayer();
        ConfigurePlayerMarkerPresentation();
        RefreshDiscoveryTexture();
        RefreshMapGrid();
        EnsureMapLegend();
        EnsureOperationInformation();
        EnsureControlHints();
        EnsureRoutePresentation();
        EnsureOperationSearchRegionPresentation();
    }

    private void OnEnable()
    {
        ExpeditionMapGenerator.AnyMapGenerated += HandleMapGenerated;
        RadarTarget.PresentationChanged += HandleTargetPresentationChanged;
        if (discoveryController == null)
        {
            discoveryController = MapDiscoveryController.Instance;
        }

        if (discoveryController != null)
        {
            discoveryController.DiscoveryChanged += HandleDiscoveryChanged;
        }

        if (routePlanner != null)
        {
            routePlanner.RouteChanged += HandleRouteChanged;
        }

        if (operationController != null)
        {
            operationController.OperationChanged += HandleOperationChanged;
        }

        InputSystem.onActionChange += HandleInputActionChange;
    }

    private void OnDisable()
    {
        ExpeditionMapGenerator.AnyMapGenerated -= HandleMapGenerated;
        RadarTarget.PresentationChanged -= HandleTargetPresentationChanged;
        if (discoveryController != null)
        {
            discoveryController.DiscoveryChanged -= HandleDiscoveryChanged;
        }

        if (routePlanner != null)
        {
            routePlanner.RouteChanged -= HandleRouteChanged;
            routePlanner.SetMapVisible(false);
        }

        if (operationController != null)
        {
            operationController.OperationChanged -= HandleOperationChanged;
        }

        InputSystem.onActionChange -= HandleInputActionChange;
        SetMapInputEnabled(false);
    }

    private void Update()
    {
        if (!visible)
        {
            return;
        }

        HandleRouteActions();

        if (!autoRefreshWhileVisible)
        {
            return;
        }

        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f)
        {
            return;
        }

        refreshTimer = refreshInterval;
        RefreshAll();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        routePlanner?.SetMapVisible(value);
        SetMapInputEnabled(value);

        if (rootObject != null && rootObject != gameObject && rootObject.activeSelf != value)
        {
            rootObject.SetActive(value);
        }

        if (value)
        {
            RefreshControlHints();
            RefreshAll();
        }
    }

    public void RefreshAll()
    {
        ResolvePlayer();
        RefreshDiscoveryTexture();
        RefreshMapGrid();
        RefreshPlayerMarker();
        RebuildTargetMarkers();
        RefreshOperationSearchRegion();
        RefreshRoutePresentation();
        RefreshOperationInformation();
    }

    private void HandleDiscoveryChanged()
    {
        if (visible)
        {
            RefreshAll();
        }
    }

    private void HandleTargetPresentationChanged(RadarTarget target)
    {
        if (!visible || target == null || discoveryController == null)
        {
            return;
        }

        if (discoveryController.IsTargetDiscovered(target))
        {
            RebuildTargetMarkers();
        }
    }

    private void HandleRouteChanged()
    {
        if (visible)
        {
            RefreshRoutePresentation();
        }
    }

    private void HandleOperationChanged()
    {
        RefreshOperationSearchRegion();
        RefreshOperationInformation();
    }

    public void SetExternalObjective(
        string title,
        string detail,
        string state = "진행 중")
    {
        hasExternalObjective = !string.IsNullOrWhiteSpace(detail);
        externalObjectiveTitle = title;
        externalObjectiveDetail = detail;
        externalObjectiveState = state;
        RefreshOperationInformation();
    }

    public void ClearExternalObjective()
    {
        hasExternalObjective = false;
        externalObjectiveTitle = string.Empty;
        externalObjectiveDetail = string.Empty;
        externalObjectiveState = string.Empty;
        RefreshOperationInformation();
    }

    public void SetExternalSearchRegion(Vector2 center, float radius)
    {
        externalSearchRegionCenter = center;
        externalSearchRegionRadius = Mathf.Max(0f, radius);
        showExternalSearchRegion = externalSearchRegionRadius > 0f;
        RefreshOperationSearchRegion();
    }

    public void ClearExternalSearchRegion()
    {
        showExternalSearchRegion = false;
        externalSearchRegionRadius = 0f;
        RefreshOperationSearchRegion();
    }

    private void HandleMapGenerated(ExpeditionMapGenerator generator)
    {
        if (mapContentRoot != null)
        {
            mapContentRoot.anchoredPosition = Vector2.zero;
        }

        SetZoom(minimumZoom);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!visible || eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        pointerGestureActive = true;
        pointerGestureDragged = false;
        pointerDownScreenPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!visible || !pointerGestureActive || eventData == null ||
            eventData.button != PointerEventData.InputButton.Left || mapContentRoot == null)
        {
            return;
        }

        if (!pointerGestureDragged &&
            (eventData.position - pointerDownScreenPosition).sqrMagnitude >= dragThresholdPixels * dragThresholdPixels)
        {
            pointerGestureDragged = true;
        }

        if (!pointerGestureDragged || currentZoom <= minimumZoom + 0.001f)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
        mapContentRoot.anchoredPosition += eventData.delta / scaleFactor;
        ClampPan();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!visible || eventData == null)
        {
            return;
        }

        bool wasDrag = pointerGestureDragged;
        pointerGestureActive = false;
        pointerGestureDragged = false;

        if (wasDrag)
        {
            return;
        }

        InputControl releasedControl = ResolvePointerButtonControl(eventData.button);
        if (IsActionBoundToControl(routeAddAction, releasedControl))
        {
            TryAddRouteAtScreenPosition(eventData.position, eventData.pressEventCamera);
        }
        else if (IsActionBoundToControl(routeRemoveAction, releasedControl))
        {
            TryRemoveRouteAtScreenPosition(eventData.position, eventData.pressEventCamera);
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!visible || eventData == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
        {
            return;
        }

        SetZoom(currentZoom + Mathf.Sign(eventData.scrollDelta.y) * zoomStep);
    }

    private void TryAddRouteAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryScreenToMapPosition(screenPosition, eventCamera, out Vector2 localPosition) || routePlanner == null)
        {
            return;
        }

        if (routePlanner.TryAddWaypoint(MapToWorldPosition(localPosition)))
        {
            AudioManager.Play(SoundEventIds.MapRoutePlaced);
        }
    }

    private void TryRemoveRouteAtScreenPosition(Vector2 screenPosition, Camera eventCamera)
    {
        if (!TryScreenToMapPosition(screenPosition, eventCamera, out Vector2 localPosition) || routePlanner == null)
        {
            return;
        }

        float localHitRadius = routeMarkerHitRadius / Mathf.Max(1f, currentZoom);
        float hitRadiusSqr = localHitRadius * localHitRadius;
        for (int i = 0; i < routePlanner.WaypointCount; i++)
        {
            if (!routePlanner.TryGetWaypoint(i, out Vector2 waypoint))
            {
                continue;
            }

            Vector2 waypointMapPosition = WorldToMapPosition(waypoint);
            if ((waypointMapPosition - localPosition).sqrMagnitude <= hitRadiusSqr)
            {
                if (routePlanner.RemoveWaypoint(i))
                {
                    AudioManager.Play(SoundEventIds.MapRouteRemoved);
                }
                return;
            }
        }
    }

    private bool TryScreenToMapPosition(Vector2 screenPosition, Camera eventCamera, out Vector2 localPosition)
    {
        localPosition = default;
        return mapArea != null && discoveryController != null &&
               RectTransformUtility.ScreenPointToLocalPointInRectangle(
                   mapArea,
                   screenPosition,
                   eventCamera,
                   out localPosition) &&
               mapArea.rect.Contains(localPosition);
    }

    private void EnsureMapViewHierarchy()
    {
        if (mapArea == null || mapUiRoot == null)
        {
            return;
        }

        if (mapViewport == null)
        {
            int originalSiblingIndex = mapArea.GetSiblingIndex();
            GameObject viewportObject = new GameObject(
                "MapViewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D)
            );
            viewportObject.layer = gameObject.layer;
            mapViewport = viewportObject.GetComponent<RectTransform>();
            mapViewport.SetParent(mapUiRoot, false);
            mapViewport.anchorMin = mapArea.anchorMin;
            mapViewport.anchorMax = mapArea.anchorMax;
            mapViewport.pivot = mapArea.pivot;
            mapViewport.anchoredPosition = mapArea.anchoredPosition;
            mapViewport.sizeDelta = mapArea.sizeDelta;
            mapViewport.SetSiblingIndex(originalSiblingIndex);

            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
        }

        if (mapContentRoot == null)
        {
            GameObject contentObject = new GameObject("MapContentRoot", typeof(RectTransform));
            contentObject.layer = gameObject.layer;
            mapContentRoot = contentObject.GetComponent<RectTransform>();
            mapContentRoot.SetParent(mapViewport, false);
            mapContentRoot.anchorMin = Vector2.zero;
            mapContentRoot.anchorMax = Vector2.one;
            mapContentRoot.pivot = new Vector2(0.5f, 0.5f);
            mapContentRoot.offsetMin = Vector2.zero;
            mapContentRoot.offsetMax = Vector2.zero;
        }

        MoveMapLayerIntoContent(mapArea);
        MoveMapLayerIntoContent(discoveryMaskImage != null ? discoveryMaskImage.rectTransform : null);
        MoveMapLayerIntoContent(markerRoot);
        SetZoom(minimumZoom);
    }

    private void MoveMapLayerIntoContent(RectTransform layer)
    {
        if (layer == null || layer == mapContentRoot || layer.parent == mapContentRoot)
        {
            return;
        }

        layer.SetParent(mapContentRoot, false);
        layer.anchorMin = new Vector2(0.5f, 0.5f);
        layer.anchorMax = new Vector2(0.5f, 0.5f);
        layer.pivot = new Vector2(0.5f, 0.5f);
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = mapViewport.rect.size;
        layer.localScale = Vector3.one;
    }

    private void BindMapInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
        InputBindingPersistence.LoadOnce(inputActions);
        mapInputMap = inputActions != null ? inputActions.FindActionMap(mapActionMapName, false) : null;
        routeAddAction = mapInputMap?.FindAction(routeAddActionName, false);
        routeRemoveAction = mapInputMap?.FindAction(routeRemoveActionName, false);
        routeClearAction = mapInputMap?.FindAction(routeClearActionName, false);
    }

    private void SetMapInputEnabled(bool enabledState)
    {
        if (mapInputMap == null)
        {
            BindMapInput();
        }

        if (enabledState)
        {
            mapInputMap?.Enable();
        }
        else
        {
            mapInputMap?.Disable();
        }
    }

    private void HandleRouteActions()
    {
        if (routeClearAction != null && routeClearAction.WasPressedThisFrame())
        {
            if (routePlanner != null && routePlanner.WaypointCount > 0)
            {
                routePlanner.ClearRoute();
                AudioManager.Play(SoundEventIds.MapRouteRemoved);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (routeAddAction != null && routeAddAction.WasPressedThisFrame() &&
            !IsActionBoundToMouse(routeAddAction))
        {
            TryAddRouteAtScreenPosition(pointerPosition, ResolveEventCamera());
        }

        if (routeRemoveAction != null && routeRemoveAction.WasPressedThisFrame() &&
            !IsActionBoundToMouse(routeRemoveAction))
        {
            TryRemoveRouteAtScreenPosition(pointerPosition, ResolveEventCamera());
        }
    }

    private Camera ResolveEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static bool IsActionBoundToControl(InputAction action, InputControl control)
    {
        if (action == null || control == null)
        {
            return false;
        }

        for (int i = 0; i < action.controls.Count; i++)
        {
            if (action.controls[i] == control)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActionBoundToMouse(InputAction action)
    {
        if (action == null)
        {
            return false;
        }

        for (int i = 0; i < action.controls.Count; i++)
        {
            if (action.controls[i].device is Mouse)
            {
                return true;
            }
        }

        return false;
    }

    private static InputControl ResolvePointerButtonControl(PointerEventData.InputButton button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return null;
        }

        return button switch
        {
            PointerEventData.InputButton.Left => mouse.leftButton,
            PointerEventData.InputButton.Right => mouse.rightButton,
            PointerEventData.InputButton.Middle => mouse.middleButton,
            _ => null
        };
    }

    private void SetZoom(float value)
    {
        currentZoom = Mathf.Clamp(value, Mathf.Max(1f, minimumZoom), Mathf.Max(minimumZoom, maximumZoom));
        if (mapContentRoot == null)
        {
            return;
        }

        mapContentRoot.localScale = new Vector3(currentZoom, currentZoom, 1f);
        ClampPan();
    }

    private void ClampPan()
    {
        if (mapContentRoot == null || mapViewport == null)
        {
            return;
        }

        Vector2 viewportSize = mapViewport.rect.size;
        Vector2 maxPan = viewportSize * Mathf.Max(0f, currentZoom - 1f) * 0.5f;
        Vector2 position = mapContentRoot.anchoredPosition;
        position.x = Mathf.Clamp(position.x, -maxPan.x, maxPan.x);
        position.y = Mathf.Clamp(position.y, -maxPan.y, maxPan.y);
        mapContentRoot.anchoredPosition = position;
    }

    private void HandleInputActionChange(object changedObject, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            RefreshControlHints();
        }
    }

    private void RefreshDiscoveryTexture()
    {
        if (discoveryController == null)
        {
            discoveryController = MapDiscoveryController.Instance;
        }

        if (discoveryMaskImage != null && discoveryController != null)
        {
            discoveryMaskImage.texture = discoveryController.DiscoveryTexture;
        }
    }

    private void RefreshMapGrid()
    {
        if (mapArea == null || discoveryController == null)
        {
            return;
        }

        EnsureMapGrid();
        mapGridGraphic?.Configure(discoveryController.MapBounds, gridWorldCellSize);
    }

    private void EnsureMapGrid()
    {
        if (mapGridGraphic != null || !createGridIfMissing || mapArea.parent == null)
        {
            return;
        }

        GameObject gridObject = new GameObject(
            "MapGrid",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(MapGridGraphic)
        );
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.SetParent(mapArea.parent, false);
        gridRect.anchorMin = mapArea.anchorMin;
        gridRect.anchorMax = mapArea.anchorMax;
        gridRect.pivot = mapArea.pivot;
        gridRect.anchoredPosition = mapArea.anchoredPosition;
        gridRect.sizeDelta = mapArea.sizeDelta;
        gridRect.localScale = mapArea.localScale;
        gridRect.SetSiblingIndex(mapArea.GetSiblingIndex());

        mapGridGraphic = gridObject.GetComponent<MapGridGraphic>();
        mapGridGraphic.raycastTarget = false;
    }

    private void EnsureMapLegend()
    {
        if (!createLegendIfMissing || mapArea == null || mapUiRoot == null)
        {
            return;
        }

        RectTransform parentRect = mapUiRoot;
        if (parentRect == null)
        {
            return;
        }

        TextMeshProUGUI fontSource = parentRect.GetComponentInChildren<TextMeshProUGUI>(true);

        if (legendRoot == null)
        {
            GameObject legendObject = new GameObject("MapLegend", typeof(RectTransform));
            legendRoot = legendObject.GetComponent<RectTransform>();
            legendRoot.SetParent(parentRect, false);
            legendRoot.anchorMin = new Vector2(0.5f, 0.5f);
            legendRoot.anchorMax = new Vector2(0.5f, 0.5f);
            legendRoot.pivot = new Vector2(0.5f, 0.5f);

            float mapLeft = mapViewport != null
                ? mapViewport.anchoredPosition.x - mapViewport.rect.width * 0.5f
                : mapArea.anchoredPosition.x - mapArea.rect.width * 0.5f;
            float availableLeft = parentRect.rect.xMin + 8f;
            float availableRight = mapLeft - 8f;
            float width = Mathf.Max(80f, availableRight - availableLeft);
            legendRoot.anchoredPosition = new Vector2((availableLeft + availableRight) * 0.5f, -12f);
            legendRoot.sizeDelta = new Vector2(width, 62f);
        }

        if (legendBuilt)
        {
            return;
        }

        if (legendText == null)
        {
            GameObject textObject = new GameObject(
                "LegendTitle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(legendRoot, false);
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -1f);
            textRect.sizeDelta = new Vector2(0f, 10f);
            legendText = textObject.GetComponent<TextMeshProUGUI>();
        }

        ApplyMapFont(legendText, fontSource);

        legendText.fontSize = 6f;
        legendText.color = new Color(0.82f, 0.87f, 0.92f, 1f);
        legendText.alignment = TextAlignmentOptions.MidlineLeft;
        legendText.textWrappingMode = TextWrappingModes.NoWrap;
        legendText.raycastTarget = false;
        legendText.text = "지도 표식";

        CreateLegendRow(0, RadarMarkerShape.Diamond, false, playerColor, "플레이어", fontSource);
        CreateTargetLegendRow(1, RadarMarkerType.Enemy, "적", fontSource);
        CreateTargetLegendRow(2, RadarMarkerType.RewardObject, "자원", fontSource);
        CreateTargetLegendRow(3, RadarMarkerType.Meteor, "운석", fontSource);
        CreateTargetLegendRow(4, RadarMarkerType.Event, "이벤트", fontSource);
        CreateTargetLegendRow(5, RadarMarkerType.Shop, "상점", fontSource);
        CreateTargetLegendRow(6, RadarMarkerType.Core, "코어", fontSource);
        legendBuilt = true;
    }

    private void EnsureOperationInformation()
    {
        if (operationInfoText != null || mapUiRoot == null || mapArea == null)
        {
            return;
        }

        TextMeshProUGUI fontSource = mapUiRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        GameObject panelObject = new GameObject("MapOperationInformation", typeof(RectTransform));
        panelObject.layer = gameObject.layer;
        operationInfoRoot = panelObject.GetComponent<RectTransform>();
        operationInfoRoot.SetParent(mapUiRoot, false);
        operationInfoRoot.anchorMin = new Vector2(0.5f, 0.5f);
        operationInfoRoot.anchorMax = new Vector2(0.5f, 0.5f);
        operationInfoRoot.pivot = new Vector2(0.5f, 1f);

        float mapLeft = mapViewport != null
            ? mapViewport.anchoredPosition.x - mapViewport.rect.width * 0.5f
            : mapArea.anchoredPosition.x - mapArea.rect.width * 0.5f;
        float availableLeft = mapUiRoot.rect.xMin + 8f;
        float availableRight = mapLeft - 8f;
        float width = Mathf.Max(80f, availableRight - availableLeft);
        operationInfoRoot.anchoredPosition = new Vector2((availableLeft + availableRight) * 0.5f, mapUiRoot.rect.yMax - 8f);
        operationInfoRoot.sizeDelta = new Vector2(width, 78f);

        GameObject textObject = new GameObject(
            "OperationInformationText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(operationInfoRoot, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        operationInfoText = textObject.GetComponent<TextMeshProUGUI>();
        ApplyMapFont(operationInfoText, fontSource);

        operationInfoText.fontSize = 5.5f;
        operationInfoText.enableAutoSizing = true;
        operationInfoText.fontSizeMin = 4.5f;
        operationInfoText.fontSizeMax = 5.5f;
        operationInfoText.alignment = TextAlignmentOptions.TopLeft;
        operationInfoText.textWrappingMode = TextWrappingModes.Normal;
        operationInfoText.overflowMode = TextOverflowModes.Ellipsis;
        operationInfoText.color = new Color(0.86f, 0.92f, 0.98f, 1f);
        operationInfoText.raycastTarget = false;
    }

    private void RefreshOperationInformation()
    {
        EnsureOperationInformation();
        if (operationInfoText == null)
        {
            return;
        }

        bool hasProductionOperation = operationController != null && operationController.HasOperation;
        bool hasOperation = hasExternalObjective || hasProductionOperation;
        if (operationInfoRoot != null && operationInfoRoot.gameObject.activeSelf != hasOperation)
        {
            operationInfoRoot.gameObject.SetActive(hasOperation);
        }

        if (!hasOperation)
        {
            return;
        }

        if (hasExternalObjective)
        {
            operationInfoText.text =
                $"<b>[{externalObjectiveTitle}]</b>\n\n" +
                $"<color=#98A6B5>현재 목표</color>\n{externalObjectiveDetail}\n\n" +
                $"<color=#98A6B5>상태</color>\n{externalObjectiveState}";
            return;
        }

        operationInfoText.text =
            $"<b>[작전]</b>\n{operationController.PresentationTitle}\n\n" +
            $"<color=#98A6B5>상태</color>\n{operationController.PresentationState}\n" +
            $"<color=#98A6B5>위치</color>\n{operationController.PresentationLocation}\n" +
            $"<color=#98A6B5>목표</color>\n{operationController.PresentationObjective}";
    }

    private void EnsureControlHints()
    {
        if (controlHintsText != null || mapUiRoot == null || mapArea == null)
        {
            return;
        }

        TextMeshProUGUI fontSource = mapUiRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        GameObject hintsObject = new GameObject("MapControlHints", typeof(RectTransform));
        hintsObject.layer = gameObject.layer;
        controlHintsRoot = hintsObject.GetComponent<RectTransform>();
        controlHintsRoot.SetParent(mapUiRoot, false);
        controlHintsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        controlHintsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        controlHintsRoot.pivot = new Vector2(0.5f, 0f);

        float mapLeft = mapViewport != null
            ? mapViewport.anchoredPosition.x - mapViewport.rect.width * 0.5f
            : mapArea.anchoredPosition.x - mapArea.rect.width * 0.5f;
        float availableLeft = mapUiRoot.rect.xMin + 8f;
        float availableRight = mapLeft - 8f;
        float width = Mathf.Max(80f, availableRight - availableLeft);
        controlHintsRoot.anchoredPosition = new Vector2((availableLeft + availableRight) * 0.5f, mapUiRoot.rect.yMin + 7f);
        controlHintsRoot.sizeDelta = new Vector2(width, 42f);

        GameObject textObject = new GameObject(
            "MapControlHintsText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(controlHintsRoot, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        controlHintsText = textObject.GetComponent<TextMeshProUGUI>();
        ApplyMapFont(controlHintsText, fontSource);

        controlHintsText.fontSize = 5.5f;
        controlHintsText.alignment = TextAlignmentOptions.BottomLeft;
        controlHintsText.textWrappingMode = TextWrappingModes.Normal;
        controlHintsText.overflowMode = TextOverflowModes.Ellipsis;
        controlHintsText.color = new Color(0.7f, 0.8f, 0.88f, 1f);
        controlHintsText.raycastTarget = false;
        RefreshControlHints();
    }

    private void RefreshControlHints()
    {
        EnsureControlHints();
        if (controlHintsText == null)
        {
            return;
        }

        string add = InputBindingUtility.GetDisplayString(inputActions, mapActionMapName, routeAddActionName, "LMB");
        string remove = InputBindingUtility.GetDisplayString(inputActions, mapActionMapName, routeRemoveActionName, "RMB");
        string clear = InputBindingUtility.GetDisplayString(inputActions, mapActionMapName, routeClearActionName, "C");
        string cancel = InputBindingUtility.GetDisplayString(inputActions, uiActionMapName, cancelActionName, "Esc");
        controlHintsText.text =
            $"[{add}] 경로 추가  [{remove}] 경로 제거\n" +
            $"[{clear}] 경로 초기화  [{cancel}] 닫기\n" +
            "휠 확대 · 드래그 이동";
    }

    private void CreateLegendRow(
        int index,
        RadarMarkerShape shape,
        bool hollow,
        Color markerColor,
        string label,
        TextMeshProUGUI fontSource)
    {
        const int rowsPerColumn = 4;
        int column = index / rowsPerColumn;
        int row = index % rowsPerColumn;

        GameObject rowObject = new GameObject($"LegendRow_{index}", typeof(RectTransform));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(legendRoot, false);
        rowRect.anchorMin = new Vector2(column * 0.5f, 1f);
        rowRect.anchorMax = new Vector2((column + 1) * 0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -12f - row * 12f);
        rowRect.sizeDelta = new Vector2(0f, 11f);

        GameObject markerObject = new GameObject(
            "Marker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RadarMarkerShapeGraphic)
        );
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.SetParent(rowRect, false);
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(4f, 0f);
        markerRect.sizeDelta = new Vector2(6f, 6f);

        RadarMarkerShapeGraphic markerGraphic = markerObject.GetComponent<RadarMarkerShapeGraphic>();
        markerGraphic.raycastTarget = false;
        markerGraphic.Configure(shape, hollow, markerColor);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rowRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 0f);
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        ApplyMapFont(labelText, fontSource);

        labelText.fontSize = 6f;
        labelText.color = new Color(0.82f, 0.87f, 0.92f, 1f);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;
        labelText.text = label;
    }

    private void CreateTargetLegendRow(
        int index,
        RadarMarkerType markerType,
        string label,
        TextMeshProUGUI fontSource)
    {
        if (!RadarMarkerPresentation.TryResolveShape(markerType, out RadarMarkerShape shape, out bool hollow))
        {
            shape = RadarMarkerShape.Circle;
            hollow = false;
        }

        Color markerColor = RadarMarkerPresentation.ResolveColor(
            markerType,
            Color.white,
            enemyColor,
            rewardColor,
            meteorColor,
            eventColor,
            specialColor,
            coreColor
        );
        CreateLegendRow(index, shape, hollow, markerColor, label, fontSource);
    }

    private void EnsureRoutePresentation()
    {
        if (mapArea == null || mapArea.parent == null)
        {
            return;
        }

        RectTransform parentRect = mapArea.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        if (mapRouteGraphic == null)
        {
            GameObject routeLineObject = new GameObject(
                "MapRouteLine",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(MapRouteGraphic)
            );
            routeLineObject.layer = gameObject.layer;
            RectTransform routeLineRect = routeLineObject.GetComponent<RectTransform>();
            CopyMapRectTransform(routeLineRect, parentRect);

            if (markerRoot != null && markerRoot.parent == parentRect)
            {
                routeLineRect.SetSiblingIndex(markerRoot.GetSiblingIndex());
            }

            mapRouteGraphic = routeLineObject.GetComponent<MapRouteGraphic>();
            mapRouteGraphic.raycastTarget = false;
        }

        if (routeMarkerRoot == null)
        {
            GameObject markerRootObject = new GameObject("RouteWaypointRoot", typeof(RectTransform));
            markerRootObject.layer = gameObject.layer;
            routeMarkerRoot = markerRootObject.GetComponent<RectTransform>();
            CopyMapRectTransform(routeMarkerRoot, parentRect);

            if (markerRoot != null && markerRoot.parent == parentRect)
            {
                routeMarkerRoot.SetSiblingIndex(markerRoot.GetSiblingIndex() + 1);
            }
        }

        TextMeshProUGUI fontSource = parentRect.GetComponentInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < routeMarkerObjects.Length; i++)
        {
            if (routeMarkerObjects[i] != null)
            {
                continue;
            }

            routeMarkerObjects[i] = CreateRouteMarker(i, fontSource);
            routeMarkerRects[i] = routeMarkerObjects[i].GetComponent<RectTransform>();
            routeMarkerObjects[i].SetActive(false);
        }
    }

    private void EnsureOperationSearchRegionPresentation()
    {
        if (operationSearchRegionGraphic != null || mapArea == null || mapArea.parent == null)
        {
            return;
        }

        RectTransform parentRect = mapArea.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        GameObject regionObject = new GameObject(
            "OperationSearchRegion",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(OperationSearchRegionGraphic)
        );
        regionObject.layer = gameObject.layer;
        RectTransform regionRect = regionObject.GetComponent<RectTransform>();
        regionRect.SetParent(parentRect, false);
        regionRect.anchorMin = new Vector2(0.5f, 0.5f);
        regionRect.anchorMax = new Vector2(0.5f, 0.5f);
        regionRect.pivot = new Vector2(0.5f, 0.5f);

        int siblingIndex = markerRoot != null && markerRoot.parent == parentRect
            ? markerRoot.GetSiblingIndex()
            : parentRect.childCount - 1;
        if (mapRouteGraphic != null && mapRouteGraphic.transform.parent == parentRect)
        {
            siblingIndex = mapRouteGraphic.transform.GetSiblingIndex();
        }

        regionRect.SetSiblingIndex(Mathf.Max(0, siblingIndex));
        operationSearchRegionGraphic = regionObject.GetComponent<OperationSearchRegionGraphic>();
        operationSearchRegionGraphic.raycastTarget = false;
        operationSearchRegionGraphic.Configure(operationSearchRegionColor);

        GameObject questionObject = new GameObject(
            "Question",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        questionObject.layer = gameObject.layer;
        RectTransform questionRect = questionObject.GetComponent<RectTransform>();
        questionRect.SetParent(regionRect, false);
        questionRect.anchorMin = Vector2.zero;
        questionRect.anchorMax = Vector2.one;
        questionRect.offsetMin = Vector2.zero;
        questionRect.offsetMax = Vector2.zero;

        operationSearchRegionText = questionObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI fontSource = parentRect.GetComponentInChildren<TextMeshProUGUI>(true);
        ApplyMapFont(operationSearchRegionText, fontSource);

        operationSearchRegionText.fontSize = 8f;
        operationSearchRegionText.fontStyle = FontStyles.Bold;
        operationSearchRegionText.alignment = TextAlignmentOptions.Center;
        operationSearchRegionText.color = new Color(
            operationSearchRegionColor.r,
            operationSearchRegionColor.g,
            operationSearchRegionColor.b,
            0.82f
        );
        operationSearchRegionText.raycastTarget = false;
        operationSearchRegionText.text = "?";
        regionObject.SetActive(false);
    }

    private void RefreshOperationSearchRegion()
    {
        EnsureOperationSearchRegionPresentation();
        if (operationSearchRegionGraphic == null)
        {
            return;
        }

        bool showProductionRegion = operationController != null &&
                                    operationController.ShowSearchRegion;
        bool showRegion = (showExternalSearchRegion || showProductionRegion) &&
                          discoveryController != null &&
                          mapArea != null;
        GameObject regionObject = operationSearchRegionGraphic.gameObject;
        if (regionObject.activeSelf != showRegion)
        {
            regionObject.SetActive(showRegion);
        }

        if (!showRegion)
        {
            return;
        }

        Bounds bounds = discoveryController.MapBounds;
        Rect mapRect = mapArea.rect;
        Vector2 regionCenter = showExternalSearchRegion
            ? externalSearchRegionCenter
            : operationController.SearchRegionCenter;
        float regionRadius = showExternalSearchRegion
            ? externalSearchRegionRadius
            : operationController.SearchRegionRadius;
        float diameter = regionRadius * 2f;
        float width = diameter / Mathf.Max(0.001f, bounds.size.x) * mapRect.width;
        float height = diameter / Mathf.Max(0.001f, bounds.size.y) * mapRect.height;
        RectTransform regionRect = operationSearchRegionGraphic.rectTransform;
        Vector2 desiredPosition = mapArea.anchoredPosition +
                                  WorldToMapPosition(regionCenter);
        Vector2 desiredSize = new Vector2(Mathf.Max(12f, width), Mathf.Max(12f, height));

        if ((regionRect.anchoredPosition - desiredPosition).sqrMagnitude > 0.0001f)
        {
            regionRect.anchoredPosition = desiredPosition;
        }

        if ((regionRect.sizeDelta - desiredSize).sqrMagnitude > 0.0001f)
        {
            regionRect.sizeDelta = desiredSize;
        }
    }

    private void CopyMapRectTransform(RectTransform target, RectTransform parentRect)
    {
        target.SetParent(parentRect, false);
        target.anchorMin = mapArea.anchorMin;
        target.anchorMax = mapArea.anchorMax;
        target.pivot = mapArea.pivot;
        target.anchoredPosition = mapArea.anchoredPosition;
        target.sizeDelta = mapArea.sizeDelta;
        target.localScale = mapArea.localScale;
    }

    private GameObject CreateRouteMarker(int index, TextMeshProUGUI fontSource)
    {
        GameObject markerObject = new GameObject(
            $"RouteWaypoint_{index + 1}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RadarMarkerShapeGraphic)
        );
        markerObject.layer = gameObject.layer;

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.SetParent(routeMarkerRoot, false);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(10f, 10f);

        RadarMarkerShapeGraphic markerGraphic = markerObject.GetComponent<RadarMarkerShapeGraphic>();
        markerGraphic.raycastTarget = false;
        markerGraphic.Configure(RadarMarkerShape.Diamond, false, routeMarkerColor);

        GameObject numberObject = new GameObject(
            "Number",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        numberObject.layer = gameObject.layer;
        RectTransform numberRect = numberObject.GetComponent<RectTransform>();
        numberRect.SetParent(markerRect, false);
        numberRect.anchorMin = Vector2.zero;
        numberRect.anchorMax = Vector2.one;
        numberRect.offsetMin = Vector2.zero;
        numberRect.offsetMax = Vector2.zero;

        TextMeshProUGUI numberText = numberObject.GetComponent<TextMeshProUGUI>();
        ApplyMapFont(numberText, fontSource);

        numberText.fontSize = 6f;
        numberText.fontStyle = FontStyles.Bold;
        numberText.color = Color.white;
        numberText.alignment = TextAlignmentOptions.Center;
        numberText.textWrappingMode = TextWrappingModes.NoWrap;
        numberText.raycastTarget = false;
        numberText.text = (index + 1).ToString();
        return markerObject;
    }

    private void ApplyMapFont(TextMeshProUGUI target, TextMeshProUGUI fontSource)
    {
        if (target == null)
        {
            return;
        }

        TMP_FontAsset resolvedFont = mapFontAsset;
        if (resolvedFont == null && fontSource != null)
        {
            resolvedFont = fontSource.font;
        }

        if (resolvedFont != null)
        {
            target.font = resolvedFont;
        }
    }

    private void RefreshRoutePresentation()
    {
        EnsureRoutePresentation();

        int waypointCount = routePlanner != null ? routePlanner.WaypointCount : 0;
        bool canDrawLine = waypointCount > 0 && player != null;
        int pointCount = canDrawLine ? waypointCount + 1 : 0;

        if (canDrawLine)
        {
            routeMapPointBuffer[0] = WorldToMapPosition(player.position);
        }

        for (int i = 0; i < routeMarkerObjects.Length; i++)
        {
            Vector2 waypoint = default;
            bool active = i < waypointCount && routePlanner.TryGetWaypoint(i, out waypoint);
            if (routeMarkerObjects[i] != null && routeMarkerObjects[i].activeSelf != active)
            {
                routeMarkerObjects[i].SetActive(active);
            }

            if (!active)
            {
                continue;
            }

            Vector2 mapPosition = WorldToMapPosition(waypoint);
            routeMarkerRects[i].anchoredPosition = mapPosition;
            routeMapPointBuffer[i + 1] = mapPosition;
        }

        mapRouteGraphic?.Configure(routeMapPointBuffer, pointCount);
    }

    private void RefreshPlayerMarker()
    {
        if (playerMarker == null || player == null || discoveryController == null)
        {
            return;
        }

        playerMarker.anchoredPosition = WorldToMapPosition(player.position);
    }

    private void ConfigurePlayerMarkerPresentation()
    {
        if (playerMarker == null)
        {
            return;
        }

        playerMarker.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);

        Image legacyImage = playerMarker.GetComponent<Image>();
        if (legacyImage != null)
        {
            legacyImage.enabled = false;
            legacyImage.raycastTarget = false;
        }

        Transform shapeTransform = playerMarker.Find("PlayerMarkerShape");
        RadarMarkerShapeGraphic markerGraphic = shapeTransform != null
            ? shapeTransform.GetComponent<RadarMarkerShapeGraphic>()
            : null;

        if (markerGraphic == null)
        {
            GameObject shapeObject = new GameObject(
                "PlayerMarkerShape",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RadarMarkerShapeGraphic)
            );
            shapeObject.layer = playerMarker.gameObject.layer;

            RectTransform shapeRect = shapeObject.GetComponent<RectTransform>();
            shapeRect.SetParent(playerMarker, false);
            shapeRect.anchorMin = new Vector2(0.5f, 0.5f);
            shapeRect.anchorMax = new Vector2(0.5f, 0.5f);
            shapeRect.pivot = new Vector2(0.5f, 0.5f);
            shapeRect.anchoredPosition = Vector2.zero;
            shapeRect.sizeDelta = new Vector2(playerMarkerSize, playerMarkerSize);

            markerGraphic = shapeObject.GetComponent<RadarMarkerShapeGraphic>();
        }

        markerGraphic.raycastTarget = false;
        markerGraphic.Configure(RadarMarkerShape.Diamond, false, playerColor);
        playerMarker.SetAsLastSibling();
    }

    private void RebuildTargetMarkers()
    {
        if (markerPrefab == null || markerRoot == null || discoveryController == null)
        {
            return;
        }

        IReadOnlyCollection<RadarTarget> activeTargets = RadarTarget.ActiveTargets;

        foreach (RadarTarget target in activeTargets)
        {
            if (!ShouldShowTarget(target))
            {
                continue;
            }

            if (!markerMap.TryGetValue(target, out RadarMarkerUI marker) || marker == null)
            {
                marker = Instantiate(markerPrefab, markerRoot);
                marker.gameObject.SetActive(true);
                markerMap[target] = marker;
            }

            marker.SetVisual(
                target.MarkerSprite,
                ResolveMarkerColor(target),
                ResolveMarkerScale(target),
                target.MarkerType
            );
            marker.SetPosition(WorldToMapPosition(target.RecordedMapPosition));
        }

        removeBuffer.Clear();

        foreach (KeyValuePair<RadarTarget, RadarMarkerUI> pair in markerMap)
        {
            if (!ShouldShowTarget(pair.Key))
            {
                removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            RadarTarget target = removeBuffer[i];
            if (markerMap.TryGetValue(target, out RadarMarkerUI marker) && marker != null)
            {
                Destroy(marker.gameObject);
            }

            markerMap.Remove(target);
        }

        playerMarker?.SetAsLastSibling();
    }

    private Color ResolveMarkerColor(RadarTarget target)
    {
        return RadarMarkerPresentation.ResolveColor(
            target.MarkerType,
            target.MarkerColor,
            enemyColor,
            rewardColor,
            meteorColor,
            eventColor,
            specialColor,
            coreColor
        );
    }

    private float ResolveMarkerScale(RadarTarget target)
    {
        float scale = RadarMarkerPresentation.ResolveScale(target.MarkerType, target.MarkerScale);
        if (target.MarkerType == RadarMarkerType.Meteor)
        {
            return scale * meteorMarkerScaleMultiplier;
        }

        return target.MarkerType == RadarMarkerType.Enemy
            ? scale * enemyMarkerScaleMultiplier
            : scale;
    }

    private bool ShouldShowTarget(RadarTarget target)
    {
        if (target == null || !target.IsRadarVisible || !target.ShowOnMap)
        {
            return false;
        }

        if (!discoveryController.IsTargetDiscovered(target))
        {
            return false;
        }

        switch (target.MarkerType)
        {
            case RadarMarkerType.Enemy:
            case RadarMarkerType.Boss:
                return true;

            case RadarMarkerType.Meteor:
                return showMeteorsOnFullMap;

            case RadarMarkerType.RewardObject:
                return showRewardObjectsOnFullMap;

            default:
                return true;
        }
    }

    private Vector2 WorldToMapPosition(Vector2 worldPosition)
    {
        Vector2 normalized = discoveryController.WorldToNormalized(worldPosition);
        Rect rect = mapArea != null ? mapArea.rect : new Rect(-100f, -100f, 200f, 200f);

        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
            Mathf.Lerp(rect.yMin, rect.yMax, normalized.y)
        );
    }

    private Vector2 MapToWorldPosition(Vector2 mapPosition)
    {
        Rect rect = mapArea != null ? mapArea.rect : new Rect(-100f, -100f, 200f, 200f);
        Vector2 normalized = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, mapPosition.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, mapPosition.y)
        );

        return discoveryController.NormalizedToWorld(normalized);
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        PlayerController2D controller = FindFirstObjectByType<PlayerController2D>();
        if (controller != null)
        {
            player = controller.transform;
        }
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class OperationSearchRegionGraphic : MaskableGraphic
{
    [SerializeField, Min(16)] private int segmentCount = 32;
    [SerializeField, Min(1f)] private float outlineWidth = 1.5f;
    [SerializeField, Min(1f)] private float hatchWidth = 1f;
    [SerializeField, Range(3, 9)] private int hatchCount = 7;

    private Color accentColor = new Color(1f, 0.72f, 0.22f, 1f);

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Configure(Color accent)
    {
        accentColor = accent;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        int resolvedSegments = Mathf.Max(16, segmentCount);
        Vector2 center = rect.center;
        Vector2 radius = rect.size * 0.5f;

        AppendFill(vertexHelper, center, radius, resolvedSegments);
        AppendOutline(vertexHelper, center, radius, resolvedSegments);
        AppendHatch(vertexHelper, center, radius);
    }

    private void AppendFill(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 radius,
        int resolvedSegments)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = WithAlpha(accentColor, 0.065f);
        vertex.position = center;
        vertexHelper.AddVert(vertex);

        for (int i = 0; i <= resolvedSegments; i++)
        {
            float angle = i / (float)resolvedSegments * Mathf.PI * 2f;
            vertex.position = center + new Vector2(
                Mathf.Cos(angle) * radius.x,
                Mathf.Sin(angle) * radius.y
            );
            vertexHelper.AddVert(vertex);
        }

        for (int i = 0; i < resolvedSegments; i++)
        {
            vertexHelper.AddTriangle(0, i + 1, i + 2);
        }
    }

    private void AppendOutline(
        VertexHelper vertexHelper,
        Vector2 center,
        Vector2 radius,
        int resolvedSegments)
    {
        Color outlineColor = WithAlpha(accentColor, 0.52f);
        float halfWidth = Mathf.Max(1f, outlineWidth) * 0.5f;

        for (int i = 0; i < resolvedSegments; i++)
        {
            float startAngle = i / (float)resolvedSegments * Mathf.PI * 2f;
            float endAngle = (i + 1) / (float)resolvedSegments * Mathf.PI * 2f;
            Vector2 start = center + new Vector2(
                Mathf.Cos(startAngle) * radius.x,
                Mathf.Sin(startAngle) * radius.y
            );
            Vector2 end = center + new Vector2(
                Mathf.Cos(endAngle) * radius.x,
                Mathf.Sin(endAngle) * radius.y
            );
            AppendSegment(vertexHelper, start, end, halfWidth, outlineColor);
        }
    }

    private void AppendHatch(VertexHelper vertexHelper, Vector2 center, Vector2 radius)
    {
        int resolvedHatchCount = Mathf.Clamp(hatchCount, 3, 9);
        Vector2 direction = new Vector2(0.7071068f, 0.7071068f);
        Vector2 normal = new Vector2(-direction.y, direction.x);
        Color hatchColor = WithAlpha(accentColor, 0.18f);
        float halfWidth = Mathf.Max(1f, hatchWidth) * 0.5f;

        for (int i = 0; i < resolvedHatchCount; i++)
        {
            float normalizedOffset = Mathf.Lerp(-0.72f, 0.72f, i / (float)(resolvedHatchCount - 1));
            float halfLength = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedOffset * normalizedOffset));
            Vector2 normalizedStart = normal * normalizedOffset - direction * halfLength;
            Vector2 normalizedEnd = normal * normalizedOffset + direction * halfLength;
            Vector2 start = center + Vector2.Scale(normalizedStart, radius);
            Vector2 end = center + Vector2.Scale(normalizedEnd, radius);
            AppendSegment(vertexHelper, start, end, halfWidth, hatchColor);
        }
    }

    private static void AppendSegment(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float halfWidth,
        Color segmentColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * halfWidth;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = segmentColor;

        vertex.position = start - perpendicular;
        vertexHelper.AddVert(vertex);
        vertex.position = start + perpendicular;
        vertexHelper.AddVert(vertex);
        vertex.position = end + perpendicular;
        vertexHelper.AddVert(vertex);
        vertex.position = end - perpendicular;
        vertexHelper.AddVert(vertex);

        int firstVertex = vertexHelper.currentVertCount - 4;
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex + 2, firstVertex + 3, firstVertex);
    }

    private static Color WithAlpha(Color source, float alpha)
    {
        source.a *= alpha;
        return source;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        segmentCount = Mathf.Max(16, segmentCount);
        outlineWidth = Mathf.Max(1f, outlineWidth);
        hatchWidth = Mathf.Max(1f, hatchWidth);
        hatchCount = Mathf.Clamp(hatchCount, 3, 9);
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapRouteGraphic : MaskableGraphic
{
    [SerializeField, Min(1f)] private float lineWidth = 1.5f;
    [SerializeField] private Color currentSegmentColor = new Color(0.55f, 0.95f, 1f, 0.75f);
    [SerializeField] private Color futureSegmentColor = new Color(0.55f, 0.95f, 1f, 0.42f);

    private readonly Vector2[] points = new Vector2[ExpeditionRoutePlanner.MaxWaypoints + 1];
    private int pointCount;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Configure(Vector2[] sourcePoints, int sourcePointCount)
    {
        int resolvedCount = sourcePoints == null
            ? 0
            : Mathf.Clamp(sourcePointCount, 0, points.Length);
        bool changed = pointCount != resolvedCount;

        for (int i = 0; i < resolvedCount; i++)
        {
            if (points[i] != sourcePoints[i])
            {
                changed = true;
                points[i] = sourcePoints[i];
            }
        }

        pointCount = resolvedCount;
        if (changed)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (pointCount < 2)
        {
            return;
        }

        float halfWidth = Mathf.Max(1f, lineWidth) * 0.5f;
        for (int i = 0; i < pointCount - 1; i++)
        {
            AppendSegment(
                vertexHelper,
                points[i],
                points[i + 1],
                halfWidth,
                i == 0 ? currentSegmentColor : futureSegmentColor
            );
        }
    }

    private static void AppendSegment(
        VertexHelper vertexHelper,
        Vector2 start,
        Vector2 end,
        float halfWidth,
        Color color)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * halfWidth;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - normal;
        vertexHelper.AddVert(vertex);
        vertex.position = start + normal;
        vertexHelper.AddVert(vertex);
        vertex.position = end + normal;
        vertexHelper.AddVert(vertex);
        vertex.position = end - normal;
        vertexHelper.AddVert(vertex);

        int firstVertex = vertexHelper.currentVertCount - 4;
        vertexHelper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
        vertexHelper.AddTriangle(firstVertex + 2, firstVertex + 3, firstVertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        lineWidth = Mathf.Max(1f, lineWidth);
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapGridGraphic : MaskableGraphic
{
    [Header("World Grid")]
    [SerializeField, Min(0.25f)] private float worldCellSize = 10f;
    [SerializeField, Min(1)] private int majorLineEvery = 5;

    [Header("Lines")]
    [SerializeField, Min(1f)] private float minorLineWidth = 1f;
    [SerializeField, Min(1f)] private float majorLineWidth = 1f;
    [SerializeField, Min(1f)] private float boundaryLineWidth = 2f;
    [SerializeField] private Color minorLineColor = new Color(0.7f, 0.76f, 0.82f, 0.1f);
    [SerializeField] private Color majorLineColor = new Color(0.78f, 0.84f, 0.9f, 0.18f);
    [SerializeField] private Color boundaryLineColor = new Color(0.55f, 0.08f, 0.08f, 0.45f);

    private Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(120f, 120f, 1f));

    public float WorldCellSize => worldCellSize;
    public Bounds WorldBounds => worldBounds;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void Configure(Bounds bounds, float cellSize)
    {
        float resolvedCellSize = Mathf.Max(0.25f, cellSize);
        bool changed = !Approximately(worldBounds, bounds) ||
                       !Mathf.Approximately(worldCellSize, resolvedCellSize);

        worldBounds = bounds;
        worldCellSize = resolvedCellSize;

        if (changed)
        {
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f ||
            worldBounds.size.x <= 0f || worldBounds.size.y <= 0f)
        {
            return;
        }

        AppendWorldGrid(vertexHelper, rect);
        AppendBoundaryFrame(vertexHelper, rect);
    }

    private void AppendWorldGrid(VertexHelper vertexHelper, Rect rect)
    {
        float cellSize = Mathf.Max(0.25f, worldCellSize);
        int firstX = Mathf.CeilToInt(worldBounds.min.x / cellSize);
        int lastX = Mathf.FloorToInt(worldBounds.max.x / cellSize);

        for (int cell = firstX; cell <= lastX; cell++)
        {
            float worldX = cell * cellSize;
            if (worldX <= worldBounds.min.x + 0.001f || worldX >= worldBounds.max.x - 0.001f)
            {
                continue;
            }

            bool major = IsMajorLine(cell);
            float lineWidth = ResolveLineWidth(major);
            float normalized = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldX);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, normalized);
            AppendQuad(
                vertexHelper,
                new Rect(x - lineWidth * 0.5f, rect.yMin, lineWidth, rect.height),
                major ? majorLineColor : minorLineColor
            );
        }

        int firstY = Mathf.CeilToInt(worldBounds.min.y / cellSize);
        int lastY = Mathf.FloorToInt(worldBounds.max.y / cellSize);

        for (int cell = firstY; cell <= lastY; cell++)
        {
            float worldY = cell * cellSize;
            if (worldY <= worldBounds.min.y + 0.001f || worldY >= worldBounds.max.y - 0.001f)
            {
                continue;
            }

            bool major = IsMajorLine(cell);
            float lineWidth = ResolveLineWidth(major);
            float normalized = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldY);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, normalized);
            AppendQuad(
                vertexHelper,
                new Rect(rect.xMin, y - lineWidth * 0.5f, rect.width, lineWidth),
                major ? majorLineColor : minorLineColor
            );
        }
    }

    private void AppendBoundaryFrame(VertexHelper vertexHelper, Rect rect)
    {
        float width = Mathf.Max(1f, boundaryLineWidth);
        AppendQuad(vertexHelper, new Rect(rect.xMin, rect.yMin, rect.width, width), boundaryLineColor);
        AppendQuad(vertexHelper, new Rect(rect.xMin, rect.yMax - width, rect.width, width), boundaryLineColor);
        AppendQuad(vertexHelper, new Rect(rect.xMin, rect.yMin, width, rect.height), boundaryLineColor);
        AppendQuad(vertexHelper, new Rect(rect.xMax - width, rect.yMin, width, rect.height), boundaryLineColor);
    }

    private bool IsMajorLine(int cell)
    {
        return majorLineEvery > 0 && Mathf.Abs(cell) % majorLineEvery == 0;
    }

    private float ResolveLineWidth(bool major)
    {
        return Mathf.Max(1f, major ? majorLineWidth : minorLineWidth);
    }

    private static void AppendQuad(VertexHelper vertexHelper, Rect rect, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(rect.xMin, rect.yMin);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMin, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMax);
        vertexHelper.AddVert(vertex);
        vertex.position = new Vector3(rect.xMax, rect.yMin);
        vertexHelper.AddVert(vertex);

        int start = vertexHelper.currentVertCount - 4;
        vertexHelper.AddTriangle(start, start + 1, start + 2);
        vertexHelper.AddTriangle(start + 2, start + 3, start);
    }

    private static bool Approximately(Bounds left, Bounds right)
    {
        return (left.center - right.center).sqrMagnitude < 0.000001f &&
               (left.size - right.size).sqrMagnitude < 0.000001f;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        worldCellSize = Mathf.Max(0.25f, worldCellSize);
        majorLineEvery = Mathf.Max(1, majorLineEvery);
        minorLineWidth = Mathf.Max(1f, minorLineWidth);
        majorLineWidth = Mathf.Max(1f, majorLineWidth);
        boundaryLineWidth = Mathf.Max(1f, boundaryLineWidth);
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif
}
