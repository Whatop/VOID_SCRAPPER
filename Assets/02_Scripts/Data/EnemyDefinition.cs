using UnityEngine;

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
    [SerializeField] private float spreadAngle = 0f;
    [SerializeField] private float chargeTime = 0f;

    [Header("Secondary Attack Optional")]
    [Tooltip("엘리트 적의 대형탄처럼 기본 공격과 함께 추가 탄환을 발사할 때 사용합니다.")]
    [SerializeField] private bool useSecondaryProjectile;

    [SerializeField] private ProjectileDefinition secondaryProjectileDefinition;
    [SerializeField] private int secondaryProjectileCount = 1;
    [SerializeField] private float secondarySpreadAngle = 0f;

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

    public RewardDefinition RewardDefinition => rewardDefinition;
    public RadarMarkerType RadarMarkerType => radarMarkerType;
}