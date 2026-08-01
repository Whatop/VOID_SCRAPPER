using UnityEngine;

[DisallowMultipleComponent]
public class BossRewardExitCoordinator : MonoBehaviour
{
    private GameObject returnBeaconPrefab;
    private GameObject wormholePortalPrefab;
    private GameObject rewardCapsulePrefab;

    private Vector3 returnBeaconPosition;
    private Vector3 wormholePosition;
    private Vector3 rewardCapsulePosition;

    private bool spawnReturnBeacon;
    private bool spawnWormhole;
    private bool completed;

    // 기존 호출부와의 호환용 오버로드.
    public void Initialize(
        GameObject beaconPrefab,
        Vector3 beaconPosition,
        GameObject wormholePrefab,
        Vector3 portalPosition,
        bool useSelectableReward,
        int baseChoiceCount)
    {
        Initialize(
            beaconPrefab,
            beaconPosition,
            wormholePrefab,
            portalPosition,
            useSelectableReward,
            baseChoiceCount,
            null,
            transform.position
        );
    }

    public void Initialize(
        GameObject beaconPrefab,
        Vector3 beaconPosition,
        GameObject wormholePrefab,
        Vector3 portalPosition,
        bool useSelectableReward,
        int baseChoiceCount,
        GameObject capsulePrefab,
        Vector3 capsulePosition)
    {
        returnBeaconPrefab = beaconPrefab;
        returnBeaconPosition = beaconPosition;
        wormholePortalPrefab = wormholePrefab;
        wormholePosition = portalPosition;
        rewardCapsulePrefab = capsulePrefab;
        rewardCapsulePosition = capsulePosition;

        spawnReturnBeacon = returnBeaconPrefab != null;
        spawnWormhole = wormholePortalPrefab != null &&
                        RunManager.Instance != null &&
                        RunManager.Instance.CanAdvanceToNextRegion(out _, out _);

        if (!useSelectableReward)
        {
            Complete();
            return;
        }

        ExpeditionObjectiveDirector director = ExpeditionObjectiveDirector.Instance;
        bool rareGuaranteed = director == null || director.BossRareGuaranteed;
        int choiceCount = director != null
            ? director.ResolveBossChoiceCount(baseChoiceCount)
            : Mathf.Max(1, baseChoiceCount);

        if (TrySpawnRewardCapsule(choiceCount, rareGuaranteed))
        {
            return;
        }

        // 캡슐이 연결되지 않은 기존 프로젝트에서는 즉시 선택 UI 방식으로 안전하게 폴백합니다.
        RunLevelTraitSelectionUI rewardChoiceUI = FindFirstObjectByType<RunLevelTraitSelectionUI>();

        if (rewardChoiceUI == null ||
            !rewardChoiceUI.ShowBossRewardChoices(
                choiceCount,
                rareGuaranteed,
                transform.position,
                _ => Complete()))
        {
            Complete();
        }
    }

    private bool TrySpawnRewardCapsule(int choiceCount, bool rareGuaranteed)
    {
        if (rewardCapsulePrefab == null)
        {
            return false;
        }

        GameObject capsuleObject = Instantiate(
            rewardCapsulePrefab,
            rewardCapsulePosition,
            Quaternion.identity
        );

        if (capsuleObject == null)
        {
            return false;
        }

        RewardCapsule capsule = capsuleObject.GetComponent<RewardCapsule>();

        if (capsule == null)
        {
            capsule = capsuleObject.AddComponent<RewardCapsule>();
        }

        if (capsule.ConfigureBossReward(choiceCount, rareGuaranteed, Complete))
        {
            return true;
        }

        Destroy(capsuleObject);
        return false;
    }

    private void Complete()
    {
        if (completed)
        {
            return;
        }

        completed = true;

        if (spawnReturnBeacon)
        {
            Instantiate(returnBeaconPrefab, returnBeaconPosition, Quaternion.identity);
        }

        if (spawnWormhole)
        {
            Instantiate(wormholePortalPrefab, wormholePosition, Quaternion.identity);
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Expedition);
        }

        Destroy(gameObject);
    }
}
