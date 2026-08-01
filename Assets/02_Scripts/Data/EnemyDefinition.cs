using UnityEngine;

public enum EnemyRangedAttackPattern
{
    Auto = 0,
    Standard = 1,
    MachineGunBurst = 2,
    ShakingShotgun = 3,
    ChargingSplit = 4,
    StaggeredShotgun = 5
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Enemies/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string enemyId;
    [SerializeField] private string displayName;
    [SerializeField] private EnemyType enemyType;

    [Header("Prefab")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Stats")]
    [SerializeField] private float maxHp = 10f;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float visionRange = 9f;
    [SerializeField] private float attackRange = 6f;

    [Header("Primary Attack")]
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle;
    [SerializeField] private float chargeTime;

    [Header("Secondary Attack Optional")]
    [Tooltip("표준 패턴에서 기본 공격과 함께 추가 탄환 패턴을 발사할 때 사용합니다. ChargingSplit에서는 분해탄 정의의 Fallback으로 사용됩니다.")]
    [SerializeField] private bool useSecondaryProjectile;
    [SerializeField] private ProjectileDefinition secondaryProjectileDefinition;
    [SerializeField] private int secondaryProjectileCount = 1;
    [SerializeField] private float secondarySpreadAngle;

    [Header("Special Ranged Pattern")]
    [Tooltip("Auto는 EnemyType에 맞춰 자동 선택합니다. 일반 적은 Standard를 사용합니다.")]
    [SerializeField] private EnemyRangedAttackPattern rangedAttackPattern = EnemyRangedAttackPattern.Auto;

    [Header("Machine Gun Burst")]
    [Min(1)]
    [SerializeField] private int burstShotCount = 10;
    [Min(0.01f)]
    [SerializeField] private float burstShotInterval = 0.1f;
    [Range(0f, 45f)]
    [SerializeField] private float burstHalfAngle = 10f;
    [Tooltip("켜면 점사 중 매 발마다 플레이어 위치를 다시 조준합니다. 기본 OFF가 회피 가능성이 더 좋습니다.")]
    [SerializeField] private bool burstTrackTargetEachShot;

    [Header("Shaking Shotgun")]
    [Tooltip("탄이 전진하면서 좌우로 흔들리는 속도 크기입니다.")]
    [Min(0f)]
    [SerializeField] private float shakeLateralSpeed = 1.35f;
    [Tooltip("초당 흔들림 횟수입니다.")]
    [Min(0f)]
    [SerializeField] private float shakeFrequency = 4.5f;

    [Header("Staggered Shotgun")]
    [Tooltip("서로 다른 중심 각도의 산탄을 시간차로 발사합니다.")]
    [Min(1)]
    [SerializeField] private int staggeredVolleyCount = 2;
    [Min(0.01f)]
    [SerializeField] private float staggeredVolleyInterval = 0.25f;
    [Range(0f, 45f)]
    [SerializeField] private float staggeredVolleyAngleOffset = 9f;
    [SerializeField] private bool staggeredTrackTargetEachVolley = true;

    [Header("Charging Split")]
    [Tooltip("비우면 Secondary Projectile Definition을 분해탄으로 사용합니다.")]
    [SerializeField] private ProjectileDefinition splitProjectileDefinition;
    [Min(1)]
    [SerializeField] private int splitProjectileCount = 6;
    [Min(0.05f)]
    [SerializeField] private float splitDelay = 0.95f;
    [SerializeField] private float splitAngleOffset;
    [SerializeField] private bool removeParentProjectileOnSplit = true;

    [Header("Predictive Aim Optional")]
    [Tooltip("공격 1회마다 플레이어의 현재 속도를 선행 계산할 확률입니다. 0.02는 2%입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float predictiveShotChance;
    [Tooltip("이 속도보다 느린 대상에게는 예측 조준을 사용하지 않습니다.")]
    [Min(0f)]
    [SerializeField] private float predictiveMinimumTargetSpeed = 0.5f;
    [Tooltip("예측 위치를 계산할 최대 선행 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float predictiveMaxLeadTime = 0.65f;
    [Tooltip("대상 속도에 곱하는 보정값입니다. 1이면 실제 속도를 사용합니다.")]
    [Min(0f)]
    [SerializeField] private float predictiveVelocityMultiplier = 1f;

    [Header("Reward")]
    [SerializeField] private RewardDefinition rewardDefinition;

    [Header("Radar")]
    [SerializeField] private RadarMarkerType radarMarkerType = RadarMarkerType.Enemy;

    public string EnemyId => enemyId;
    public string DisplayName => displayName;
    public EnemyType EnemyType => enemyType;
    public GameObject EnemyPrefab => enemyPrefab;

    public float MaxHp => maxHp;
    public float MoveSpeed => moveSpeed;
    public float VisionRange => visionRange;
    public float AttackRange => attackRange;

    public ProjectileDefinition ProjectileDefinition => projectileDefinition;
    public float AttackInterval => attackInterval;
    public int ProjectileCount => projectileCount;
    public float SpreadAngle => spreadAngle;
    public float ChargeTime => chargeTime;

    public bool UseSecondaryProjectile => useSecondaryProjectile;
    public ProjectileDefinition SecondaryProjectileDefinition => secondaryProjectileDefinition;
    public int SecondaryProjectileCount => secondaryProjectileCount;
    public float SecondarySpreadAngle => secondarySpreadAngle;

    public EnemyRangedAttackPattern RangedAttackPattern => ResolveRangedAttackPattern();
    public int BurstShotCount => Mathf.Max(1, burstShotCount);
    public float BurstShotInterval => Mathf.Max(0.01f, burstShotInterval);
    public float BurstHalfAngle => Mathf.Clamp(burstHalfAngle, 0f, 45f);
    public bool BurstTrackTargetEachShot => burstTrackTargetEachShot;

    public float ShakeLateralSpeed => Mathf.Max(0f, shakeLateralSpeed);
    public float ShakeFrequency => Mathf.Max(0f, shakeFrequency);

    public int StaggeredVolleyCount => Mathf.Max(1, staggeredVolleyCount);
    public float StaggeredVolleyInterval => Mathf.Max(0.01f, staggeredVolleyInterval);
    public float StaggeredVolleyAngleOffset => Mathf.Clamp(staggeredVolleyAngleOffset, 0f, 45f);
    public bool StaggeredTrackTargetEachVolley => staggeredTrackTargetEachVolley;

    public ProjectileDefinition SplitProjectileDefinition => splitProjectileDefinition != null
        ? splitProjectileDefinition
        : secondaryProjectileDefinition;
    public int SplitProjectileCount => Mathf.Max(1, splitProjectileCount);
    public float SplitDelay => Mathf.Max(0.05f, splitDelay);
    public float SplitAngleOffset => splitAngleOffset;
    public bool RemoveParentProjectileOnSplit => removeParentProjectileOnSplit;

    public float PredictiveShotChance => Mathf.Clamp01(predictiveShotChance);
    public float PredictiveMinimumTargetSpeed => Mathf.Max(0f, predictiveMinimumTargetSpeed);
    public float PredictiveMaxLeadTime => Mathf.Max(0f, predictiveMaxLeadTime);
    public float PredictiveVelocityMultiplier => Mathf.Max(0f, predictiveVelocityMultiplier);

    public RewardDefinition RewardDefinition => rewardDefinition;
    public RadarMarkerType RadarMarkerType => radarMarkerType;

    private EnemyRangedAttackPattern ResolveRangedAttackPattern()
    {
        if (rangedAttackPattern != EnemyRangedAttackPattern.Auto)
        {
            return rangedAttackPattern;
        }

        return enemyType switch
        {
            EnemyType.EliteMachineGun => EnemyRangedAttackPattern.MachineGunBurst,
            EnemyType.EliteShotgun => EnemyRangedAttackPattern.StaggeredShotgun,
            EnemyType.EliteCharging => EnemyRangedAttackPattern.ChargingSplit,
            _ => EnemyRangedAttackPattern.Standard
        };
    }
}
