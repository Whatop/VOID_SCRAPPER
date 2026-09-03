using System;
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
    private readonly Collider2D[] placementOverlapBuffer = new Collider2D[24];

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

        ResolveSafeExitPositions();

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

    private void ResolveSafeExitPositions()
    {
        if (!spawnReturnBeacon && !spawnWormhole)
        {
            return;
        }

        Bounds placementBounds;
        Vector2 preferredCenter;
        SalvageDevourerCorridorController corridor =
            FindFirstObjectByType<SalvageDevourerCorridorController>(FindObjectsInactive.Include);

        if (corridor != null && corridor.TryGetCurrentCombatViewportBounds(out placementBounds))
        {
            preferredCenter = placementBounds.center;
        }
        else if (TryResolveRegion2VictoryViewport(out placementBounds))
        {
            preferredCenter = transform.position;
        }
        else
        {
            ExpeditionMapGenerator mapGenerator =
                FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
            if (mapGenerator != null && mapGenerator.CameraSafeBounds.size.x > 0.1f &&
                mapGenerator.CameraSafeBounds.size.y > 0.1f)
            {
                placementBounds = mapGenerator.CameraSafeBounds;
                preferredCenter = spawnReturnBeacon && spawnWormhole
                    ? ((Vector2)returnBeaconPosition + (Vector2)wormholePosition) * 0.5f
                    : spawnReturnBeacon
                        ? returnBeaconPosition
                        : wormholePosition;
            }
            else
            {
                preferredCenter = transform.position;
                placementBounds = new Bounds(
                    preferredCenter,
                    new Vector3(14f, 10f, 1f)
                );
            }
        }

        Vector2[] avoidPoints = new Vector2[3];
        int avoidCount = 0;
        avoidPoints[avoidCount++] = rewardCapsulePosition;
        avoidPoints[avoidCount++] = transform.position;

        PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            avoidPoints[avoidCount++] = player.transform.position;
        }

        BossExitPlacement placement = BossExitSafePlacement.Resolve(
            placementBounds,
            preferredCenter,
            avoidPoints,
            avoidCount,
            IsExitCandidateBlocked,
            out bool usedBlockedFallback
        );
        returnBeaconPosition = placement.ReturnBeaconPosition;
        wormholePosition = placement.NextRegionPosition;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (usedBlockedFallback)
        {
            Debug.LogWarning(
                $"[Boss Exit] All clear candidates were occupied. Used the bounded deterministic fallback " +
                $"inside {placementBounds}. Beacon={returnBeaconPosition:F2}, Portal={wormholePosition:F2}.",
                this
            );
        }
#endif
    }

    private static bool TryResolveRegion2VictoryViewport(out Bounds bounds)
    {
        bounds = default;
        RunManager runManager = RunManager.Instance;
        if (runManager == null || !runManager.HasActiveRun ||
            runManager.CurrentRun.ExpeditionDepth != ExpeditionDepth.DeepZone1)
        {
            return false;
        }

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null || !gameplayCamera.orthographic)
        {
            return false;
        }

        float halfHeight = Mathf.Max(2f, gameplayCamera.orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.1f, gameplayCamera.aspect);
        bounds = new Bounds(
            gameplayCamera.transform.position,
            new Vector3(halfWidth * 2f, halfHeight * 2f, 1f)
        );
        return true;
    }

    private bool IsExitCandidateBlocked(Vector2 position)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;
        int count = Physics2D.OverlapCircle(
            position,
            BossExitSafePlacement.BlockingCheckRadius,
            filter,
            placementOverlapBuffer
        );
        return count > 0;
    }
}

public readonly struct BossExitPlacement
{
    public Vector2 ReturnBeaconPosition { get; }
    public Vector2 NextRegionPosition { get; }

    public BossExitPlacement(Vector2 returnBeaconPosition, Vector2 nextRegionPosition)
    {
        ReturnBeaconPosition = returnBeaconPosition;
        NextRegionPosition = nextRegionPosition;
    }
}

public static class BossExitSafePlacement
{
    public const float BlockingCheckRadius = 0.8f;
    public const float MinimumExitSeparation = 2.5f;
    public const float AvoidPointClearance = 1.75f;
    private const float BoundsInset = 1f;

    private static readonly Vector2[] CenterOffsets =
    {
        Vector2.zero,
        new Vector2(0f, 0.2f),
        new Vector2(0.2f, 0f),
        new Vector2(-0.2f, 0f),
        new Vector2(0f, -0.2f),
        new Vector2(0.2f, 0.2f),
        new Vector2(-0.2f, 0.2f),
        new Vector2(0.2f, -0.2f),
        new Vector2(-0.2f, -0.2f),
        new Vector2(-0.55f, 0f),
        new Vector2(0.55f, 0f),
        new Vector2(0f, 0.55f),
        new Vector2(0f, -0.55f),
        new Vector2(-0.55f, 0.55f),
        new Vector2(0.55f, 0.55f),
        new Vector2(-0.55f, -0.55f),
        new Vector2(0.55f, -0.55f)
    };

