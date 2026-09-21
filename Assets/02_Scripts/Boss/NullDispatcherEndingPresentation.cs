using System;
using System.Collections;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.Events;

// Presentation only. BossDummyController remains the sole final-victory authority.
[DisallowMultipleComponent]
public sealed class NullDispatcherEndingPresentation : MonoBehaviour
{
    public const string ConversationTitle = "FINAL_NullDispatcherTermination";

    [SerializeField] private DialogueStoryEntryPoint dialogueEntry;
    [SerializeField] private SpriteRenderer innerCore;
    [SerializeField] private GameObject outerShellRoot;
    [SerializeField] private SpriteRenderer shutdownPulse;
    [SerializeField, Min(0.1f)] private float openingSeconds = 1.2f;
    [SerializeField, Min(0.1f)] private float shutdownSeconds = 1.3f;
    [SerializeField] private UnityEvent networkShutdown;

    private PlayerController2D player;
    private PlayerWeaponController weapon;
    private GungeonStyleCamera2D cameraRig;
    private RunManager run;
    private GameStateManager state;
    private ExpeditionHUD hud;
    private Sequence visual;
    private bool started;
    private bool playing;
    private bool ownsInput;
    private bool awaitingDialogue;
    private bool naturalCompletion;
    private bool stabilizationStarted;
    private bool visualStateCached;
    private int dialogueInterruptions;
    private Vector3 coreScale;
    private Vector3 pulseScale;
    private Color coreColor;
    private Color pulseColor;

    public bool Completed { get; private set; }
    public bool Canceled { get; private set; }
    public bool UsedDialogueFallback { get; private set; }
    public bool IsPlaying => playing;
    public bool NaturallyCompletedDialogue => naturalCompletion;

    public IEnumerator PlayRoutine()
    {
        if (Canceled) yield break;
        if (started)
        {
            while (playing) yield return null;
            yield break;
        }
        BeginPresentation();
        try
        {
            if (!CanContinue()) yield break;
            BeginVisual(false);
            while (VisualPlaying() && CanContinue()) yield return null;
            if (!CanContinue()) yield break;

            yield return PlayDialogue();
            if (!CanContinue()) yield break;
            BeginStabilization();
            while (VisualPlaying() && CanContinue()) yield return null;
            if (!CanContinue()) yield break;
            CompletePresentation();
        }
        finally
        {
            if (!Completed) Canceled = true;
            playing = false;
            ReleaseOwnership();
        }
    }

    private void BeginPresentation()
    {
        if (started) return;
        started = true;
        playing = true;
        run = RunManager.Instance;
        state = GameStateManager.Instance;
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (player != null) weapon = player.GetComponent<PlayerWeaponController>();
        if (cameraRig == null) cameraRig = GungeonStyleCamera2D.Instance;
        hud = FindFirstObjectByType<ExpeditionHUD>();
        if (run != null) run.RunEnded += HandleRunEnded;
        if (state != null) state.StateChanged += HandleStateChanged;
        if (dialogueEntry != null) dialogueEntry.Completed += HandleDialogueCompleted;
        if (!visualStateCached)
        {
            visualStateCached = true;
            if (innerCore != null) { coreScale = innerCore.transform.localScale; coreColor = innerCore.color; }
            if (shutdownPulse != null) { pulseScale = shutdownPulse.transform.localScale; pulseColor = shutdownPulse.color; }
        }
        // Death breakup hides the renderer; the surviving administrative core is the ending focus.
        if (outerShellRoot != null) outerShellRoot.SetActive(false);
        if (innerCore != null)
        {
            innerCore.gameObject.SetActive(true);
            innerCore.enabled = true;
            innerCore.color = coreColor;
        }
        AcquireInput();
    }

    private void AcquireInput()
    {
        if (ownsInput) return;
        ownsInput = true;
        player?.GetComponent<PlayerDash>()?.CancelActiveDash();
        player?.SetExternalControlLocked(this, true);
        weapon?.SetExternalInputLocked(this, true);
        if (cameraRig != null)
        {
            cameraRig.SetCinematicInputOffsetLocked(this, true);
            Vector3 center = innerCore != null ? innerCore.transform.position : transform.position;
            if (player != null) center = Vector3.Lerp(center, player.transform.position, 0.35f);
            cameraRig.TryBeginOwnedCinematicFocusBlend(this, center, 0.5f, null, out _);
        }
    }

    private IEnumerator PlayDialogue()
    {
        int startFailures = 0;
        var retryDelay = new WaitForSecondsRealtime(0.75f);
        while (CanContinue() && !naturalCompletion)
        {
            // A foreign conversation keeps its ownership and its reading time.
            while (DialogueManager.isConversationActive && CanContinue()) yield return null;
            if (!CanContinue()) yield break;
            awaitingDialogue = true;
            bool accepted = false;
            try
            {
                accepted = dialogueEntry != null && dialogueEntry.TryStartConversation(
                    ConversationTitle, player != null ? player.transform : null, transform);
            }
            catch (Exception exception)
            {
                Debug.LogError("[NullDispatcherEndingPresentation] Dialogue start exception: " + exception.Message, this);
            }
            if (!accepted)
            {
                awaitingDialogue = false;
                if (++startFailures >= 3)
                {
                    AllowDialogueFallback("Ending conversation could not start after three attempts. Check the installed database/entry binding.");
                    yield break;
                }
                yield return retryDelay;
                continue;
            }
            // No reading timeout. Our completion callback checks the bridge's terminal marker.
            while (dialogueEntry != null && dialogueEntry.IsRunning && CanContinue()) yield return null;
            awaitingDialogue = false;
            if (!CanContinue() || naturalCompletion) yield break;
            if (!TryRestartInterruptedDialogue()) yield break;
            yield return retryDelay;
        }
    }

