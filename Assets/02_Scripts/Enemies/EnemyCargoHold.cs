using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyCargoHold : MonoBehaviour
{
    [Header("Capacity")]
    [SerializeField] private int capacity = 24;

    [Header("Cargo Weight")]
    [SerializeField] private int experienceWeight = 1;
    [SerializeField] private int creditsWeight = 1;
    [SerializeField] private int scrapWeight = 1;
    [SerializeField] private int coreShardWeight = 12;

    [Header("Allowed Currency")]
    [SerializeField] private bool allowExperience;
    [SerializeField] private bool allowCredits = true;
    [SerializeField] private bool allowScrap = true;
    [SerializeField] private bool allowCoreShards = true;

    [Header("Drop")]
    [SerializeField] private bool dropCargoOnDeath = true;
    [SerializeField] private RewardDropper rewardDropper;

    [Header("Runtime")]
    [SerializeField] private int experience;
    [SerializeField] private int credits;
    [SerializeField] private int scrapParts;
    [SerializeField] private int coreShards;

    private EnemyHealth health;
    private bool suppressNextDeathDrop;

    public int Capacity => Mathf.Max(1, capacity);
    public int UsedCapacity =>
        experience * GetWeight(CurrencyType.Experience) +
        credits * GetWeight(CurrencyType.Credits) +
        scrapParts * GetWeight(CurrencyType.ScrapParts) +
        coreShards * GetWeight(CurrencyType.CoreShards);

    public int RemainingCapacity => Mathf.Max(0, Capacity - UsedCapacity);
    public float FillRatio => Capacity <= 0 ? 0f : Mathf.Clamp01((float)UsedCapacity / Capacity);
    public bool HasCargo => experience > 0 || credits > 0 || scrapParts > 0 || coreShards > 0;
    public bool IsFull => RemainingCapacity <= 0;

    public event Action<EnemyCargoHold> CargoChanged;

    private void Reset()
    {
        health = GetComponent<EnemyHealth>();
        rewardDropper = GetComponent<RewardDropper>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        suppressNextDeathDrop = false;
        ResetCargo();

        if (health != null)
        {
            health.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }
    }

    public void Configure(
        int newCapacity,
        bool canTakeExperience,
        bool canTakeCredits,
        bool canTakeScrap,
        bool canTakeCoreShards)
    {
        capacity = Mathf.Max(1, newCapacity);
        allowExperience = canTakeExperience;
        allowCredits = canTakeCredits;
        allowScrap = canTakeScrap;
        allowCoreShards = canTakeCoreShards;
        CargoChanged?.Invoke(this);
    }

    public bool CanStore(CurrencyType type)
    {
        if (!IsCurrencyAllowed(type))
        {
            return false;
        }

        return RemainingCapacity >= GetWeight(type);
    }

    public bool TryCollect(RewardPickup pickup)
    {
        if (pickup == null || !pickup.IsAvailable || pickup.PickupKind != RewardPickupKind.Currency)
        {
            return false;
        }

        CurrencyType type = pickup.CurrencyType;

        if (!IsCurrencyAllowed(type))
        {
            return false;
        }

        int weight = GetWeight(type);
        int maxAmount = RemainingCapacity / weight;

        if (maxAmount <= 0)
        {
            return false;
        }

        if (!pickup.TryTakeCurrencyByEnemy(maxAmount, out CurrencyType takenType, out int takenAmount))
        {
            return false;
        }

        Add(takenType, takenAmount);
        return true;
    }

    public void Add(CurrencyType type, int amount)
    {
        if (amount <= 0 || !IsCurrencyAllowed(type))
        {
            return;
        }

        int weight = GetWeight(type);
        int accepted = Mathf.Min(amount, RemainingCapacity / weight);

        if (accepted <= 0)
        {
            return;
        }

        switch (type)
        {
            case CurrencyType.Experience:
                experience += accepted;
                break;

            case CurrencyType.Credits:
                credits += accepted;
                break;

            case CurrencyType.ScrapParts:
                scrapParts += accepted;
                break;

            case CurrencyType.CoreShards:
                coreShards += accepted;
                break;
        }

        CargoChanged?.Invoke(this);
    }

    public int GetAmount(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => experience,
            CurrencyType.Credits => credits,
            CurrencyType.ScrapParts => scrapParts,
            CurrencyType.CoreShards => coreShards,
            _ => 0
        };
    }

    /// <summary>
    /// 화물에서 실제로 제거된 수량을 반환한다.
    /// 기지 보관함으로 이전할 때 사용한다.
    /// </summary>
    public int Remove(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int removed;

        switch (type)
        {
            case CurrencyType.Experience:
                removed = Mathf.Min(amount, experience);
                experience -= removed;
                break;

            case CurrencyType.Credits:
                removed = Mathf.Min(amount, credits);
                credits -= removed;
                break;

            case CurrencyType.ScrapParts:
                removed = Mathf.Min(amount, scrapParts);
                scrapParts -= removed;
                break;

            case CurrencyType.CoreShards:
                removed = Mathf.Min(amount, coreShards);
                coreShards -= removed;
                break;

            default:
                return 0;
        }

        if (removed > 0)
        {
            CargoChanged?.Invoke(this);
        }

        return removed;
    }

    public void DropAll()
    {
        ResolveReferences();

        if (!HasCargo)
        {
            return;
        }

        if (rewardDropper == null)
        {
            Debug.LogWarning($"[{name}] 적 화물을 드랍할 RewardDropper가 없습니다.", this);
            ResetCargo();
            return;
        }

        Vector3 origin = transform.position;

        rewardDropper.DropCurrencyRewardAt(origin, CurrencyType.Experience, experience);
        rewardDropper.DropCurrencyRewardAt(origin, CurrencyType.Credits, credits);
        rewardDropper.DropCurrencyRewardAt(origin, CurrencyType.ScrapParts, scrapParts);
        rewardDropper.DropCurrencyRewardAt(origin, CurrencyType.CoreShards, coreShards);

        ResetCargo();
    }

    public void ClearWithoutDrop()
    {
        suppressNextDeathDrop = false;
        ResetCargo();
    }

    public void ResetCargo()
    {
        experience = 0;
        credits = 0;
        scrapParts = 0;
        coreShards = 0;
        CargoChanged?.Invoke(this);
    }

    private void ResolveReferences()
    {
        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }
    }

    private void HandleDied(EnemyHealth _)
    {
        if (suppressNextDeathDrop)
        {
            suppressNextDeathDrop = false;
            ResetCargo();
            return;
        }

        if (dropCargoOnDeath)
        {
            DropAll();
        }
        else
        {
            ResetCargo();
        }
    }

    private bool IsCurrencyAllowed(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => allowExperience,
            CurrencyType.Credits => allowCredits,
            CurrencyType.ScrapParts => allowScrap,
            CurrencyType.CoreShards => allowCoreShards,
            _ => false
        };
    }

    private int GetWeight(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => Mathf.Max(1, experienceWeight),
            CurrencyType.Credits => Mathf.Max(1, creditsWeight),
            CurrencyType.ScrapParts => Mathf.Max(1, scrapWeight),
            CurrencyType.CoreShards => Mathf.Max(1, coreShardWeight),
            _ => 1
        };
    }
}
