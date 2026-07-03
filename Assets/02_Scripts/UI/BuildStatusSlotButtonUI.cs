using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BuildStatusSlotButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private Sprite fallbackIcon;

    private Action clicked;

    private void Reset()
    {
        button = GetComponent<Button>();
        iconImage = GetComponentInChildren<Image>(true);
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Bind(Sprite icon, Action onClicked)
    {
        clicked = onClicked;
        SetIcon(icon != null ? icon : fallbackIcon);
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedRoot != null)
        {
            selectedRoot.SetActive(selected);
        }
    }

    private void SetIcon(Sprite icon)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
    }

    private void HandleClicked()
    {
        clicked?.Invoke();
    }
}
