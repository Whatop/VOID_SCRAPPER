using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum ShopMaintenanceBaySlotKind
{
    CurrentEquipped,
    Storage
}

[DisallowMultipleComponent]
public class ShopMaintenanceBaySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private static ShopMaintenanceBaySlotUI draggingSlot;

    private ShopMaintenanceBayUI owner;
    private ShopMaintenanceBaySlotKind slotKind;
    private int slotIndex;
    private Image iconImage;

    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform dragIconRect;
    private Image dragIconImage;

    private bool isDragging;
    private bool dropHandled;
    private Vector2 lastPointerPosition;

    public ShopMaintenanceBaySlotKind SlotKind => slotKind;
    public int SlotIndex => slotIndex;
    public RectTransform RectTransform => transform as RectTransform;

    public void Bind(
        ShopMaintenanceBayUI ownerUI,
        ShopMaintenanceBaySlotKind kind,
        int index,
        Image icon)
    {
        owner = ownerUI;
        slotKind = kind;
        slotIndex = index;
        iconImage = icon;

        rootCanvas = GetComponentInParent<Canvas>();

        if (rootCanvas != null)
        {
            rootCanvas = rootCanvas.rootCanvas;
        }

        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null || isDragging)
        {
            return;
        }

        owner.SelectSlot(slotKind, slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        if (!owner.CanDragSlot(slotKind, slotIndex))
        {
            return;
        }

        Sprite sprite = owner.GetSlotIcon(slotKind, slotIndex);

        if (sprite == null)
        {
            return;
        }

        draggingSlot = this;
        isDragging = true;
        dropHandled = false;
        lastPointerPosition = eventData.position;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = owner.SourceSlotAlphaWhileDragging;
        }

        owner.SelectSlot(slotKind, slotIndex);
        CreateDragIcon(sprite, eventData);
        UpdateDragIconPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || draggingSlot != this)
        {
            return;
        }

        lastPointerPosition = eventData.position;
        UpdateDragIconPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        lastPointerPosition = eventData.position;

        if (!dropHandled && owner != null)
        {
            owner.TryHandleDropToNearest(this, eventData.position);
        }

        if (draggingSlot == this)
        {
            draggingSlot = null;
        }

        isDragging = false;
        dropHandled = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }

        DestroyDragIcon();

        if (owner != null)
        {
            owner.ClearDragHover();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggingSlot == null || owner == null)
        {
            return;
        }

        if (draggingSlot == this)
        {
            return;
        }

        bool handled = owner.HandleSlotDrop(
            draggingSlot.SlotKind,
            draggingSlot.SlotIndex,
            slotKind,
            slotIndex
        );

        if (handled)
        {
            draggingSlot.MarkDropHandled();
        }

        eventData.Use();
    }

    private void MarkDropHandled()
    {
        dropHandled = true;
    }

    private void CreateDragIcon(Sprite sprite, PointerEventData eventData)
    {
        if (rootCanvas == null)
        {
            return;
        }

        GameObject dragObject = new GameObject("Dragging_Reinforcement_Icon");
        dragObject.transform.SetParent(rootCanvas.transform, false);
        dragObject.transform.SetAsLastSibling();

        dragIconRect = dragObject.AddComponent<RectTransform>();
        dragIconRect.sizeDelta = ResolveDragIconSize();

        dragIconImage = dragObject.AddComponent<Image>();
        dragIconImage.sprite = sprite;
        dragIconImage.preserveAspect = true;
        dragIconImage.raycastTarget = false;

        CanvasGroup dragCanvasGroup = dragObject.AddComponent<CanvasGroup>();
        dragCanvasGroup.blocksRaycasts = false;
        dragCanvasGroup.alpha = owner != null ? owner.DragIconAlpha : 0.9f;
    }

    private Vector2 ResolveDragIconSize()
    {
        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;

            if (iconRect != null)
            {
                Vector2 size = iconRect.rect.size;

                if (size.x > 0f && size.y > 0f)
                {
                    return size;
                }
            }
        }

        return new Vector2(64f, 64f);
    }

    private void UpdateDragIconPosition(PointerEventData eventData)
    {
        if (dragIconRect == null || rootCanvas == null)
        {
            return;
        }

        Vector2 screenPosition = eventData.position;

        if (owner != null && owner.TryGetDropSnapScreenPosition(this, eventData.position, out Vector2 snapScreenPosition))
        {
            screenPosition = snapScreenPosition;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : eventData.pressEventCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                eventCamera,
                out Vector2 localPoint))
        {
            dragIconRect.anchoredPosition = localPoint;
        }
    }

    private void DestroyDragIcon()
    {
        if (dragIconRect == null)
        {
            return;
        }

        Destroy(dragIconRect.gameObject);
        dragIconRect = null;
        dragIconImage = null;
    }
}
