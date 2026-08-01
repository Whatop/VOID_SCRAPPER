using System;
using UnityEngine;

/// <summary>
/// 적이 기지로 운반한 화물을 저장하고, 저장량에 따라 상자의 시각 크기와
/// 기본 RewardDefinition의 재화 보상을 증가시킨다.
/// 실제 파괴/피격 처리는 같은 오브젝트의 HarvestObjectHealth가 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HarvestObjectHealth))]
[RequireComponent(typeof(RewardDropper))]
public class FieldBaseResourceChest : MonoBehaviour
{
    [Header("Storage Capacity")]
    [Min(1)]
    [SerializeField] private int storageCapacityWeight = 40;

    [Header("Cargo Weight")]
    [Min(1)]
    [SerializeField] private int experienceWeight = 1;
    [Min(1)]
    [SerializeField] private int creditsWeight = 1;
    [Min(1)]
    [SerializeField] private int scrapWeight = 1;
    [Min(1)]
    [SerializeField] private int coreShardWeight = 12;

    [Header("Accepted Currency")]
    [SerializeField] private bool acceptExperience;
    [SerializeField] private bool acceptCredits = true;
    [SerializeField] private bool acceptScrap = true;
    [SerializeField] private bool acceptCoreShards = true;

    [Header("Growth")]
    [Tooltip("VisualRoot처럼 스프라이트만 들어있는 자식을 권장합니다. 비우면 이 오브젝트 전체가 커집니다.")]
    [SerializeField] private Transform scaleRoot;

    [Min(1f)]
    [Tooltip("저장량이 가득 찼을 때 시각 크기 배율입니다.")]
    [SerializeField] private float maxScaleMultiplier = 1.5f;

    [Min(1f)]
    [Tooltip("저장량이 가득 찼을 때 RewardDefinition 재화 보상 배율입니다.")]
    [SerializeField] private float maxRewardMultiplier = 1.5f;

    [SerializeField] private AnimationCurve growthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool smoothScale = true;
    [Min(0.01f)]
    [SerializeField] private float scaleResponseSpeed = 7f;
    [SerializeField] private bool resetStoredCargoOnEnable = true;

    [Header("Runtime - Read Only")]
    [SerializeField] private int storedExperience;
    [SerializeField] private int storedCredits;
    [SerializeField] private int storedScrap;
    [SerializeField] private int storedCoreShards;
    [SerializeField] private float currentRewardMultiplier = 1f;

    private HarvestObjectHealth health;
    private RewardDropper rewardDropper;
    private Vector3 baseLocalScale = Vector3.one;
    private Vector3 targetLocalScale = Vector3.one;
    private bool baseScaleCaptured;

    public int StorageCapacityWeight => Mathf.Max(1, storageCapacityWeight);
    public int StoredWeight =>
        storedExperience * GetWeight(CurrencyType.Experience) +
        storedCredits * GetWeight(CurrencyType.Credits) +
        storedScrap * GetWeight(CurrencyType.ScrapParts) +
        storedCoreShards * GetWeight(CurrencyType.CoreShards);

    public int RemainingWeight => Mathf.Max(0, StorageCapacityWeight - StoredWeight);
    public float FillRatio => StorageCapacityWeight <= 0
        ? 0f
        : Mathf.Clamp01((float)StoredWeight / StorageCapacityWeight);

    public bool IsFull => RemainingWeight <= 0;
    public bool IsDestroyed => health == null || health.IsDead;
    public float CurrentRewardMultiplier => currentRewardMultiplier;

    public event Action<FieldBaseResourceChest> StorageChanged;

    private void Reset()
    {
        health = GetComponent<HarvestObjectHealth>();
        rewardDropper = GetComponent<RewardDropper>();
        scaleRoot = transform;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseScale();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBaseScale();

        if (resetStoredCargoOnEnable)
        {
            ResetStorage();
        }
        else
        {
            RefreshGrowth(true);
        }
    }

