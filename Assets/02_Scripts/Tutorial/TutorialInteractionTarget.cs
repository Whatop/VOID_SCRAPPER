using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialInteractionTarget : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionText = "Inspect Signal Relay";
    [SerializeField] private bool interactionEnabled;
    [SerializeField] private GameObject inactiveVisual;
    [SerializeField] private GameObject activatedVisual;

    public string InteractionText => interactionText;
    public bool InteractionEnabled => interactionEnabled;
    public bool IsActivated { get; private set; }

    private void OnEnable()
    {
        RefreshVisuals();
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactionEnabled && !IsActivated && isActiveAndEnabled;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        IsActivated = true;
        interactionEnabled = false;
        RefreshVisuals();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled && !IsActivated;
    }

    public void SetInteractionText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            interactionText = text;
        }
    }

    public void ResetTarget()
    {
        IsActivated = false;
        interactionEnabled = false;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!IsActivated);
        }

        if (activatedVisual != null)
        {
            activatedVisual.SetActive(IsActivated);
        }
    }
}

[DisallowMultipleComponent]
public sealed class TutorialPurpleCoreDamageReceiver : MonoBehaviour, IProjectileDamageReceiver
{
    private readonly TutorialPurpleCoreDamageProgress progress =
        new TutorialPurpleCoreDamageProgress();

    private TutorialFlowController tutorialController;
    private float forcedInteractionThreshold = 15f;
    private float contributionWindowSeconds = 0.12f;
    private float contributionCap = 4f;
    private bool receivingEnabled;

    public float AccumulatedDamage => progress.AccumulatedDamage;
    public bool ReceivingEnabled => receivingEnabled;

    public void Configure(
        TutorialFlowController controller,
        float threshold,
        float duplicateWindowSeconds,
        float perWindowContributionCap)
    {
        tutorialController = controller;
        forcedInteractionThreshold = Mathf.Max(0.01f, threshold);
        contributionWindowSeconds = Mathf.Max(0f, duplicateWindowSeconds);
        contributionCap = Mathf.Max(0.01f, perWindowContributionCap);
    }

    public void SetReceivingEnabled(bool enabled)
    {
        receivingEnabled = enabled;
    }

    public void ResetProgress()
    {
        receivingEnabled = false;
        progress.Reset();
    }

    public bool TryReceiveProjectileDamage(in ProjectileDamageContext context)
    {
        if (!receivingEnabled ||
            tutorialController == null ||
            context.Owner != ProjectileOwner.Player ||
            context.SourceRoot == null ||
            (context.SourceRoot.GetComponentInParent<PlayerController2D>() == null &&
             context.SourceRoot.GetComponentInParent<IPlayerOwnedAlly>() == null) ||
            !progress.TryAddPlayerDamage(
                context.Owner,
                context.Damage,
                Time.unscaledTime,
                contributionWindowSeconds,
                contributionCap,
                forcedInteractionThreshold,
                out _))
        {
            return false;
        }

        tutorialController.HandlePurpleCorePlayerDamageFeedback(context.HitPoint);
        if (progress.ThresholdReached)
        {
            receivingEnabled = false;
            tutorialController.TryRequestPurpleCoreAcquisition(
                TutorialPurpleCoreActivationSource.PlayerDamage);
        }

        return true;
    }
}
