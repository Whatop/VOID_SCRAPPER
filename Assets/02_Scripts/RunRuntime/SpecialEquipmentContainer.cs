using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HarvestObjectHealth))]
public class SpecialEquipmentContainer : MonoBehaviour
{
    [Header("Container Reward")]
    [SerializeField] private SpecialRewardMode rewardMode = SpecialRewardMode.Mixed;
    [SerializeField] private int choiceCount = 3;
    [SerializeField] private int tuningChipReward = 1;

    [Header("Objective")]
    [SerializeField] private bool countsAsHighValueObjective = true;
    [SerializeField] private string objectiveId;

    [Header("References")]
    [SerializeField] private HarvestObjectHealth harvestObject;
    [SerializeField] private RunLevelTraitSelectionUI rewardChoiceUI;

    [Header("Fallback")]
    [SerializeField] private bool applyRandomFallbackWhenUiMissing = true;

    private bool handled;

    private void Reset()
    {
        harvestObject = GetComponent<HarvestObjectHealth>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        handled = false;
        ResolveReferences();

        if (harvestObject != null)
        {
            harvestObject.Died -= HandleContainerBroken;
            harvestObject.Died += HandleContainerBroken;
        }
    }

    private void OnDisable()
    {
        if (harvestObject != null)
        {
            harvestObject.Died -= HandleContainerBroken;
        }
    }

    public void Configure(
        SpecialRewardMode mode,
        RunLevelTraitSelectionUI selectionUI,
        string configuredObjectiveId,
        int configuredChoiceCount = 3,
        int configuredTuningChipReward = 1)
    {
        rewardMode = mode;
        rewardChoiceUI = selectionUI;
        objectiveId = configuredObjectiveId;
        choiceCount = Mathf.Max(1, configuredChoiceCount);
        tuningChipReward = Mathf.Max(0, configuredTuningChipReward);
        ResolveReferences();
    }

    private void HandleContainerBroken(HarvestObjectHealth source)
    {
        if (handled)
        {
            return;
        }

        handled = true;
        Vector2 sourcePosition = transform.position;

        if (tuningChipReward > 0 && RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.AddCurrency(CurrencyType.TuningChips, tuningChipReward);
        }

        if (countsAsHighValueObjective)
        {
            string id = ResolveObjectiveId();
            ExpeditionObjectiveDirector.Instance?.RegisterObjective(
                id,
                HighValueObjectiveSource.SpecialEquipmentContainer
            );
        }

        RunContext run = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun
            : null;

        bool forceRare = run != null && run.SpecialContainerRareMissStreak >= 2;
        ResolveReferences();

        bool offeredRareOrBetter = false;

        bool opened = rewardChoiceUI != null &&
                      rewardChoiceUI.ShowSpecialContainerChoices(
                          rewardMode,
                          Mathf.Max(1, choiceCount),
                          forceRare,
                          sourcePosition,
                          HandleRewardSelected,
                          out offeredRareOrBetter
                      );

        bool rewardResolved = opened;

        if (!opened && applyRandomFallbackWhenUiMissing)
        {
            rewardResolved = ApplyFallbackReward(
                sourcePosition,
                forceRare,
                out offeredRareOrBetter
            );
        }

        if (run != null && rewardResolved)
        {
            run.RecordSpecialContainerOffer(offeredRareOrBetter);
        }
    }

    private void HandleRewardSelected(RunRewardChoiceResult result)
    {
        if (!result.Success)
        {
            return;
        }

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null && tuningChipReward > 0)
        {
            hud.ShowWarning($"특수 화물 회수 완료 · 튜닝 칩 +{tuningChipReward}");
        }
    }

    private bool ApplyFallbackReward(
        Vector2 sourcePosition,
        bool forceRare,
        out bool offeredRareOrBetter)
    {
        offeredRareOrBetter = false;

        RunLevelTraitSelectionUI resolver = rewardChoiceUI != null
            ? rewardChoiceUI
            : FindFirstObjectByType<RunLevelTraitSelectionUI>();

        if (resolver == null)
        {
            Debug.LogWarning("특수 장비 상자의 선택 UI와 카탈로그를 찾지 못했습니다.", this);
            return false;
        }

        if (!resolver.TryBuildRewardOptions(
                rewardMode,
                1,
                RunRewardRarity.Common,
                forceRare,
                out var options) ||
            options == null ||
            options.Count <= 0)
        {
            return false;
        }

        offeredRareOrBetter = RunRewardChoiceGenerator.ContainsRareOrBetter(options);
        return RunRewardChoiceApplier.Apply(options[0], sourcePosition).Success;
    }

    private string ResolveObjectiveId()
    {
        if (!string.IsNullOrWhiteSpace(objectiveId))
        {
            return objectiveId;
        }

        return $"special_container_{rewardMode}_{GetInstanceID()}";
    }

    private void ResolveReferences()
    {
        if (harvestObject == null)
        {
            harvestObject = GetComponent<HarvestObjectHealth>();
        }

        if (rewardChoiceUI == null)
        {
            rewardChoiceUI = FindFirstObjectByType<RunLevelTraitSelectionUI>();
        }
    }
}
