using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UISoundButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private string clickSoundEventId = SoundEventIds.UiClick;
    [SerializeField] private string hoverSoundEventId = SoundEventIds.UiHover;
    [SerializeField] private string disabledClickSoundEventId = SoundEventIds.UiDisabled;

    [SerializeField] private bool playClick = true;
    [SerializeField] private bool playHover = true;
    [SerializeField] private bool playDisabledClick = true;

    private Button button;

    private void Awake()
    {
        CacheButton();
    }

    private void OnEnable()
    {
        CacheButton();

        if (button != null)
        {
            button.onClick.AddListener(PlayClick);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClick);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playHover)
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

    public void OnPointerClick(PointerEventData eventData)
    {
        CacheButton();

        if (!playDisabledClick)
        {
            return;
        }

        if (button != null && !button.interactable)
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
        clickSoundEventId = eventId;
    }

    public void SetHoverSoundEventId(string eventId)
    {
        hoverSoundEventId = eventId;
    }

    public void SetDisabledClickSoundEventId(string eventId)
    {
        disabledClickSoundEventId = eventId;
    }

    private void PlayClick()
    {
        if (!playClick)
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
}
