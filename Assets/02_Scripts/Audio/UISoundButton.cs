using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UISoundButton : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private string clickSoundEventId = SoundEventIds.UiClick;
    [SerializeField] private string hoverSoundEventId = SoundEventIds.UiHover;
    [SerializeField] private bool playHover = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

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

        if (button != null && !button.interactable)
        {
            return;
        }

        AudioManager.Play(hoverSoundEventId);
    }

    private void PlayClick()
    {
        AudioManager.Play(clickSoundEventId);
    }
}
