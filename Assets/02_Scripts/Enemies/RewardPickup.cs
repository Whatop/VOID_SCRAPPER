using UnityEngine;

public enum RewardPickupKind
{
    Currency,
    Heal
}

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class RewardPickup : MonoBehaviour
{
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

    [Header("Motion")]
    [SerializeField] private float driftDamping = 5f;
    [SerializeField] private float lifeTime = 30f;

    [Header("Visual Optional")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite experienceSprite;
    [SerializeField] private Sprite creditsSprite;
    [SerializeField] private Sprite scrapSprite;
    [SerializeField] private Sprite coreShardSprite;
    [SerializeField] private Sprite healSprite;

    private Rigidbody2D rb;
    private Collider2D pickupCollider;
    private Transform player;
    private PlayerRuntimeBonusState playerBonusState;

    private Vector2 currentVelocity;
    private float lifeTimer;
    private float playerFindTimer;
    private bool collected;

    private void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        pickupCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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
        collected = false;
        lifeTimer = lifeTime;
        playerFindTimer = 0f;
        player = null;
        playerBonusState = null;
        currentVelocity = Vector2.zero;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        if (collected)
        {
            return;
        }

        lifeTimer -= Time.deltaTime;

        if (lifeTimer <= 0f)
        {
            ReleaseSelf();
            return;
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

        if (distance <= finalCollectRadius)
        {
            Collect(player.gameObject);
            return;
        }

        if (distance <= finalAttractRadius)
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || other == null)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            Collect(playerHealth.gameObject);
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag))
        {
            Collect(other.gameObject);
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

        ApplyVisual();
    }

    public void InitializeHeal(float value, Vector2 initialVelocity)
    {
        pickupKind = RewardPickupKind.Heal;
        healAmount = Mathf.Max(0f, value);
        amount = 0;
        currentVelocity = initialVelocity;
        collected = false;

        ApplyVisual();
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

        switch (pickupKind)
        {
            case RewardPickupKind.Currency:
                GrantCurrency(bonusState);
                break;

            case RewardPickupKind.Heal:
                GrantHeal(playerObject, bonusState);
                break;
        }

        ReleaseSelf();
    }

    private void GrantCurrency(PlayerRuntimeBonusState bonusState)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            Debug.LogWarning("활성화된 RunManager가 없어 RewardPickup 재화를 지급할 수 없습니다.", this);
            return;
        }

        int finalAmount = amount;

        if (bonusState != null)
        {
            finalAmount = bonusState.ApplyCurrencyGain(currencyType, amount);
        }

        if (finalAmount <= 0)
        {
            return;
        }

        RunManager.Instance.AddCurrency(currencyType, finalAmount);
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
            targetSprite = currencyType switch
            {
                CurrencyType.Experience => experienceSprite,
                CurrencyType.Credits => creditsSprite,
                CurrencyType.ScrapParts => scrapSprite,
                CurrencyType.CoreShards => coreShardSprite,
                _ => null
            };
        }

        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
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