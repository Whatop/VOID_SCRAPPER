using System;
using System.Collections;
using DG.Tweening;
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
    private bool initialized;
    private bool revealFinished;
    private Sequence revealTween;
    private ExitRevealTarget beaconReveal;
    private ExitRevealTarget portalReveal;
    private Region2BossCoreRewardPresentation recoveryPresentation;
    private RunManager observedRunManager;
    private BossStoryPart recoveredPart;
    private Sprite recoveredPartSprite;
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
        Vector3 capsulePosition,
        Region2BossCoreRewardPresentation recoveryPrefab = null,
        bool recoverToPlayer = false,
        BossStoryPart storyPart = BossStoryPart.None,
        Sprite storyPartSprite = null)
    {
        if (initialized)
        {
            return;
        }
        initialized = true;
        recoveredPart = storyPart;
        recoveredPartSprite = storyPartSprite;
        observedRunManager = RunManager.Instance;
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
        returnBeaconPrefab = beaconPrefab;
        returnBeaconPosition = beaconPosition;
        wormholePortalPrefab = wormholePrefab;
        wormholePosition = portalPosition;
        rewardCapsulePrefab = capsulePrefab;
        rewardCapsulePosition = capsulePosition;

        spawnReturnBeacon = returnBeaconPrefab != null;
        spawnWormhole = CanPresentPortal();

        if (recoveryPrefab != null)
        {
            StartCoroutine(PresentRecoveryThenRewards(recoveryPrefab, recoverToPlayer,
                useSelectableReward, baseChoiceCount));
        }
        else
        {
            BossDummyController.ShowStoryPartRecovery(recoveredPart);
            BeginRewards(useSelectableReward, baseChoiceCount);
        }
    }

    private IEnumerator PresentRecoveryThenRewards(Region2BossCoreRewardPresentation prefab,
        bool recoverToPlayer, bool useSelectableReward, int baseChoiceCount)
    {
        // The boss can be released during recovery. This existing reward owner
        // keeps the visual and subsequent exits alive independently of its corpse.
        recoveryPresentation = Instantiate(prefab, transform.position, Quaternion.identity);
        if (recoveryPresentation != null)
        {
            if (recoveredPart != BossStoryPart.None)
            {
                recoveryPresentation.SetStoryPartSprite(recoveredPartSprite);
            }
            yield return recoveryPresentation.PlayRoutine(transform.position, null, recoverToPlayer);
            CleanupRecovery();
        }
        if (!revealFinished)
        {
            BossDummyController.ShowStoryPartRecovery(recoveredPart);
            BeginRewards(useSelectableReward, baseChoiceCount);
        }
    }

    private void BeginRewards(bool useSelectableReward, int baseChoiceCount)
    {
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
        if (completed || revealFinished || !isActiveAndEnabled ||
            (RunManager.Instance != null && RunManager.Instance.IsCompletingRun))
        {
            return;
        }

        completed = true;

        // Recheck at reward completion; a stale initial decision cannot expose a
        // portal after run ending or a changed region/state.
        spawnWormhole = CanPresentPortal();

        ResolveSafeExitPositions();

        if (spawnReturnBeacon)
        {
            beaconReveal = new ExitRevealTarget(
                Instantiate(returnBeaconPrefab, returnBeaconPosition, Quaternion.identity));
            beaconReveal.ObserveDisable(CompleteExitReveal);
        }

        if (spawnWormhole)
        {
            portalReveal = new ExitRevealTarget(
                Instantiate(wormholePortalPrefab, wormholePosition, Quaternion.identity));
            portalReveal.ObserveDisable(CompleteExitReveal);
        }

        BeginExitReveal();
    }

    private void BeginExitReveal()
    {
        try
        {
            revealTween = DOTween.Sequence().SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
            beaconReveal?.Animate(revealTween, transform.position, false);
            portalReveal?.Animate(revealTween, transform.position, true);
            revealTween.OnComplete(CompleteExitReveal).OnKill(CompleteExitReveal);
            if (!revealTween.IsActive() || (beaconReveal == null && portalReveal == null))
            {
                CompleteExitReveal();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Boss Exit] Reveal skipped: " + exception.Message, this);
            CompleteExitReveal();
        }
    }

    public static bool IsPortalEligible(RunContext run, PermanentProgress progress, bool runEnding)
    {
        return !runEnding && CampaignProgressionCatalog.CanAdvanceToNextRegion(run, progress);
    }

    private bool CanPresentPortal()
    {
        RunManager manager = RunManager.Instance;
        return wormholePortalPrefab != null && manager != null &&
               IsPortalEligible(manager.CurrentRun, PermanentProgress.Instance, manager.IsCompletingRun) &&
               manager.CanAdvanceToNextRegion(out _, out _);
    }

    public void CompleteExitReveal()
    {
        if (revealFinished)
        {
            return;
        }
        revealFinished = true;
        Sequence tween = revealTween;
        revealTween = null;
        tween?.Kill();
        beaconReveal?.Restore(CompleteExitReveal);
        portalReveal?.Restore(CompleteExitReveal);
        if (Application.isPlaying && isActiveAndEnabled)
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
        StopAllCoroutines();
        CleanupRecovery();
        CompleteExitReveal();
    }

    private void HandleRunEnded(RunResultData _)
    {
        StopAllCoroutines();
        CleanupRecovery();
        CompleteExitReveal();
    }

    private void CleanupRecovery()
    {
        if (recoveryPresentation == null)
        {
            return;
        }
        recoveryPresentation.CleanupPresentation();
        Destroy(recoveryPresentation.gameObject);
        recoveryPresentation = null;
    }

    // Setup-only snapshots; no gameplay state or per-frame interpolation here.
    private sealed class ExitRevealTarget
    {
        private readonly GameObject root;
        private readonly Vector3 position;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        private readonly SpriteRenderer[] sprites;
        private readonly Color[] colors;
        private readonly Collider2D[] colliders;
        private readonly bool[] colliderStates;
        private readonly ReturnBeacon beacon;
        private readonly WormholePortal portal;

        public ExitRevealTarget(GameObject instance)
        {
            root = instance;
            position = root.transform.position;
            scale = root.transform.localScale;
            rotation = root.transform.rotation;
            beacon = root.GetComponent<ReturnBeacon>();
            portal = root.GetComponent<WormholePortal>();
            beacon?.SetPresentationReady(false);
            portal?.SetPresentationReady(false);
            colliders = root.GetComponentsInChildren<Collider2D>(true);
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
            sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            colors = new Color[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                colors[i] = sprites[i].color;
                Color transparent = colors[i];
                transparent.a = 0f;
                sprites[i].color = transparent;
            }
            root.transform.localScale = scale * 0.05f;
        }

        public void ObserveDisable(Action callback)
        {
            if (beacon != null)
            {
                beacon.PresentationDisabled += callback;
            }
            if (portal != null)
            {
                portal.PresentationDisabled += callback;
            }
        }

        public void Animate(Sequence tween, Vector3 origin, bool rotate)
        {
            root.transform.position = origin;
            tween.Insert(0f, root.transform.DOMove(position, 0.65f).SetEase(Ease.OutCubic));
            tween.Insert(0.08f, root.transform.DOScale(scale, 0.57f).SetEase(Ease.OutBack));
            if (rotate)
            {
                root.transform.rotation = rotation * Quaternion.Euler(0f, 0f, -35f);
                tween.Insert(0f, root.transform.DORotateQuaternion(rotation, 0.65f));
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                tween.Insert(0.08f, sprites[i].DOFade(colors[i].a, 0.4f));
            }
        }

        public void Restore(Action callback)
        {
            if (beacon != null)
            {
                beacon.PresentationDisabled -= callback;
            }
            if (portal != null)
            {
                portal.PresentationDisabled -= callback;
            }
            if (root == null)
            {
                return;
            }
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = scale;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    sprites[i].color = colors[i];
                }
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = colliderStates[i];
                }
            }
            beacon?.SetPresentationReady(true);
            portal?.SetPresentationReady(true);
        }
    }

    private void ResolveSafeExitPositions()
    {
        if (!spawnReturnBeacon && !spawnWormhole)
        {
            return;
        }

        Bounds placementBounds;
        Vector2 preferredCenter = transform.position;
        SalvageDevourerCorridorController corridor =
            FindFirstObjectByType<SalvageDevourerCorridorController>(FindObjectsInactive.Include);

        if (!(corridor != null && corridor.TryGetCurrentCombatViewportBounds(out placementBounds)) &&
            !TryResolveRegion2VictoryViewport(out placementBounds))
        {
            ExpeditionMapGenerator mapGenerator =
                FindFirstObjectByType<ExpeditionMapGenerator>(FindObjectsInactive.Include);
            if (mapGenerator != null && mapGenerator.CameraSafeBounds.size.x > 0.1f &&
                mapGenerator.CameraSafeBounds.size.y > 0.1f)
            {
                placementBounds = mapGenerator.CameraSafeBounds;
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
        // The consumed reward and the death anchor are not obstacles. Actual
        // remaining solid objects (including the player) are checked below.

        PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            avoidPoints[avoidCount++] = player.transform.position;
        }

        bool usedBlockedFallback;
        if (spawnReturnBeacon && !spawnWormhole)
        {
            returnBeaconPosition = BossExitSafePlacement.ResolveSingle(
                placementBounds, preferredCenter, IsExitCandidateBlocked, out usedBlockedFallback);
        }
        else
        {
            BossExitPlacement placement = BossExitSafePlacement.Resolve(
                placementBounds,
                preferredCenter,
                avoidPoints,
                avoidCount,
                IsExitCandidateBlocked,
                out usedBlockedFallback
            );
            returnBeaconPosition = placement.ReturnBeaconPosition;
            wormholePosition = placement.NextRegionPosition;
        }

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

    public static Vector2 ResolveSingle(Bounds bounds, Vector2 preferredPosition,
        Func<Vector2, bool> isBlocked, out bool usedBlockedFallback)
    {
        Bounds safeBounds = NormalizeBounds(bounds, preferredPosition);
        Vector2 origin = ClampInside(safeBounds, preferredPosition);
        usedBlockedFallback = false;
        if (isBlocked == null || !isBlocked(origin))
        {
            return origin;
        }

        // Search outward in distance order instead of offsetting a lone beacon
        // as if it were half of a pair. This is setup-only, with no allocations.
        const float step = 0.25f;
        float maximumRadius = safeBounds.size.magnitude;
        for (float radius = step; radius <= maximumRadius; radius += step)
        {
            for (int direction = 0; direction < 32; direction++)
            {
                float angle = direction * Mathf.PI * 2f / 32f;
                Vector2 candidate = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (candidate != ClampInside(safeBounds, candidate) || isBlocked(candidate))
                {
                    continue;
                }
                return candidate;
            }
        }
        usedBlockedFallback = true;
        return origin;
    }

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
