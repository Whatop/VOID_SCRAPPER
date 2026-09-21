using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class SettlementPrimaryNavigationPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IMoveHandler, ISelectHandler, IDeselectHandler, IUpdateSelectedHandler
{
    [SerializeField] private SettlementUIController owner;
    [SerializeField] private int navigationIndex;

    public void Configure(SettlementUIController controller, int index)
    {
        owner = controller;
        navigationIndex = index;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.SetPrimaryNavigationHover(navigationIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.SetPrimaryNavigationHover(navigationIndex, false);
    }

    private void OnDisable()
    {
        // Scene unload may destroy the owner before disabling this child.
        if (owner != null)
        {
            owner.SetPrimaryNavigationHover(navigationIndex, false);
        }
    }

    public void OnMove(AxisEventData eventData)
    {
        owner?.MovePrimaryNavigation(navigationIndex, eventData);
    }

    public void OnSelect(BaseEventData eventData)
    {
        owner?.SetPrimaryNavigationFocus(navigationIndex, true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        owner?.SetPrimaryNavigationFocus(navigationIndex, false);
    }

    public void OnUpdateSelected(BaseEventData eventData)
    {
        owner?.UpdatePrimaryNavigationInput(navigationIndex, eventData);
    }
}
