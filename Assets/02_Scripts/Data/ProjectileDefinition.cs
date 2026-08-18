using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Projectiles/Projectile Definition")]
public class ProjectileDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string projectileId;
    [SerializeField] private string displayName;

    [Header("Prefab")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Base Stat")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float range = 8f;
    [SerializeField] private float lifeTime = 2f;

    [Header("Impact VFX")]
    [SerializeField] private GameObject impactEffectPrefab;
    [Min(0.01f)]
    [SerializeField] private float impactEffectLifeTime = 0.18f;
    [SerializeField] private bool rotateImpactEffectToProjectile = true;

    [Header("Advanced")]
    [SerializeField] private int pierceCount;
    [Tooltip("Damage retained after each successful pierced target. 1 keeps full damage.")]
    [Range(0f, 1f)]
    [SerializeField] private float pierceDamageRetention = 1f;
    [SerializeField] private bool useHoming;
    [SerializeField] private float homingAngle = 0f;
    [SerializeField] private float homingRange = 0f;

    public string ProjectileId => projectileId;
    public string DisplayName => displayName;
    public GameObject ProjectilePrefab => projectilePrefab;

    public float Damage => damage;
    public float Speed => speed;
    public float Range => range;
    public float LifeTime => lifeTime;

    public GameObject ImpactEffectPrefab => impactEffectPrefab;
    public float ImpactEffectLifeTime => Mathf.Max(0.01f, impactEffectLifeTime);
    public bool RotateImpactEffectToProjectile => rotateImpactEffectToProjectile;

    public int PierceCount => pierceCount;
    public float PierceDamageRetention => pierceDamageRetention > 0f
        ? Mathf.Clamp01(pierceDamageRetention)
        : 1f;
    public bool UseHoming => useHoming;
    public float HomingAngle => homingAngle;
    public float HomingRange => homingRange;
}