    private void LateUpdate()
    {
        if (!smoothScale || scaleRoot == null)
        {
            return;
        }

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, scaleResponseSpeed) * Time.deltaTime);
        scaleRoot.localScale = Vector3.Lerp(scaleRoot.localScale, targetLocalScale, t);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        storageCapacityWeight = Mathf.Max(1, storageCapacityWeight);
        experienceWeight = Mathf.Max(1, experienceWeight);
        creditsWeight = Mathf.Max(1, creditsWeight);
        scrapWeight = Mathf.Max(1, scrapWeight);
        coreShardWeight = Mathf.Max(1, coreShardWeight);
        maxScaleMultiplier = Mathf.Max(1f, maxScaleMultiplier);
        maxRewardMultiplier = Mathf.Max(1f, maxRewardMultiplier);
        scaleResponseSpeed = Mathf.Max(0.01f, scaleResponseSpeed);
    }
#endif

    public bool CanDeposit(CurrencyType type)
    {
        return !IsDestroyed &&
               IsCurrencyAccepted(type) &&
               RemainingWeight >= GetWeight(type);
    }

    /// <summary>
    /// 요청 수량 중 실제로 저장된 수량을 반환한다.
    /// </summary>
    public int TryDeposit(CurrencyType type, int amount)
    {
        if (amount <= 0 || !CanDeposit(type))
        {
            return 0;
        }

        int weight = GetWeight(type);
        int accepted = Mathf.Min(amount, RemainingWeight / weight);

        if (accepted <= 0)
        {
            return 0;
        }

        switch (type)
        {
            case CurrencyType.Experience:
                storedExperience += accepted;
                break;

            case CurrencyType.Credits:
                storedCredits += accepted;
                break;

            case CurrencyType.ScrapParts:
                storedScrap += accepted;
                break;

            case CurrencyType.CoreShards:
                storedCoreShards += accepted;
                break;

            default:
                return 0;
        }

        RefreshGrowth(false);
        StorageChanged?.Invoke(this);
        return accepted;
    }

    public int GetStoredAmount(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => storedExperience,
            CurrencyType.Credits => storedCredits,
            CurrencyType.ScrapParts => storedScrap,
            CurrencyType.CoreShards => storedCoreShards,
            _ => 0
        };
    }

    public void ResetStorage()
    {
        storedExperience = 0;
        storedCredits = 0;
        storedScrap = 0;
        storedCoreShards = 0;
        RefreshGrowth(true);
        StorageChanged?.Invoke(this);
    }

    private void ResolveReferences()
    {
        if (health == null)
        {
            health = GetComponent<HarvestObjectHealth>();
        }

        if (rewardDropper == null)
        {
            rewardDropper = GetComponent<RewardDropper>();
        }

        if (scaleRoot == null)
        {
            scaleRoot = transform;
        }
    }

    private void CaptureBaseScale()
    {
        if (baseScaleCaptured || scaleRoot == null)
        {
            return;
        }

        baseLocalScale = scaleRoot.localScale;
        targetLocalScale = baseLocalScale;
        baseScaleCaptured = true;
    }

    private void RefreshGrowth(bool immediate)
    {
        ResolveReferences();
        CaptureBaseScale();

        float normalized = FillRatio;
        float curved = growthCurve != null
            ? Mathf.Clamp01(growthCurve.Evaluate(normalized))
            : normalized;

        float scaleMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maxScaleMultiplier), curved);
        currentRewardMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maxRewardMultiplier), curved);

        targetLocalScale = baseLocalScale * scaleMultiplier;

        if (scaleRoot != null && (immediate || !smoothScale))
        {
            scaleRoot.localScale = targetLocalScale;
        }

        if (rewardDropper != null)
        {
            rewardDropper.SetRuntimeCurrencyMultiplier(currentRewardMultiplier);
        }
    }

    private bool IsCurrencyAccepted(CurrencyType type)
    {
        return type switch
        {
            CurrencyType.Experience => acceptExperience,
            CurrencyType.Credits => acceptCredits,
            CurrencyType.ScrapParts => acceptScrap,
            CurrencyType.CoreShards => acceptCoreShards,
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
