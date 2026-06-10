using System.Collections.Generic;
using UnityEngine;

public class ExpeditionBootstrap : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private PlayerRuntimeStatApplier statApplier;

    [Header("Catalog - Settlement과 같은 SO를 연결")]
    [SerializeField] private List<ShipDefinition> shipDefinitions = new List<ShipDefinition>();
    [SerializeField] private List<BuildingDefinition> buildingDefinitions = new List<BuildingDefinition>();

    [Header("Trait Catalog")]
    [SerializeField] private TraitCatalog traitCatalog;

    [Tooltip("기존 인스펙터 TraitDefinition 리스트도 같이 사용할지 여부")]
    [SerializeField] private bool includeInspectorTraitDefinitions = true;

    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Apply Option")]
    [SerializeField] private bool refillHealthOnApply = true;
    [SerializeField] private bool applySeaRegionPlayerEffect = true;
    [SerializeField] private bool setGameStateToExpedition = true;
    [SerializeField] private bool logBootstrapResult = true;

    [Header("Debug - Expedition 씬 단독 실행용")]
    [SerializeField] private bool createDebugRunWhenMissing = true;
    [SerializeField] private WeaponTreeType debugWeaponTree = WeaponTreeType.MachineGun;
    [SerializeField] private string debugShipId = "basic_ship";
    [SerializeField] private ExpeditionDepth debugDepth = ExpeditionDepth.Normal;
    [SerializeField] private SeaRegionType debugSeaRegion = SeaRegionType.DenseDebris;
    [SerializeField] private bool useRandomDebugSeaRegion = true;

    private readonly List<TraitDefinition> resolvedTraitDefinitions = new List<TraitDefinition>();

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
            Debug.LogError("PlayerRuntimeStatApplier를 찾지 못했습니다. Expedition 씬 Player 세팅을 확인하세요.", this);
            return;
        }

        RunContext runContext = ResolveRunContext();
        PermanentProgress progress = PermanentProgress.Instance;
        IReadOnlyList<TraitDefinition> runtimeTraits = ResolveTraitDefinitions();

        statApplier.Apply(
            runContext,
            progress,
            shipDefinitions,
            buildingDefinitions,
            runtimeTraits,
            refillHealthOnApply
        );

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

            Debug.Log(
                $"Expedition Bootstrap 완료 / Weapon: {weaponText}, Ship: {shipText}, " +
                $"Depth: {depthText}, SeaRegion: {seaRegionText}, Traits: {runtimeTraits.Count}",
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

            return;
        }

        if (statApplier == null)
        {
            statApplier = playerObject.GetComponent<PlayerRuntimeStatApplier>();
        }

        if (statApplier == null)
        {
            statApplier = playerObject.AddComponent<PlayerRuntimeStatApplier>();
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

    private void AppendUniqueTrait(List<TraitDefinition> target, TraitDefinition trait)
    {
        if (target == null || trait == null)
        {
            return;
        }

        for (int i = 0; i < target.Count; i++)
        {
            TraitDefinition existing = target[i];

            if (existing == null)
            {
                continue;
            }

            if (existing.TraitId == trait.TraitId)
            {
                return;
            }
        }

        target.Add(trait);
    }
}