using Michsky.UI.MTP;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MotionTitleView : MonoBehaviour
{
    [Header("Motion Titles Pack")]
    [SerializeField] private StyleManager styleManager;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Fallback")]
    [SerializeField] private bool autoFindTextIfMissing = true;
    [SerializeField] private bool hideEmptyTextObjects = true;

    public StyleManager StyleManager => styleManager;

    private void Awake()
    {
        CacheReferences();
    }

    public void Setup(
        string title,
        string subtitle,
        float showFor,
        float animationSpeed,
        bool useUnscaledTime)
    {
        CacheReferences();
        SetText(title, subtitle);

        if (styleManager == null)
        {
            return;
        }

        styleManager.loopAnimations = false;
        styleManager.playOnEnable = false;
        styleManager.AnimationSpeed = Mathf.Max(0.01f, animationSpeed);
        styleManager.showFor = Mathf.Max(0f, showFor);
        styleManager.playOutAnimation = false;
        styleManager.disableOnOut = false;
        styleManager.UseUnscaledTime = useUnscaledTime;
    }

    public void SetText(string title, string subtitle)
    {
        if (titleText != null)
        {
            titleText.text = title;

            if (hideEmptyTextObjects)
            {
                titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(title));
            }
        }

        if (subtitleText != null)
        {
            subtitleText.text = subtitle;

            if (hideEmptyTextObjects)
            {
                subtitleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            }
        }
    }

    public void Play()
    {
        CacheReferences();

        if (styleManager != null)
        {
            styleManager.Play();
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void PlayIn()
    {
        CacheReferences();

        if (styleManager != null)
        {
            styleManager.PlayIn();
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    public void PlayOut()
    {
        CacheReferences();

        if (styleManager != null)
        {
            styleManager.PlayOut();
        }
    }

    private void CacheReferences()
    {
        if (styleManager == null)
        {
            styleManager = GetComponent<StyleManager>();
        }

        if (autoFindTextIfMissing && (titleText == null || subtitleText == null))
        {
            AutoFindTexts();
        }
    }

    private void AutoFindTexts()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);

        if (texts == null || texts.Length == 0)
        {
            return;
        }

        if (titleText == null)
        {
            titleText = texts[0];
        }

        if (subtitleText == null && texts.Length >= 2)
        {
            subtitleText = texts[1];
        }
    }
}