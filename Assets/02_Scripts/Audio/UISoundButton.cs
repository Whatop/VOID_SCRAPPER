using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UISoundButton :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler,
    IPointerDownHandler,
    IPointerClickHandler
{
    [SerializeField] private string clickSoundEventId = SoundEventIds.UiClick;
    [SerializeField] private string hoverSoundEventId = SoundEventIds.UiHover;
    [SerializeField] private string disabledClickSoundEventId = SoundEventIds.UiDisabled;

    [SerializeField] private bool playClick = true;
    [SerializeField] private bool playHover = true;
    [SerializeField] private bool playSelectionHover;
    [SerializeField] private bool playDisabledClick = true;

    private Button button;
    private bool listenerBound;
    private bool isPrimaryInstance = true;

    private bool pointerDownStateCaptured;
    private bool wasInteractableOnPointerDown = true;
    private bool pointerInside;
    private bool selectionHovered;
    private int lastHoverFrame = -1;

    private void Awake()
    {
        ResolvePrimaryInstance();
        CacheButton();
    }

    private void OnEnable()
    {
        ResolvePrimaryInstance();

        if (!isPrimaryInstance)
        {
            return;
        }

        CacheButton();

        if (button != null && !listenerBound)
        {
            button.onClick.AddListener(PlayClick);
            listenerBound = true;
        }
    }

    private void OnDisable()
    {
        if (button != null && listenerBound)
        {
            button.onClick.RemoveListener(PlayClick);
            listenerBound = false;
        }

        pointerDownStateCaptured = false;
        wasInteractableOnPointerDown = true;
        pointerInside = selectionHovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (playSelectionHover)
        {
            if (!pointerInside) PlayFocusHover();
            pointerInside = true;
            return;
        }
        if (!isPrimaryInstance || !playHover)
        {
            return;
        }

        CacheButton();

        if (button != null && !button.interactable)
        {
            return;
        }

        AudioManager.Play(hoverSoundEventId);
    }

    public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

    public void OnSelect(BaseEventData eventData)
    {
        if (!playSelectionHover || selectionHovered) return;
        selectionHovered = true;
        if (!pointerInside) PlayFocusHover();
    }

    public void OnDeselect(BaseEventData eventData) => selectionHovered = false;

    private void PlayFocusHover()
    {
        CacheButton();
        if (!isActiveAndEnabled || !isPrimaryInstance || !playHover || button == null ||
            !button.IsActive() || !button.IsInteractable() || lastHoverFrame == Time.frameCount) return;
        lastHoverFrame = Time.frameCount;
        AudioManager.Play(hoverSoundEventId);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isPrimaryInstance)
        {
            return;
        }

        CacheButton();

        pointerDownStateCaptured = true;
        wasInteractableOnPointerDown =
            button == null || button.interactable;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isPrimaryInstance || !playDisabledClick)
        {
            pointerDownStateCaptured = false;
            return;
        }

        CacheButton();

        bool wasInteractable = pointerDownStateCaptured
            ? wasInteractableOnPointerDown
            : button == null || button.interactable;

        pointerDownStateCaptured = false;

        if (!wasInteractable)
        {
            AudioManager.Play(disabledClickSoundEventId);
        }
    }

    public void SetClickSoundEnabled(bool value)
    {
        playClick = value;
    }

    public void SetHoverSoundEnabled(bool value)
    {
        playHover = value;
    }

    public void SetDisabledClickSoundEnabled(bool value)
    {
        playDisabledClick = value;
    }

    public void SetClickSoundEventId(string eventId)
    {
        clickSoundEventId = SoundEventIds.ToNumbered(eventId);
    }

    public void SetHoverSoundEventId(string eventId)
    {
        hoverSoundEventId = SoundEventIds.ToNumbered(eventId);
    }

    public void SetDisabledClickSoundEventId(string eventId)
    {
        disabledClickSoundEventId = SoundEventIds.ToNumbered(eventId);
    }

    private void PlayClick()
    {
        if (!isPrimaryInstance || !playClick)
        {
            return;
        }

        CacheButton();

        if (button != null && !button.interactable)
        {
            return;
        }

        AudioManager.Play(clickSoundEventId);
    }

    private void CacheButton()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void ResolvePrimaryInstance()
    {
        UISoundButton[] components = GetComponents<UISoundButton>();

        isPrimaryInstance =
            components == null ||
            components.Length == 0 ||
            components[0] == this;

        if (!isPrimaryInstance && listenerBound && button != null)
        {
            button.onClick.RemoveListener(PlayClick);
            listenerBound = false;
        }
    }
}
