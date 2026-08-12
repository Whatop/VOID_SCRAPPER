using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReinforcementSlotUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("State Roots")]
    [Tooltip("장비가 장착되어 있을 때 켜지는 루트. 없으면 무시합니다.")]
    [SerializeField] private GameObject equippedRoot;

    [Tooltip("장비가 없을 때 켜지는 루트. 텍스트 없이 빈 슬롯 프레임만 둘 때 사용합니다.")]
    [SerializeField] private GameObject emptyRoot;

    [Tooltip("사용 가능 상태 연출 루트. 선택 사항입니다.")]
    [SerializeField] private GameObject readyRoot;

    [Tooltip("충전 중 상태 연출 루트. 선택 사항입니다.")]
    [SerializeField] private GameObject rechargingRoot;

    [Tooltip("사용 불가 상태 연출 루트. 선택 사항입니다.")]
    [SerializeField] private GameObject unavailableRoot;

    [Header("Icon")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite defaultIcon;

    [Tooltip("사용 가능할 때 아이콘 색. 보통 흰색으로 둡니다.")]
    [SerializeField] private Color readyIconColor = Color.white;

    [Tooltip("사용 불가/충전 중일 때 아이콘 색. Enter the Gungeon식 회색 비활성 처리용.")]
    [SerializeField] private Color disabledIconColor = new Color(0.42f, 0.42f, 0.42f, 0.78f);

    [Tooltip("장비가 없을 때 아이콘 색. 기본 아이콘을 비워두면 아이콘 자체가 꺼집니다.")]
    [SerializeField] private Color emptyIconColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);

    [Header("Icon Recharge Fill")]
    [Tooltip("아이콘과 같은 Sprite를 사용하는 Filled Image입니다. Fill Method는 Inspector에서 Radial 360 또는 Vertical로 설정합니다.")]
    [SerializeField] private Image iconRechargeFillImage;
    [SerializeField] private bool useIconFillAsPrimaryCooldownVisual = true;
    [SerializeField] private bool syncRechargeFillSpriteToIcon = true;

    [Header("Icon Text")]
    [SerializeField] private TextMeshProUGUI chargeText;
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private string chargeTextFormat = "{0}/{1}";
    [SerializeField] private bool hideChargeTextWhenSingleCharge = true;

    [Header("Disabled Overlay")]
    [Tooltip("아이콘 위에 덮는 회색/검정 반투명 오버레이. 없으면 아이콘 tint만 사용합니다.")]
    [SerializeField] private Image disabledOverlayImage;

    [SerializeField] private Color disabledOverlayColor = new Color(0f, 0f, 0f, 0.42f);

    [Header("Gauge - Non OneShot Only")]
    [Tooltip("1회용이 아닌 Reinforcement일 때만 켜지는 게이지 루트. 아이콘 오른쪽에 배치합니다.")]
    [SerializeField] private GameObject rechargeGaugeRoot;

    [Tooltip("Unity Slider로 만든 게이지. 방향은 Slider 컴포넌트에서 정합니다.")]
    [SerializeField] private Slider rechargeSlider;

    [Tooltip("Image Type Filled를 쓰는 경우 연결합니다. Slider가 있으면 둘 다 갱신됩니다.")]
    [SerializeField] private Image rechargeFillImage;

    [Tooltip("기존 GaugeBarUI를 재사용할 경우 연결합니다.")]
    [SerializeField] private GaugeBarUI rechargeGauge;

    [Header("Gauge Visual")]
    [SerializeField] private Color gaugeReadyColor = Color.white;
    [SerializeField] private Color gaugeChargingColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color gaugeDisabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Behaviour")]
    [Tooltip("true면 1회용 장비는 게이지를 숨깁니다. 요구사항 기준 기본 true.")]
    [SerializeField] private bool hideGaugeForOneShot = true;

    [Tooltip("true면 장비가 사용 가능해도 max charge가 아니면 다음 충전 진행도를 계속 보여줍니다.")]
    [SerializeField] private bool showPartialRechargeWhileUsable = true;

    private ReinforcementDefinition currentDefinition;

    private void Reset()
    {
        root = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
        iconImage = GetComponentInChildren<Image>(true);
        rechargeSlider = GetComponentInChildren<Slider>(true);
        rechargeGauge = GetComponentInChildren<GaugeBarUI>(true);

        if (rechargeSlider != null)
        {
            rechargeGaugeRoot = rechargeSlider.gameObject;

            if (rechargeSlider.fillRect != null)
            {
                rechargeFillImage = rechargeSlider.fillRect.GetComponent<Image>();
            }
        }
    }

    private void Awake()
    {
        CacheReferences();
        SetEmpty();
    }

    public void SetVisible(bool visible)
    {
        CacheReferences();

        if (root != null)
        {
            root.SetActive(visible);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void SetEmpty()
    {
        currentDefinition = null;

        SetActive(equippedRoot, false);
        SetActive(emptyRoot, true);
        SetActive(readyRoot, false);
        SetActive(rechargingRoot, false);
        SetActive(unavailableRoot, false);

        SetIcon(defaultIcon, emptyIconColor);
        SetDisabledOverlay(false);
        SetGaugeVisible(false);
        SetGaugeRatio(0f, false, false);
        SetChargeText(0, 0);
    }

    public void SetState(
        ReinforcementDefinition definition,
        int currentCharges,
        int maxCharges,
        float rechargeRatio,
        float totalChargeRatio,
        bool isRecharging,
        bool canUse)
    {
        if (definition == null)
        {
            SetEmpty();
            return;
        }

        currentDefinition = definition;

        currentCharges = Mathf.Max(0, currentCharges);
        maxCharges = Mathf.Max(0, maxCharges);
        rechargeRatio = Mathf.Clamp01(rechargeRatio);
        totalChargeRatio = Mathf.Clamp01(totalChargeRatio);

        bool isOneShot = definition.UseType == ReinforcementUseType.LegacyOneShotConsumable;
        bool shouldShowGauge = !isOneShot || !hideGaugeForOneShot;
        bool hasAnyCharge = currentCharges > 0;
        bool hasEffects = definition.Effects != null && definition.Effects.Count > 0;

        canUse = canUse && hasEffects;
        bool unavailable = !canUse;

        SetActive(equippedRoot, true);
        SetActive(emptyRoot, false);
        SetActive(readyRoot, canUse);
        SetActive(rechargingRoot, isRecharging && !canUse);
        SetActive(unavailableRoot, unavailable);

        Sprite icon = definition.Icon != null ? definition.Icon : defaultIcon;

        SetIcon(icon, canUse ? readyIconColor : disabledIconColor);
        SetDisabledOverlay(unavailable);

        SetGaugeVisible(shouldShowGauge);

        if (shouldShowGauge)
        {
            float gaugeRatio = ResolveGaugeRatio(
                currentCharges,
                maxCharges,
                rechargeRatio,
                totalChargeRatio,
                canUse
            );

            SetGaugeRatio(
                gaugeRatio,
                canUse,
                isRecharging || (!hasAnyCharge && definition.UsesRecharge)
            );
        }

        SetChargeText(currentCharges, maxCharges);
    }

    public void RefreshFrom(PlayerReinforcementController controller)
    {
        if (controller == null || !controller.HasEquipment)
        {
            SetEmpty();
            return;
        }

        ReinforcementDefinition definition = controller.EquippedDefinition;

        float totalChargeRatio = CalculateTotalChargeRatio(
            definition,
            controller.CurrentCharges,
            controller.MaxCharges,
            controller.RechargeRatio
        );

        SetState(
            definition,
            controller.CurrentCharges,
            controller.MaxCharges,
            controller.RechargeRatio,
            totalChargeRatio,
            controller.IsRecharging,
            controller.CanUseCurrent()
        );
    }

    private float CalculateTotalChargeRatio(
        ReinforcementDefinition definition,
        int currentCharges,
        int maxCharges,
        float rechargeRatio)
    {
        if (definition == null)
        {
            return 0f;
        }

        if (definition.UseType == ReinforcementUseType.LegacyOneShotConsumable)
        {
            return currentCharges > 0 ? 1f : 0f;
        }

        maxCharges = Mathf.Max(1, maxCharges);

        if (currentCharges >= maxCharges)
        {
            return 1f;
        }

        if (!definition.UsesRecharge)
        {
            return Mathf.Clamp01(currentCharges / (float)maxCharges);
        }

        return Mathf.Clamp01((Mathf.Max(0, currentCharges) + Mathf.Clamp01(rechargeRatio)) / maxCharges);
    }

    private float ResolveGaugeRatio(
        int currentCharges,
        int maxCharges,
        float rechargeRatio,
        float totalChargeRatio,
        bool canUse)
    {
        if (maxCharges <= 0)
        {
            return 0f;
        }

        if (showPartialRechargeWhileUsable)
        {
            return totalChargeRatio;
        }

        if (canUse)
        {
            return 1f;
        }

        return rechargeRatio;
    }

    private void CacheReferences()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (rechargeSlider == null && rechargeGaugeRoot != null)
        {
            rechargeSlider = rechargeGaugeRoot.GetComponentInChildren<Slider>(true);
        }

        if (rechargeGauge == null && rechargeGaugeRoot != null)
        {
            rechargeGauge = rechargeGaugeRoot.GetComponentInChildren<GaugeBarUI>(true);
        }

        if (rechargeFillImage == null && rechargeSlider != null && rechargeSlider.fillRect != null)
        {
            rechargeFillImage = rechargeSlider.fillRect.GetComponent<Image>();
        }

        if (iconRechargeFillImage != null)
        {
            iconRechargeFillImage.raycastTarget = false;
            iconRechargeFillImage.preserveAspect = true;
        }
    }

    private void SetIcon(Sprite icon, Color color)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.color = color;
        iconImage.preserveAspect = true;

        if (iconRechargeFillImage != null && syncRechargeFillSpriteToIcon)
        {
            iconRechargeFillImage.sprite = icon;
        }
    }

    private void SetDisabledOverlay(bool visible)
    {
        if (disabledOverlayImage == null)
        {
            return;
        }

        disabledOverlayImage.enabled = visible;
        disabledOverlayImage.color = disabledOverlayColor;
    }

    private void SetGaugeVisible(bool visible)
    {
        bool showLegacyGauge = visible && !useIconFillAsPrimaryCooldownVisual;

        if (rechargeGaugeRoot != null && rechargeGaugeRoot.activeSelf != showLegacyGauge)
        {
            rechargeGaugeRoot.SetActive(showLegacyGauge);
        }

        if (rechargeSlider != null && rechargeSlider.gameObject != rechargeGaugeRoot)
        {
            rechargeSlider.gameObject.SetActive(showLegacyGauge);
        }

        if (rechargeGauge != null)
        {
            rechargeGauge.SetVisible(showLegacyGauge);
        }

        if (rechargeFillImage != null)
        {
            rechargeFillImage.enabled = showLegacyGauge;
        }

        if (iconRechargeFillImage != null)
        {
            iconRechargeFillImage.enabled = visible;
        }
    }

    private void SetGaugeRatio(float ratio, bool canUse, bool isCharging)
    {
        ratio = Mathf.Clamp01(ratio);

        if (rechargeSlider != null)
        {
            rechargeSlider.minValue = 0f;
            rechargeSlider.maxValue = 1f;
            rechargeSlider.value = ratio;
            rechargeSlider.interactable = false;
        }

        Color gaugeColor = canUse
            ? gaugeReadyColor
            : (isCharging ? gaugeChargingColor : gaugeDisabledColor);

        if (rechargeFillImage != null)
        {
            rechargeFillImage.fillAmount = ratio;
            rechargeFillImage.color = gaugeColor;
        }

        if (iconRechargeFillImage != null)
        {
            iconRechargeFillImage.fillAmount = ratio;
            iconRechargeFillImage.color = gaugeColor;

            if (syncRechargeFillSpriteToIcon && iconImage != null)
            {
                iconRechargeFillImage.sprite = iconImage.sprite;
            }
        }

        if (rechargeGauge != null)
        {
            rechargeGauge.SetRatio(ratio);
            rechargeGauge.SetText(string.Empty);
        }
    }

    public void SetKeyLabel(string label)
    {
        if (keyText != null)
        {
            keyText.text = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        }
    }

    private void SetChargeText(int currentCharges, int maxCharges)
    {
        if (chargeText == null)
        {
            return;
        }

        bool visible = maxCharges > 0 && (!hideChargeTextWhenSingleCharge || maxCharges > 1);
        chargeText.gameObject.SetActive(visible);

        if (visible)
        {
            chargeText.text = string.Format(
                chargeTextFormat,
                Mathf.Max(0, currentCharges),
                Mathf.Max(1, maxCharges)
            );
        }
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}