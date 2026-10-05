using UnityEngine;

// Presentation bindings only. Existing core owners retain all activation timing.
[DefaultExecutionOrder(-600)]
[DisallowMultipleComponent]
public sealed class CoreFamilyPresentation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private RuntimeAnimatorController[] familyControllers;
    [SerializeField] private Sprite[] idleSprites;
    [SerializeField] private CoreActivationPresentation activationPresentation;
    [SerializeField] private GameObject[] activationPulses;
    [SerializeField] private RuntimeAnimatorController grayController;
    [SerializeField] private Sprite grayIdle, grayIcon, grayShard;
    [SerializeField] private GameObject grayPulse;
    public static CoreFamilyPresentation Current { get; private set; }
    public bool UsesGrayCore { get; private set; }
    public Sprite GrayIcon => grayIcon;
    private void OnDestroy() { if (Current == this) Current = null; }
    private void Awake() { Current = this; ApplyDepth(RunManager.Instance != null && RunManager.Instance.HasActiveRun
        ? RunManager.Instance.CurrentRun.ExpeditionDepth : ExpeditionDepth.Normal); }
    public static int FamilyIndex(ExpeditionDepth depth) => depth switch
    {
        ExpeditionDepth.DeepZone1 => 1,
        ExpeditionDepth.DeepZone2 => 2,
        ExpeditionDepth.FinalNetwork => 3,
        _ => 0
    };
    public void ApplyGrayPickup(GameObject pickup)
    {
        if (pickup == null || grayShard == null) return;
        var renderer = pickup.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) renderer.sprite = grayShard;
    }
    public void ApplyDepth(ExpeditionDepth depth)
    {
        UsesGrayCore = depth == ExpeditionDepth.Normal && GetComponent<CoreObject>() != null && GetComponent<CoreObject>().UsesGrayCore;
        if (UsesGrayCore && grayController != null && grayIdle != null)
        {
            if (animator != null) animator.runtimeAnimatorController = grayController;
            if (body != null) body.sprite = grayIdle;
            activationPresentation?.SetApprovedPulse(grayPulse);
            return;
        }
        int i = FamilyIndex(depth);
        if (animator != null && familyControllers != null && i < familyControllers.Length && familyControllers[i] != null)
            animator.runtimeAnimatorController = familyControllers[i];
        if (body != null && idleSprites != null && i < idleSprites.Length && idleSprites[i] != null)
            body.sprite = idleSprites[i];
        if (activationPresentation != null && activationPulses != null && i < activationPulses.Length)
            activationPresentation.SetApprovedPulse(activationPulses[i]);
    }
}
