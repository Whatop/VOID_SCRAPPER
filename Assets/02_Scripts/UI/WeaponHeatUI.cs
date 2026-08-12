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

    private MachineGunWeapon machineGunWeapon;
    private bool externalVisible = true;

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
            SetVisibleInternal(false);
        }
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }

        BindMachineGun(null);
    }

    public void SetExternalVisible(bool visible)
    {
        externalVisible = visible;
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
            SetVisibleInternal(false);
            return;
        }

        float ratio = machineGunWeapon.HeatRatio;
        bool visible = externalVisible && (!hideAtZeroHeat || ratio > 0.001f || machineGunWeapon.IsOverheated);
        SetVisibleInternal(visible);

        heatGauge?.SetRatio(ratio);
        heatGauge?.SetText(machineGunWeapon.IsOverheated ? "OVERHEAT" : $"HEAT {ratio * 100f:0}%");

        Color color = machineGunWeapon.IsOverheated
            ? overheatColor
            : ratio >= 0.75f ? warningColor : normalColor;

        heatGauge?.SetFillColor(color);

        if (fillImage != null)
        {
            fillImage.fillAmount = ratio;
            fillImage.color = color;
        }

        if (stateText != null)
        {
            stateText.text = machineGunWeapon.IsOverheated ? "OVERHEAT" : string.Empty;
            stateText.color = color;
        }
    }

    private void SetVisibleInternal(bool visible)
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        // 이 컴포넌트가 붙은 오브젝트 자체를 끄면 HeatChanged 구독도 끊겨
        // 이후 열이 올라가도 다시 표시할 수 없습니다. 같은 오브젝트를 루트로 쓸 때는
        // CanvasGroup으로만 숨기고 컴포넌트는 활성 상태를 유지합니다.
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

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (rootObject.activeSelf != visible)
        {
            rootObject.SetActive(visible);
        }
    }
}
