using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ReinforcementPickup : MonoBehaviour, IInteractable
{
    public static event Action<ReinforcementPickup, float, bool> DismantleProgressChanged;

    [Header("Runtime")]
    [SerializeField] private ReinforcementDefinition reinforcementDefinition;
    [SerializeField] private int storedCharges = -1;

    [Header("Visual")]
    [Tooltip("장비 아이콘 SpriteRenderer")]
    [SerializeField] private SpriteRenderer iconRenderer;

    [Tooltip("아이콘 뒤 버블/테두리 SpriteRenderer. 등급 색상이 여기에 들어갑니다.")]
    [SerializeField] private SpriteRenderer rarityBubbleRenderer;

    [SerializeField] private Sprite fallbackSprite;

    [Header("Interaction Text")]
    [SerializeField] private string acquireText = "획득";
    [SerializeField] private string exchangeText = "교체";
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

    [Header("Lifetime")]
    [SerializeField] private bool releaseWhenNoItem = true;

    private float blockTimer;
    private float dismantleTimer;
    private Collider2D pickupCollider;
    private GameObject currentInteractor;

    public ReinforcementDefinition ReinforcementDefinition => reinforcementDefinition;
    public int StoredCharges => storedCharges;
    public bool CanDismantle => allowDismantle && reinforcementDefinition != null;

    public string InteractionText
    {
        get
        {
            if (reinforcementDefinition == null)
            {
                return "장비 없음";
            }

            string primaryAction = ResolveController(currentInteractor) != null && ResolveController(currentInteractor).HasEquipment
                ? exchangeText
                : acquireText;

            string title = $"[{reinforcementDefinition.GetRarityText()}] {reinforcementDefinition.DisplayName}";
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

    public void Setup(ReinforcementDefinition definition, float repickupDelay)
    {
        Initialize(definition, -1, repickupDelay);
    }

    public void Setup(ReinforcementDefinition definition, int charges, float repickupDelay)
    {
        Initialize(definition, charges, repickupDelay);
    }

    public void Initialize(ReinforcementDefinition definition, int charges, float blockSeconds = 0.5f)
    {
        CacheReferences();

        reinforcementDefinition = definition;
        storedCharges = charges;
        pickupBlockSeconds = Mathf.Max(0f, blockSeconds);
        blockTimer = pickupBlockSeconds;
        dismantleTimer = 0f;

        ConfigureCollider();
        ApplyVisual();
        RaiseDismantleProgress(0f, false);

        if (releaseWhenNoItem && reinforcementDefinition == null)
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

        if (reinforcementDefinition == null)
        {
            return false;
        }

        return ResolveController(interactor) != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        PlayerReinforcementController controller = ResolveController(interactor);

        if (controller == null)
        {
            return;
        }

        ReinforcementDefinition pickedDefinition = reinforcementDefinition;
        int pickedCharges = storedCharges;

        ReinforcementDefinition previousDefinition = controller.EquippedDefinition;
        int previousCharges = controller.CurrentCharges;

        bool equipped = controller.EquipWithoutDropping(pickedDefinition, pickedCharges, true);

        if (!equipped)
        {
            return;
        }

        if (previousDefinition != null)
        {
            Initialize(previousDefinition, previousCharges, pickupBlockSeconds);
        }
        else
        {
            reinforcementDefinition = null;
            storedCharges = -1;
            ApplyVisual();
            ReleaseSelf();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerReinforcementController controller = other.GetComponentInParent<PlayerReinforcementController>();
        if (controller != null)
        {
            currentInteractor = controller.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || currentInteractor == null)
        {
            return;
        }

        PlayerReinforcementController controller = other.GetComponentInParent<PlayerReinforcementController>();
        if (controller != null && controller.gameObject == currentInteractor)
        {
            currentInteractor = null;
            CancelDismantleHold();
        }
    }

    private void UpdateDismantleInput()
    {
        if (!allowDismantle || reinforcementDefinition == null || currentInteractor == null || GameplayPauseManager.IsPaused)
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
        if (reinforcementDefinition == null)
        {
            CancelDismantleHold();
            return;
        }

        int amount = reinforcementDefinition.DismantleScrapReward;
        ShopRunBridge.AddCurrency(dismantleCurrency, amount);

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null)
        {
            hud.ShowWarning($"{reinforcementDefinition.DisplayName} 분해: {dismantleCurrency} +{amount}");
        }

        reinforcementDefinition = null;
        storedCharges = -1;
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
            pickupCollider.enabled = reinforcementDefinition != null || !releaseWhenNoItem;
        }
    }

    private PlayerReinforcementController ResolveController(GameObject interactor)
    {
        if (interactor == null)
        {
            return null;
        }

        PlayerReinforcementController controller = interactor.GetComponentInParent<PlayerReinforcementController>();

        if (controller != null)
        {
            return controller;
        }

        return interactor.GetComponentInChildren<PlayerReinforcementController>(true);
    }

    private void ApplyVisual()
    {
        Sprite sprite = reinforcementDefinition != null && reinforcementDefinition.Icon != null
            ? reinforcementDefinition.Icon
            : fallbackSprite;

        if (iconRenderer != null)
        {
            iconRenderer.sprite = sprite;
            iconRenderer.enabled = sprite != null && (reinforcementDefinition != null || !releaseWhenNoItem);
        }

        if (rarityBubbleRenderer != null)
        {
            bool visible = reinforcementDefinition != null;
            rarityBubbleRenderer.enabled = visible;
            rarityBubbleRenderer.color = visible ? reinforcementDefinition.GetRarityColor() : Color.white;
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
