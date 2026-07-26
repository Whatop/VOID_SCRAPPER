using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGunshotNoiseEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private PlayerWeaponBase[] weaponSources;

    [Header("Noise Radius")]
    [SerializeField] private float machineGunNoiseRadius = 8f;
    [SerializeField] private float shotgunNoiseRadius = 10f;
    [SerializeField] private float sniperNoiseRadius = 7f;

    [Header("Debug")]
    [SerializeField] private bool logNoise;

    private void Reset()
    {
        weaponController = GetComponent<PlayerWeaponController>();
        weaponSources = GetComponents<PlayerWeaponBase>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeWeapons();
    }

    private void OnDisable()
    {
        UnsubscribeWeapons();
    }

    private void ResolveReferences()
    {
        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (weaponSources == null || weaponSources.Length == 0)
        {
            weaponSources = GetComponents<PlayerWeaponBase>();
        }
    }

    private void SubscribeWeapons()
    {
        if (weaponSources == null)
        {
            return;
        }

        for (int i = 0; i < weaponSources.Length; i++)
        {
            PlayerWeaponBase weapon = weaponSources[i];

            if (weapon == null)
            {
                continue;
            }

            weapon.Fired -= HandleWeaponFired;
            weapon.Fired += HandleWeaponFired;
        }
    }

    private void UnsubscribeWeapons()
    {
        if (weaponSources == null)
        {
            return;
        }

        for (int i = 0; i < weaponSources.Length; i++)
        {
            PlayerWeaponBase weapon = weaponSources[i];

            if (weapon != null)
            {
                weapon.Fired -= HandleWeaponFired;
            }
        }
    }

    private void HandleWeaponFired(PlayerWeaponBase weapon, float powerRatio)
    {
        float radius = ResolveNoiseRadius();
        radius *= Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(powerRatio));

        EnemyVisionSensor.BroadcastNoise(transform.position, radius);

        if (logNoise)
        {
            Debug.Log($"플레이어 총성 발생 · 반경 {radius:0.0}", this);
        }
    }

    private float ResolveNoiseRadius()
    {
        WeaponTreeType weaponTree = weaponController != null
            ? weaponController.CurrentWeaponTree
            : WeaponTreeType.MachineGun;

        return weaponTree switch
        {
            WeaponTreeType.Shotgun => Mathf.Max(0f, shotgunNoiseRadius),
            WeaponTreeType.Sniper => Mathf.Max(0f, sniperNoiseRadius),
            _ => Mathf.Max(0f, machineGunNoiseRadius)
        };
    }
}
