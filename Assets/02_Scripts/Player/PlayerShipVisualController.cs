using UnityEngine;

[DisallowMultipleComponent]
public class PlayerShipVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Animator Override Controllers")]
    [SerializeField] private RuntimeAnimatorController shotgunController;
    [SerializeField] private RuntimeAnimatorController sniperController;
    [SerializeField] private RuntimeAnimatorController machineGunController;

    [Header("Option")]
    [SerializeField] private bool applyOnEnable = true;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
        }

        if (applyOnEnable)
        {
            ApplyCurrentVisual();
        }
    }

    private void Start()
    {
        ApplyCurrentVisual();
    }

    private void OnDisable()
    {
        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }
    }

    private void CacheReferences()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponentInChildren<Animator>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }
    }

    private void HandleWeaponEquipped(WeaponTreeType weaponTreeType, PlayerWeaponBase weapon)
    {
        ApplyVisual(weaponTreeType);
    }

    public void ApplyCurrentVisual()
    {
        if (weaponController != null)
        {
            ApplyVisual(weaponController.CurrentWeaponTree);
            return;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            ApplyVisual(RunManager.Instance.CurrentRun.SelectedWeaponTree);
        }
    }

    public void ApplyVisual(WeaponTreeType weaponTreeType)
    {
        if (targetAnimator == null)
        {
            return;
        }

        RuntimeAnimatorController targetController = GetController(weaponTreeType);

        if (targetController == null)
        {
            Debug.LogWarning($"기체 Animator Controller가 연결되지 않았습니다: {weaponTreeType}", this);
            return;
        }

        if (targetAnimator.runtimeAnimatorController == targetController)
        {
            return;
        }

        targetAnimator.runtimeAnimatorController = targetController;
    }

    private RuntimeAnimatorController GetController(WeaponTreeType weaponTreeType)
    {
        return weaponTreeType switch
        {
            WeaponTreeType.Shotgun => shotgunController,
            WeaponTreeType.Sniper => sniperController,
            WeaponTreeType.MachineGun => machineGunController,
            _ => null
        };
    }
}