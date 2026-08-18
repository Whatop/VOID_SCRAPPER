using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CurrencyRange
{
    public CurrencyType currencyType;
    public int minAmount;
    public int maxAmount;

    public CurrencyAmount Roll()
    {
        int min = Mathf.Min(minAmount, maxAmount);
        int max = Mathf.Max(minAmount, maxAmount);
        int amount = UnityEngine.Random.Range(min, max + 1);

        return new CurrencyAmount(currencyType, amount);
    }
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Rewards/Reward Definition")]
public class RewardDefinition : ScriptableObject
{
    [Header("Fixed Rewards")]
    [SerializeField] private List<CurrencyAmount> fixedRewards = new List<CurrencyAmount>();

    [Header("Random Rewards")]
    [SerializeField] private List<CurrencyRange> randomRewards = new List<CurrencyRange>();

    [Header("Heal Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float healDropChance;
    [SerializeField] private int healAmount = 1;

    [Header("Reinforcement Item Drop")]
    [Tooltip("켜면 이 보상 테이블에서 액티브 장비 ReinforcementPickup을 드랍합니다.")]
    [SerializeField] private bool enableReinforcementDrop;

    [Range(0f, 1f)]
    [SerializeField] private float reinforcementDropChance;

    [SerializeField] private int minReinforcementDropCount = 1;
    [SerializeField] private int maxReinforcementDropCount = 1;

    [Tooltip("비워두면 RewardDropper에 연결된 Fallback Catalog를 사용합니다.")]
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;

    [SerializeField] private bool includeCatalogReinforcements = true;
    [SerializeField] private List<ReinforcementDefinition> reinforcementDropPool = new List<ReinforcementDefinition>();

    [Tooltip("긴급복귀 앵커처럼 기본 액티브는 랜덤 드랍에서 제외합니다.")]
    [SerializeField] private bool excludeEmergencyReturnEquipment = true;

    [Tooltip("같은 보상에서 같은 장비가 중복으로 뽑히지 않게 합니다.")]
    [SerializeField] private bool preventDuplicateReinforcementDrops = true;

    [Tooltip("켜면 ReinforcementDefinition.Rarity 기반 가중치로 뽑습니다.")]
    [SerializeField] private bool useReinforcementRarityWeight = true;

    [Header("Trait Item Drop")]
    [Tooltip("켜면 이 보상 테이블에서 패시브 TraitPickup을 드랍합니다.")]
    [SerializeField] private bool enableTraitDrop;

    [Range(0f, 1f)]
    [SerializeField] private float traitDropChance;

    [SerializeField] private int minTraitDropCount = 1;
    [SerializeField] private int maxTraitDropCount = 1;

    [Tooltip("비워두면 RewardDropper에 연결된 Fallback Trait Catalog를 사용합니다.")]
    [SerializeField] private TraitCatalog traitCatalog;

    [SerializeField] private bool includeCatalogTraits = true;
    [SerializeField] private List<TraitDefinition> traitDropPool = new List<TraitDefinition>();
    [SerializeField] private bool preventDuplicateTraitDrops = true;

    public IReadOnlyList<CurrencyAmount> FixedRewards => fixedRewards;
    public IReadOnlyList<CurrencyRange> RandomRewards => randomRewards;
    public float HealDropChance => healDropChance;
    public int HealAmount => healAmount;

    public bool EnableReinforcementDrop => enableReinforcementDrop;
    public float ReinforcementDropChance => reinforcementDropChance;
    public int MinReinforcementDropCount => Mathf.Max(0, minReinforcementDropCount);
    public int MaxReinforcementDropCount => Mathf.Max(MinReinforcementDropCount, maxReinforcementDropCount);
    public ReinforcementCatalog ReinforcementCatalog => reinforcementCatalog;
    public IReadOnlyList<ReinforcementDefinition> ReinforcementDropPool => reinforcementDropPool;

    public bool EnableTraitDrop => enableTraitDrop;
    public float TraitDropChance => traitDropChance;
    public int MinTraitDropCount => Mathf.Max(0, minTraitDropCount);
    public int MaxTraitDropCount => Mathf.Max(MinTraitDropCount, maxTraitDropCount);
    public TraitCatalog TraitCatalog => traitCatalog;
    public IReadOnlyList<TraitDefinition> TraitDropPool => traitDropPool;

    public List<CurrencyAmount> RollCurrencies()
    {
        List<CurrencyAmount> result = new List<CurrencyAmount>();

        foreach (CurrencyAmount reward in fixedRewards)
        {
            if (reward.IsValid())
            {
                result.Add(reward);
            }
        }

        foreach (CurrencyRange range in randomRewards)
        {
            CurrencyAmount rolled = range.Roll();
            if (rolled.IsValid())
            {
                result.Add(rolled);
            }
        }

        return result;
    }

    public bool RollHealDrop()
    {
        if (healDropChance <= 0f)
        {
            return false;
        }

        return UnityEngine.Random.value <= healDropChance;
    }

    public List<ReinforcementDefinition> RollReinforcementDrops(ReinforcementCatalog fallbackCatalog = null)
    {
        List<ReinforcementDefinition> result = new List<ReinforcementDefinition>();

        if (!enableReinforcementDrop || reinforcementDropChance <= 0f)
        {
            return result;
        }

        if (UnityEngine.Random.value > reinforcementDropChance)
        {
            return result;
        }

        List<ReinforcementDefinition> candidates = BuildReinforcementDropCandidates(fallbackCatalog);

        if (candidates.Count == 0)
        {
            return result;
        }

        int minCount = MinReinforcementDropCount;
        int maxCount = Mathf.Max(minCount, MaxReinforcementDropCount);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        count = Mathf.Clamp(count, 0, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            int index = useReinforcementRarityWeight
                ? PickWeightedReinforcementIndex(candidates)
                : UnityEngine.Random.Range(0, candidates.Count);

            ReinforcementDefinition selected = candidates[index];

            if (selected != null)
            {
                result.Add(selected);
            }

            if (preventDuplicateReinforcementDrops)
            {
                candidates.RemoveAt(index);
            }
        }

        return result;
    }

    public List<TraitDefinition> RollTraitDrops(TraitCatalog fallbackCatalog = null)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (!enableTraitDrop || traitDropChance <= 0f)
        {
            return result;
        }

        if (UnityEngine.Random.value > traitDropChance)
        {
            return result;
        }

        List<TraitDefinition> candidates = BuildTraitDropCandidates(fallbackCatalog);

        if (candidates.Count == 0)
        {
            return result;
        }

        int minCount = MinTraitDropCount;
        int maxCount = Mathf.Max(minCount, MaxTraitDropCount);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        count = Mathf.Clamp(count, 0, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0)
            {
                break;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            TraitDefinition selected = candidates[index];

            if (selected != null)
            {
                result.Add(selected);
            }

            if (preventDuplicateTraitDrops)
            {
                candidates.RemoveAt(index);
            }
        }

        return result;
    }

    private int PickWeightedReinforcementIndex(List<ReinforcementDefinition> candidates)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            ReinforcementDefinition definition = candidates[i];
            totalWeight += definition != null ? Mathf.Max(0.001f, definition.EffectiveRandomDropWeight) : 0f;
        }

        if (totalWeight <= 0f)
        {
            return UnityEngine.Random.Range(0, candidates.Count);
        }

        float roll = UnityEngine.Random.value * totalWeight;
        float accumulated = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            ReinforcementDefinition definition = candidates[i];
            accumulated += definition != null ? Mathf.Max(0.001f, definition.EffectiveRandomDropWeight) : 0f;

            if (roll <= accumulated)
            {
                return i;
            }
        }

        return candidates.Count - 1;
    }

    private List<ReinforcementDefinition> BuildReinforcementDropCandidates(ReinforcementCatalog fallbackCatalog)
    {
        List<ReinforcementDefinition> result = new List<ReinforcementDefinition>();

        if (includeCatalogReinforcements)
        {
            ReinforcementCatalog sourceCatalog = reinforcementCatalog != null ? reinforcementCatalog : fallbackCatalog;

            if (sourceCatalog != null)
            {
                List<ReinforcementDefinition> catalogBuffer = new List<ReinforcementDefinition>();
                sourceCatalog.AppendAllTo(catalogBuffer);

                for (int i = 0; i < catalogBuffer.Count; i++)
                {
                    AppendReinforcementCandidate(result, catalogBuffer[i]);
                }
            }
        }

        if (reinforcementDropPool != null)
        {
            for (int i = 0; i < reinforcementDropPool.Count; i++)
            {
                AppendReinforcementCandidate(result, reinforcementDropPool[i]);
            }
        }

        return result;
    }

    private List<TraitDefinition> BuildTraitDropCandidates(TraitCatalog fallbackCatalog)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (includeCatalogTraits)
        {
            TraitCatalog sourceCatalog = traitCatalog != null ? traitCatalog : fallbackCatalog;

            if (sourceCatalog != null)
            {
                List<TraitDefinition> catalogBuffer = new List<TraitDefinition>();
                sourceCatalog.AppendAllTo(catalogBuffer);

                for (int i = 0; i < catalogBuffer.Count; i++)
                {
                    AppendTraitCandidate(result, catalogBuffer[i]);
                }
            }
        }

        if (traitDropPool != null)
        {
            for (int i = 0; i < traitDropPool.Count; i++)
            {
                AppendTraitCandidate(result, traitDropPool[i]);
            }
        }

        return result;
    }

    private void AppendReinforcementCandidate(List<ReinforcementDefinition> result, ReinforcementDefinition definition)
    {
        if (result == null || definition == null)
        {
            return;
        }

        if (!definition.CanAppearAsRandomDrop)
        {
            return;
        }

        if (excludeEmergencyReturnEquipment && definition.HasEffect(ReinforcementEffectType.EmergencyReturn))
        {
            return;
        }

        string id = definition.EquipmentId;

        for (int i = 0; i < result.Count; i++)
        {
            ReinforcementDefinition existing = result[i];

            if (existing != null && existing.EquipmentId == id)
            {
                return;
            }
        }

        result.Add(definition);
    }

    private void AppendTraitCandidate(List<TraitDefinition> result, TraitDefinition trait)
    {
        if (result == null || trait == null)
        {
            return;
        }

        if (!trait.CanAppearAsRandomDropTrait)
        {
            return;
        }

        if (!RunTraitAcquisitionService.MeetsOfferPrerequisites(trait))
        {
            return;
        }

        string id = trait.TraitId;

        for (int i = 0; i < result.Count; i++)
        {
            TraitDefinition existing = result[i];

            if (existing != null && existing.TraitId == id)
            {
                return;
            }
        }

        result.Add(trait);
    }
}
