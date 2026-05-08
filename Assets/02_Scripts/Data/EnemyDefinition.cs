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

    [Header("Attack")]
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private float attackInterval = 1.2f;
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle = 0f;
    [SerializeField] private float chargeTime = 0f;

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

    public RewardDefinition RewardDefinition => rewardDefinition;
    public RadarMarkerType RadarMarkerType => radarMarkerType;
}