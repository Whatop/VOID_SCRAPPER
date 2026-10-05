using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Authored only on Equipment Development controls. EventSystem dispatches this to its
// selected object, so no second cursor, global input binding, or polling loop is needed.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class EquipmentDevelopmentInput : MonoBehaviour, IUpdateSelectedHandler, ISelectHandler
{
    [SerializeField] private ShipTraitTreePanel owner;
    private Button button;
    private int submittedFrame = -1;

    private void Awake() => button = GetComponent<Button>();

    public void OnSelect(BaseEventData eventData)
    {
        if (owner != null && owner.CanUseTraitInput) owner.RevealEquipmentControl(transform as RectTransform);
    }

    public void OnUpdateSelected(BaseEventData eventData)
    {
        if (!isActiveAndEnabled || owner == null || !owner.IsEquipmentDevelopment || !owner.CanUseTraitInput ||
            button == null || !button.IsActive() || !button.IsInteractable() || EventSystem.current == null ||
            EventSystem.current.currentSelectedGameObject != gameObject || Keyboard.current == null ||
            !Keyboard.current.spaceKey.wasPressedThisFrame) return;
        // Like primary Settlement navigation, consume updateSelected to suppress the
        // module's Submit in this dispatch, even if Space is also bound by the user.
        eventData.Use();
        if (submittedFrame == Time.frameCount) return;
        submittedFrame = Time.frameCount;
        button.onClick.Invoke();
    }
}
