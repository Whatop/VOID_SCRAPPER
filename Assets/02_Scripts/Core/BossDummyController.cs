using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossDummyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private BossDeathPresentation deathPresentation;

    private BossPatternController bossPatternController;

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
    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        deathPresentation = GetComponent<BossDeathPresentation>();
        bossPatternController = GetComponent<BossPatternController>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (deathPresentation == null)
        {
            deathPresentation = GetComponent<BossDeathPresentation>();
        }

        if (bossPatternController == null)
        {
            bossPatternController = GetComponent<BossPatternController>();
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
        bossPatternController?.StopCombatForDeathPresentation();

        if (deathPresentation != null)
        {
            StartCoroutine(CompleteBossDeathAfterPresentation());
            return;
        }

        CompleteBossDeath();
    }

    private IEnumerator CompleteBossDeathAfterPresentation()
    {
        yield return deathPresentation.PlayRoutine(transform.position);
        CompleteBossDeath();
    }

    private void CompleteBossDeath()
    {
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
        }

        CreateRewardExitCoordinator();

        if (!grantSelectableBossReward &&
            changeStateToExpeditionAfterDeath &&
            GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Expedition);
        }
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
