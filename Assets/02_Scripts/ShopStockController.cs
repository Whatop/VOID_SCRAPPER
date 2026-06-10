using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopStockController : MonoBehaviour
{
    private enum ShopOfferKind
    {
        Trait,
        Reinforcement
    }

    [Header("Catalog")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeCatalogTraits = true;
    [SerializeField] private bool includeManualPools = true;

    [Header("Trait Items")]
    [SerializeField] private int traitCost = 45;
    [SerializeField] private int traitChoiceCount = 3;
    [SerializeField] private List<TraitDefinition> traitPool = new List<TraitDefinition>();

    [Header("Ship Reinforcement Items")]
    [SerializeField] private int reinforcementCost = 60;
    [SerializeField] private int minReinforcementChoiceCount = 1;
    [SerializeField] private int maxReinforcementChoiceCount = 3;
    [SerializeField] private List<TraitDefinition> reinforcementPool = new List<TraitDefinition>();

    [Header("Purchase Limit")]
    [SerializeField] private bool oneTraitPurchasePerShop = true;
    [SerializeField] private bool oneReinforcementPurchasePerShop = true;

    [Header("Duplicate Rule")]
    [SerializeField] private bool excludeAlreadyOwnedRunTraits = true;
    [SerializeField] private bool excludePermanentTraits;

    private readonly List<TraitDefinition> cachedTraitChoices = new List<TraitDefinition>();
    private readonly List<TraitDefinition> cachedReinforcementChoices = new List<TraitDefinition>();
    private readonly HashSet<string> boughtReinforcementIdsThisShop = new HashSet<string>(StringComparer.Ordinal);

    private bool stockRolled;
    private bool boughtTraitThisShop;
    private bool boughtReinforcementThisShop;

    public int TraitCost => Mathf.Max(0, traitCost);
    public int ReinforcementCost => Mathf.Max(0, reinforcementCost);

    public bool TraitSoldOut => oneTraitPurchasePerShop && boughtTraitThisShop;
    public bool ReinforcementSoldOut => oneReinforcementPurchasePerShop && boughtReinforcementThisShop;

    public IReadOnlyList<TraitDefinition> TraitPool => traitPool;
    public IReadOnlyList<TraitDefinition> ReinforcementPool => reinforcementPool;

    private void OnEnable()
    {
        ResetStockRuntime();
    }

    public void ResetStockRuntime()
    {
        stockRolled = false;

        boughtTraitThisShop = false;
        boughtReinforcementThisShop = false;
        boughtReinforcementIdsThisShop.Clear();

        cachedTraitChoices.Clear();
        cachedReinforcementChoices.Clear();
    }

    public void ForceRerollStock()
    {
        stockRolled = false;
        cachedTraitChoices.Clear();
        cachedReinforcementChoices.Clear();
        EnsureStockRolled();
    }

    public List<TraitDefinition> RollTraitChoices()
    {
        EnsureStockRolled();
        return new List<TraitDefinition>(cachedTraitChoices);
    }

    public List<TraitDefinition> RollReinforcementChoices()
    {
        EnsureStockRolled();
        return new List<TraitDefinition>(cachedReinforcementChoices);
    }

    public bool CanBuyTrait(TraitDefinition trait)
    {
        if (TraitSoldOut)
        {
            return false;
        }

        if (!IsValidPurchasableTrait(trait))
        {
            return false;
        }

        return ShopRunBridge.CanSpendCredits(TraitCost);
    }

    public bool TryBuyTrait(ShopStructure shop, TraitDefinition trait, GameObject playerObject)
    {
        if (shop != null && !shop.CanTrade)
        {
            return false;
        }

        if (!CanBuyTrait(trait))
        {
            return false;
        }

        if (!ShopRunBridge.TrySpendCredits(TraitCost))
        {
            return false;
        }

        if (!ShopRunBridge.AddRunTrait(trait.TraitId))
        {
            ShopRunBridge.AddCredits(TraitCost);
            return false;
        }

        boughtTraitThisShop = true;

        if (oneTraitPurchasePerShop)
        {
            cachedTraitChoices.Clear();
        }
        else
        {
            RemoveCachedTrait(cachedTraitChoices, trait.TraitId);
        }

        RegisterSpentCredits(shop, TraitCost);
        ShopRuntimeEffectApplier.ApplyTraitImmediate(trait, playerObject);

        return true;
    }

    public bool CanBuyReinforcement(TraitDefinition reinforcement)
    {
        if (ReinforcementSoldOut)
        {
            return false;
        }

        if (!IsValidPurchasableTrait(reinforcement))
        {
            return false;
        }

        if (boughtReinforcementIdsThisShop.Contains(reinforcement.TraitId))
        {
            return false;
        }

        return ShopRunBridge.CanSpendCredits(ReinforcementCost);
    }

    public bool TryBuyReinforcement(ShopStructure shop, TraitDefinition reinforcement, GameObject playerObject)
    {
        if (shop != null && !shop.CanTrade)
        {
            return false;
        }

        if (!CanBuyReinforcement(reinforcement))
        {
            return false;
        }

        if (!ShopRunBridge.TrySpendCredits(ReinforcementCost))
        {
            return false;
        }

        if (!ShopRunBridge.AddRunTrait(reinforcement.TraitId))
        {
            ShopRunBridge.AddCredits(ReinforcementCost);
            return false;
        }

        boughtReinforcementThisShop = true;
        boughtReinforcementIdsThisShop.Add(reinforcement.TraitId);

        if (oneReinforcementPurchasePerShop)
        {
            cachedReinforcementChoices.Clear();
        }
        else
        {
            RemoveCachedTrait(cachedReinforcementChoices, reinforcement.TraitId);
        }

        RegisterSpentCredits(shop, ReinforcementCost);
        ShopRuntimeEffectApplier.ApplyTraitImmediate(reinforcement, playerObject);

        return true;
    }

    public string BuildOptionDescription(TraitDefinition trait)
    {
        if (trait == null)
        {
            return "설명 없음";
        }

        return trait.Description;
    }

    private void EnsureStockRolled()
    {
        if (stockRolled)
        {
            return;
        }

        stockRolled = true;

        cachedTraitChoices.Clear();
        cachedReinforcementChoices.Clear();

        if (!TraitSoldOut)
        {
            List<TraitDefinition> traitCandidates = BuildAvailableCandidates(
                ShopOfferKind.Trait,
                ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun),
                excludeAlreadyOwnedRunTraits,
                null
            );

            cachedTraitChoices.AddRange(PickRandom(traitCandidates, Mathf.Max(1, traitChoiceCount)));
        }

        if (!ReinforcementSoldOut)
        {
            int minCount = Mathf.Max(1, minReinforcementChoiceCount);
            int maxCount = Mathf.Max(minCount, maxReinforcementChoiceCount);
            int count = UnityEngine.Random.Range(minCount, maxCount + 1);

            List<TraitDefinition> reinforcementCandidates = BuildAvailableCandidates(
                ShopOfferKind.Reinforcement,
                ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun),
                excludeAlreadyOwnedRunTraits,
                boughtReinforcementIdsThisShop
            );

            cachedReinforcementChoices.AddRange(PickRandom(reinforcementCandidates, count));
        }
    }

    private List<TraitDefinition> BuildAvailableCandidates(
        ShopOfferKind offerKind,
        WeaponTreeType selectedTree,
        bool excludeOwnedRunTraits,
        HashSet<string> excludeIds)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (includeCatalogTraits && traitCatalog != null)
        {
            List<TraitDefinition> catalogTraits = new List<TraitDefinition>();
            traitCatalog.AppendAllTo(catalogTraits);

            for (int i = 0; i < catalogTraits.Count; i++)
            {
                AppendCandidate(
                    result,
                    catalogTraits[i],
                    selectedTree,
                    offerKind,
                    true,
                    excludeOwnedRunTraits,
                    excludeIds
                );
            }
        }

        if (includeManualPools)
        {
            List<TraitDefinition> source = offerKind == ShopOfferKind.Trait
                ? traitPool
                : reinforcementPool;

            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    AppendCandidate(
                        result,
                        source[i],
                        selectedTree,
                        offerKind,
                        false,
                        excludeOwnedRunTraits,
                        excludeIds
                    );
                }
            }
        }

        return result;
    }

    private void AppendCandidate(
        List<TraitDefinition> result,
        TraitDefinition trait,
        WeaponTreeType selectedTree,
        ShopOfferKind offerKind,
        bool checkShopItemType,
        bool excludeOwnedRunTraits,
        HashSet<string> excludeIds)
    {
        if (result == null || trait == null)
        {
            return;
        }

        if (!trait.IsAvailableFor(selectedTree))
        {
            return;
        }

        if (checkShopItemType && !CanAppearInOfferKind(trait, offerKind))
        {
            return;
        }

        if (excludeOwnedRunTraits && ShopRunBridge.HasRunTrait(trait.TraitId))
        {
            return;
        }

        if (excludePermanentTraits && HasPermanentTrait(trait))
        {
            return;
        }

        if (excludeIds != null && excludeIds.Contains(trait.TraitId))
        {
            return;
        }

        if (ContainsTraitId(result, trait.TraitId))
        {
            return;
        }

        result.Add(trait);
    }

    private bool CanAppearInOfferKind(TraitDefinition trait, ShopOfferKind offerKind)
    {
        if (trait == null)
        {
            return false;
        }

        return offerKind switch
        {
            ShopOfferKind.Trait => trait.CanAppearAsShopTrait,
            ShopOfferKind.Reinforcement => trait.CanAppearAsShopReinforcement,
            _ => false
        };
    }

    private bool IsValidPurchasableTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        WeaponTreeType selectedTree = ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun);

        if (!trait.IsAvailableFor(selectedTree))
        {
            return false;
        }

        if (excludeAlreadyOwnedRunTraits && ShopRunBridge.HasRunTrait(trait.TraitId))
        {
            return false;
        }

        if (excludePermanentTraits && HasPermanentTrait(trait))
        {
            return false;
        }

        return true;
    }

    private bool HasPermanentTrait(TraitDefinition trait)
    {
        if (trait == null || PermanentProgress.Instance == null)
        {
            return false;
        }

        return PermanentProgress.Instance.GetTraitLevel(trait.TraitId) > 0;
    }

    private bool ContainsTraitId(List<TraitDefinition> list, string traitId)
    {
        if (list == null || string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        for (int i = 0; i < list.Count; i++)
        {
            TraitDefinition trait = list[i];

            if (trait == null)
            {
                continue;
            }

            if (trait.TraitId == traitId)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveCachedTrait(List<TraitDefinition> list, string traitId)
    {
        if (list == null || string.IsNullOrWhiteSpace(traitId))
        {
            return;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            TraitDefinition trait = list[i];

            if (trait == null || trait.TraitId == traitId)
            {
                list.RemoveAt(i);
            }
        }
    }

    private List<TraitDefinition> PickRandom(List<TraitDefinition> candidates, int count)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (candidates == null || candidates.Count == 0 || count <= 0)
        {
            return result;
        }

        count = Mathf.Min(count, candidates.Count);

        for (int i = 0; i < count; i++)
        {
            int index = UnityEngine.Random.Range(0, candidates.Count);
            result.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return result;
    }

    private void RegisterSpentCredits(ShopStructure shop, int amount)
    {
        if (shop == null || amount <= 0)
        {
            return;
        }

        FieldInfo field = typeof(ShopStructure).GetField(
            "spentCredits",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (field == null)
        {
            return;
        }

        int current = (int)field.GetValue(shop);
        field.SetValue(shop, current + amount);
    }
}