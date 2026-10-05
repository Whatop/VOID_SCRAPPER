using System.Collections.Generic;
using UnityEngine;

// Presentation scope on the existing HUD. Encounter and gameplay owners stay unchanged.
public partial class ExpeditionHUD
{
    private readonly HashSet<object> regionBossOwners = new HashSet<object>();
    private readonly List<KeyValuePair<Renderer, bool>> hiddenNpcRenderers = new List<KeyValuePair<Renderer, bool>>();
    private bool regionBossPresented;
    private bool IsRegionCoreObjectiveRelevant => !regionBossPresented || RunManager.Instance == null || !RunManager.Instance.HasActiveRun || !RunManager.Instance.CurrentRun.BossDefeated;
    private void HandleRegionBossDefeated(CampaignBossId _, bool __) { if (regionBossPresented) RefreshObjectiveProgress(); }
    private InteractionPromptUI regionBossPrompt;
    private PlayerHealth regionBossPlayer;
    private PlayerInteractor regionBossInteractor;
    private RunManager regionBossRun;
    public bool IsRegionBossPresentationActive => regionBossOwners.Count > 0;

    public void SetRegionBossPresentation(object owner, bool active)
    {
        if (owner == null) return;
        bool wasActive = IsRegionBossPresentationActive;
        if (active) regionBossOwners.Add(owner); else regionBossOwners.Remove(owner);
        if (wasActive == IsRegionBossPresentationActive) return;
        if (!IsRegionBossPresentationActive) { EndRegionBossPresentation(); return; }

        regionBossPresented = true;
        SetOperationBriefingSuppressed(regionBossOwners, true);
        RefreshObjectiveProgress();
        warningMessageUI?.SetRegionBossPriority(true);
        BossHealthBarUI.Instance?.SetRegionPresentation(true);
        regionBossPrompt = FindFirstObjectByType<InteractionPromptUI>(FindObjectsInactive.Include);
        regionBossPrompt?.SetRegionBossSuppressed(true);
        regionBossPlayer = FindFirstObjectByType<PlayerHealth>();
        regionBossInteractor = regionBossPlayer != null ? regionBossPlayer.GetComponent<PlayerInteractor>() : null;
        // These region fights have no required interactions. Suppress ordinary service
        // access as well as its prompt, using the existing owner-scoped input authority.
        regionBossInteractor?.SetExternalInputLocked(this, true);
        regionBossRun = RunManager.Instance;
        if (regionBossPlayer != null) regionBossPlayer.Died += ClearRegionBossPresentation;
        if (regionBossRun != null) regionBossRun.RunEnded += HandleRegionBossRunEnded;

        // Map generation is complete before a boss intro. Snapshot once, never scan in Update.
        foreach (var npc in FindObjectsByType<FieldNpcObjective>(FindObjectsSortMode.None))
            foreach (var visual in npc.GetComponentsInChildren<Renderer>(true))
            {
                hiddenNpcRenderers.Add(new KeyValuePair<Renderer, bool>(visual, visual.forceRenderingOff));
                visual.forceRenderingOff = true;
            }
    }

    public void ShowBossCommunication(string message, float duration = 1.4f)
    {
        if (IsRegionBossPresentationActive) warningMessageUI?.ShowBossCritical(message, duration);
    }

    private void HandleRegionBossRunEnded(RunResultData _) => ClearRegionBossPresentation();
    private void ClearRegionBossPresentation()
    {
        if (!IsRegionBossPresentationActive) return;
        regionBossOwners.Clear();
        EndRegionBossPresentation();
    }

    private void EndRegionBossPresentation()
    {
        if (regionBossPlayer != null) regionBossPlayer.Died -= ClearRegionBossPresentation;
        if (regionBossRun != null) regionBossRun.RunEnded -= HandleRegionBossRunEnded;
        if (regionBossInteractor != null) regionBossInteractor.SetExternalInputLocked(this, false);
        regionBossInteractor = null;
        regionBossPlayer = null; regionBossRun = null;
        if (regionBossPrompt != null) regionBossPrompt.SetRegionBossSuppressed(false);
        regionBossPrompt = null;
        if (warningMessageUI != null) warningMessageUI.SetRegionBossPriority(false);
        BossHealthBarUI.Instance?.SetRegionPresentation(false);
        for (int i = 0; i < hiddenNpcRenderers.Count; i++)
            if (hiddenNpcRenderers[i].Key != null) hiddenNpcRenderers[i].Key.forceRenderingOff = hiddenNpcRenderers[i].Value;
        hiddenNpcRenderers.Clear();
        SetOperationBriefingSuppressed(regionBossOwners, false);
        // Recompute relevance from the existing Core tracking authority, never force it on.
        if (isActiveAndEnabled) RefreshObjectiveProgress();
    }
}
