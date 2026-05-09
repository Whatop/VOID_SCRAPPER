using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MouseCursorTarget : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Cursor")]
    [SerializeField] private GameCursorType hoverCursor = GameCursorType.Hover;
    [SerializeField] private GameCursorType pressedCursor = GameCursorType.Pressed;
    [SerializeField] private GameCursorType disabledCursor = GameCursorType.Disabled;

    [Header("Reference")]
    [SerializeField] private Selectable selectable;

    private bool isHovering;
    private bool isPressed;

    private void Reset()
    {
        selectable = GetComponent<Selectable>();
    }

    private void Awake()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }
    }

    private void Update()
    {
        if (!isHovering)
        {
            return;
        }

        if (isPressed)
        {
            return;
        }

        ApplyHoverCursor();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        isPressed = false;
        ApplyHoverCursor();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        isPressed = false;

        if (MouseCursorManager.Instance != null)
        {
            MouseCursorManager.Instance.ResetToSceneDefault();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;

        if (MouseCursorManager.Instance == null)
        {
            return;
        }

        MouseCursorManager.Instance.SetCursor(IsUsable() ? pressedCursor : disabledCursor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        if (isHovering)
        {
            ApplyHoverCursor();
            return;
        }

        if (MouseCursorManager.Instance != null)
        {
            MouseCursorManager.Instance.ResetToSceneDefault();
        }
    }

    private void ApplyHoverCursor()
    {
        if (MouseCursorManager.Instance == null)
        {
            return;
        }

        MouseCursorManager.Instance.SetCursor(IsUsable() ? hoverCursor : disabledCursor);
    }

    private bool IsUsable()
    {
        if (selectable == null)
        {
            return true;
        }

        return selectable.IsInteractable();
    }
}