    private static readonly Vector2[] PairDirections =
    {
        Vector2.right,
        Vector2.up,
        new Vector2(1f, 1f).normalized,
        new Vector2(1f, -1f).normalized
    };

    public static BossExitPlacement Resolve(
        Bounds bounds,
        Vector2 preferredCenter,
        Vector2[] avoidPoints,
        int avoidCount,
        Func<Vector2, bool> isBlocked,
        out bool usedBlockedFallback)
    {
        usedBlockedFallback = false;
        Bounds safeBounds = NormalizeBounds(bounds, preferredCenter);
        Vector2 clampedPreferred = ClampInside(safeBounds, preferredCenter);

        if (TryResolveCandidates(
                safeBounds,
                clampedPreferred,
                avoidPoints,
                avoidCount,
                isBlocked,
                true,
                out BossExitPlacement placement))
        {
            return placement;
        }

        usedBlockedFallback = true;
        if (TryResolveCandidates(
                safeBounds,
                clampedPreferred,
                avoidPoints,
                avoidCount,
                null,
                false,
                out placement))
        {
            return placement;
        }

        Vector2 center = ClampInside(safeBounds, safeBounds.center);
        float halfSeparation = ResolveHalfSeparation(safeBounds, Vector2.right);
        return new BossExitPlacement(
            ClampInside(safeBounds, center + Vector2.left * halfSeparation),
            ClampInside(safeBounds, center + Vector2.right * halfSeparation)
        );
    }

    private static bool TryResolveCandidates(
        Bounds bounds,
        Vector2 preferredCenter,
        Vector2[] avoidPoints,
        int avoidCount,
        Func<Vector2, bool> isBlocked,
        bool requireClear,
        out BossExitPlacement placement)
    {
        Vector2 usableOffset = new Vector2(
            Mathf.Max(0f, bounds.extents.x - BoundsInset) * 0.5f,
            Mathf.Max(0f, bounds.extents.y - BoundsInset) * 0.5f
        );

        for (int centerIndex = 0; centerIndex < CenterOffsets.Length; centerIndex++)
        {
            Vector2 center = ClampInside(
                bounds,
                preferredCenter + Vector2.Scale(CenterOffsets[centerIndex], usableOffset)
            );

            for (int directionIndex = 0; directionIndex < PairDirections.Length; directionIndex++)
            {
                Vector2 direction = PairDirections[directionIndex];
                float halfSeparation = ResolveHalfSeparation(bounds, direction);
                Vector2 first = ClampInside(bounds, center - direction * halfSeparation);
                Vector2 second = ClampInside(bounds, center + direction * halfSeparation);

                if ((second - first).sqrMagnitude <
                    MinimumExitSeparation * MinimumExitSeparation ||
                    !IsClearOfAvoidPoints(first, avoidPoints, avoidCount) ||
                    !IsClearOfAvoidPoints(second, avoidPoints, avoidCount) ||
                    requireClear && isBlocked != null &&
                    (isBlocked(first) || isBlocked(second)))
                {
                    continue;
                }

                placement = new BossExitPlacement(first, second);
                return true;
            }
        }

        placement = default;
        return false;
    }

    private static float ResolveHalfSeparation(Bounds bounds, Vector2 direction)
    {
        float maximumX = Mathf.Abs(direction.x) <= 0.0001f
            ? float.MaxValue
            : Mathf.Max(0f, bounds.extents.x - BoundsInset) / Mathf.Abs(direction.x);
        float maximumY = Mathf.Abs(direction.y) <= 0.0001f
            ? float.MaxValue
            : Mathf.Max(0f, bounds.extents.y - BoundsInset) / Mathf.Abs(direction.y);
        return Mathf.Min(MinimumExitSeparation * 0.5f, maximumX, maximumY);
    }

    private static bool IsClearOfAvoidPoints(
        Vector2 candidate,
        Vector2[] avoidPoints,
        int avoidCount)
    {
        if (avoidPoints == null)
        {
            return true;
        }

        float clearanceSquared = AvoidPointClearance * AvoidPointClearance;
        int count = Mathf.Clamp(avoidCount, 0, avoidPoints.Length);
        for (int i = 0; i < count; i++)
        {
            if ((candidate - avoidPoints[i]).sqrMagnitude < clearanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static Bounds NormalizeBounds(Bounds bounds, Vector2 fallbackCenter)
    {
        if (bounds.size.x > BoundsInset * 2f + MinimumExitSeparation &&
            bounds.size.y > BoundsInset * 2f + MinimumExitSeparation)
        {
            return bounds;
        }

        return new Bounds(fallbackCenter, new Vector3(14f, 10f, 1f));
    }

    private static Vector2 ClampInside(Bounds bounds, Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, bounds.min.x + BoundsInset, bounds.max.x - BoundsInset),
            Mathf.Clamp(position.y, bounds.min.y + BoundsInset, bounds.max.y - BoundsInset)
        );
    }
}
