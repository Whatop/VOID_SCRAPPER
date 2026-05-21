using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossDummyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;

    [Header("Exit Object Prefabs")]
    [SerializeField] private GameObject returnBeaconPrefab;
    [SerializeField] private GameObject wormholePortalPrefab;

    [Header("Spawn Positions")]
    [SerializeField] private bool useBossDeathPosition = true;

    [Tooltip("귀환 비콘은 웜홀과 겹치지 않게 보스 사망 위치 기준으로 살짝 옆에 생성합니다.")]
    [SerializeField] private Vector2 returnBeaconSpawnOffset = new Vector2(-1.5f, 0f);

    [Tooltip("웜홀은 기본적으로 보스가 죽은 위치에 생성합니다.")]
    [SerializeField] private Vector2 wormholeSpawnOffset = Vector2.zero;

    [Header("External Config Optional")]
    [SerializeField] private bool hasConfiguredReturnBeaconPosition;
    [SerializeField] private Vector3 configuredReturnBeaconPosition;

    [SerializeField] private bool hasConfiguredWormholePosition;
    [SerializeField] private Vector3 configuredWormholePosition;

    [Header("Deep Zone Reward")]
    [SerializeField] private bool grantDeepZoneAdditionalCoreDirectly = true;
    [SerializeField] private int deepZoneAdditionalCoreShards = 1;

    [Header("State")]
    [SerializeField] private bool changeStateToExpeditionAfterDeath = true;

    private bool deathHandled;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }
    }

    private void OnEnable()
    {
        deathHandled = false;

        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleBossDied;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleBossDied;
        }
    }

    // 기존 CoreObject 코드와 호환용.
    // 예전에는 귀환 비콘 위치만 CoreObject에서 넘겨줬기 때문에 그대로 유지한다.
    public void ConfigureReturnBeacon(GameObject beaconPrefab, Vector3 spawnPosition)
    {
        returnBeaconPrefab = beaconPrefab;
        configuredReturnBeaconPosition = spawnPosition;
        hasConfiguredReturnBeaconPosition = true;
    }

    // 새 구조용.
    // CoreObject에서 둘 다 넘기고 싶으면 이 메서드를 호출하면 된다.
    public void ConfigureExitObjects(
        GameObject beaconPrefab,
        Vector3 beaconSpawnPosition,
        GameObject wormholePrefab,
        Vector3 wormholeSpawnPosition)
    {
        returnBeaconPrefab = beaconPrefab;
        configuredReturnBeaconPosition = beaconSpawnPosition;
        hasConfiguredReturnBeaconPosition = true;

        wormholePortalPrefab = wormholePrefab;
        configuredWormholePosition = wormholeSpawnPosition;
        hasConfiguredWormholePosition = true;
    }

    private void HandleBossDied(EnemyHealth health)
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.MarkBossDefeated();

            if (grantDeepZoneAdditionalCoreDirectly &&
                RunManager.Instance.CurrentRun.ExpeditionDepth == ExpeditionDepth.DeepZone1 &&
                deepZoneAdditionalCoreShards > 0)
            {
                RunManager.Instance.AddCurrency(CurrencyType.CoreShards, deepZoneAdditionalCoreShards);
            }
        }

        SpawnReturnBeacon();
        SpawnWormholePortal();

        if (changeStateToExpeditionAfterDeath && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Expedition);
        }
    }

    private void SpawnReturnBeacon()
    {
        if (returnBeaconPrefab == null)
        {
            Debug.LogWarning("보스 사망 후 생성할 returnBeaconPrefab이 없습니다.", this);
            return;
        }

        Vector3 spawnPosition = ResolveReturnBeaconSpawnPosition();
        Instantiate(returnBeaconPrefab, spawnPosition, Quaternion.identity);
    }

    private void SpawnWormholePortal()
    {
        if (wormholePortalPrefab == null)
        {
            Debug.LogWarning("보스 사망 후 생성할 wormholePortalPrefab이 없습니다.", this);
            return;
        }

        Vector3 spawnPosition = ResolveWormholeSpawnPosition();
        Instantiate(wormholePortalPrefab, spawnPosition, Quaternion.identity);
    }

    private Vector3 ResolveReturnBeaconSpawnPosition()
    {
        if (hasConfiguredReturnBeaconPosition)
        {
            return configuredReturnBeaconPosition;
        }

        Vector3 basePosition = useBossDeathPosition ? transform.position : Vector3.zero;
        return basePosition + (Vector3)returnBeaconSpawnOffset;
    }

    private Vector3 ResolveWormholeSpawnPosition()
    {
        if (hasConfiguredWormholePosition)
        {
            return configuredWormholePosition;
        }

        Vector3 basePosition = useBossDeathPosition ? transform.position : Vector3.zero;
        return basePosition + (Vector3)wormholeSpawnOffset;
    }
}