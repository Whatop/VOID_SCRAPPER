using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Existing selection handler, now persistable and also the card template's typed binding owner.
[DisallowMultipleComponent]
public sealed class SettlementSectorTechnologyEntrySelection : MonoBehaviour, ISelectHandler, IDeselectHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button cardButton;
    [SerializeField] private Image cardImage;
    [SerializeField] private Outline cardOutline;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI iconLabel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI summaryText;
    private Action<bool> previewAction;
    private bool hovered;
    private bool focused;

    public Button CardButton => cardButton;
    public Image CardImage => cardImage;
    public Outline CardOutline => cardOutline;
    public Image Icon => icon;
    public TextMeshProUGUI IconLabel => iconLabel;
    public TextMeshProUGUI NameText => nameText;
    public TextMeshProUGUI SummaryText => summaryText;

    public void Configure(Action<bool> action) { previewAction = action; }
    public void OnSelect(BaseEventData eventData) { focused = true; RefreshPreview(); }
    public void OnDeselect(BaseEventData eventData) { focused = false; RefreshPreview(); }
    public void OnPointerEnter(PointerEventData eventData) { hovered = true; RefreshPreview(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; RefreshPreview(); }

    private void OnDisable()
    {
        hovered = focused = false;
        RefreshPreview();
    }

    private void RefreshPreview() { previewAction?.Invoke(hovered || focused); }

    public void CollectBindingErrors(List<string> errors)
    {
        var unique = new Dictionary<Component, string>();
        Component[] values = { cardButton, cardImage, cardOutline, icon, iconLabel, nameText, summaryText };
        string[] fields = { nameof(cardButton), nameof(cardImage), nameof(cardOutline), nameof(icon), nameof(iconLabel), nameof(nameText), nameof(summaryText) };
        for (int i = 0; i < values.Length; i++)
        {
            Component value = values[i];
            string property = GetType().Name + "." + fields[i] + " on " + SettlementSectorTechnologyPanelUI.BindingLocation(transform);
            Transform container = i == 4 ? icon != null ? icon.transform : null : transform;
            if (value == null)
            {
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, container, gameObject.scene, "Missing card template binding"));
                continue;
            }
            if (unique.TryGetValue(value, out string other))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, container, gameObject.scene, "Duplicate mapping with " + other));
            else unique.Add(value, fields[i]);
            if (value.gameObject.scene != gameObject.scene)
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, container, gameObject.scene, "Scene ownership mismatch (cross-scene reference)"));
            else if (i < 3 ? value.transform != transform : container == null || value.transform == container || !value.transform.IsChildOf(container))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, container, gameObject.scene,
                    i < 3 ? "Ancestry mismatch (expected the card root itself)" : "Ancestry mismatch (expected a descendant)"));
            if (i >= 5 && icon != null && value.transform.IsChildOf(icon.transform))
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, transform, gameObject.scene,
                    "Role ancestry mismatch: Name/Summary cannot belong to the separate Icon container " + SettlementSectorTechnologyPanelUI.BindingLocation(icon.transform)));
            if (value is TMP_Text text && text.font == null)
                errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic(property, value, container, gameObject.scene, "Missing TMP font"));
        }
        if (cardButton != null && (cardButton.transform != transform || cardButton.targetGraphic != cardImage))
            errors.Add("Card template button/root/targetGraphic mapping is invalid.");
    }
}
