using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuButtonPresenter : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
    private const float TransitionDuration = 0.12f;

    private RectTransform targetRect;
    private Image background;
    private TextMeshProUGUI label;
    private Image accentStrip;
    private Vector2 restPosition;
    private Color restBackgroundColor;
    private Color restLabelColor;
    private Color restAccentColor;
    private Color activeAccentColor;
    private bool pointerInside;
    private bool selected;
    private bool configured;

    public void Configure(
        RectTransform rect,
        Image backgroundImage,
        TextMeshProUGUI labelText,
        Image strip,
        Color accentColor,
        Color labelColor)
    {
        targetRect = rect;
        background = backgroundImage;
        label = labelText;
        accentStrip = strip;
        restPosition = targetRect != null ? targetRect.anchoredPosition : Vector2.zero;
        restBackgroundColor = background != null ? background.color : Color.white;
        restLabelColor = label != null ? label.color : labelColor;
        restAccentColor = accentStrip != null
            ? accentStrip.color
            : new Color(accentColor.r, accentColor.g, accentColor.b, 0.48f);
        activeAccentColor = new Color(
            restAccentColor.r,
            restAccentColor.g,
            restAccentColor.b,
            1f);
        configured = true;
        ApplyVisual(false, true);
    }

    private void OnDisable()
    {
        pointerInside = false;
        selected = false;
        KillTweens();
        ApplyVisual(false, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        EventSystem.current?.SetSelectedGameObject(gameObject);
        ApplyVisual(true, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        ApplyVisual(selected, false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        ApplyVisual(true, false);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        ApplyVisual(pointerInside, false);
    }

    private void ApplyVisual(bool active, bool immediate)
    {
        if (!configured)
        {
            return;
        }

        KillTweens();
        Vector2 targetPosition = restPosition + (active ? Vector2.right * 4f : Vector2.zero);
        Color targetBackground = active
            ? new Color(0.11f, 0.2f, 0.25f, 0.94f)
            : restBackgroundColor;
        Color targetLabel = active ? Color.white : restLabelColor;
        float targetStripAlpha = active ? 1f : restAccentColor.a;

        if (immediate)
        {
            if (targetRect != null)
            {
                targetRect.anchoredPosition = targetPosition;
            }

            if (background != null)
            {
                background.color = targetBackground;
            }

            if (label != null)
            {
                label.color = targetLabel;
            }

            SetStripAlpha(targetStripAlpha);
            return;
        }

        if (targetRect != null)
        {
            targetRect.DOAnchorPos(targetPosition, TransitionDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        if (background != null)
        {
            background.DOColor(targetBackground, TransitionDuration).SetUpdate(true);
        }

        if (label != null)
        {
            label.DOColor(targetLabel, TransitionDuration).SetUpdate(true);
        }

        if (accentStrip != null)
        {
            Color stripColor = activeAccentColor;
            stripColor.a = targetStripAlpha;
            accentStrip.DOColor(stripColor, TransitionDuration).SetUpdate(true);
        }
    }

    private void KillTweens()
    {
        targetRect?.DOKill(false);
        background?.DOKill(false);
        label?.DOKill(false);
        accentStrip?.DOKill(false);
    }

    private void SetStripAlpha(float alpha)
    {
        if (accentStrip == null)
        {
            return;
        }

        Color color = activeAccentColor;
        color.a = alpha;
        accentStrip.color = color;
    }
}
