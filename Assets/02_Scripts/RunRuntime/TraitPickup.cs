using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class TraitPickup : MonoBehaviour, IInteractable
{
    public static event Action<TraitPickup, float, bool> DismantleProgressChanged;

    [Header("Runtime")]
    [SerializeField] private TraitDefinition traitDefinition;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private Sprite fallbackSprite;
    [SerializeField] private Color traitBubbleColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Header("Interaction Text")]
    [SerializeField] private string acquireText = "획득";
    [SerializeField] private string acquireBlockedText = "보유중";
    [SerializeField] private string dismantleText = "분해";
    [SerializeField] private string acquireKeyText = "E";
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

    private float blockTimer;
    private float dismantleTimer;
    private Collider2D pickupCollider;
    private GameObject currentInteractor;

    public TraitDefinition TraitDefinition => traitDefinition;
    public bool CanDismantle => allowDismantle && traitDefinition != null;

    public string InteractionText
    {
        get
        {
            if (traitDefinition == null)
            {
                return "특성 없음";
            }

            int currentLevel = RunRuntimeTraitStore.Instance.GetLevel(traitDefinition.TraitId);
            bool maxed = currentLevel >= traitDefinition.MaxLevel;
            string primaryAction = maxed
                ? acquireBlockedText
                : (currentLevel > 0 ? "강화" : acquireText);
            string title = traitDefinition.DisplayName;
            string actions = $"[{acquireKeyText}] {primaryAction}    {BuildDismantleActionText()}";

            return useTwoLinePrompt ? $"{title}\n{actions}" : $"{actions}  {title}";
        }
    }

    private string BuildDismantleActionText()
    {
        string keyText = string.IsNullOrWhiteSpace(dismantleKeyText) ? "G" : dismantleKeyText.Trim();
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
        ForceHoldDismantleSetting();
        CacheReferences();
        ConfigureCollider();
        ApplyVisual();
    }

    private void OnEnable()
    {
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
        RaiseDismantleProgress(0f, false);
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

        traitDefinition = definition;
        pickupBlockSeconds = Mathf.Max(0f, blockSeconds);
        blockTimer = pickupBlockSeconds;
        dismantleTimer = 0f;

        ConfigureCollider();
        ApplyVisual();
        RaiseDismantleProgress(0f, false);

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

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int previousLevel = store.GetLevel(traitDefinition.TraitId);
        int newLevel = store.AddOrUpgrade(traitDefinition);

        if (newLevel <= previousLevel)
        {
            ExpeditionHUD blockedHud = FindFirstObjectByType<ExpeditionHUD>();
            AudioManager.Play(SoundEventIds.ActionDenied);

            if (blockedHud != null)
            {
                blockedHud.ShowWarning($"최대 레벨 특성입니다: {traitDefinition.DisplayName}");
            }

            return;
        }

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.CurrentRun.AddTrait(traitDefinition.TraitId);
        }

        RunTraitEffectApplier applier = FindFirstObjectByType<RunTraitEffectApplier>();

        if (applier != null)
        {
            applier.ApplyTraitLevel(traitDefinition, newLevel);
        }
        else if (newLevel == 1)
        {
            ShopRuntimeEffectApplier.ApplyTraitImmediate(traitDefinition, playerObject);
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
        if (!allowDismantle || traitDefinition == null || currentInteractor == null || GameplayPauseManager.IsPaused)
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

        if (Keyboard.current == null)
        {
            CancelDismantleHold();
            return;
        }

        KeyControl key = Keyboard.current[dismantleKey];

        if (key == null)
        {
            CancelDismantleHold();
            return;
        }

        // 분해는 항상 홀드 입력만 허용한다.
        // G 즉시 입력은 Tab 상태창의 필드드랍 전용으로만 사용한다.
        if (!key.isPressed)
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
        if (traitDefinition == null)
        {
            CancelDismantleHold();
            return;
        }

        int amount = Mathf.Max(1, dismantleScrapReward);
        ShopRunBridge.AddCurrency(dismantleCurrency, amount);

        AudioManager.PlayAt(SoundEventIds.ReinforcementDrop, transform.position);

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null)
        {
            hud.ShowWarning($"{traitDefinition.DisplayName} 분해: {dismantleCurrency} +{amount}");
        }

        traitDefinition = null;
        ApplyVisual();
        RaiseDismantleProgress(1f, false);
        ReleaseSelf();
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
