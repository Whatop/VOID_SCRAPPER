using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum RewardPickupKind
{
    Currency,
    Heal
}

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class RewardPickup : MonoBehaviour, IInteractable
{
    private static readonly HashSet<RewardPickup> ActiveRegistry = new HashSet<RewardPickup>();

    public static IEnumerable<RewardPickup> ActivePickups => ActiveRegistry;
    [Header("Runtime Reward")]
    [SerializeField] private RewardPickupKind pickupKind = RewardPickupKind.Currency;
    [SerializeField] private CurrencyType currencyType = CurrencyType.Experience;
    [SerializeField] private int amount = 1;
    [SerializeField] private float healAmount;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float attractRadius = 2.5f;
    [SerializeField] private float collectRadius = 0.35f;
    [SerializeField] private float attractSpeed = 7f;
    [SerializeField] private float maxAttractSpeed = 12f;
    [Tooltip("적재량 때문에 획득할 수 없는 스크랩/코어는 플레이어를 따라오지 않습니다.")]
    [SerializeField] private bool stopAttractionWhenCargoBlocked = true;
    [Tooltip("체력이 가득 찬 경우 회복 픽업도 플레이어를 따라오지 않고 월드에 남습니다.")]
    [SerializeField] private bool stopHealAttractionAtFullHealth = true;
    [FormerlySerializedAs("cargoBlockedStopSpeed")]
    [SerializeField] private float blockedAttractionStopSpeed = 10f;

    [Header("Enemy Collection Protection")]
    [Tooltip("드랍 직후 이 시간 동안은 플레이어만 회수할 수 있습니다.")]
    [SerializeField] private float enemyCollectionProtectionDuration = 2f;

    [Header("Motion")]
    [SerializeField] private float driftDamping = 5f;
    [SerializeField] private float lifeTime = 30f;

    [Header("Visual Optional")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite experienceSprite;
    [SerializeField] private Sprite creditsSprite;
    [SerializeField] private Sprite scrapSprite;
    [SerializeField] private Sprite coreShardSprite;
    [SerializeField] private Sprite stabilizedAlloySprite;
    [SerializeField] private Sprite tuningChipSprite;
    [SerializeField] private Sprite healSprite;
    [SerializeField] private Color stabilizedAlloySpriteColor = new Color(0.82f, 0.95f, 1f, 1f);

    [Header("Visual Presentation Optional")]
    [Tooltip("Collider와 분리된 자식 Transform을 연결하세요. 비워두거나 Root를 연결하면 부유/회전은 적용하지 않습니다.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("공용 원형 버블/테두리 SpriteRenderer. 재화 종류에 따라 색상이 바뀝니다.")]
    [SerializeField] private SpriteRenderer bubbleRenderer;
    [SerializeField] private bool animateVisual = true;
    [SerializeField] private float bobAmplitude = 0.055f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private float spinSpeed;
    [SerializeField] private float pulseScaleAmount = 0.055f;
    [SerializeField] private float pulseSpeed = 3.2f;

    [Header("Visual Scale By Type")]
    [SerializeField] private float experienceVisualScale = 0.5f;
    [SerializeField] private float creditsVisualScale = 0.56f;
    [SerializeField] private float scrapVisualScale = 0.58f;
    [SerializeField] private float coreVisualScale = 0.78f;
    [SerializeField] private float stabilizedAlloyVisualScale = 0.62f;
    [SerializeField] private float tuningChipVisualScale = 0.66f;
    [SerializeField] private float healVisualScale = 0.64f;

    [Header("Bubble Color By Type")]
    [SerializeField] private Color experienceBubbleColor = new Color(0.45f, 0.85f, 1f, 0.8f);
    [SerializeField] private Color creditsBubbleColor = new Color(1f, 0.78f, 0.12f, 0.85f);
    [SerializeField] private Color scrapBubbleColor = new Color(0.9f, 0.42f, 0.14f, 0.85f);
    [SerializeField] private Color coreBubbleColor = new Color(1f, 0.65f, 0.12f, 0.95f);
    [SerializeField] private Color stabilizedAlloyBubbleColor = new Color(0.72f, 0.9f, 0.96f, 0.9f);
    [SerializeField] private Color tuningChipBubbleColor = new Color(0.25f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color healBubbleColor = new Color(0.35f, 1f, 0.35f, 0.85f);

    private Rigidbody2D rb;
    private Collider2D pickupCollider;
    private Transform player;
    private PlayerRuntimeBonusState playerBonusState;
    private PlayerCargoController playerCargoController;
    private float cargoBlockedWarningTimer;
    private float playerCollectionBlockTimer;
    private bool preserveExactCargoAmount;

    private Vector2 currentVelocity;
    private float lifeTimer;
    private float playerFindTimer;
    private float activeAge;
    private bool collected;

    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale = Vector3.one;
    private Quaternion visualBaseLocalRotation = Quaternion.identity;
    private float visualPhase;
    private float currentVisualScaleMultiplier = 1f;
    private bool visualBaseCaptured;

    public RewardPickupKind PickupKind => pickupKind;
    public CurrencyType CurrencyType => currencyType;
    public int Amount => amount;
    public string InteractionText => BuildCargoInteractionText();
    public bool IsAvailable => !collected && isActiveAndEnabled && gameObject.activeInHierarchy;
    public bool CanBeTakenByEnemy => !preserveExactCargoAmount &&
                                     IsAvailable &&
                                     activeAge >= Mathf.Max(0f, enemyCollectionProtectionDuration);
    public float EnemyProtectionRemaining => Mathf.Max(0f, enemyCollectionProtectionDuration - activeAge);

    public Sprite GetCurrencySprite(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => experienceSprite,
            CurrencyType.Credits => creditsSprite,
            CurrencyType.ScrapParts => scrapSprite,
            CurrencyType.CoreShards => coreShardSprite,
            CurrencyType.StabilizedAlloy => stabilizedAlloySprite != null
                ? stabilizedAlloySprite
                : scrapSprite,
            CurrencyType.TuningChips => tuningChipSprite != null ? tuningChipSprite : experienceSprite,
            _ => null
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRegistry()
    {
        ActiveRegistry.Clear();
    }

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        pickupCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (visualRoot == null && spriteRenderer != null && spriteRenderer.transform != transform)
        {
            visualRoot = spriteRenderer.transform;
        }

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pickupCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (visualRoot == null && spriteRenderer != null && spriteRenderer.transform != transform)
        {
            visualRoot = spriteRenderer.transform;
        }

        CaptureVisualBaseState();

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void OnEnable()
    {
        ActiveRegistry.Add(this);
        collected = false;
        lifeTimer = lifeTime;
        playerFindTimer = 0f;
        player = null;
        playerBonusState = null;
        playerCargoController = null;
        currentVelocity = Vector2.zero;
        cargoBlockedWarningTimer = 0f;
        playerCollectionBlockTimer = 0f;
        preserveExactCargoAmount = false;
        activeAge = 0f;
        visualPhase = Random.Range(0f, Mathf.PI * 2f);
        RestoreVisualBaseState();
        ApplyVisual();

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        ActiveRegistry.Remove(this);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        RestoreVisualBaseState();
    }

    private void OnDestroy()
    {
        ActiveRegistry.Remove(this);
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        activeAge += Time.deltaTime;
        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            ReleaseSelf();
            return;
        }

        if (cargoBlockedWarningTimer > 0f)
        {
            cargoBlockedWarningTimer -= Time.deltaTime;
        }

        if (playerCollectionBlockTimer > 0f)
        {
            playerCollectionBlockTimer -= Time.deltaTime;
        }

        ResolvePlayer();

        if (player == null)
        {
            return;
        }

        float bonusRange = playerBonusState != null ? playerBonusState.PickupRangeBonus : 0f;
        float finalCollectRadius = collectRadius + bonusRange;
        float finalAttractRadius = attractRadius + bonusRange;

        float distance = Vector2.Distance(transform.position, player.position);
        bool cargoBlocked = IsCargoBlockedForPlayer(player.gameObject);
        bool manualCargoPickup = RequiresManualCargoPickup(player.gameObject);
        bool healBlocked = IsHealBlockedForPlayer(player.gameObject);
        bool attractionBlocked =
            playerCollectionBlockTimer > 0f ||
            manualCargoPickup ||
            (cargoBlocked && stopAttractionWhenCargoBlocked) ||
            (healBlocked && stopHealAttractionAtFullHealth);

        if (attractionBlocked)
        {
            currentVelocity = Vector2.MoveTowards(
                currentVelocity,
                Vector2.zero,
                Mathf.Max(0.1f, blockedAttractionStopSpeed) * Time.deltaTime
            );

            if (!manualCargoPickup && cargoBlocked && distance <= finalCollectRadius + 0.15f)
            {
                ShowCargoBlockedWarning();
            }

            return;
        }

        if (!manualCargoPickup && distance <= finalCollectRadius)
        {
            if (CanCollect(player.gameObject))
            {
                Collect(player.gameObject);
            }
            else
            {
                ShowCargoBlockedWarning();
            }

            return;
        }

        if (!manualCargoPickup && distance <= finalAttractRadius)
        {
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            float speed = Mathf.Min(maxAttractSpeed, attractSpeed + distance);
            currentVelocity = Vector2.Lerp(currentVelocity, direction * speed, Time.deltaTime * 8f);
        }
    }

    private void FixedUpdate()
    {
        if (collected)
        {
            return;
        }

        currentVelocity = Vector2.Lerp(currentVelocity, Vector2.zero, driftDamping * Time.fixedDeltaTime);

        if (rb != null)
        {
            rb.linearVelocity = currentVelocity;
        }
        else
        {
            transform.position += (Vector3)(currentVelocity * Time.fixedDeltaTime);
        }
    }

    private void LateUpdate()
    {
        if (!animateVisual || visualRoot == null || visualRoot == transform)
        {
            return;
        }

        float time = Time.time + visualPhase;
        float bob = Mathf.Sin(time * Mathf.Max(0.01f, bobSpeed)) * Mathf.Max(0f, bobAmplitude);
        float pulse = 1f + Mathf.Sin(time * Mathf.Max(0.01f, pulseSpeed)) * Mathf.Max(0f, pulseScaleAmount);

        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bob;
        visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, 0f, time * spinSpeed);
        visualRoot.localScale = visualBaseLocalScale * Mathf.Max(0.05f, currentVisualScaleMultiplier) * pulse;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            if (!RequiresManualCargoPickup(playerHealth.gameObject))
            {
                TryCollectOrWarn(playerHealth.gameObject);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
        {
            if (!RequiresManualCargoPickup(other.gameObject))
            {
                TryCollectOrWarn(other.gameObject);
            }
        }
    }

    public void InitializeCurrency(CurrencyType type, int value, Vector2 initialVelocity)
    {
        pickupKind = RewardPickupKind.Currency;
        currencyType = type;
        amount = Mathf.Max(1, value);
        healAmount = 0f;
        currentVelocity = initialVelocity;
        collected = false;
        activeAge = 0f;
        preserveExactCargoAmount = false;
        playerCollectionBlockTimer = 0f;

        ApplyVisual();
    }

    public void InitializeOwnedCurrency(
        CurrencyType type,
        int value,
        Vector2 initialVelocity,
        float collectionBlockSeconds)
    {
        InitializeCurrency(type, value, initialVelocity);
        preserveExactCargoAmount = true;
        playerCollectionBlockTimer = Mathf.Max(0f, collectionBlockSeconds);
        lifeTimer = Mathf.Max(lifeTimer, 120f);
    }

    public void InitializeHeal(float value, Vector2 initialVelocity)
    {
        pickupKind = RewardPickupKind.Heal;
        healAmount = Mathf.Max(0f, value);
        amount = 0;
        currentVelocity = initialVelocity;
        collected = false;
        activeAge = 0f;
        preserveExactCargoAmount = false;
        playerCollectionBlockTimer = 0f;

        ApplyVisual();
    }

    public bool TryTakeCurrencyByEnemy(
        int maxAmount,
        out CurrencyType takenType,
        out int takenAmount)
    {
        takenType = currencyType;
        takenAmount = 0;

        if (!CanBeTakenByEnemy ||
            pickupKind != RewardPickupKind.Currency ||
            currencyType == CurrencyType.TuningChips ||
            maxAmount <= 0)
        {
            return false;
        }

        takenAmount = Mathf.Min(amount, maxAmount);

        if (takenAmount <= 0)
        {
            return false;
        }

        amount -= takenAmount;
        currentVelocity = Vector2.zero;

        if (amount <= 0)
        {
            collected = true;

            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }

            ReleaseSelf();
        }

        return true;
    }

    private bool CanCollect(GameObject playerObject)
    {
        if (playerCollectionBlockTimer > 0f)
        {
            return false;
        }

        if (pickupKind == RewardPickupKind.Heal)
        {
            return !IsHealBlockedForPlayer(playerObject);
        }

        if (pickupKind != RewardPickupKind.Currency)
        {
            return true;
        }

        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return true;
        }

        if (!RunManager.Instance.CurrentRun.UsesCargo(currencyType))
        {
            return true;
        }

        int finalAmount = GetFinalCurrencyAmount(playerObject);
        return RunManager.Instance.CurrentRun.GetAcceptedAmountByCargo(currencyType, finalAmount) >= finalAmount;
    }

    private void TryCollectOrWarn(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return;
        }

        if (CanCollect(playerObject))
        {
            Collect(playerObject);
        }
        else if (IsCargoBlockedForPlayer(playerObject))
        {
            ShowCargoBlockedWarning();
        }
    }

    private bool IsCargoBlockedForPlayer(GameObject playerObject)
    {
        if (pickupKind != RewardPickupKind.Currency ||
            RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            !RunManager.Instance.CurrentRun.UsesCargo(currencyType))
        {
            return false;
        }

        int finalAmount = GetFinalCurrencyAmount(playerObject);

        if (finalAmount <= 0)
        {
            return false;
        }

        int acceptedAmount = RunManager.Instance.CurrentRun.GetAcceptedAmountByCargo(currencyType, finalAmount);
        return acceptedAmount < finalAmount;
    }

    private bool IsHealBlockedForPlayer(GameObject playerObject)
    {
        if (pickupKind != RewardPickupKind.Heal ||
            !stopHealAttractionAtFullHealth ||
            playerObject == null)
        {
            return false;
        }

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();

        if (health == null)
        {
            health = playerObject.GetComponentInParent<PlayerHealth>();
        }

        return health != null && !health.IsDead && health.CurrentHp >= health.MaxHp - 0.001f;
    }

    private int GetFinalCurrencyAmount(GameObject playerObject)
    {
        int finalAmount = amount;
        PlayerRuntimeBonusState bonusState = playerObject != null
            ? playerObject.GetComponent<PlayerRuntimeBonusState>()
            : playerBonusState;

        if (!preserveExactCargoAmount && bonusState != null)
        {
            finalAmount = bonusState.ApplyCurrencyGain(currencyType, amount);
        }

        return Mathf.Max(0, finalAmount);
    }

    private void Collect(GameObject playerObject)
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        if (playerObject == null)
        {
            ReleaseSelf();
            return;
        }

        PlayerRuntimeBonusState bonusState = playerObject.GetComponent<PlayerRuntimeBonusState>();
        playerCargoController ??= playerObject.GetComponentInParent<PlayerCargoController>();

        switch (pickupKind)
        {
            case RewardPickupKind.Currency:
                if (!GrantCurrency(bonusState))
                {
                    collected = false;

                    if (pickupCollider != null)
                    {
                        pickupCollider.enabled = true;
                    }

                    ShowCargoBlockedWarning();
                    return;
                }
                break;

            case RewardPickupKind.Heal:
                GrantHeal(playerObject, bonusState);
                break;
        }

        PlayPickupSound();
        ReleaseSelf();
    }

    private bool GrantCurrency(PlayerRuntimeBonusState bonusState)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            Debug.LogWarning("진행 중인 탐사가 없어 RewardPickup 재화를 지급하지 못했습니다.", this);
            return false;
        }

        int finalAmount = amount;

        if (!preserveExactCargoAmount && bonusState != null)
        {
            finalAmount = bonusState.ApplyCurrencyGain(currencyType, amount);
        }

        if (finalAmount <= 0)
        {
            return false;
        }

        RunContext runContext = RunManager.Instance.CurrentRun;

        if (runContext.UsesCargo(currencyType) &&
            runContext.GetAcceptedAmountByCargo(currencyType, finalAmount) < finalAmount)
        {
            return false;
        }

        int acceptedAmount = preserveExactCargoAmount && playerCargoController != null
            ? playerCargoController.AddExactOwnedCurrency(currencyType, finalAmount)
            : RunManager.Instance.AddCurrencyRespectingCargo(currencyType, finalAmount);
        return acceptedAmount >= finalAmount;
    }

    public bool CanInteract(GameObject interactor)
    {
        return IsAvailable &&
               playerCollectionBlockTimer <= 0f &&
               RequiresManualCargoPickup(interactor);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        TryCollectOrWarn(interactor);
    }

    private bool RequiresManualCargoPickup(GameObject playerObject)
    {
        if (pickupKind != RewardPickupKind.Currency ||
            playerCollectionBlockTimer > 0f ||
            RunManager.Instance == null ||
            !RunManager.Instance.HasActiveRun ||
            !RunManager.Instance.CurrentRun.UsesCargo(currencyType))
        {
            return false;
        }

        PlayerCargoController cargoController = playerCargoController;

        if (cargoController == null && playerObject != null)
        {
            cargoController = playerObject.GetComponentInParent<PlayerCargoController>();

            if (player != null && playerObject.transform.root == player.root)
            {
                playerCargoController = cargoController;
            }
        }

        return cargoController != null &&
               (cargoController.RequiresManualPickup ||
                !cargoController.IsAutoPickupEnabled(currencyType));
    }

    private string BuildCargoInteractionText()
    {
        int displayAmount = player != null ? GetFinalCurrencyAmount(player.gameObject) : Mathf.Max(1, amount);
        int weight = RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.GetCargoWeight(currencyType) * displayAmount
            : 0;
        string resourceName = GetCargoDisplayName(currencyType);

        return weight > 0
            ? $"회수  {resourceName} ×{displayAmount} · 적재 +{weight}"
            : $"회수  {resourceName} ×{displayAmount}";
    }

    private static string GetCargoDisplayName(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.ScrapParts => "스크랩",
            CurrencyType.CoreShards => "코어",
            CurrencyType.StabilizedAlloy => "안정화 합금",
            _ => type.ToString()
        };
    }

    private void GrantHeal(GameObject playerObject, PlayerRuntimeBonusState bonusState)
    {
        PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = playerObject.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning("RewardPickup이 PlayerHealth를 찾지 못했습니다.", this);
            return;
        }

        float finalHealAmount = healAmount;

        if (bonusState != null)
        {
            finalHealAmount = bonusState.ApplyHealAmount(healAmount);
        }

        if (finalHealAmount <= 0f)
        {
            return;
        }

        playerHealth.Heal(finalHealAmount);
    }

    private void PlayPickupSound()
    {
        if (pickupKind == RewardPickupKind.Heal)
        {
            AudioManager.PlayAt(SoundEventIds.PickupHeal, transform.position);
            return;
        }

        switch (currencyType)
        {
            case CurrencyType.Credits:
                AudioManager.PlayAt(SoundEventIds.PickupCredit, transform.position);
                break;

            case CurrencyType.ScrapParts:
                AudioManager.PlayAt(SoundEventIds.PickupScrap, transform.position);
                break;

            case CurrencyType.CoreShards:
                AudioManager.PlayAt(SoundEventIds.PickupCore, transform.position);
                break;

            case CurrencyType.StabilizedAlloy:
                AudioManager.PlayAt(SoundEventIds.PickupScrap, transform.position);
                break;

            case CurrencyType.TuningChips:
                AudioManager.PlayAt(SoundEventIds.PickupTuningChip, transform.position);
                break;
        }
    }

    private void ShowCargoBlockedWarning()
    {
        if (cargoBlockedWarningTimer > 0f)
        {
            return;
        }

        cargoBlockedWarningTimer = 1f;
        PlayerCargoController cargoController = player != null
            ? player.GetComponentInParent<PlayerCargoController>()
            : null;
        cargoController?.NotifyCargoFullRejected();

        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            hud.ShowCommunication(
                ShipCommunicationChannel.Cargo,
                "적재 공간이 가득 찼습니다.",
                ShipCommunicationSeverity.Warning
            );
        }
    }

    private void ResolvePlayer()
    {
        if (player != null)
        {
            return;
        }

        playerFindTimer -= Time.deltaTime;

        if (playerFindTimer > 0f)
        {
            return;
        }

        playerFindTimer = 0.25f;

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        playerBonusState = playerObject.GetComponent<PlayerRuntimeBonusState>();
        playerCargoController = playerObject.GetComponent<PlayerCargoController>();

        if (playerCargoController == null)
        {
            playerCargoController = playerObject.GetComponentInParent<PlayerCargoController>();
        }
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite targetSprite = null;

        if (pickupKind == RewardPickupKind.Heal)
        {
            targetSprite = healSprite;
        }
        else
        {
            targetSprite = GetCurrencySprite(currencyType);
        }

        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
        }

        spriteRenderer.color = pickupKind == RewardPickupKind.Currency &&
                               currencyType == CurrencyType.StabilizedAlloy
            ? stabilizedAlloySpriteColor
            : Color.white;

        currentVisualScaleMultiplier = ResolveVisualScaleMultiplier();

        if (bubbleRenderer != null)
        {
            bubbleRenderer.color = ResolveBubbleColor();
        }

        ApplyVisualScaleImmediately();
    }

    private float ResolveVisualScaleMultiplier()
    {
        if (pickupKind == RewardPickupKind.Heal)
        {
            return Mathf.Max(0.05f, healVisualScale);
        }

        return Mathf.Max(0.05f, currencyType switch
        {
            CurrencyType.Experience => experienceVisualScale,
            CurrencyType.Credits => creditsVisualScale,
            CurrencyType.ScrapParts => scrapVisualScale,
            CurrencyType.CoreShards => coreVisualScale,
            CurrencyType.StabilizedAlloy => stabilizedAlloyVisualScale,
            CurrencyType.TuningChips => tuningChipVisualScale,
            _ => 1f
        });
    }

    private Color ResolveBubbleColor()
    {
        if (pickupKind == RewardPickupKind.Heal)
        {
            return healBubbleColor;
        }

        return currencyType switch
        {
            CurrencyType.Experience => experienceBubbleColor,
            CurrencyType.Credits => creditsBubbleColor,
            CurrencyType.ScrapParts => scrapBubbleColor,
            CurrencyType.CoreShards => coreBubbleColor,
            CurrencyType.StabilizedAlloy => stabilizedAlloyBubbleColor,
            CurrencyType.TuningChips => tuningChipBubbleColor,
            _ => Color.white
        };
    }

    private void CaptureVisualBaseState()
    {
        if (visualBaseCaptured || visualRoot == null || visualRoot == transform)
        {
            return;
        }

        visualBaseLocalPosition = visualRoot.localPosition;
        visualBaseLocalScale = visualRoot.localScale;
        visualBaseLocalRotation = visualRoot.localRotation;
        visualBaseCaptured = true;
    }

    private void RestoreVisualBaseState()
    {
        if (!visualBaseCaptured || visualRoot == null || visualRoot == transform)
        {
            return;
        }

        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localRotation = visualBaseLocalRotation;
        visualRoot.localScale = visualBaseLocalScale;
    }

    private void ApplyVisualScaleImmediately()
    {
        if (!visualBaseCaptured || visualRoot == null || visualRoot == transform)
        {
            return;
        }

        visualRoot.localScale = visualBaseLocalScale * Mathf.Max(0.05f, currentVisualScaleMultiplier);
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
