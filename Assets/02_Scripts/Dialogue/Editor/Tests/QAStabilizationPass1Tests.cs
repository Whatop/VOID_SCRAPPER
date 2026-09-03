using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class QAStabilizationPass1Tests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        Physics2D.SyncTransforms();
    }

    [Test]
    public void BossExitPlacement_IsDeterministicSeparatedAndInsideBounds()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 14f, 1f));
        Vector2[] avoidPoints = { Vector2.zero, new Vector2(3f, 2f) };
        GameObject blockerObject = Track(new GameObject("Owned exit-placement blocker"));
        blockerObject.transform.position = new Vector2(-2.475f, -1.25f);
        BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
        blocker.size = new Vector2(0.6f, 0.6f);
        Physics2D.SyncTransforms();
        ExitBlockerProbe blockerProbe = new ExitBlockerProbe(blocker);

        BossExitPlacement first = BossExitSafePlacement.Resolve(
            bounds,
            Vector2.zero,
            avoidPoints,
            avoidPoints.Length,
            blockerProbe.IsBlocked,
            out bool firstFallback
        );
        BossExitPlacement second = BossExitSafePlacement.Resolve(
            bounds,
            Vector2.zero,
            avoidPoints,
            avoidPoints.Length,
            blockerProbe.IsBlocked,
            out bool secondFallback
        );

        string diagnostics = BuildExitPlacementDiagnostics(
            bounds,
            avoidPoints,
            first,
            second,
            firstFallback,
            secondFallback,
            blockerProbe
        );

        Assert.That(firstFallback, Is.False, diagnostics);
        Assert.That(secondFallback, Is.False, diagnostics);
        Assert.That(second.ReturnBeaconPosition, Is.EqualTo(first.ReturnBeaconPosition), diagnostics);
        Assert.That(second.NextRegionPosition, Is.EqualTo(first.NextRegionPosition), diagnostics);
        Assert.That(IsInside2D(bounds, first.ReturnBeaconPosition), Is.True, diagnostics);
        Assert.That(IsInside2D(bounds, first.NextRegionPosition), Is.True, diagnostics);
        Assert.That(
            Vector2.Distance(first.ReturnBeaconPosition, first.NextRegionPosition),
            Is.GreaterThanOrEqualTo(BossExitSafePlacement.MinimumExitSeparation - 0.001f),
            diagnostics
        );

        for (int i = 0; i < avoidPoints.Length; i++)
        {
            Assert.That(
                Vector2.Distance(first.ReturnBeaconPosition, avoidPoints[i]),
                Is.GreaterThanOrEqualTo(BossExitSafePlacement.AvoidPointClearance),
                diagnostics
            );
            Assert.That(
                Vector2.Distance(first.NextRegionPosition, avoidPoints[i]),
                Is.GreaterThanOrEqualTo(BossExitSafePlacement.AvoidPointClearance),
                diagnostics
            );
        }

        Assert.That(blockerProbe.IsBlocked(first.ReturnBeaconPosition), Is.False, diagnostics);
        Assert.That(blockerProbe.IsBlocked(first.NextRegionPosition), Is.False, diagnostics);
    }

    [Test]
    public void BossExitPlacement_AllBlocked_UsesBoundedDeterministicFallback()
    {
        Bounds bounds = new Bounds(new Vector3(4f, -3f), new Vector3(16f, 12f, 1f));
        BossExitPlacement placement = BossExitSafePlacement.Resolve(
            bounds,
            bounds.center,
            null,
            0,
            _ => true,
            out bool usedFallback
        );

        Assert.That(usedFallback, Is.True);
        Assert.That(IsInside2D(bounds, placement.ReturnBeaconPosition), Is.True);
        Assert.That(IsInside2D(bounds, placement.NextRegionPosition), Is.True);
    }

    [Test]
    public void Region2Corridor_TerminalPoint_RemainsBottomWorldEndpoint()
    {
        ExpeditionMapGenerator.Region2BossCorridorData data =
            new ExpeditionMapGenerator.Region2BossCorridorData(
                7f,
                24f,
                -31f,
                8f,
                new Bounds(Vector3.zero, Vector3.one),
                new Bounds(Vector3.zero, Vector3.one)
            );

        Assert.That(data.TerminalPoint, Is.EqualTo(new Vector2(7f, -31f)));
    }

    [Test]
    public void FrigateHomingAimPoint_UsesDamageColliderInsteadOfPartPivot()
    {
        GameObject partObject = Track(new GameObject("Frigate part target"));
        partObject.transform.position = new Vector3(10f, 0f, 0f);
        BoxCollider2D collider = partObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(6f, 2f);
        FrigateBossPart part = partObject.AddComponent<FrigateBossPart>();
        SetPrivateField(part, "damageCollider", collider);
        Physics2D.SyncTransforms();

        Vector2 aimPoint = part.ResolveHomingAimPoint(Vector2.zero);

        Assert.That(aimPoint.x, Is.EqualTo(7f).Within(0.05f));
        Assert.That(aimPoint.y, Is.EqualTo(0f).Within(0.05f));
        Assert.That(aimPoint, Is.Not.EqualTo((Vector2)partObject.transform.position));
    }

    [Test]
    public void RadarSuccessfulScan_DoesNotDeactivateRadarMode()
    {
        RadarModeInputState state = new RadarModeInputState();
        Assert.That(state.TryToggle(true, out bool active), Is.True);
        Assert.That(active, Is.True);
        Assert.That(state.TryBeginInstantScan(true, 10f, 0.5f), Is.True);
        Assert.That(state.IsRadarActive, Is.True);
    }

    [Test]
    public void CombatActivity_RemainsPublishedWithoutOwningRadarVisibility()
    {
        GameObject playerObject = Track(new GameObject("Combat state"));
        PlayerCombatState combatState = playerObject.AddComponent<PlayerCombatState>();
        int activityCount = 0;
        combatState.CombatActivityRegistered += () => activityCount++;

        combatState.RegisterAttack();
        combatState.RegisterHit();

        Assert.That(activityCount, Is.EqualTo(2));

        MonoScript radarScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Temp/PlayerRadarScanner.cs");
        Assert.That(radarScript, Is.Not.Null);
        Assert.That(
            radarScript.text,
            Does.Not.Contain("CombatActivityRegistered"),
            "Ordinary combat may remain observable without closing Radar.");
    }

    [Test]
    public void UnknownSignalRadarGuidance_UsesStableStateSpecificKeys()
    {
        Assert.That(
            Phase2CStoryDialogueIds.ResolveUnknownSignalRadarObjectiveTextKey(false),
            Is.EqualTo(Phase2CStoryDialogueIds.TutorialRadarObjectiveTextKey)
        );
        Assert.That(
            Phase2CStoryDialogueIds.ResolveUnknownSignalRadarObjectiveTextKey(true),
            Is.EqualTo(Phase2CStoryDialogueIds.TutorialRadarScanOnlyTextKey)
        );
    }

    [Test]
    public void SettlementActivationToggle_IsHiddenUntilUnlocked()
    {
        Assert.That(ShipTraitTreePanel.ShouldShowActivationToggle(false), Is.False);
        Assert.That(ShipTraitTreePanel.ShouldShowActivationToggle(true), Is.True);
    }

    [Test]
    public void ShopPurchaseSuccessEvent_UsesExistingConfirmationClip()
    {
        AudioEventDatabase database = AssetDatabase.LoadAssetAtPath<AudioEventDatabase>(
            "Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset"
        );
        AudioClip expectedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/06_Audio/SFX/Shop/shop_buy_success.wav"
        );

        Assert.That(database, Is.Not.Null);
        Assert.That(expectedClip, Is.Not.Null);
        Assert.That(database.TryGet(SoundEventIds.ShopBuySuccess, out AudioEventDefinition definition), Is.True);
        Assert.That(definition.Clips, Is.Not.Null);
        Assert.That(definition.Clips.Count, Is.GreaterThan(0));

        bool containsExpectedClip = false;
        for (int i = 0; i < definition.Clips.Count; i++)
        {
            AudioClip assignedClip = definition.Clips[i];
            Assert.That(assignedClip == null, Is.False, $"Missing clip reference at index {i}.");
            containsExpectedClip |= ReferenceEquals(assignedClip, expectedClip);
        }

        Assert.That(
            containsExpectedClip,
            Is.True,
            "The success event must reference the intended shop_buy_success.wav asset."
        );

        MonoScript shopTradeScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
            "Assets/02_Scripts/Shop/ShopTradeUI.cs"
        );
        Assert.That(shopTradeScript, Is.Not.Null);
        AssertShopSuccessAudioRemainsTransactionGated(shopTradeScript.text);
    }

    private static string BuildExitPlacementDiagnostics(
        Bounds bounds,
        Vector2[] avoidPoints,
        BossExitPlacement first,
        BossExitPlacement second,
        bool firstFallback,
        bool secondFallback,
        ExitBlockerProbe blockerProbe)
    {
        float firstSeparation = Vector2.Distance(
            first.ReturnBeaconPosition,
            first.NextRegionPosition
        );
        Vector2 referencePoint = avoidPoints != null && avoidPoints.Length > 0
            ? avoidPoints[0]
            : Vector2.zero;

        return
            $"Bounds center={bounds.center:F3}, size={bounds.size:F3}; " +
            $"first beacon={first.ReturnBeaconPosition:F3}, portal={first.NextRegionPosition:F3}, " +
            $"fallback={firstFallback}; second beacon={second.ReturnBeaconPosition:F3}, " +
            $"portal={second.NextRegionPosition:F3}, fallback={secondFallback}; " +
            $"separation actual={firstSeparation:F3}, " +
            $"required={BossExitSafePlacement.MinimumExitSeparation:F3}; " +
            $"reference={referencePoint:F3}, beaconDistance=" +
            $"{Vector2.Distance(first.ReturnBeaconPosition, referencePoint):F3}, portalDistance=" +
            $"{Vector2.Distance(first.NextRegionPosition, referencePoint):F3}; " +
            $"beaconBlocked={blockerProbe.IsBlocked(first.ReturnBeaconPosition)}, " +
            $"portalBlocked={blockerProbe.IsBlocked(first.NextRegionPosition)}; " +
            $"blockingHits={blockerProbe.DescribeBlockingHits()}";
    }

    private static void AssertShopSuccessAudioRemainsTransactionGated(string source)
    {
        const string methodMarker = "private void OnClickBuy()";
        const string nextMethodMarker = "private bool TryBuyReinforcement()";
        const string eligibilityGuard = "if (!CanBuySelectedOption())";
        const string transactionGuard = "if (!success)";
        const string failureAudio = "AudioManager.Play(SoundEventIds.ShopBuyFail);";
        const string successAudio = "AudioManager.Play(SoundEventIds.ShopBuySuccess);";

        int methodStart = source.IndexOf(methodMarker, System.StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));

        int methodEnd = source.IndexOf(nextMethodMarker, methodStart, System.StringComparison.Ordinal);
        Assert.That(methodEnd, Is.GreaterThan(methodStart));

        string method = source.Substring(methodStart, methodEnd - methodStart);
        int eligibilityIndex = method.IndexOf(eligibilityGuard, System.StringComparison.Ordinal);
        Assert.That(eligibilityIndex, Is.GreaterThanOrEqualTo(0));

        int firstFailureIndex = method.IndexOf(failureAudio, eligibilityIndex, System.StringComparison.Ordinal);
        Assert.That(firstFailureIndex, Is.GreaterThan(eligibilityIndex));

        int transactionIndex = method.IndexOf(transactionGuard, System.StringComparison.Ordinal);
        Assert.That(transactionIndex, Is.GreaterThan(firstFailureIndex));

        int secondFailureIndex = method.IndexOf(failureAudio, firstFailureIndex + 1, System.StringComparison.Ordinal);
        Assert.That(secondFailureIndex, Is.GreaterThan(transactionIndex));

        int successIndex = method.IndexOf(successAudio, System.StringComparison.Ordinal);
        Assert.That(successIndex, Is.GreaterThan(secondFailureIndex));

        Assert.That(
            CountOccurrences(method, successAudio),
            Is.EqualTo(1),
            "The success sound must have one post-transaction call site."
        );
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class ExitBlockerProbe
    {
        private readonly Collider2D blocker;
        private readonly List<string> blockingHits = new List<string>();

        public ExitBlockerProbe(Collider2D blocker)
        {
            this.blocker = blocker;
        }

        public bool IsBlocked(Vector2 position)
        {
            if (blocker == null || !blocker.enabled || !blocker.gameObject.activeInHierarchy)
            {
                return false;
            }

            Vector2 closestPoint = blocker.ClosestPoint(position);
            bool blocked = Vector2.Distance(position, closestPoint) <=
                           BossExitSafePlacement.BlockingCheckRadius;
            if (blocked)
            {
                blockingHits.Add(
                    $"candidate={position:F3}, object={blocker.name}, " +
                    $"layer={blocker.gameObject.layer}({LayerMask.LayerToName(blocker.gameObject.layer)}), " +
                    $"id={blocker.GetInstanceID()}"
                );
            }

            return blocked;
        }

        public string DescribeBlockingHits()
        {
            return blockingHits.Count > 0
                ? string.Join(" | ", blockingHits)
                : "<none>";
        }
    }

    private T Track<T>(T createdObject) where T : Object
    {
        createdObjects.Add(createdObject);
        return createdObject;
    }

    private static bool IsInside2D(Bounds bounds, Vector2 position)
    {
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
               position.y >= bounds.min.y && position.y <= bounds.max.y;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
