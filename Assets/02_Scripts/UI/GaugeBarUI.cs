using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GaugeBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject rootObject;

    [Header("Display")]
    [SerializeField] private bool showValueText = true;
    [SerializeField] private string valueFormat = "{0:0}/{1:0}";
    [SerializeField] private string ratioFormat = "{0:0}%";

    public float Ratio { get; private set; }

    private void Reset()
    {
        slider = GetComponent<Slider>();
        fillImage = FindFillImage();
        valueText = GetComponentInChildren<TextMeshProUGUI>(true);
        canvasGroup = GetComponent<CanvasGroup>();
        rootObject = gameObject;
    }

    private void Awake()
    {
        CacheReferences();
        SetRatio(Ratio);
    }

    public void SetValue(float current, float max)
    {
        float ratio = max <= 0f ? 0f : current / max;
        SetRatio(ratio);

        if (valueText != null && showValueText)
        {
            valueText.text = string.Format(valueFormat, current, max);
        }
    }

    public void SetRatio(float ratio)
    {
        Ratio = Mathf.Clamp01(ratio);

        if (fillImage != null)
        {
            fillImage.fillAmount = Ratio;
        }

        if (slider != null)
        {
            slider.value = Ratio;
        }

        if (valueText != null && showValueText)
        {
            valueText.text = string.Format(ratioFormat, Ratio * 100f);
        }
    }

    public void SetText(string text)
    {
        if (valueText != null)
        {
            valueText.text = text;
        }
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void CacheReferences()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (valueText == null)
        {
            valueText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (fillImage == null)
        {
            fillImage = FindFillImage();
        }
    }

    private Image FindFillImage()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (slider != null && slider.fillRect != null)
        {
            Image sliderFillImage = slider.fillRect.GetComponent<Image>();
            if (sliderFillImage != null)
            {
                return sliderFillImage;
            }
        }

        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image == null)
            {
                continue;
            }

            if (image.name.ToLower().Contains("fill"))
            {
                return image;
            }
        }

        return images != null && images.Length > 0 ? images[0] : null;
    }
}