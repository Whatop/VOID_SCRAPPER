using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ShopStockController : MonoBehaviour
{
    [Header("Trait Catalog")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeCatalogTraits = true;
    [SerializeField] private bool includeManualTraitPool = true;

    [Header("Trait Items")]
    [SerializeField] private int traitCost = 45;
    [SerializeField] private int traitChoiceCount = 3;
    [SerializeField] private List<TraitDefinition> traitPool = new List<TraitDefinition>();

    [Header("Reinforcement Catalog")]
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private bool includeCatalogReinforcements = true;
    [SerializeField] private bool includeManualReinforcementPool = true;

    [Header("Reinforcement Items")]
    [SerializeField] private int fallbackReinforcementCost = 60;
    [SerializeField] private int minReinforcementChoiceCount = 1;
    [SerializeField] private int maxReinforcementChoiceCount = 3;
    [SerializeField] private List<ReinforcementDefinition> reinforcementPool = new List<ReinforcementDefinition>();

    [Header("Purchase Limit")]
    [Tooltip("기본 false. true면 이 상점에서 추가 특성을 1개만 살 수 있습니다.")]
    [SerializeField] private bool limitTraitPurchasePerShop;

    [Tooltip("기본 false. true면 이 상점에서 Reinforcement를 1개만 살 수 있습니다.")]
    [SerializeField] private bool limitReinforcementPurchasePerShop;

    [Header("Duplicate Rule")]
    [Tooltip("켜면 이미 현재 런에서 가진 특성은 후보 목록에서 제외합니다. 구매 가능 판정에서는 항상 중복 구매를 막습니다.")]
    [SerializeField] private bool excludeAlreadyOwnedRunTraits = true;

    [SerializeField] private bool excludePermanentTraits;

    [Tooltip("켜면 현재 장착 중인 Reinforcement는 같은 상점 후보에서 제외합니다.")]
    [SerializeField] private bool excludeCurrentlyEquippedReinforcement = true;

    private readonly List<TraitDefinition> cachedTraitChoices = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> cachedReinforcementChoices = new List<ReinforcementDefinition>();

    private readonly HashSet<string> boughtTraitIdsThisShop = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> boughtReinforcementIdsThisShop = new HashSet<string>(StringComparer.Ordinal);

    private bool stockRolled;
    private bool boughtTraitThisShop;
    private bool boughtReinforcementThisShop;

    public TraitCatalog TraitCatalog => traitCatalog;
    public ReinforcementCatalog ReinforcementCatalog => reinforcementCatalog;
    public int TraitCost => Mathf.Max(0, traitCost);
    public int FallbackReinforcementCost => Mathf.Max(0, fallbackReinforcementCost);

    public bool TraitSoldOut => limitTraitPurchasePerShop && boughtTraitThisShop;
    public bool ReinforcementSoldOut => limitReinforcementPurchasePerShop && boughtReinforcementThisShop;

    public IReadOnlyList<TraitDefinition> TraitPool => traitPool;
    public IReadOnlyList<ReinforcementDefinition> ReinforcementPool => reinforcementPool;

    public bool WasTraitPurchased(TraitDefinition trait)
    {
        return trait != null && boughtTraitIdsThisShop.Contains(trait.TraitId);
    }

    public bool WasReinforcementPurchased(ReinforcementDefinition reinforcement)
    {
        return reinforcement != null &&
               boughtReinforcementIdsThisShop.Contains(reinforcement.EquipmentId);
    }

    private void OnEnable()
    {
        ResetStockRuntime();
    }

    public void ResetStockRuntime()
    {
        stockRolled = false;

        boughtTraitThisShop = false;
        boughtReinforcementThisShop = false;

        boughtTraitIdsThisShop.Clear();
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

    public List<ReinforcementDefinition> RollReinforcementChoices()
    {
        EnsureStockRolled();
        return new List<ReinforcementDefinition>(cachedReinforcementChoices);
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

        if (boughtTraitIdsThisShop.Contains(trait.TraitId))
        {
            return false;
        }

        if (RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId) >= trait.MaxLevel)
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

        if (!RunTraitAcquisitionService.TryAcquire(
                trait,
                playerObject,
                out _,
                out _))
        {
            ShopRunBridge.AddCredits(TraitCost);
            return false;
        }

        boughtTraitThisShop = true;
        boughtTraitIdsThisShop.Add(trait.TraitId);

        if (limitTraitPurchasePerShop)
        {
            cachedTraitChoices.Clear();
        }
        else
        {
            RemoveCachedTrait(cachedTraitChoices, trait.TraitId);
        }

        shop?.RegisterSpentCredits(TraitCost);
        return true;
    }

    public int GetReinforcementCost(ReinforcementDefinition reinforcement)
    {
        int baseCost = reinforcement != null && reinforcement.Cost > 0
            ? reinforcement.Cost
            : FallbackReinforcementCost;

        float rarityMultiplier = reinforcement != null ? reinforcement.EffectivePriceMultiplier : 1f;
        float depthMultiplier = ResolveDepthPriceMultiplier();

        return Mathf.Max(0, Mathf.RoundToInt(baseCost * rarityMultiplier * depthMultiplier));
    }

    private float ResolveDepthPriceMultiplier()
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return 1f;
        }

        return CampaignProgressionCatalog.GetShopPriceMultiplier(
            RunManager.Instance.CurrentRun.ExpeditionDepth
        );
    }

    public bool CanBuyReinforcement(ReinforcementDefinition reinforcement)
    {
        return CanBuyReinforcement(reinforcement, null, null);
    }

    public bool CanBuyReinforcement(ReinforcementDefinition reinforcement, ShopStructure shop, GameObject playerObject)
    {
        if (ReinforcementSoldOut)
        {
            return false;
        }

        if (!IsValidPurchasableReinforcement(reinforcement))
        {
            return false;
        }

        if (boughtReinforcementIdsThisShop.Contains(reinforcement.EquipmentId))
        {
            return false;
        }

        if (!CanStoreCurrentReinforcementInShop(shop, playerObject, reinforcement))
        {
            return false;
        }

        return ShopRunBridge.CanSpendCredits(GetReinforcementCost(reinforcement));
    }

    public bool TryBuyReinforcement(ShopStructure shop, ReinforcementDefinition reinforcement, GameObject playerObject)
    {
        if (shop != null && !shop.CanTrade)
        {
            return false;
        }

        if (!CanBuyReinforcement(reinforcement, shop, playerObject))
        {
            return false;
        }

        int cost = GetReinforcementCost(reinforcement);

        if (!ShopRunBridge.TrySpendCredits(cost))
        {
            return false;
        }

        PlayerReinforcementController controller = playerObject != null
            ? playerObject.GetComponentInChildren<PlayerReinforcementController>(true)
            : null;

        if (controller == null && playerObject != null)
        {
            controller = playerObject.AddComponent<PlayerReinforcementController>();
        }

        bool equipped = controller != null && controller.EquipFromShop(
            reinforcement,
            shop != null ? shop.ActiveMaintenanceBay : null
        );

        if (!equipped)
        {
            ShopRunBridge.AddCredits(cost);
            return false;
        }

        boughtReinforcementThisShop = true;
        boughtReinforcementIdsThisShop.Add(reinforcement.EquipmentId);

        if (limitReinforcementPurchasePerShop)
        {
            cachedReinforcementChoices.Clear();
        }
        else
        {
            RemoveCachedReinforcement(cachedReinforcementChoices, reinforcement.EquipmentId);
        }

        shop?.RegisterSpentCredits(cost);
        return true;
    }

    public string BuildOptionDescription(TraitDefinition trait)
    {
        return trait != null ? trait.Description : "설명 없음";
    }

    public string BuildOptionDescription(ReinforcementDefinition reinforcement)
    {
        if (reinforcement == null)
        {
            return "설명 없음";
        }

        return
            $"{reinforcement.GetUseTypeText()}\n" +
            $"{reinforcement.GetAvailabilityText()}\n\n" +
            $"{reinforcement.Description}\n\n" +
            $"효과\n{reinforcement.BuildEffectSummary()}";
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

        WeaponTreeType selectedTree = ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun);

        if (!TraitSoldOut)
        {
            List<TraitDefinition> traitCandidates = BuildAvailableTraitCandidates(selectedTree);
            cachedTraitChoices.AddRange(PickRandom(traitCandidates, Mathf.Max(1, traitChoiceCount)));
        }

        if (!ReinforcementSoldOut)
        {
            int minCount = Mathf.Max(1, minReinforcementChoiceCount);
            int maxCount = Mathf.Max(minCount, maxReinforcementChoiceCount);
            int count = UnityEngine.Random.Range(minCount, maxCount + 1);

            List<ReinforcementDefinition> reinforcementCandidates = BuildAvailableReinforcementCandidates(selectedTree);
            cachedReinforcementChoices.AddRange(PickRandom(reinforcementCandidates, count));
        }
    }

    private List<TraitDefinition> BuildAvailableTraitCandidates(WeaponTreeType selectedTree)
    {
        List<TraitDefinition> result = new List<TraitDefinition>();

        if (includeCatalogTraits && traitCatalog != null)
        {
            List<TraitDefinition> catalogTraits = new List<TraitDefinition>();
            traitCatalog.AppendAllTo(catalogTraits);

            for (int i = 0; i < catalogTraits.Count; i++)
            {
                AppendTraitCandidate(result, catalogTraits[i], selectedTree);
            }
        }

        if (includeManualTraitPool && traitPool != null)
        {
            for (int i = 0; i < traitPool.Count; i++)
            {
                AppendTraitCandidate(result, traitPool[i], selectedTree);
            }
        }

        return result;
    }

    private List<ReinforcementDefinition> BuildAvailableReinforcementCandidates(WeaponTreeType selectedTree)
    {
        List<ReinforcementDefinition> result = new List<ReinforcementDefinition>();

        if (includeCatalogReinforcements && reinforcementCatalog != null)
        {
            List<ReinforcementDefinition> catalogReinforcements = new List<ReinforcementDefinition>();
            reinforcementCatalog.AppendAllTo(catalogReinforcements);

            for (int i = 0; i < catalogReinforcements.Count; i++)
            {
                AppendReinforcementCandidate(result, catalogReinforcements[i], selectedTree);
            }
        }

        if (includeManualReinforcementPool && reinforcementPool != null)
        {
            for (int i = 0; i < reinforcementPool.Count; i++)
            {
                AppendReinforcementCandidate(result, reinforcementPool[i], selectedTree);
            }
        }

        return result;
    }

    private void AppendTraitCandidate(List<TraitDefinition> result, TraitDefinition trait, WeaponTreeType selectedTree)
    {
        if (result == null || trait == null)
        {
            return;
        }

        if (!trait.CanAppearAsShopTrait)
        {
            return;
        }

        if (!trait.IsAvailableFor(selectedTree))
        {
            return;
        }

        if (!RunTraitAcquisitionService.MeetsOfferPrerequisites(trait))
        {
            return;
        }

        int runtimeLevel = RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId);

        if (runtimeLevel >= trait.MaxLevel)
        {
            return;
        }

        if (excludeAlreadyOwnedRunTraits && runtimeLevel > 0)
        {
            return;
        }

        if (excludePermanentTraits && HasPermanentTrait(trait))
        {
            return;
        }

        if (boughtTraitIdsThisShop.Contains(trait.TraitId))
        {
            return;
        }

        if (ContainsTraitId(result, trait.TraitId))
        {
            return;
        }

        result.Add(trait);
    }

    private void AppendReinforcementCandidate(
        List<ReinforcementDefinition> result,
        ReinforcementDefinition reinforcement,
        WeaponTreeType selectedTree)
    {
        if (result == null || reinforcement == null)
        {
            return;
        }

        if (!reinforcement.CanAppearInShop)
        {
            return;
        }

        if (!reinforcement.CanUseFor(selectedTree))
        {
            return;
        }

        if (boughtReinforcementIdsThisShop.Contains(reinforcement.EquipmentId))
        {
            return;
        }

        if (excludeCurrentlyEquippedReinforcement &&
            ShopRunBridge.HasEquippedReinforcement(reinforcement.EquipmentId))
        {
            return;
        }

        if (ContainsReinforcementId(result, reinforcement.EquipmentId))
        {
            return;
        }

        result.Add(reinforcement);
    }

    private bool IsValidPurchasableTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        if (!trait.CanAppearAsShopTrait)
        {
            return false;
        }

        WeaponTreeType selectedTree = ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun);

        if (!trait.IsAvailableFor(selectedTree))
        {
            return false;
        }

        if (!RunTraitAcquisitionService.MeetsOfferPrerequisites(trait))
        {
            return false;
        }

        int runtimeLevel = RunRuntimeTraitStore.Instance.GetLevel(trait.TraitId);

        if (runtimeLevel >= trait.MaxLevel)
        {
            return false;
        }

        if (excludeAlreadyOwnedRunTraits && runtimeLevel > 0)
        {
            return false;
        }

        if (excludePermanentTraits && HasPermanentTrait(trait))
        {
            return false;
        }

        return true;
    }

    private bool CanStoreCurrentReinforcementInShop(
        ShopStructure shop,
        GameObject playerObject,
        ReinforcementDefinition newReinforcement)
    {
        if (shop == null || playerObject == null)
        {
            return true;
        }

        PlayerReinforcementController controller = playerObject.GetComponentInParent<PlayerReinforcementController>();

        if (controller == null)
        {
            controller = playerObject.GetComponentInChildren<PlayerReinforcementController>(true);
        }

        if (controller == null || !controller.HasEquipment)
        {
            return true;
        }

        if (controller.EquippedDefinition == null || controller.EquippedDefinition == newReinforcement)
        {
            return true;
        }

        ShopActiveMaintenanceBay maintenanceBay = shop.ActiveMaintenanceBay;
        return maintenanceBay == null || maintenanceBay.CanStore(controller.EquippedDefinition);
    }

    private bool IsValidPurchasableReinforcement(ReinforcementDefinition reinforcement)
    {
        if (reinforcement == null)
        {
            return false;
        }

        if (!reinforcement.CanAppearInShop)
        {
            return false;
        }

        WeaponTreeType selectedTree = ShopRunBridge.GetSelectedWeaponTree(WeaponTreeType.MachineGun);

        if (!reinforcement.CanUseFor(selectedTree))
        {
            return false;
        }

        if (excludeCurrentlyEquippedReinforcement &&
            ShopRunBridge.HasEquippedReinforcement(reinforcement.EquipmentId))
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

            if (trait != null && trait.TraitId == traitId)
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsReinforcementId(List<ReinforcementDefinition> list, string equipmentId)
    {
        if (list == null || string.IsNullOrWhiteSpace(equipmentId))
        {
            return false;
        }

        for (int i = 0; i < list.Count; i++)
        {
            ReinforcementDefinition reinforcement = list[i];

            if (reinforcement != null && reinforcement.EquipmentId == equipmentId)
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

    private void RemoveCachedReinforcement(List<ReinforcementDefinition> list, string equipmentId)
    {
        if (list == null || string.IsNullOrWhiteSpace(equipmentId))
        {
            return;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            ReinforcementDefinition reinforcement = list[i];

            if (reinforcement == null || reinforcement.EquipmentId == equipmentId)
            {
                list.RemoveAt(i);
            }
        }
    }

    private List<T> PickRandom<T>(List<T> candidates, int count) where T : UnityEngine.Object
    {
        List<T> result = new List<T>();

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
}
