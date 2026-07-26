using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerWorldChargeGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fillImage;

    [Header("Follow")]
    [SerializeField] private bool followTarget = true;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Option")]
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool autoAddCanvasGroup = true;
    [SerializeField] private bool autoConfigureFillImage = true;
    [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
    [SerializeField] private int fillOrigin;

    private PlayerWeaponBase currentWeapon;
    private bool externalChargeActive;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        fillImage = GetComponentInChildren<Image>();
        playerTarget = transform.root;
        weaponController = GetComponentInParent<PlayerWeaponController>();
    }

    private void Awake()
    {
        CacheReferences();

        if (hideOnStart)
        {
            Hide();
        }
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeWeaponController();

        if (weaponController != null)
        {
            AttachWeapon(weaponController.CurrentWeapon);
        }

        if (!externalChargeActive && currentWeapon != null && currentWeapon.IsCharging)
        {
            Show(currentWeapon.ChargeRatio);
        }
    }

    private void OnDisable()
    {
        UnsubscribeWeaponController();
        DetachWeapon();
    }

    private void LateUpdate()
    {
        if (!followTarget || playerTarget == null)
        {
            return;
        }

        transform.position = playerTarget.position + worldOffset;
        transform.rotation = Quaternion.identity;
    }

    public void BeginExternalCharge()
    {
        externalChargeActive = true;
        Show(0f);
    }

    public void SetExternalChargeRatio(float ratio)
    {
        externalChargeActive = true;
        Show(ratio);
    }

    public void EndExternalCharge(bool hide)
    {
        externalChargeActive = false;

        if (hide)
        {
            Hide();
        }
    }

    public void Show(float ratio)
    {
        SetRatio(ratio);
        SetVisible(true);
    }

    public void Hide()
    {
        SetRatio(0f);
        SetVisible(false);
    }

    private void CacheReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && autoAddCanvasGroup)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (fillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Image candidate = images[i];

                if (candidate != null &&
                    candidate.name.IndexOf("fill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    fillImage = candidate;
                    break;
                }
            }

            if (fillImage == null && images.Length > 0)
            {
                fillImage = images[0];
            }
        }

        if (fillImage != null && autoConfigureFillImage)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = fillMethod;
            fillImage.fillOrigin = Mathf.Clamp(fillOrigin, 0, 3);
            fillImage.fillClockwise = true;
        }

        if (weaponController == null)
        {
            weaponController = GetComponentInParent<PlayerWeaponController>();
        }

        if (playerTarget == null)
        {
            PlayerController2D player = GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }
    }

    private void SubscribeWeaponController()
    {
        if (weaponController == null)
        {
            return;
        }

        weaponController.WeaponEquipped += HandleWeaponEquipped;
    }

    private void UnsubscribeWeaponController()
    {
        if (weaponController == null)
        {
            return;
        }

        weaponController.WeaponEquipped -= HandleWeaponEquipped;
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTreeType, PlayerWeaponBase weapon)
    {
        AttachWeapon(weapon);
    }

    private void AttachWeapon(PlayerWeaponBase weapon)
    {
        if (currentWeapon == weapon)
        {
            return;
        }

        DetachWeapon();

        currentWeapon = weapon;

        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.ChargeStarted += HandleChargeStarted;
        currentWeapon.ChargeChanged += HandleChargeChanged;
        currentWeapon.ChargeReleased += HandleChargeReleased;
        currentWeapon.ChargeCanceled += HandleChargeCanceled;
    }

    private void DetachWeapon()
    {
        if (currentWeapon == null)
        {
            return;
        }

        currentWeapon.ChargeStarted -= HandleChargeStarted;
        currentWeapon.ChargeChanged -= HandleChargeChanged;
        currentWeapon.ChargeReleased -= HandleChargeReleased;
        currentWeapon.ChargeCanceled -= HandleChargeCanceled;

        currentWeapon = null;
    }

    private void HandleChargeStarted(PlayerWeaponBase weapon)
    {
        if (externalChargeActive)
        {
            return;
        }

        Show(0f);
    }

    private void HandleChargeChanged(PlayerWeaponBase weapon, float ratio)
    {
        if (externalChargeActive)
        {
            return;
        }

        Show(ratio);
    }

    private void HandleChargeReleased(PlayerWeaponBase weapon, float ratio)
    {
        if (externalChargeActive)
        {
            return;
        }

        Show(ratio);
        Hide();
    }

    private void HandleChargeCanceled(PlayerWeaponBase weapon)
    {
        if (externalChargeActive)
        {
            return;
        }

        Hide();
    }

    private void SetRatio(float ratio)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null && autoAddCanvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 자기 GameObject를 비활성화하면 차징 이벤트 구독도 끊기므로
        // 항상 활성 상태를 유지하고 CanvasGroup 알파만 조절한다.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}