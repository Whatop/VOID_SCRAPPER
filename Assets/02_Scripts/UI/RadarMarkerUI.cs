using UnityEngine;
using UnityEngine.UI;

public class RadarMarkerUI : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image iconImage;

    public RectTransform RectTransform => rectTransform;

    private void Reset()
    {
        rectTransform = transform as RectTransform;
        iconImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
    }

    public void SetPosition(Vector2 anchoredPosition)
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    public void SetVisual(Sprite sprite, Color color, float scale)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = color;
            iconImage.enabled = sprite != null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }
    }
}
