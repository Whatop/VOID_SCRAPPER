using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Map/Map Generation Config")]
public class MapGenerationConfig : ScriptableObject
{
    [Header("Map Size")]
    [SerializeField] private Vector2 mapSize = new Vector2(80f, 80f);

    [Header("Placement Rules")]
    [SerializeField] private float startSafeRadius = 10f;
    [SerializeField] private float importantPointMinDistance = 20f;

    [Header("Important Points")]
    [SerializeField] private int shopCount = 2;
    [SerializeField] private int eventCount = 2;
    [SerializeField] private int coreCount = 1;

    [Header("Objects")]
    [SerializeField] private int highValueWreckCount = 3;
    [SerializeField] private int supplyContainerCount = 16;
    [SerializeField] private int destroyedHullCount = 8;
    [SerializeField] private int meteorCount = 24;

    [Header("Enemies")]
    [SerializeField] private int basicEnemyCount = 20;
    [SerializeField] private int shotgunEnemyCount = 5;
    [SerializeField] private int chargingEnemyCount = 4;
    [SerializeField] private int eliteEnemyCount = 2;

    [Header("Deep Zone Modifier")]
    [SerializeField] private float deepZoneEnemyHpMultiplier = 1.2f;
    [SerializeField] private float deepZoneEnemyDamageMultiplier = 1.2f;
    [SerializeField] private float deepZoneScrapMultiplier = 1.3f;
    [SerializeField] private int deepZoneAdditionalCoreReward = 1;

    public Vector2 MapSize => mapSize;

    public float StartSafeRadius => startSafeRadius;
    public float ImportantPointMinDistance => importantPointMinDistance;

    public int ShopCount => shopCount;
    public int EventCount => eventCount;
    public int CoreCount => coreCount;

    public int HighValueWreckCount => highValueWreckCount;
    public int SupplyContainerCount => supplyContainerCount;
    public int DestroyedHullCount => destroyedHullCount;
    public int MeteorCount => meteorCount;

    public int BasicEnemyCount => basicEnemyCount;
    public int ShotgunEnemyCount => shotgunEnemyCount;
    public int ChargingEnemyCount => chargingEnemyCount;
    public int EliteEnemyCount => eliteEnemyCount;

    public float DeepZoneEnemyHpMultiplier => deepZoneEnemyHpMultiplier;
    public float DeepZoneEnemyDamageMultiplier => deepZoneEnemyDamageMultiplier;
    public float DeepZoneScrapMultiplier => deepZoneScrapMultiplier;
    public int DeepZoneAdditionalCoreReward => deepZoneAdditionalCoreReward;
}