    private bool TryRestartInterruptedDialogue()
    {
        if (!CanContinue() || naturalCompletion || UsedDialogueFallback) return false;
        if (++dialogueInterruptions == 1) return true;
        AllowDialogueFallback("Ending conversation was interrupted again after its one restart.");
        return false;
    }

    private void HandleDialogueCompleted(DialogueStoryEntryPoint entry)
    {
        var bridge = DialogueManager.hasInstance
            ? DialogueManager.instance.GetComponent<DialoguePixelCrushersBridge>() : null;
        RecordDialogueCompletion(entry == dialogueEntry && bridge != null &&
            bridge.WasConversationCompletedNaturally(ConversationTitle));
    }

    private void RecordDialogueCompletion(bool natural)
    {
        if (playing && awaitingDialogue && !Canceled && natural) naturalCompletion = true;
    }

    private void AllowDialogueFallback(string reason)
    {
        if (!playing || Canceled || naturalCompletion) return;
        UsedDialogueFallback = true;
        Debug.LogError("[NullDispatcherEndingPresentation] " + reason + " Finishing the shutdown safely; BossDummyController retains victory ownership.", this);
    }

    private void BeginStabilization()
    {
        if (stabilizationStarted || Canceled || (!naturalCompletion && !UsedDialogueFallback)) return;
        stabilizationStarted = true;
        networkShutdown?.Invoke();
        if (!CanContinue()) return;
        if (VoidScrapperLocalizationService.HasInstance)
            hud?.ShowCommunication(ShipCommunicationChannel.System,
                VoidScrapperLocalizationService.Instance.GetText("final.ending.network_closed"),
                ShipCommunicationSeverity.Confirmation, 2f);
        BeginVisual(true);
    }

    private void BeginVisual(bool shutdown)
    {
        visual?.Kill();
        visual = null;
        try
        {
            visual = DOTween.Sequence().SetUpdate(true);
            float duration = Mathf.Max(0.1f, shutdown ? shutdownSeconds : openingSeconds);
            if (innerCore != null)
            {
                visual.Append(innerCore.DOColor(shutdown ? Color.white : new Color(0.65f, 0.45f, 0.8f, 0.4f), duration * 0.45f));
                visual.Join(innerCore.transform.DOPunchScale(coreScale * 0.08f, duration * 0.45f, 5));
                visual.Append(innerCore.DOFade(shutdown ? 0f : 0.85f, duration * 0.55f));
            }
            if (shutdown && shutdownPulse != null)
            {
                shutdownPulse.enabled = true;
                shutdownPulse.color = new Color(1f, 1f, 1f, 0.7f);
                shutdownPulse.transform.localScale = pulseScale;
                visual.Insert(0f, shutdownPulse.transform.DOScale(pulseScale * 2.5f, duration));
                visual.Insert(0f, shutdownPulse.DOFade(0f, duration));
            }
            if (innerCore == null && (!shutdown || shutdownPulse == null)) visual.AppendInterval(duration);
        }
        catch (Exception exception)
        {
            visual?.Kill();
            visual = null;
            Debug.LogWarning("[NullDispatcherEndingPresentation] Visual unavailable: " + exception.Message, this);
        }
    }

    private bool VisualPlaying() => visual != null && visual.IsActive() && !visual.IsComplete();

    private bool CanContinue()
    {
        if (Canceled) return false;
        if (!Application.isPlaying) return true;
        if (!isActiveAndEnabled || run == null || !run.HasActiveRun || run.IsCompletingRun || player == null)
        {
            CancelPresentation();
            return false;
        }
        return true;
    }

    private void CompletePresentation()
    {
        if (Completed || Canceled || !stabilizationStarted) return;
        Completed = true;
        playing = false;
        if (innerCore != null) innerCore.enabled = false;
        ReleaseOwnership();
    }

    public void CancelPresentation()
    {
        if (!Completed) Canceled = true;
        playing = false;
        awaitingDialogue = false;
        ReleaseOwnership();
    }

    private void ReleaseOwnership()
    {
        visual?.Kill();
        visual = null;
        if (visualStateCached)
        {
            if (innerCore != null) innerCore.transform.localScale = coreScale;
            if (shutdownPulse != null)
            {
                shutdownPulse.enabled = false;
                shutdownPulse.transform.localScale = pulseScale;
                shutdownPulse.color = pulseColor;
            }
        }
        if (dialogueEntry != null)
        {
            dialogueEntry.Completed -= HandleDialogueCompleted;
            if (dialogueEntry.IsRunning && DialogueManager.isConversationActive &&
                DialogueManager.lastConversationStarted == ConversationTitle)
                DialogueManager.StopConversation();
        }
        if (run != null) run.RunEnded -= HandleRunEnded;
        if (state != null) state.StateChanged -= HandleStateChanged;
        if (!ownsInput) return;
        ownsInput = false;
        player?.SetExternalControlLocked(this, false);
        weapon?.SetExternalInputLocked(this, false);
        if (cameraRig != null)
        {
            cameraRig.ReleaseOwnedCinematicFocus(this, true);
            cameraRig.SetCinematicInputOffsetLocked(this, false);
        }
    }

    private void HandleRunEnded(RunResultData _) => CancelPresentation();
    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.Boot || current == GameState.Settlement ||
            current == GameState.ExpeditionLoading || current == GameState.RunResult)
            CancelPresentation();
    }
    private void OnDisable() => CancelPresentation();
    private void OnDestroy() => CancelPresentation();
}
