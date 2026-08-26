using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingModifierType
{
    MaxHpBonus,
    RepairEfficiencyBonus,
    MoveSpeedPercent,
    DashDistanceBonus,
    DashCooldownReduction,
    DamagePercent,
    ProjectileSpeedPercent,
    FireRatePercent,
    ScrapGainPercent,
    HealEfficiencyPercent,
    PickupRangeBonus,
    CreditsGainPercent
}

[Serializable]
public class BuildingModifier
{
    [SerializeField] private BuildingModifierType modifierType;
    [SerializeField] private float value;

    public BuildingModifierType ModifierType => modifierType;
    public float Value => value;
}

[Serializable]
public class BuildingLevelDefinition
{
    [SerializeField] private int level = 1;
    [SerializeField] private int scrapCost;
    [SerializeField] private int coreShardCost;

    [TextArea]
    [SerializeField] private string effectDescription;

    [SerializeField] private List<BuildingModifier> modifiers = new List<BuildingModifier>();

    public int Level => level;
    public int ScrapCost => scrapCost;
    public int CoreShardCost => coreShardCost;
    public string EffectDescription => effectDescription;
    public IReadOnlyList<BuildingModifier> Modifiers => modifiers;
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Buildings/Building Definition")]
public class BuildingDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;

    [Header("Levels")]
    [SerializeField] private List<BuildingLevelDefinition> levels = new List<BuildingLevelDefinition>();

    [Header("Settlement Restoration")]
    [SerializeField] private BossStoryPart requiredBossStoryPart = BossStoryPart.None;
    [Min(0)]
    [SerializeField] private int requiredSynchronizationStage;
    [SerializeField] private bool requiresPriorRestoration;
    [SerializeField] private BuildingType priorRestorationBuilding;
    [SerializeField] private string requiredUnlockFlag;
    [TextArea]
    [SerializeField] private string completionResultDescription;
    [SerializeField] private string grantedUnlockFlag;
    [SerializeField] private string visualStateKey;
    [SerializeField] private string restorationActionLabel = "복구";

    public BuildingType BuildingType => buildingType;
    public string DisplayName => displayName;
    public string Description => description;
    public IReadOnlyList<BuildingLevelDefinition> Levels => levels;
    public BossStoryPart RequiredBossStoryPart => requiredBossStoryPart;
    public int RequiredSynchronizationStage => Mathf.Max(0, requiredSynchronizationStage);
    public bool RequiresPriorRestoration => requiresPriorRestoration;
    public BuildingType PriorRestorationBuilding => priorRestorationBuilding;
    public string RequiredUnlockFlag => requiredUnlockFlag ?? string.Empty;
    public string CompletionResultDescription => completionResultDescription ?? string.Empty;
    public string GrantedUnlockFlag => grantedUnlockFlag ?? string.Empty;
    public string VisualStateKey => visualStateKey ?? string.Empty;
    public string RestorationActionLabel => string.IsNullOrWhiteSpace(restorationActionLabel)
        ? "복구"
        : restorationActionLabel;

    public BuildingLevelDefinition GetLevelDefinition(int targetLevel)
    {
        return levels.Find(level => level.Level == targetLevel);
    }
}
