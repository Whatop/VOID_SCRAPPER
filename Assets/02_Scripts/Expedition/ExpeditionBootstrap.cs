using System.Collections.Generic;
using UnityEngine;

public class ExpeditionBootstrap : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private PlayerRuntimeStatApplier statApplier;
    [SerializeField] private PlayerReinforcementController reinforcementController;

    [Header("Catalog - Settlement  SO ")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private List<BuildingDefinition> buildingDefinitions = new List<BuildingDefinition>();

    [Header("Trait Catalog")]
    [SerializeField] private TraitCatalog traitCatalog;

    [Tooltip(" ν TraitDefinition Ʈ   ")]
    [SerializeField] private bool includeInspectorTraitDefinitions = true;

    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Reinforcement Catalog")]
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private bool includeInspectorReinforcementDefinitions = true;
    [SerializeField] private List<ReinforcementDefinition> reinforcementDefinitions = new List<ReinforcementDefinition>();
    [SerializeField] private bool restoreReinforcementFromRun = true;
    [SerializeField] private bool equipDefaultReinforcementWhenMissing = true;
    [SerializeField] private string fallbackDefaultReinforcementId = "rf_emergency_return_anchor";

    [Header("Permanent Unlock Reinforcement")]
    [Tooltip("추가특성 화면에서 ReinforcementDefinition 노드를 해금하면 새 탐사 시작 시 기본 장비 후보로 장착합니다.")]
    [SerializeField] private bool equipUnlockedPermanentReinforcementOnRunStart = true;

    [Tooltip("여기에 넣은 순서가 시작 장비 우선순위입니다. 비워두면 Reinforcement Catalog/Inspector 목록 순서를 사용합니다.")]
    [SerializeField] private List<ReinforcementDefinition> permanentStartingReinforcementPriority = new List<ReinforcementDefinition>();

    [Header("Apply Option")]
    [SerializeField] private bool refillHealthOnApply = true;
    [SerializeField] private bool applySeaRegionPlayerEffect = true;
    [SerializeField] private bool setGameStateToExpedition = true;
    [SerializeField] private bool logBootstrapResult = true;

    [Header("Debug - Expedition  ܵ ")]
    [SerializeField] private bool createDebugRunWhenMissing = true;
    [SerializeField] private WeaponTreeType debugWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string debugShipId = "basic_ship";
    [SerializeField] private ExpeditionDepth debugDepth = ExpeditionDepth.Normal;
    [SerializeField] private SeaRegionType debugSeaRegion = SeaRegionType.DenseDebris;
    [SerializeField] private bool useRandomDebugSeaRegion = true;

    private readonly List<TraitDefinition> resolvedTraitDefinitions = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> resolvedReinforcementDefinitions = new List<ReinforcementDefinition>();

    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        ResolvePlayerReferences();

        if (statApplier == null)
        {
            Debug.LogError("PlayerRuntimeStatApplier ã ߽ϴ. Expedition  Player  Ȯϼ.", this);
            return;
        }

        RunContext runContext = ResolveRunContext();
        PermanentProgress progress = PermanentProgress.Instance;
        IReadOnlyList<TraitDefinition> runtimeTraits = ResolveTraitDefinitions();
        IReadOnlyList<ReinforcementDefinition> runtimeReinforcements = ResolveReinforcementDefinitions();

        statApplier.Apply(
            runContext,
            progress,
            shipDefinitions,
            buildingDefinitions,
            runtimeTraits,
            refillHealthOnApply
        );

        RestoreReinforcement(runContext, runtimeReinforcements);

        if (applySeaRegionPlayerEffect && playerObject != null)
        {
            SeaRegionType regionType = runContext != null && runContext.IsActive
                ? runContext.SeaRegionType
                : debugSeaRegion;

            SeaRegionRuntimeApplier.ApplyToPlayer(playerObject, regionType, logBootstrapResult);
        }

        if (setGameStateToExpedition && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Expedition);
        }

        if (logBootstrapResult)
        {
            string weaponText = runContext != null ? runContext.SelectedWeaponTree.ToString() : "No RunContext";
            string shipText = runContext != null ? runContext.SelectedShipId : "No RunContext";
            string depthText = runContext != null ? runContext.ExpeditionDepth.ToString() : "No RunContext";
            string seaRegionText = runContext != null ? runContext.SeaRegionDisplayName : SeaRegionCatalog.GetDisplayName(debugSeaRegion);
            string reinforcementText = reinforcementController != null && reinforcementController.HasEquipment
                ? reinforcementController.EquippedReinforcementId
                : "None";

            Debug.Log(
                $"Expedition Bootstrap Ϸ / Weapon: {weaponText}, Ship: {shipText}, " +
                $"Depth: {depthText}, SeaRegion: {seaRegionText}, Traits: {runtimeTraits.Count}, Reinforcement: {reinforcementText}",
                this
            );
        }
    }

    private void ResolvePlayerReferences()
    {
        if (playerObject == null && !string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);

            if (foundPlayer != null)
            {
                playerObject = foundPlayer;
            }
        }

        if (playerObject == null)
        {
            PlayerRuntimeStatApplier foundApplier = FindFirstObjectByType<PlayerRuntimeStatApplier>();

            if (foundApplier != null)
            {
                statApplier = foundApplier;
                playerObject = foundApplier.gameObject;
            }

            if (playerObject == null)
            {
                return;
            }
        }

        if (statApplier == null)
        {
            statApplier = playerObject.GetComponent<PlayerRuntimeStatApplier>();
        }

        if (statApplier == null)
        {
            statApplier = playerObject.AddComponent<PlayerRuntimeStatApplier>();
        }

        if (reinforcementController == null)
        {
            reinforcementController = playerObject.GetComponent<PlayerReinforcementController>();
        }

        if (reinforcementController == null)
        {
            reinforcementController = playerObject.AddComponent<PlayerReinforcementController>();
        }
    }

    private RunContext ResolveRunContext()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun;
        }

        SeaRegionType debugRegion = useRandomDebugSeaRegion
            ? SeaRegionCatalog.GetRandom()
            : debugSeaRegion;

        if (createDebugRunWhenMissing && RunManager.Instance != null)
        {
            RunManager.Instance.StartNewRun(debugWeaponTree, debugDepth, debugShipId, debugRegion);
            return RunManager.Instance.CurrentRun;
        }

        if (createDebugRunWhenMissing)
        {
            return new RunContext(debugWeaponTree, debugDepth, debugShipId, debugRegion);
        }

        return null;
    }

    private IReadOnlyList<TraitDefinition> ResolveTraitDefinitions()
    {
        resolvedTraitDefinitions.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraitDefinitions);
        }

        if (includeInspectorTraitDefinitions && traitDefinitions != null)
        {
            for (int i = 0; i < traitDefinitions.Count; i++)
            {
                AppendUniqueTrait(resolvedTraitDefinitions, traitDefinitions[i]);
            }
        }

        return resolvedTraitDefinitions;
    }

    private IReadOnlyList<ReinforcementDefinition> ResolveReinforcementDefinitions()
    {
        resolvedReinforcementDefinitions.Clear();

        if (reinforcementCatalog != null)
        {
            reinforcementCatalog.AppendAllTo(resolvedReinforcementDefinitions);
        }

        if (includeInspectorReinforcementDefinitions && reinforcementDefinitions != null)
        {
            for (int i = 0; i < reinforcementDefinitions.Count; i++)
            {
                AppendUniqueReinforcement(resolvedReinforcementDefinitions, reinforcementDefinitions[i]);
            }
        }

        return resolvedReinforcementDefinitions;
    }

    private void RestoreReinforcement(RunContext runContext, IReadOnlyList<ReinforcementDefinition> definitions)
    {
        if (reinforcementController == null)
        {
            return;
        }

        bool restored = false;

        if (restoreReinforcementFromRun)
        {
            restored = reinforcementController.RestoreFromRunContext(runContext, definitions);
        }

        if (restored)
        {
            return;
        }

        ReinforcementDefinition permanentDefinition = FindUnlockedPermanentReinforcement(runContext, definitions);

        if (permanentDefinition != null)
        {
            reinforcementController.Equip(permanentDefinition);
            return;
        }

        if (!equipDefaultReinforcementWhenMissing)
        {
            return;
        }

        ReinforcementDefinition defaultDefinition = FindDefaultReinforcement(runContext, definitions);

        if (defaultDefinition != null)
        {
            reinforcementController.Equip(defaultDefinition);
        }
    }

    private ReinforcementDefinition FindUnlockedPermanentReinforcement(RunContext runContext, IReadOnlyList<ReinforcementDefinition> definitions)
    {
        if (!equipUnlockedPermanentReinforcementOnRunStart || PermanentProgress.Instance == null)
        {
            return null;
        }

        WeaponTreeType selectedTree = runContext != null
            ? runContext.SelectedWeaponTree
            : (PermanentProgress.Instance != null ? PermanentProgress.Instance.LastSelectedWeaponTree : debugWeaponTree);

        ReinforcementDefinition priorityDefinition = FindFirstUnlockedPermanentReinforcement(
            permanentStartingReinforcementPriority,
            selectedTree
        );

        if (priorityDefinition != null)
        {
            return priorityDefinition;
        }

        return FindFirstUnlockedPermanentReinforcement(definitions, selectedTree);
    }

    private ReinforcementDefinition FindFirstUnlockedPermanentReinforcement(
        IReadOnlyList<ReinforcementDefinition> definitions,
        WeaponTreeType selectedTree)
    {
        if (definitions == null || PermanentProgress.Instance == null)
        {
            return null;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            ReinforcementDefinition definition = definitions[i];

            if (definition == null)
            {
                continue;
            }

            if (!definition.CanUseFor(selectedTree))
            {
                continue;
            }

            if (PermanentProgress.Instance.IsTraitActive(definition.EquipmentId))
            {
                return definition;
            }
        }

        return null;
    }

    private ReinforcementDefinition FindDefaultReinforcement(RunContext runContext, IReadOnlyList<ReinforcementDefinition> definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return null;
        }

        string defaultId = fallbackDefaultReinforcementId;
        string selectedShipId = runContext != null ? runContext.SelectedShipId : debugShipId;

        for (int i = 0; i < shipDefinitions.Count; i++)
        {
            ShipDefinition ship = shipDefinitions[i];

            if (ship != null && ship.ShipId == selectedShipId)
            {
                defaultId = ship.DefaultReinforcementId;
                break;
            }
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            ReinforcementDefinition definition = definitions[i];

            if (definition != null && definition.EquipmentId == defaultId)
            {
                return definition;
            }
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            ReinforcementDefinition definition = definitions[i];

            if (definition != null && definition.HasEffect(ReinforcementEffectType.EmergencyReturn))
            {
                return definition;
            }
        }

        return null;
    }

    private void AppendUniqueTrait(List<TraitDefinition> target, TraitDefinition trait)
    {
        if (target == null || trait == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            TraitDefinition existing = target[i];

            if (existing != null && existing.TraitId == trait.TraitId)
            {
                return;
            }
        }

        target.Add(trait);
    }

    private void AppendUniqueReinforcement(List<ReinforcementDefinition> target, ReinforcementDefinition reinforcement)
    {
        if (target == null || reinforcement == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            ReinforcementDefinition existing = target[i];

            if (existing != null && existing.EquipmentId == reinforcement.EquipmentId)
            {
                return;
            }
        }

        target.Add(reinforcement);
    }
}