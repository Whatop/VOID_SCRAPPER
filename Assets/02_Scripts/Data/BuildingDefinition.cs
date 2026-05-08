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

    public BuildingType BuildingType => buildingType;
    public string DisplayName => displayName;
    public string Description => description;
    public IReadOnlyList<BuildingLevelDefinition> Levels => levels;

    public BuildingLevelDefinition GetLevelDefinition(int targetLevel)
    {
        return levels.Find(level => level.Level == targetLevel);
    }
}