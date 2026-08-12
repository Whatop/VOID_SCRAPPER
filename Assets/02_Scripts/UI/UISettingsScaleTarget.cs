using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class UISettingsScaleTarget : MonoBehaviour
{
    [SerializeField] private RectTransform scaleRoot;
    [SerializeField] private TMP_Text[] textTargets;

    private Vector3 baseScale = Vector3.one;
    private float[] baseFontSizes;
    private bool captured;

    private void Awake()
    {
        CaptureBaseValues();
    }

    private void OnEnable()
    {
        CaptureBaseValues();
        GameSettingsRuntime.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettingsRuntime.Changed -= Apply;
    }

    public void Apply()
    {
        CaptureBaseValues();

        if (scaleRoot != null)
        {
            scaleRoot.localScale = baseScale * GameSettingsRuntime.UiScale;
        }

        if (textTargets == null || baseFontSizes == null)
        {
            return;
        }

        for (int i = 0; i < textTargets.Length && i < baseFontSizes.Length; i++)
        {
            if (textTargets[i] != null)
            {
                textTargets[i].fontSize = baseFontSizes[i] * GameSettingsRuntime.TextScale;
            }
        }
    }

    private void CaptureBaseValues()
    {
        if (captured)
        {
            return;
        }

        captured = true;

        if (scaleRoot == null)
        {
            scaleRoot = transform as RectTransform;
        }

        if (scaleRoot != null)
        {
            baseScale = scaleRoot.localScale;
        }

        if (textTargets == null || textTargets.Length == 0)
        {
            textTargets = GetComponentsInChildren<TMP_Text>(true);
        }

        baseFontSizes = new float[textTargets != null ? textTargets.Length : 0];

        for (int i = 0; i < baseFontSizes.Length; i++)
        {
            baseFontSizes[i] = textTargets[i] != null ? textTargets[i].fontSize : 0f;
        }
    }
}
