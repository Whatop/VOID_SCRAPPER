using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WeaponHeatUI : MonoBehaviour
{
    [SerializeField] private GameObject rootObject;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private GaugeBarUI heatGauge;
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private bool hideAtZeroHeat = true;
    [SerializeField] private Color normalColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.2f, 1f);
    [SerializeField] private Color overheatColor = new Color(1f, 0.2f, 0.12f, 1f);
    [SerializeField, Range(0f, 1f)] private float warningStartRatio = 0.65f;
    [SerializeField, Range(0f, 1f)] private float dangerStartRatio = 0.85f;
    [SerializeField, Min(0f)] private float zeroHeatHoldDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float zeroHeatFadeDuration = 0.2f;

    private MachineGunWeapon machineGunWeapon;
    private bool externalVisible = true;
    private bool presentationVisible;
    private Sequence visibilitySequence;

    public void ConfigureRuntime(
        GameObject presentationRoot,
        CanvasGroup presentationCanvasGroup,
        PlayerWeaponController controller,
        Image presentationFill,
        TextMeshProUGUI presentationText)
    {
        rootObject = presentationRoot != null ? presentationRoot : gameObject;
        canvasGroup = presentationCanvasGroup;
        weaponController = controller;
        fillImage = presentationFill;
        stateText = presentationText;
        heatGauge ??= rootObject.GetComponent<GaugeBarUI>();

        if (fillImage != null)
        {
            bool usesSlider = rootObject.GetComponent<Slider>() != null;
            fillImage.type = usesSlider ? Image.Type.Simple : Image.Type.Filled;
            if (!usesSlider)
            {
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImage.fillClockwise = true;
            }

            fillImage.raycastTarget = false;
        }

        if (stateText != null)
        {
            stateText.text = string.Empty;
            stateText.raycastTarget = false;
            stateText.gameObject.SetActive(false);
        }

        ResolveController();
        Refresh();
    }

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (canvasGroup == null && rootObject == gameObject)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        ResolveController();
    }

    private void OnEnable()
    {
        ResolveController();

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
            HandleWeaponEquipped(weaponController.CurrentWeaponTree, weaponController.CurrentWeapon);
        }
        else
        {
            SetVisibleInternal(false, true);
        }
    }

    private void OnDisable()
    {
        KillVisibilityTween();

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }

        BindMachineGun(null);
    }

    public void SetExternalVisible(bool visible)
    {
        externalVisible = visible;
        if (!externalVisible)
        {
            SetVisibleInternal(false, true);
            return;
        }

        Refresh();
    }

    private void ResolveController()
    {
        if (weaponController == null)
        {
            weaponController = FindFirstObjectByType<PlayerWeaponController>();
        }
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTree, PlayerWeaponBase weapon)
    {
        BindMachineGun(weaponTree == WeaponTreeType.MachineGun ? weapon as MachineGunWeapon : null);
        Refresh();
    }

    private void BindMachineGun(MachineGunWeapon weapon)
    {
        if (machineGunWeapon == weapon)
        {
            return;
        }

        if (machineGunWeapon != null)
        {
            machineGunWeapon.HeatChanged -= HandleHeatChanged;
        }

        machineGunWeapon = weapon;
        if (machineGunWeapon != null)
        {
            machineGunWeapon.HeatChanged += HandleHeatChanged;
        }
    }

    private void HandleHeatChanged(float current, float max, bool overheated)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (machineGunWeapon == null || !machineGunWeapon.UsesHeatSystem)
        {
            SetVisibleInternal(false, true);
            return;
        }

        float heatRatio = machineGunWeapon.HeatRatio;
        bool overheated = machineGunWeapon.IsOverheated;
        bool visible = externalVisible && (!hideAtZeroHeat || heatRatio > 0.001f || overheated);
        SetVisibleInternal(visible, false);

        heatGauge?.SetRatio(heatRatio);
        heatGauge?.SetText(string.Empty);

        Color color = ResolveHeatColor(heatRatio, overheated);
        heatGauge?.SetFillColor(color);

        if (fillImage != null)
        {
            fillImage.fillAmount = heatRatio;
            fillImage.color = color;
        }

        if (stateText != null)
        {
            stateText.text = string.Empty;
            stateText.gameObject.SetActive(false);
        }
    }

    private Color ResolveHeatColor(float heatRatio, bool overheated)
    {
        if (overheated)
        {
            return overheatColor;
        }

        float warningStart = Mathf.Clamp01(warningStartRatio);
        float dangerStart = Mathf.Clamp(dangerStartRatio, warningStart, 1f);
        if (heatRatio >= dangerStart)
        {
            float dangerT = Mathf.InverseLerp(dangerStart, 1f, heatRatio);
            return Color.Lerp(warningColor, overheatColor, dangerT);
        }

        if (heatRatio >= warningStart)
        {
            float warningT = Mathf.InverseLerp(warningStart, dangerStart, heatRatio);
            return Color.Lerp(normalColor, warningColor, warningT);
        }

        return normalColor;
    }

    private void SetVisibleInternal(bool visible, bool immediate)
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (rootObject == gameObject)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            if (visible)
            {
                KillVisibilityTween();
                canvasGroup.alpha = 1f;
                presentationVisible = true;
                return;
            }

            if (immediate || !presentationVisible)
            {
                KillVisibilityTween();
                canvasGroup.alpha = 0f;
                presentationVisible = false;
                return;
            }

            if (visibilitySequence != null)
            {
                return;
            }

            visibilitySequence = DOTween.Sequence().SetUpdate(true);
            visibilitySequence.AppendInterval(Mathf.Max(0f, zeroHeatHoldDuration));
            visibilitySequence.Append(
                canvasGroup.DOFade(0f, Mathf.Max(0.05f, zeroHeatFadeDuration))
                    .SetEase(Ease.OutQuad)
            );
            visibilitySequence.OnComplete(() =>
            {
                presentationVisible = false;
                visibilitySequence = null;
            });
            return;
        }

        KillVisibilityTween();
        if (rootObject.activeSelf != visible)
        {
            rootObject.SetActive(visible);
        }

        presentationVisible = visible;
    }

    private void KillVisibilityTween()
    {
        visibilitySequence?.Kill();
        visibilitySequence = null;
        canvasGroup?.DOKill();
    }
}
