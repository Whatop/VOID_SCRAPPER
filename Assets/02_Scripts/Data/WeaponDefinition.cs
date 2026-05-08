using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Weapons/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string weaponId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Tree")]
    [SerializeField] private WeaponTreeType weaponTreeType;

    [Header("Projectile")]
    [SerializeField] private ProjectileDefinition projectileDefinition;

    [Header("Common Fire Settings")]
    [SerializeField] private float fireInterval = 0.25f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle = 0f;

    [Header("Sniper Charge Settings")]
    [SerializeField] private bool useCharge;
    [SerializeField] private float maxChargeTime = 1.2f;
    [SerializeField] private float minChargeDamage = 4f;
    [SerializeField] private float maxChargeDamage = 10f;
    [SerializeField] private bool cancelChargeOnMove = true;

    [Header("Camera")]
    [SerializeField] private bool modifyCameraOnCharge;
    [SerializeField] private float chargeCameraZoomBonus = 0.2f;

    public string WeaponId => weaponId;
    public string DisplayName => displayName;
    public string Description => description;

    public WeaponTreeType WeaponTreeType => weaponTreeType;
    public ProjectileDefinition ProjectileDefinition => projectileDefinition;

    public float FireInterval => fireInterval;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;

    public bool UseCharge => useCharge;
    public float MaxChargeTime => maxChargeTime;
    public float MinChargeDamage => minChargeDamage;
    public float MaxChargeDamage => maxChargeDamage;
    public bool CancelChargeOnMove => cancelChargeOnMove;

    public bool ModifyCameraOnCharge => modifyCameraOnCharge;
    public float ChargeCameraZoomBonus => chargeCameraZoomBonus;
}