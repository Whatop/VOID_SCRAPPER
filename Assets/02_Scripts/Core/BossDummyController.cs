using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossDummyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private RewardDropper rewardDropper;

    [Header("Campaign")]
    [SerializeField] private BossCampaignDefinition campaignDefinition;
    [SerializeField] private bool completeFinalBossAsVictory = true;

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

    [Header("Core Shard World Pickup")]
    [Tooltip("보스 처치 후 코어 조각을 즉시 지급하지 않고 방전된 월드 코어 위치에 물리 드랍합니다.")]
    [SerializeField] private bool dropCoreShardsAsWorldPickup = true;
    [SerializeField] private int normalCoreShards = 1;
    [SerializeField] private int deepZoneAdditionalCoreShards = 1;
    [Tooltip("켜두면 RewardPickup 연결이 잘못되어도 코어를 즉시 지급하지 않고 오류를 드러냅니다.")]
    [SerializeField] private bool forceWorldPickupOnly = true;
    [Tooltip("디버그용 레거시 옵션입니다. Force World Pickup Only가 꺼져 있을 때만 사용됩니다.")]
    [SerializeField] private bool fallbackToDirectCoreGrant;

    [Header("Selectable Boss Reward")]
    [Tooltip("보스 처치 후 귀환 오브젝트를 열기 전에 선택형 장비 보상을 지급합니다.")]
    [SerializeField] private bool grantSelectableBossReward = true;
    [SerializeField] private int bossRewardChoiceCount = 3;

    [Header("Boss Reward Capsule")]
    [Tooltip("연결하면 보스 사망 즉시 UI를 띄우지 않고, 보상 캡슐을 투하한 뒤 E 상호작용으로 보상을 선택합니다.")]
    [SerializeField] private GameObject bossRewardCapsulePrefab;
    [SerializeField] private Vector2 bossRewardCapsuleSpawnOffset = new Vector2(0f, -1f);

    [Header("State")]
    [SerializeField] private bool changeStateToExpeditionAfterDeath = true;

    private bool deathHandled;
    private bool hasConfiguredCoreShardRewardPosition;
    private Vector3 configuredCoreShardRewardPosition;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        rewardDropper = GetComponent<RewardDropper>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
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

    public void ConfigureCampaignDefinition(BossCampaignDefinition definition)
    {
        campaignDefinition = definition;
    }

    public void ConfigureCoreShardRewardPoint(Vector3 worldPosition)
    {
        configuredCoreShardRewardPosition = worldPosition;
        hasConfiguredCoreShardRewardPosition = true;
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

        BossCampaignDefinition resolvedDefinition = ResolveCampaignDefinition();
        CampaignBossId bossId = ResolveCampaignBossId(resolvedDefinition);

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            bool grantStoryPart = resolvedDefinition == null ||
                                  resolvedDefinition.GrantStoryPartOnFirstDefeat;

            RunManager.Instance.MarkBossDefeated(bossId, grantStoryPart);
            CampaignBossRewardService.GrantGuaranteedPassive(
                resolvedDefinition,
                transform.position
            );

            if (bossId == CampaignBossId.NullDispatcher && completeFinalBossAsVictory)
            {
                RunManager.Instance.CompleteRun(RunEndReason.FinalVictory);
                return;
            }

            DropOrGrantCoreShards(resolvedDefinition);
        }

        CreateRewardExitCoordinator();

        if (!grantSelectableBossReward &&
            changeStateToExpeditionAfterDeath &&
            GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Expedition);
        }
    }

    private void DropOrGrantCoreShards(BossCampaignDefinition resolvedDefinition)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return;
        }

        ExpeditionDepth depth = RunManager.Instance.CurrentRun.ExpeditionDepth;
        int amount = resolvedDefinition != null
            ? resolvedDefinition.CoreShardReward
            : CampaignProgressionCatalog.GetCoreShardReward(depth);

        // 캠페인 정의가 없을 때만 기존 인스펙터 값을 호환용으로 사용합니다.
        if (resolvedDefinition == null && amount <= 0)
        {
            amount = Mathf.Max(0, normalCoreShards);

            if (depth != ExpeditionDepth.Normal)
            {
                amount += Mathf.Max(0, deepZoneAdditionalCoreShards);
            }
        }

        if (amount <= 0)
        {
            return;
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        bool dropped = false;

        if (dropCoreShardsAsWorldPickup && rewardDropper != null)
        {
            dropped = rewardDropper.TryDropCurrencyRewardAt(
                ResolveCoreShardRewardPosition(),
                CurrencyType.CoreShards,
                amount
            );
        }

        if (!dropped)
        {
            Debug.LogWarning(
                "코어 조각 월드 픽업 드랍에 실패했습니다. 보스 RewardDropper와 RewardPickup Prefab 연결을 확인하세요.",
                this
            );

            if (!forceWorldPickupOnly && fallbackToDirectCoreGrant)
            {
                RunManager.Instance.AddCurrency(CurrencyType.CoreShards, amount);
            }
        }
    }

    private Vector3 ResolveCoreShardRewardPosition()
    {
        return hasConfiguredCoreShardRewardPosition
            ? configuredCoreShardRewardPosition
            : transform.position;
    }

    private BossCampaignDefinition ResolveCampaignDefinition()
    {
        ExpeditionDepth depth = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.ExpeditionDepth
            : ExpeditionDepth.Normal;

        return CampaignBossRewardService.ResolveDefinition(campaignDefinition, depth);
    }

    private CampaignBossId ResolveCampaignBossId(BossCampaignDefinition definition)
    {
        ExpeditionDepth depth = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.ExpeditionDepth
            : ExpeditionDepth.Normal;

        return CampaignBossRewardService.ResolveBossId(definition, depth);
    }

    private void CreateRewardExitCoordinator()
    {
        GameObject coordinatorObject = new GameObject("BossRewardExitCoordinator");
        coordinatorObject.transform.position = transform.position;

        BossRewardExitCoordinator coordinator = coordinatorObject.AddComponent<BossRewardExitCoordinator>();
        coordinator.Initialize(
            returnBeaconPrefab,
            ResolveReturnBeaconSpawnPosition(),
            wormholePortalPrefab,
            ResolveWormholeSpawnPosition(),
            grantSelectableBossReward,
            Mathf.Max(1, bossRewardChoiceCount),
            bossRewardCapsulePrefab,
            ResolveBossRewardCapsuleSpawnPosition()
        );
    }


    private Vector3 ResolveBossRewardCapsuleSpawnPosition()
    {
        Vector3 basePosition = useBossDeathPosition ? transform.position : Vector3.zero;
        return basePosition + (Vector3)bossRewardCapsuleSpawnOffset;
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