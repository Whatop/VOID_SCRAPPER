using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopMaintenanceBayUI : MonoBehaviour
{
    [Header("루트")]
    [SerializeField] private GameObject root;

    [Header("왼쪽 상세 정보")]
    [SerializeField] private Image selectedIconImage;
    [SerializeField] private TextMeshProUGUI selectedNameText;
    [SerializeField] private TextMeshProUGUI selectedTypeText;
    [SerializeField] private TextMeshProUGUI selectedDescriptionText;

    [Header("현재 액티브 슬롯")]
    [SerializeField] private Button currentEquippedButton;
    [SerializeField] private Image currentEquippedIconImage;
    [SerializeField] private TextMeshProUGUI currentEquippedNameText;
    [SerializeField] private TextMeshProUGUI currentEquippedTypeText;

    [Header("선택 장비 표시")]
    [SerializeField] private Image selectedPreviewIconImage;
    [SerializeField] private TextMeshProUGUI selectedPreviewNameText;
    [SerializeField] private TextMeshProUGUI selectedPreviewTypeText;

    [Header("보관 슬롯")]
    [SerializeField] private Button[] storageSlotButtons = new Button[6];
    [SerializeField] private Image[] storageSlotIcons = new Image[6];
    [SerializeField] private TextMeshProUGUI[] storageSlotNameTexts = new TextMeshProUGUI[6];
    [SerializeField] private bool hideStorageSlotNameWhenEmpty = true;
    [SerializeField] private string emptySlotNameText = "";
    [SerializeField] private GameObject[] storageSlotFilledRoots = new GameObject[6];
    [SerializeField] private GameObject[] storageSlotEmptyRoots = new GameObject[6];

    [Header("드래그 보정")]
    [Tooltip("마우스가 슬롯 중심에서 이 거리 안에 들어오면 자동으로 그 슬롯에 스냅/드랍됩니다.")]
    [SerializeField] private float dragSnapRadiusPixels = 130f;

    [Tooltip("드래그 중 원본 슬롯 투명도")]
    [Range(0.1f, 1f)]
    [SerializeField] private float sourceSlotAlphaWhileDragging = 0.45f;

    [Tooltip("드래그 중 따라다니는 아이콘 투명도")]
    [Range(0.1f, 1f)]
    [SerializeField] private float dragIconAlpha = 0.92f;

    [Tooltip("켜면 드래그 아이콘이 슬롯 근처에서 슬롯 중앙으로 딱 붙습니다.")]
    [SerializeField] private bool snapDragIconToNearestSlot = true;

    [Header("하단 버튼")]
    [SerializeField] private Button swapButton;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button exitButton;

    [Header("기본 이미지")]
    [SerializeField] private Sprite fallbackIcon;
    [SerializeField] private Sprite emptySlotIcon;

    private ShopStructure currentShop;
    private GameObject currentPlayer;
    private PlayerReinforcementController currentController;
    private ShopActiveMaintenanceBay currentBay;

    private int selectedStoredIndex = -1;
    private bool selectedCurrentEquipped;

    private Canvas rootCanvas;
    private ShopMaintenanceBaySlotUI currentEquippedSlotUI;
    private readonly ShopMaintenanceBaySlotUI[] storageSlotUIs = new ShopMaintenanceBaySlotUI[16];

    public bool IsOpen => root != null ? root.activeSelf : gameObject.activeSelf;
    public float SourceSlotAlphaWhileDragging => sourceSlotAlphaWhileDragging;
    public float DragIconAlpha => dragIconAlpha;

    public event Action ExitRequested;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas != null)
        {
            rootCanvas = rootCanvas.rootCanvas;
        }

        BindButtons();
        RegisterSlotDragHandlers();
        SetVisible(false);
    }


    private void OnDestroy()
    {
        if (swapButton != null)
        {
            swapButton.onClick.RemoveListener(OnClickSwap);
        }

        if (dropButton != null)
        {
            dropButton.onClick.RemoveListener(OnClickDrop);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnClickExit);
        }
    }

    public void Open(ShopStructure shop, GameObject playerObject, ShopActiveMaintenanceBay bay)
    {
        currentShop = shop;
        currentPlayer = playerObject;
        currentBay = bay != null ? bay : (shop != null ? shop.ActiveMaintenanceBay : null);
        currentController = playerObject != null
            ? playerObject.GetComponentInChildren<PlayerReinforcementController>(true)
            : null;

        if (currentBay != null && storageSlotButtons != null && storageSlotButtons.Length > 0)
        {
            currentBay.SetCapacity(storageSlotButtons.Length);
        }

        selectedStoredIndex = -1;
        selectedCurrentEquipped = true;

        RegisterSlotDragHandlers();
        SetVisible(true);
        RefreshView();
        SelectCurrentEquipped();
    }

    public void Close()
    {
        currentShop = null;
        currentPlayer = null;
        currentController = null;
        currentBay = null;

        selectedStoredIndex = -1;
        selectedCurrentEquipped = false;

        SetVisible(false);
    }

    public void RefreshView()
    {
        RefreshCurrentEquipped();
        RefreshStorageSlots();
        RefreshBottomButtons();
    }

    public void SelectSlot(ShopMaintenanceBaySlotKind kind, int index)
    {
        switch (kind)
        {
            case ShopMaintenanceBaySlotKind.CurrentEquipped:
                SelectCurrentEquipped();
                break;

            case ShopMaintenanceBaySlotKind.Storage:
                SelectStored(index);
                break;
        }
    }

    public bool CanDragSlot(ShopMaintenanceBaySlotKind kind, int index)
    {
        switch (kind)
        {
            case ShopMaintenanceBaySlotKind.CurrentEquipped:
                return currentController != null && currentController.HasEquipment;

            case ShopMaintenanceBaySlotKind.Storage:
                return currentBay != null && currentBay.GetAt(index) != null;

            default:
                return false;
        }
    }

    public Sprite GetSlotIcon(ShopMaintenanceBaySlotKind kind, int index)
    {
        switch (kind)
        {
            case ShopMaintenanceBaySlotKind.CurrentEquipped:
            {
                ReinforcementDefinition equipped = currentController != null
                    ? currentController.EquippedDefinition
                    : null;

                if (equipped != null && equipped.Icon != null)
                {
                    return equipped.Icon;
                }

                return fallbackIcon;
            }

            case ShopMaintenanceBaySlotKind.Storage:
            {
                ReinforcementDefinition stored = currentBay != null
                    ? currentBay.GetAt(index)
                    : null;

                if (stored != null && stored.Icon != null)
                {
                    return stored.Icon;
                }

                return emptySlotIcon != null ? emptySlotIcon : fallbackIcon;
            }

            default:
                return fallbackIcon;
        }
    }

    public bool HandleSlotDrop(
        ShopMaintenanceBaySlotKind fromKind,
        int fromIndex,
        ShopMaintenanceBaySlotKind toKind,
        int toIndex)
    {
        if (currentBay == null)
        {
            return false;
        }

        bool changed = false;

        if (fromKind == ShopMaintenanceBaySlotKind.CurrentEquipped &&
            toKind == ShopMaintenanceBaySlotKind.Storage)
        {
            changed = currentBay.TryStoreCurrentInSlot(toIndex, currentController);
        }
        else if (fromKind == ShopMaintenanceBaySlotKind.Storage &&
                 toKind == ShopMaintenanceBaySlotKind.CurrentEquipped)
        {
            changed = currentBay.TryEquipStored(fromIndex, currentController);
        }
        else if (fromKind == ShopMaintenanceBaySlotKind.Storage &&
                 toKind == ShopMaintenanceBaySlotKind.Storage)
        {
            changed = currentBay.TryMoveOrSwapStored(fromIndex, toIndex);
        }

        if (!changed)
        {
            return false;
        }

        RefreshView();

        if (toKind == ShopMaintenanceBaySlotKind.CurrentEquipped)
        {
            SelectCurrentEquipped();
        }
        else
        {
            SelectStored(toIndex);
        }

        return true;
    }

    public bool TryGetDropSnapScreenPosition(
        ShopMaintenanceBaySlotUI sourceSlot,
        Vector2 pointerScreenPosition,
        out Vector2 snapScreenPosition)
    {
        snapScreenPosition = pointerScreenPosition;

        if (!snapDragIconToNearestSlot)
        {
            return false;
        }

        if (!TryFindNearestDropTarget(sourceSlot, pointerScreenPosition, out ShopMaintenanceBaySlotUI targetSlot, out Vector2 targetCenter))
        {
            return false;
        }

        snapScreenPosition = targetCenter;
        return true;
    }

    public bool TryHandleDropToNearest(ShopMaintenanceBaySlotUI sourceSlot, Vector2 pointerScreenPosition)
    {
        if (!TryFindNearestDropTarget(sourceSlot, pointerScreenPosition, out ShopMaintenanceBaySlotUI targetSlot, out _))
        {
            return false;
        }

        return HandleSlotDrop(
            sourceSlot.SlotKind,
            sourceSlot.SlotIndex,
            targetSlot.SlotKind,
            targetSlot.SlotIndex
        );
    }

    public void ClearDragHover()
    {
        // 필요하면 나중에 하이라이트 오브젝트를 여기서 끄면 된다.
    }

    private bool TryFindNearestDropTarget(
        ShopMaintenanceBaySlotUI sourceSlot,
        Vector2 pointerScreenPosition,
        out ShopMaintenanceBaySlotUI targetSlot,
        out Vector2 targetCenter)
    {
        targetSlot = null;
        targetCenter = pointerScreenPosition;

        if (sourceSlot == null)
        {
            return false;
        }

        float maxDistance = Mathf.Max(1f, dragSnapRadiusPixels);
        float bestSqrDistance = maxDistance * maxDistance;

        TryConsiderDropTarget(sourceSlot, currentEquippedSlotUI, pointerScreenPosition, ref targetSlot, ref targetCenter, ref bestSqrDistance);

        if (storageSlotButtons != null)
        {
            for (int i = 0; i < storageSlotButtons.Length && i < storageSlotUIs.Length; i++)
            {
                TryConsiderDropTarget(sourceSlot, storageSlotUIs[i], pointerScreenPosition, ref targetSlot, ref targetCenter, ref bestSqrDistance);
            }
        }

        return targetSlot != null;
    }

    private void TryConsiderDropTarget(
        ShopMaintenanceBaySlotUI sourceSlot,
        ShopMaintenanceBaySlotUI candidate,
        Vector2 pointerScreenPosition,
        ref ShopMaintenanceBaySlotUI bestTarget,
        ref Vector2 bestCenter,
        ref float bestSqrDistance)
    {
        if (candidate == null || candidate == sourceSlot)
        {
            return;
        }

        if (!IsValidDropPair(sourceSlot.SlotKind, candidate.SlotKind))
        {
            return;
        }

        RectTransform candidateRect = candidate.RectTransform;

        if (candidateRect == null || !candidate.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector2 center = GetRectScreenCenter(candidateRect);
        float sqrDistance = GetScreenRectDistanceSqr(candidateRect, pointerScreenPosition);

        if (sqrDistance > bestSqrDistance)
        {
            return;
        }

        bestSqrDistance = sqrDistance;
        bestTarget = candidate;
        bestCenter = center;
    }

    private bool IsValidDropPair(ShopMaintenanceBaySlotKind fromKind, ShopMaintenanceBaySlotKind toKind)
    {
        if (fromKind == toKind && fromKind == ShopMaintenanceBaySlotKind.CurrentEquipped)
        {
            return false;
        }

        if (fromKind == ShopMaintenanceBaySlotKind.CurrentEquipped && toKind == ShopMaintenanceBaySlotKind.Storage)
        {
            return true;
        }

        if (fromKind == ShopMaintenanceBaySlotKind.Storage && toKind == ShopMaintenanceBaySlotKind.CurrentEquipped)
        {
            return true;
        }

        if (fromKind == ShopMaintenanceBaySlotKind.Storage && toKind == ShopMaintenanceBaySlotKind.Storage)
        {
            return true;
        }

        return false;
    }


    private float GetScreenRectDistanceSqr(RectTransform rectTransform, Vector2 screenPosition)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Camera camera = ResolveEventCamera();
        Vector2 c0 = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 c1 = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
        Vector2 c2 = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        Vector2 c3 = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);

        float minX = Mathf.Min(c0.x, c1.x, c2.x, c3.x);
        float maxX = Mathf.Max(c0.x, c1.x, c2.x, c3.x);
        float minY = Mathf.Min(c0.y, c1.y, c2.y, c3.y);
        float maxY = Mathf.Max(c0.y, c1.y, c2.y, c3.y);

        float dx = 0f;
        if (screenPosition.x < minX)
        {
            dx = minX - screenPosition.x;
        }
        else if (screenPosition.x > maxX)
        {
            dx = screenPosition.x - maxX;
        }

        float dy = 0f;
        if (screenPosition.y < minY)
        {
            dy = minY - screenPosition.y;
        }
        else if (screenPosition.y > maxY)
        {
            dy = screenPosition.y - maxY;
        }

        return dx * dx + dy * dy;
    }

    private Vector2 GetRectScreenCenter(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
        Camera camera = ResolveEventCamera();
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
    }

    private Camera ResolveEventCamera()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();

            if (rootCanvas != null)
            {
                rootCanvas = rootCanvas.rootCanvas;
            }
        }

        if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
    }

    private void BindButtons()
    {
        if (swapButton != null)
        {
            swapButton.onClick.RemoveListener(OnClickSwap);
            swapButton.onClick.AddListener(OnClickSwap);
        }

        if (dropButton != null)
        {
            dropButton.onClick.RemoveListener(OnClickDrop);
            dropButton.onClick.AddListener(OnClickDrop);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnClickExit);
            exitButton.onClick.AddListener(OnClickExit);
        }
    }

    private void RegisterSlotDragHandlers()
    {
        currentEquippedSlotUI = BindSlot(
            currentEquippedButton,
            ShopMaintenanceBaySlotKind.CurrentEquipped,
            -1,
            currentEquippedIconImage
        );

        if (storageSlotButtons == null)
        {
            return;
        }

        for (int i = 0; i < storageSlotUIs.Length; i++)
        {
            storageSlotUIs[i] = null;
        }

        for (int i = 0; i < storageSlotButtons.Length && i < storageSlotUIs.Length; i++)
        {
            Image icon = GetArrayItem(storageSlotIcons, i);
            storageSlotUIs[i] = BindSlot(storageSlotButtons[i], ShopMaintenanceBaySlotKind.Storage, i, icon);
        }
    }

    private ShopMaintenanceBaySlotUI BindSlot(Button button, ShopMaintenanceBaySlotKind kind, int index, Image icon)
    {
        if (button == null)
        {
            return null;
        }

        ShopMaintenanceBaySlotUI slotUI = button.GetComponent<ShopMaintenanceBaySlotUI>();

        if (slotUI == null)
        {
            slotUI = button.gameObject.AddComponent<ShopMaintenanceBaySlotUI>();
        }

        slotUI.Bind(this, kind, index, icon);

        // 빈 슬롯도 드롭을 받아야 하므로 버튼은 항상 켜둔다.
        button.interactable = true;
        return slotUI;
    }

    private void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void RefreshCurrentEquipped()
    {
        ReinforcementDefinition equipped = currentController != null
            ? currentController.EquippedDefinition
            : null;

        SetIcon(currentEquippedIconImage, equipped != null ? equipped.Icon : fallbackIcon);

        if (currentEquippedNameText != null)
        {
            currentEquippedNameText.text = equipped != null ? equipped.DisplayName : "장착 없음";
        }

        if (currentEquippedTypeText != null)
        {
            currentEquippedTypeText.text = equipped != null ? equipped.GetUseTypeText() : string.Empty;
        }

        if (currentEquippedButton != null)
        {
            currentEquippedButton.interactable = true;
        }
    }

    private void RefreshStorageSlots()
    {
        if (storageSlotButtons == null)
        {
            return;
        }

        for (int i = 0; i < storageSlotButtons.Length; i++)
        {
            ReinforcementDefinition definition = currentBay != null ? currentBay.GetAt(i) : null;
            bool hasItem = definition != null;

            if (storageSlotButtons[i] != null)
            {
                storageSlotButtons[i].interactable = true;
            }

            GameObject filledRoot = GetArrayItem(storageSlotFilledRoots, i);
            GameObject emptyRoot = GetArrayItem(storageSlotEmptyRoots, i);

            if (filledRoot != null)
            {
                filledRoot.SetActive(hasItem);
            }

            if (emptyRoot != null)
            {
                emptyRoot.SetActive(!hasItem);
            }

            Image icon = GetArrayItem(storageSlotIcons, i);

            if (icon != null)
            {
                Sprite sprite = hasItem
                    ? definition.Icon
                    : emptySlotIcon;

                SetIcon(icon, sprite);
            }

            TextMeshProUGUI nameText = GetArrayItem(storageSlotNameTexts, i);

            if (nameText != null)
            {
                nameText.text = hasItem ? definition.DisplayName : emptySlotNameText;
                nameText.gameObject.SetActive(hasItem || !hideStorageSlotNameWhenEmpty);
            }
        }
    }

    private void RefreshBottomButtons()
    {
        bool hasStoredSelection = selectedStoredIndex >= 0 &&
                                  currentBay != null &&
                                  currentBay.GetAt(selectedStoredIndex) != null;

        if (swapButton != null)
        {
            swapButton.interactable = hasStoredSelection && currentController != null;
        }

        if (dropButton != null)
        {
            dropButton.interactable = hasStoredSelection && currentShop != null;
        }
    }

    private void SelectCurrentEquipped()
    {
        selectedCurrentEquipped = true;
        selectedStoredIndex = -1;

        ReinforcementDefinition equipped = currentController != null
            ? currentController.EquippedDefinition
            : null;

        ApplySelectedDetail(equipped, equipped == null ? "현재 장착된 액티브 장비가 없습니다." : null);
        ApplySelectedPreview(equipped);
        RefreshBottomButtons();
    }

    private void SelectStored(int index)
    {
        ReinforcementDefinition definition = currentBay != null ? currentBay.GetAt(index) : null;

        if (definition == null)
        {
            selectedCurrentEquipped = false;
            selectedStoredIndex = -1;

            ApplySelectedDetail(null, "빈 보관 슬롯입니다.\n현재 액티브를 드래그해서 이 칸에 보관할 수 있습니다.");
            ApplySelectedPreview(null);
            RefreshBottomButtons();
            return;
        }

        selectedCurrentEquipped = false;
        selectedStoredIndex = index;

        ApplySelectedDetail(definition, null);
        ApplySelectedPreview(definition);
        RefreshBottomButtons();
    }

    private void ApplySelectedDetail(ReinforcementDefinition definition, string fallbackDescription)
    {
        SetIcon(selectedIconImage, definition != null ? definition.Icon : fallbackIcon);

        if (selectedNameText != null)
        {
            selectedNameText.text = definition != null ? definition.DisplayName : "선택 없음";
        }

        if (selectedTypeText != null)
        {
            selectedTypeText.text = definition != null
                ? $"{definition.GetUseTypeText()} / {definition.GetAvailabilityText()}"
                : string.Empty;
        }

        if (selectedDescriptionText != null)
        {
            selectedDescriptionText.text = definition != null
                ? $"{definition.Description}\n\n효과\n{definition.BuildEffectSummary()}"
                : fallbackDescription;
        }
    }

    private void ApplySelectedPreview(ReinforcementDefinition definition)
    {
        SetIcon(selectedPreviewIconImage, definition != null ? definition.Icon : fallbackIcon);

        if (selectedPreviewNameText != null)
        {
            selectedPreviewNameText.text = definition != null ? definition.DisplayName : "선택 없음";
        }

        if (selectedPreviewTypeText != null)
        {
            selectedPreviewTypeText.text = definition != null ? definition.GetUseTypeText() : string.Empty;
        }
    }

    private void OnClickSwap()
    {
        if (currentBay == null || currentController == null || selectedStoredIndex < 0)
        {
            return;
        }

        bool swapped = currentBay.TryEquipStored(selectedStoredIndex, currentController);

        if (!swapped)
        {
            return;
        }

        RefreshView();
        SelectCurrentEquipped();
    }

    private void OnClickDrop()
    {
        if (currentBay == null || currentShop == null || selectedStoredIndex < 0)
        {
            return;
        }

        int droppedIndex = selectedStoredIndex;

        if (!currentBay.RemoveAt(droppedIndex, out ReinforcementDefinition removed, out int removedCharges))
        {
            return;
        }

        bool dropped = currentShop.TryDropReinforcementToField(removed, removedCharges);

        if (!dropped)
        {
            bool restored = currentBay.TryStoreAt(droppedIndex, removed, removedCharges);

            if (!restored)
            {
                currentBay.TryStore(removed, removedCharges);
            }

            return;
        }

        selectedStoredIndex = -1;
        selectedCurrentEquipped = true;

        RefreshView();
        SelectCurrentEquipped();
    }

    private void OnClickExit()
    {
        if (ExitRequested != null)
        {
            ExitRequested.Invoke();
            return;
        }

        Close();
    }

    private void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private T GetArrayItem<T>(T[] array, int index) where T : UnityEngine.Object
    {
        if (array == null || index < 0 || index >= array.Length)
        {
            return null;
        }

        return array[index];
    }
}
