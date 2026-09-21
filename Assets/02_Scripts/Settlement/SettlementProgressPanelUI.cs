using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Read-only campaign projection on the authored Settlement sidebar.</summary>
[DisallowMultipleComponent]
public sealed class SettlementProgressPanelUI : MonoBehaviour
{
    private const string Prefix = "ui.settlement.progress.";

    [SerializeField] private SettlementController settlementController;
    [SerializeField] private LocalizationCatalog localizationCatalog;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text statusText;

    private PermanentProgress subscribedProgress;
    private SettlementController subscribedController;
    private readonly Dictionary<string, string> arguments = new Dictionary<string, string>();

    private void OnEnable()
    {
        if (subscribedController != null) subscribedController.Changed -= Refresh;
        subscribedController = settlementController;
        if (subscribedController != null) subscribedController.Changed += Refresh;
        GameSettingsRuntime.Changed -= Refresh;
        GameSettingsRuntime.Changed += Refresh;
        Refresh();
    }

    private void Start()
    {
        // Persistent services may finish Awake after this scene component's OnEnable.
        Refresh();
    }

    private void OnDisable()
    {
        if (subscribedController != null) subscribedController.Changed -= Refresh;
        subscribedController = null;
        GameSettingsRuntime.Changed -= Refresh;
        if (subscribedProgress != null) subscribedProgress.Changed -= Refresh;
        subscribedProgress = null;
    }

    public void Refresh()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        PermanentProgress nextSubscription = isActiveAndEnabled ? progress : null;
        if (nextSubscription != subscribedProgress)
        {
            if (subscribedProgress != null) subscribedProgress.Changed -= Refresh;
            subscribedProgress = nextSubscription;
            if (subscribedProgress != null) subscribedProgress.Changed += Refresh;
        }
        if (headerText != null) headerText.text = Text("header");
        if (objectiveText != null) objectiveText.text = Text(GetObjectiveState(progress));
        if (statusText == null) return;
        if (progress == null)
        {
            statusText.text = string.Empty;
            return;
        }

        arguments["count"] = progress.AcquiredBossStoryPartCount.ToString();
        string parts = Format("parts");
        arguments["state"] = Text(GetCoreState(progress.CurrentRouteCoreState));
        string status = parts + "\n" + Format("core");
        if (progress.CurrentRouteCoreState == RouteCoreState.Activated)
        {
            status += "\n" + Text(progress.SettlementDefenseCleared ? "defense_clear" : "defense_pending");
            if (progress.CanLaunchFinalExpedition)
                status += "\n" + Text(progress.FinalBossDefeated ? "final_complete" : "final_available");
        }
        statusText.text = status;
    }

    public static string GetObjectiveState(PermanentProgress progress)
    {
        if (progress == null) return "loading";
        if (progress.FinalBossDefeated) return "objective_complete";
        if (progress.CurrentRouteCoreState == RouteCoreState.Activated)
            return progress.CanLaunchFinalExpedition ? "objective_final" : "objective_defend";
        if (progress.CanActivateRouteCore) return "objective_activate";
        if (progress.CanAssembleRouteCore) return "objective_restore";
        if (progress.HasPendingCampaignRouteAnalysis ||
            (progress.HasUnlockFlag(StoryProgressionIds.FirstSettlementPendingFlag) &&
             !progress.HasUnlockFlag(StoryProgressionIds.FirstSettlementCompleteFlag)))
            return "objective_analyze";
        return progress.AcquiredBossStoryPartCount == 0 ? "objective_first_part" : "objective_remaining_parts";
    }

    private static string GetCoreState(RouteCoreState state)
    {
        switch (state)
        {
            case RouteCoreState.ReadyToAssemble: return "core_ready";
            case RouteCoreState.Assembled: return "core_restored";
            case RouteCoreState.Activated: return "core_active";
            default: return "core_missing";
        }
    }

    private string Text(string suffix)
    {
        string key = Prefix + suffix;
        if (VoidScrapperLocalizationService.HasInstance)
            return VoidScrapperLocalizationService.Instance.GetText(key);
        return localizationCatalog != null && localizationCatalog.TryGetText(
            key, GameSettingsRuntime.LanguageCode, out string text, out _)
            ? text : string.Empty;
    }

    private string Format(string suffix)
    {
        string template = Text(suffix);
        return NamedPlaceholderUtility.TryFormat(template, arguments, out string text, out _)
            ? text : template;
    }
}
