using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Config/Game Balance Config")]
public class GameBalanceConfig : ScriptableObject
{
    [Header("Player Base")]
    [SerializeField] private int playerBaseHp = 20;
    [SerializeField] private float playerBaseMoveSpeed = 6f;
    [SerializeField] private float playerBaseDashDistance = 5f;

    [Header("Combat State")]
    [SerializeField] private float outOfCombatNoAttackTime = 2f;
    [SerializeField] private float outOfCombatNoHitTime = 2f;
    [SerializeField] private float chasingEnemyCheckRadius = 12f;

    [Header("Radar")]
    [SerializeField] private float radarScanRadius = 15f;

    [Header("Return")]
    [SerializeField] private float emergencyReturnPrepareTime = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float emergencyReturnLossRate = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float deathScrapKeepRate = 0.5f;

    [Header("Map")]
    [SerializeField] private Vector2 mapSize = new Vector2(80f, 80f);
    [SerializeField] private float startSafeRadius = 10f;
    [SerializeField] private float importantPointMinDistance = 20f;

    public int PlayerBaseHp => playerBaseHp;
    public float PlayerBaseMoveSpeed => playerBaseMoveSpeed;
    public float PlayerBaseDashDistance => playerBaseDashDistance;

    public float OutOfCombatNoAttackTime => outOfCombatNoAttackTime;
    public float OutOfCombatNoHitTime => outOfCombatNoHitTime;
    public float ChasingEnemyCheckRadius => chasingEnemyCheckRadius;

    public float RadarScanRadius => radarScanRadius;

    public float EmergencyReturnPrepareTime => emergencyReturnPrepareTime;
    public float EmergencyReturnLossRate => emergencyReturnLossRate;
    public float DeathScrapKeepRate => deathScrapKeepRate;

    public Vector2 MapSize => mapSize;
    public float StartSafeRadius => startSafeRadius;
    public float ImportantPointMinDistance => importantPointMinDistance;
}
