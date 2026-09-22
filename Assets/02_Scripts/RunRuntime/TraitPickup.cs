using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TraitPickup : MonoBehaviour, IInteractable
{
    public static event Action<TraitPickup, float, bool> DismantleProgressChanged;
    public static event Action<TraitPickup> PresentationChanged;

    [Header("Runtime")]
    [SerializeField] private TraitDefinition traitDefinition;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private Sprite fallbackSprite;
    [SerializeField] private Color traitBubbleColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string dismantleActionName = "Dismantle";

    [Header("Interaction Text")]
    [SerializeField] private string acquireText = "획득";
    [SerializeField] private string acquireBlockedText = "보유중";
    [SerializeField] private string dismantleText = "분해";
    [SerializeField] private string acquireKeyText = "F";
    [SerializeField] private string dismantleKeyText = "G";
    [SerializeField] private bool useTwoLinePrompt = true;
    [SerializeField] private float pickupBlockSeconds = 0.5f;

    [Header("Dismantle")]
    [SerializeField] private bool allowDismantle = true;
    [SerializeField] private Key dismantleKey = Key.G;
    [Tooltip("분해는 항상 G를 길게 눌러야 합니다. 필드드랍만 G 즉시 입력을 사용합니다.")]
    [SerializeField] private bool dismantleRequiresHold = true;
    [SerializeField] private float dismantleHoldSeconds = 1.1f;
    [SerializeField] private CurrencyType dismantleCurrency = CurrencyType.ScrapParts;
    [SerializeField] private int dismantleScrapReward = 4;

    [Header("Lifetime")]
    [SerializeField] private bool releaseWhenNoItem = true;

    private InputAction dismantleAction;
    private float blockTimer;
    private float dismantleTimer;
    private Collider2D pickupCollider;
    private GameObject currentInteractor;
    private bool nonRefundableDeploymentGrant;

    public int DismantleRewardAmount => nonRefundableDeploymentGrant ? 0 : Mathf.Max(1, dismantleScrapReward);
    public bool IsNonRefundableDeploymentGrant => nonRefundableDeploymentGrant;
    public void SetDeploymentGrantProvenance(bool nonRefundable) => nonRefundableDeploymentGrant = nonRefundable;

    public TraitDefinition TraitDefinition => traitDefinition;
    public bool CanDismantle =>
        allowDismantle &&
        traitDefinition != null &&
        traitDefinition.CanDismantle;

    public string InteractionText
    {
        get
        {
            if (traitDefinition == null)
            {
                return "특성 없음";
            }

            int currentLevel = traitDefinition.IsPersistentStoryTrait && PermanentProgress.Instance != null
                ? (PermanentProgress.Instance.HasPersistentStoryTrait(traitDefinition) ? 1 : 0)
                : RunRuntimeTraitStore.Instance.GetLevel(traitDefinition.TraitId);
            bool maxed = currentLevel >= traitDefinition.MaxLevel;
            string primaryAction = maxed
                ? acquireBlockedText
                : (currentLevel > 0 ? "강화" : acquireText);
            string title = traitDefinition.DisplayName;
            string actions = $"[{ResolveAcquireKeyText()}] {primaryAction}";

            if (CanDismantle)
            {
                actions += $"    {BuildDismantleActionText()}";
            }

            return useTwoLinePrompt ? $"{title}\n{actions}" : $"{actions}  {title}";
        }
    }

    private string BuildDismantleActionText()
    {
        string keyText = ResolveDismantleKeyText();
        string label = string.IsNullOrWhiteSpace(dismantleText) ? "분해" : dismantleText.Trim();

        label = label.Replace($"[{keyText}]", string.Empty).Replace(keyText, string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(label))
        {
            label = "분해";
        }

        if (!label.Contains("길게") && !label.Contains("꾹"))
        {
            label = $"길게 {label}";
        }

        return $"[{keyText}] {label}";
    }

    private void ResolveInputActions()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);
    }

    private void BindDismantleInput()
    {
        ResolveInputActions();
        dismantleAction = InputBindingUtility.ResolveAction(
            inputActions,
            playerActionMapName,
            dismantleActionName
        );
    }

    private string ResolveAcquireKeyText()
    {
        ResolveInputActions();
        string fallback = string.IsNullOrWhiteSpace(acquireKeyText) ? "F" : acquireKeyText.Trim();
        return InputBindingUtility.GetDisplayString(
            inputActions,
            playerActionMapName,
            interactActionName,
            fallback
        );
    }

    private string ResolveDismantleKeyText()
    {
        ResolveInputActions();
        string fallback = string.IsNullOrWhiteSpace(dismantleKeyText) ? dismantleKey.ToString() : dismantleKeyText.Trim();
        return InputBindingUtility.GetDisplayString(
            inputActions,
            playerActionMapName,
            dismantleActionName,
            fallback
        );
    }

    private void ForceHoldDismantleSetting()
    {
        dismantleRequiresHold = true;
        dismantleHoldSeconds = Mathf.Max(0.1f, dismantleHoldSeconds);
    }

    private void OnValidate()
    {
        ForceHoldDismantleSetting();
    }

    private void Reset()
    {
        pickupCollider = GetComponent<Collider2D>();
        iconRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        ResolveInputActions();
        ForceHoldDismantleSetting();
        CacheReferences();
        ConfigureCollider();
        ApplyVisual();
    }

    private void OnEnable()
    {
        ResolveInputActions();
        BindDismantleInput();
        ForceHoldDismantleSetting();
        CacheReferences();
        ConfigureCollider();
        blockTimer = Mathf.Max(0f, pickupBlockSeconds);
        dismantleTimer = 0f;
        ApplyVisual();
        RaiseDismantleProgress(0f, false);
    }

    private void OnDisable()
    {
        dismantleAction = null;
        RaiseDismantleProgress(0f, false);
        PresentationChanged?.Invoke(this);
        currentInteractor = null;
        dismantleTimer = 0f;
    }

    private void Update()
    {
        if (blockTimer > 0f)
        {
            blockTimer -= Time.deltaTime;
        }

        UpdateDismantleInput();
    }

    public void Setup(TraitDefinition definition, float repickupDelay)
    {
        Initialize(definition, repickupDelay);
    }

    public void Initialize(TraitDefinition definition, float blockSeconds = 0.5f)
    {
        CacheReferences();

        nonRefundableDeploymentGrant = false; // Every pooled initialization replaces the prior item's provenance.
        traitDefinition = definition;
        pickupBlockSeconds = Mathf.Max(0f, blockSeconds);
        blockTimer = pickupBlockSeconds;
        dismantleTimer = 0f;

        ConfigureCollider();
        ApplyVisual();
        RaiseDismantleProgress(0f, false);
        PresentationChanged?.Invoke(this);

        if (releaseWhenNoItem && traitDefinition == null)
        {
            ReleaseSelf();
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        if (interactor != null)
        {
            currentInteractor = interactor;
        }

        if (blockTimer > 0f)
        {
            return false;
        }

        if (traitDefinition == null)
        {
            return false;
        }

        return ResolvePlayer(interactor) != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        GameObject playerObject = ResolvePlayer(interactor);

        if (playerObject == null)
        {
            return;
        }

        if (!RunTraitAcquisitionService.TryAcquireFieldPickup(
                traitDefinition,
                playerObject,
                nonRefundableDeploymentGrant,
                out int previousLevel,
                out int newLevel))
        {
            ExpeditionHUD blockedHud = FindFirstObjectByType<ExpeditionHUD>();
            AudioManager.Play(SoundEventIds.ActionDenied);
            blockedHud?.ShowWarning($"최대 레벨 특성입니다: {traitDefinition.DisplayName}");
            return;
        }

        AudioManager.PlayAt(SoundEventIds.TraitSelect, transform.position);


        ExpeditionHUD acquireHud = FindFirstObjectByType<ExpeditionHUD>();
        if (acquireHud != null)
        {
            string actionText = previousLevel > 0 ? "강화" : "획득";
            acquireHud.ShowWarning($"특성 {actionText}: {traitDefinition.DisplayName} Lv{newLevel}");
        }

        traitDefinition = null;
        ApplyVisual();
        PresentationChanged?.Invoke(this);
        ReleaseSelf();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        GameObject player = ResolvePlayer(other.gameObject);
        if (player != null)
        {
            currentInteractor = player;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || currentInteractor == null)
        {
            return;
        }

        GameObject player = ResolvePlayer(other.gameObject);
        if (player != null && player == currentInteractor)
        {
            currentInteractor = null;
            CancelDismantleHold();
        }
    }

    private void UpdateDismantleInput()
    {
        if (!CanDismantle || currentInteractor == null || GameplayPauseManager.IsPaused)
        {
            CancelDismantleHold();
            return;
        }

        if (blockTimer > 0f)
        {
            CancelDismantleHold();
            return;
        }

        if (!IsCurrentInteractionTarget())
        {
            CancelDismantleHold();
            return;
        }

        bool held;

        if (dismantleAction != null)
        {
            held = dismantleAction.IsPressed();
        }
        else
        {
            KeyControl key = Keyboard.current != null ? Keyboard.current[dismantleKey] : null;
            held = key != null && key.isPressed;
        }

        // 분해는 항상 홀드 입력만 허용한다.
        // 즉시 입력은 상태창의 필드드랍 전용으로만 사용한다.
        if (!held)
        {
            CancelDismantleHold();
            return;
        }

        dismantleTimer += Time.deltaTime;
        float ratio = Mathf.Clamp01(dismantleTimer / Mathf.Max(0.01f, dismantleHoldSeconds));
        RaiseDismantleProgress(ratio, true);

        if (ratio >= 1f)
        {
            CompleteDismantle();
        }
    }

    private bool IsCurrentInteractionTarget()
    {
        if (currentInteractor == null)
        {
            return false;
        }

        PlayerInteractor playerInteractor = currentInteractor.GetComponent<PlayerInteractor>();

        if (playerInteractor == null)
        {
            playerInteractor = currentInteractor.GetComponentInParent<PlayerInteractor>();
        }

        if (playerInteractor == null)
        {
            playerInteractor = currentInteractor.GetComponentInChildren<PlayerInteractor>(true);
        }

        if (playerInteractor == null)
        {
            return true;
        }

        return ReferenceEquals(playerInteractor.CurrentTarget, this);
    }

    private void CompleteDismantle()
    {
        if (!CanDismantle)
        {
            CancelDismantleHold();
            return;
        }

        int amount = DismantleRewardAmount;
        if (amount > 0) ShopRunBridge.AddCurrency(dismantleCurrency, amount);

        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, transform.position);

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null)
        {
            hud.ShowWarning($"{traitDefinition.DisplayName} 분해: {GetCurrencyDisplayName(dismantleCurrency)} +{amount}");
        }

        traitDefinition = null;
        ApplyVisual();
        RaiseDismantleProgress(1f, false);
        PresentationChanged?.Invoke(this);
        ReleaseSelf();
    }

    private static string GetCurrencyDisplayName(CurrencyType currencyType)
    {
        return currencyType switch
        {
            CurrencyType.Experience => "경험치",
            CurrencyType.Credits => "크레딧",
            CurrencyType.ScrapParts => "스크랩 부품",
            CurrencyType.CoreShards => "코어",
            CurrencyType.TuningChips => "튜닝 칩",
            CurrencyType.StabilizedAlloy => "안정화 합금",
            _ => currencyType.ToString()
        };
    }

    private void CancelDismantleHold()
    {
        if (dismantleTimer <= 0f)
        {
            return;
        }

        dismantleTimer = 0f;
        RaiseDismantleProgress(0f, false);
    }

    private void RaiseDismantleProgress(float ratio, bool active)
    {
        DismantleProgressChanged?.Invoke(this, Mathf.Clamp01(ratio), active);
    }

    private void CacheReferences()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider2D>();
        }

        if (iconRenderer == null)
        {
            iconRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void ConfigureCollider()
    {
        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
            pickupCollider.enabled = traitDefinition != null || !releaseWhenNoItem;
        }
    }

    private GameObject ResolvePlayer(GameObject interactor)
    {
        if (interactor == null)
        {
            return null;
        }

        PlayerReinforcementController reinforcementController = interactor.GetComponentInParent<PlayerReinforcementController>();
        if (reinforcementController != null)
        {
            return reinforcementController.gameObject;
        }

        PlayerInteractor playerInteractor = interactor.GetComponentInParent<PlayerInteractor>();
        if (playerInteractor != null)
        {
            return playerInteractor.gameObject;
        }

        return null;
    }

    private void ApplyVisual()
    {
        Sprite sprite = traitDefinition != null && traitDefinition.Icon != null
            ? traitDefinition.Icon
            : fallbackSprite;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = sprite;
            iconRenderer.enabled = sprite != null && (traitDefinition != null || !releaseWhenNoItem);
        }

        if (bubbleRenderer != null)
        {
            bool visible = traitDefinition != null;
            bubbleRenderer.enabled = visible;
            bubbleRenderer.color = traitBubbleColor;
        }
    }

    private void ReleaseSelf()
    {
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
