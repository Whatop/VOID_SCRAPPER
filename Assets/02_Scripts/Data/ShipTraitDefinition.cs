using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Traits/Ship Trait Definition")]
public class ShipTraitDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string traitId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Ship Requirement")]
    [SerializeField] private bool availableForAllShips;
    [SerializeField] private string requiredShipId = "basic_ship";

    [Header("Tree Position")]
    [Tooltip("가로 진행 단계. 1-2-3-4 구조에서 1, 2, 3, 4.")]
    [SerializeField] private int stage = 1;

    [Tooltip("세로 분기 라인. 0은 중앙, -1은 위, 1은 아래.")]
    [SerializeField] private int lane = 0;

    [Tooltip("자동 배치 대신 직접 좌표를 쓸지 여부.")]
    [SerializeField] private bool useCustomAnchoredPosition;

    [SerializeField] private Vector2 customAnchoredPosition;

    [Header("Prerequisites")]
    [Tooltip("이 특성을 해금하기 전에 먼저 해금되어야 하는 특성 ID 목록.")]
    [SerializeField] private List<string> prerequisiteTraitIds = new List<string>();

    [Header("Unlock Cost")]
    [SerializeField] private int scrapCost = 10;
    [SerializeField] private int coreShardCost;

    [Header("Effects")]
    [SerializeField] private List<TraitLevelEffect> effects = new List<TraitLevelEffect>();

    public string TraitId => string.IsNullOrWhiteSpace(traitId) ? name : traitId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TraitId : displayName;
    public string Description => string.IsNullOrWhiteSpace(description) ? "특성 설명이 없습니다." : description;
    public Sprite Icon => icon;

    public bool AvailableForAllShips => availableForAllShips;
    public string RequiredShipId => string.IsNullOrWhiteSpace(requiredShipId) ? "basic_ship" : requiredShipId;

    public int Stage => Mathf.Max(1, stage);
    public int Lane => lane;

    public bool UseCustomAnchoredPosition => useCustomAnchoredPosition;
    public Vector2 CustomAnchoredPosition => customAnchoredPosition;

    public IReadOnlyList<string> PrerequisiteTraitIds => prerequisiteTraitIds;

    public int ScrapCost => Mathf.Max(0, scrapCost);
    public int CoreShardCost => Mathf.Max(0, coreShardCost);

    public IReadOnlyList<TraitLevelEffect> Effects => effects;

    public bool BelongsToShip(string shipId)
    {
        if (availableForAllShips)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(shipId))
        {
            return false;
        }

        return RequiredShipId == shipId;
    }
}