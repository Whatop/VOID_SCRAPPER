using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RewardCapsuleState
{
    Unconfigured = 0,
    Landing = 1,
    Closed = 2,
    Opening = 3,
    WaitingForChoice = 4,
    Closing = 5,
    Completed = 6
}

public enum RewardCapsulePayloadType
{
    None = 0,
    NpcRescue = 1,
    BossReward = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RewardCapsule : MonoBehaviour, IInteractable
{
    [Header("Animation Frames")]
    [Tooltip("0번이 완전히 열린 상태, 마지막 프레임이 완전히 닫힌 상태여야 합니다.")]
    [SerializeField] private Sprite[] framesOpenToClosed;
    [SerializeField] private SpriteRenderer capsuleRenderer;
    [SerializeField] private string resourcesFramesPath = "RewardCapsule/Frames";
    [SerializeField, Min(0.01f)] private float frameDuration = 0.055f;
    [SerializeField] private bool animateWithUnscaledTime = true;

    [Header("Landing")]
    [SerializeField] private bool playLandingAnimation = true;
    [SerializeField, Min(0f)] private float landingHeight = 2.25f;
    [SerializeField, Min(0.01f)] private float landingDuration = 0.45f;
    [SerializeField, Min(0f)] private float landingSquashAmount = 0.08f;
    [SerializeField, Min(0f)] private float landingSquashDuration = 0.10f;

    [Header("Claim")]
    [SerializeField, Min(0f)] private float openHoldDuration = 0.25f;
    [SerializeField] private bool closeAfterClaim = true;
    [SerializeField] private bool destroyAfterClaim = true;
    [SerializeField, Min(0f)] private float destroyDelay = 0.05f;
    [SerializeField, Min(0)] private int bossFallbackTuningChips = 1;

    [Header("Interaction")]
    [SerializeField] private string closedInteractionText = "보상 캡슐 개방";
    [SerializeField] private Collider2D interactionCollider;

    [Header("Runtime References")]
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private RunLevelTraitSelectionUI rewardChoiceUI;

    [Header("Debug")]
    [SerializeField] private bool logFlow;

    private RewardCapsuleState state = RewardCapsuleState.Unconfigured;
    private RewardCapsulePayloadType payloadType = RewardCapsulePayloadType.None;

    private int npcTuningChipAmount;
    private string npcObjectiveId;

    private int bossChoiceCount = 3;
    private bool bossRareGuaranteed;

    private Action claimedCallback;
    private Coroutine activeRoutine;
    private bool configured;
    private bool lifecycleStarted;
    private bool rewardGranted;
    private Vector3 baseScale = Vector3.one;

    public RewardCapsuleState State => state;
    public RewardCapsulePayloadType PayloadType => payloadType;

    public string InteractionText => closedInteractionText;

    private void Reset()
    {
        capsuleRenderer = GetComponentInChildren<SpriteRenderer>(true);
        interactionCollider = GetComponent<Collider2D>();
        radarTarget = GetComponent<RadarTarget>();

        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveFrames();
        baseScale = transform.localScale;
        SetClosedFrame();
        SetInteractionEnabled(false);
    }

    private void OnEnable()
    {
        if (configured && !lifecycleStarted)
        {
            StartLifecycle();
        }
    }

    private void Start()
    {
        if (configured && !lifecycleStarted)
        {
            StartLifecycle();
        }
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
    }

    public bool ConfigureNpcRescueReward(
        int tuningChipAmount,
        string objectiveId,
        Action onClaimed = null)
    {
        if (configured)
        {
            return false;
        }

        payloadType = RewardCapsulePayloadType.NpcRescue;
        npcTuningChipAmount = Mathf.Max(0, tuningChipAmount);
        npcObjectiveId = objectiveId;
        claimedCallback = onClaimed;
        configured = true;

        StartLifecycleIfPossible();
        return true;
    }

    public bool ConfigureBossReward(
        int requestedChoiceCount,
        bool rareGuaranteed,
        Action onClaimed = null)
    {
        if (configured)
        {
            return false;
        }

        payloadType = RewardCapsulePayloadType.BossReward;
        bossChoiceCount = Mathf.Max(1, requestedChoiceCount);
        bossRareGuaranteed = rareGuaranteed;
        claimedCallback = onClaimed;
        configured = true;

        StartLifecycleIfPossible();
        return true;
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null &&
               configured &&
               state == RewardCapsuleState.Closed &&
               RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        BeginOpen();
    }

    private void StartLifecycleIfPossible()
    {
        if (isActiveAndEnabled && !lifecycleStarted)
        {
            StartLifecycle();
        }
    }

    private void StartLifecycle()
    {
        if (lifecycleStarted)
        {
            return;
        }

        lifecycleStarted = true;
        ResolveReferences();
        ResolveFrames();
        SetClosedFrame();

        if (radarTarget != null)
        {
            radarTarget.SetMarkerType(RadarMarkerType.RewardObject);
            radarTarget.SetVisible(true);
        }

        activeRoutine = StartCoroutine(playLandingAnimation
            ? LandingRoutine()
            : BecomeInteractableRoutine());
    }

    private IEnumerator LandingRoutine()
    {
        state = RewardCapsuleState.Landing;
        SetInteractionEnabled(false);

        Vector3 targetPosition = transform.position;
        Vector3 startPosition = targetPosition + Vector3.up * Mathf.Max(0f, landingHeight);
        transform.position = startPosition;
        transform.localScale = baseScale;

        float duration = Mathf.Max(0.01f, landingDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
            yield return null;
        }

        transform.position = targetPosition;
        AudioManager.PlayAt(SoundEventIds.EventStart, transform.position);

        if (landingSquashAmount > 0f && landingSquashDuration > 0f)
        {
            Vector3 squashedScale = new Vector3(
                baseScale.x * (1f + landingSquashAmount),
                baseScale.y * (1f - landingSquashAmount),
                baseScale.z
            );

            transform.localScale = squashedScale;
            yield return WaitFlexible(landingSquashDuration);
            transform.localScale = baseScale;
        }

        state = RewardCapsuleState.Closed;
        SetInteractionEnabled(true);
        activeRoutine = null;
        Log("Landing complete");
    }

    private IEnumerator BecomeInteractableRoutine()
    {
        yield return null;
        state = RewardCapsuleState.Closed;
        transform.localScale = baseScale;
        SetInteractionEnabled(true);
        activeRoutine = null;
    }

    private void BeginOpen()
    {
        if (activeRoutine != null || state != RewardCapsuleState.Closed)
        {
            return;
        }

        activeRoutine = StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        state = RewardCapsuleState.Opening;
        SetInteractionEnabled(false);
        AudioManager.PlayAt(SoundEventIds.UiUnlock, transform.position);

        yield return PlayFrames(GetLastFrameIndex(), 0);
        SetOpenFrame();

        switch (payloadType)
        {
            case RewardCapsulePayloadType.NpcRescue:
                GrantNpcRescueReward();
                activeRoutine = StartCoroutine(CloseAndCompleteRoutine());
                yield break;

            case RewardCapsulePayloadType.BossReward:
                state = RewardCapsuleState.WaitingForChoice;
                activeRoutine = null;

                if (!TryShowBossRewardChoices())
                {
                    ApplyBossFallbackReward();
                    BeginCloseAndComplete();
                }
                yield break;

            default:
                ShowWarning("보상 캡슐에 보상 데이터가 없습니다.");
                activeRoutine = StartCoroutine(CloseAndCompleteRoutine());
                yield break;
        }
    }

    private bool TryShowBossRewardChoices()
    {
        ResolveReferences();

        if (rewardChoiceUI == null)
        {
            return false;
        }

        return rewardChoiceUI.ShowBossRewardChoices(
            bossChoiceCount,
            bossRareGuaranteed,
            transform.position,
            result =>
            {
                if (!result.Success)
                {
                    return;
                }

                BeginCloseAndComplete();
            }
        );
    }

    private void GrantNpcRescueReward()
    {
        if (rewardGranted)
        {
            return;
        }

        rewardGranted = true;

        if (npcTuningChipAmount > 0 &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.AddCurrency(CurrencyType.TuningChips, npcTuningChipAmount);
            AudioManager.Play(SoundEventIds.PickupTuningChip);
        }

        string resolvedObjectiveId = string.IsNullOrWhiteSpace(npcObjectiveId)
            ? $"npc_capsule_{GetInstanceID()}"
            : npcObjectiveId;

        ExpeditionObjectiveDirector.Instance?.RegisterObjective(
            resolvedObjectiveId,
            HighValueObjectiveSource.NpcRescue
        );

        ShowWarning($"보상 캡슐 회수 · 튜닝 칩 +{npcTuningChipAmount}");
    }

    private void ApplyBossFallbackReward()
    {
        ResolveReferences();

        if (rewardChoiceUI != null &&
            rewardChoiceUI.TryBuildRewardOptions(
                SpecialRewardMode.Mixed,
                1,
                bossRareGuaranteed ? RunRewardRarity.Rare : RunRewardRarity.Common,
                bossRareGuaranteed,
                out List<RunRewardOption> options) &&
            options != null &&
            options.Count > 0)
        {
            RunRewardChoiceResult result = RunRewardChoiceApplier.Apply(options[0], transform.position);

            if (result.Success)
            {
                ShowWarning($"보스 회수품 획득: {options[0].DisplayName}");
                return;
            }
        }

        if (bossFallbackTuningChips > 0 &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.AddCurrency(CurrencyType.TuningChips, bossFallbackTuningChips);
            ShowWarning($"보스 회수품 대체 지급 · 튜닝 칩 +{bossFallbackTuningChips}");
        }
    }

    private void BeginCloseAndComplete()
    {
        if (state == RewardCapsuleState.Closing || state == RewardCapsuleState.Completed)
        {
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(CloseAndCompleteRoutine());
    }

    private IEnumerator CloseAndCompleteRoutine()
    {
        state = RewardCapsuleState.Closing;
        SetInteractionEnabled(false);

        if (openHoldDuration > 0f)
        {
            yield return WaitFlexible(openHoldDuration);
        }

        if (closeAfterClaim)
        {
            AudioManager.PlayAt(SoundEventIds.UiPanelClose, transform.position);
            yield return PlayFrames(0, GetLastFrameIndex());
            SetClosedFrame();
        }

        state = RewardCapsuleState.Completed;
        activeRoutine = null;

        if (radarTarget != null)
        {
            radarTarget.SetVisible(false);
        }

        AudioManager.PlayAt(SoundEventIds.EventComplete, transform.position);

        Action callback = claimedCallback;
        claimedCallback = null;
        callback?.Invoke();

        if (destroyAfterClaim)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
    }

    private IEnumerator PlayFrames(int fromIndex, int toIndex)
    {
        if (framesOpenToClosed == null || framesOpenToClosed.Length <= 0)
        {
            yield break;
        }

        int from = Mathf.Clamp(fromIndex, 0, framesOpenToClosed.Length - 1);
        int to = Mathf.Clamp(toIndex, 0, framesOpenToClosed.Length - 1);
        int step = from <= to ? 1 : -1;

        for (int i = from; ; i += step)
        {
            SetFrame(i);

            if (i == to)
            {
                break;
            }

            yield return WaitFlexible(frameDuration);
        }
    }

    private IEnumerator WaitFlexible(float duration)
    {
        float targetDuration = Mathf.Max(0f, duration);
        float elapsed = 0f;

        while (elapsed < targetDuration)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }
    }

    private float GetDeltaTime()
    {
        return animateWithUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private void ResolveReferences()
    {
        if (capsuleRenderer == null)
        {
            capsuleRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (capsuleRenderer == null)
        {
            capsuleRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider2D>();
        }

        if (interactionCollider == null)
        {
            interactionCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        interactionCollider.isTrigger = true;

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        if (radarTarget == null)
        {
            radarTarget = gameObject.AddComponent<RadarTarget>();
        }

        radarTarget.SetMarkerType(RadarMarkerType.RewardObject);

        if (rewardChoiceUI == null)
        {
            rewardChoiceUI = FindFirstObjectByType<RunLevelTraitSelectionUI>();
        }
    }

    private void ResolveFrames()
    {
        if (HasUsableFrames())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(resourcesFramesPath))
        {
            return;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>(resourcesFramesPath);

        if (loaded == null || loaded.Length <= 0)
        {
            return;
        }

        Array.Sort(loaded, CompareFrameSprites);
        framesOpenToClosed = loaded;
    }

    private bool HasUsableFrames()
    {
        if (framesOpenToClosed == null || framesOpenToClosed.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < framesOpenToClosed.Length; i++)
        {
            if (framesOpenToClosed[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareFrameSprites(Sprite a, Sprite b)
    {
        int aIndex = ExtractTrailingNumber(a != null ? a.name : string.Empty);
        int bIndex = ExtractTrailingNumber(b != null ? b.name : string.Empty);
        int indexCompare = aIndex.CompareTo(bIndex);

        if (indexCompare != 0)
        {
            return indexCompare;
        }

        return string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty);
    }

    private static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return int.MaxValue;
        }

        int end = value.Length - 1;

        while (end >= 0 && !char.IsDigit(value[end]))
        {
            end--;
        }

        if (end < 0)
        {
            return int.MaxValue;
        }

        int start = end;

        while (start > 0 && char.IsDigit(value[start - 1]))
        {
            start--;
        }

        return int.TryParse(value.Substring(start, end - start + 1), out int result)
            ? result
            : int.MaxValue;
    }

    private int GetLastFrameIndex()
    {
        return framesOpenToClosed != null && framesOpenToClosed.Length > 0
            ? framesOpenToClosed.Length - 1
            : 0;
    }

    private void SetOpenFrame()
    {
        SetFrame(0);
    }

    private void SetClosedFrame()
    {
        SetFrame(GetLastFrameIndex());
    }

    private void SetFrame(int index)
    {
        if (capsuleRenderer == null ||
            framesOpenToClosed == null ||
            framesOpenToClosed.Length <= 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, framesOpenToClosed.Length - 1);
        Sprite frame = framesOpenToClosed[safeIndex];

        if (frame != null)
        {
            capsuleRenderer.sprite = frame;
        }
    }

    private void SetInteractionEnabled(bool value)
    {
        if (interactionCollider != null)
        {
            interactionCollider.enabled = value;
        }
    }

    private void ShowWarning(string message)
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowWarning(message);
        }
    }

    private void Log(string message)
    {
        if (logFlow)
        {
            Debug.Log($"[{name}] RewardCapsule: {message}", this);
        }
    }
}
