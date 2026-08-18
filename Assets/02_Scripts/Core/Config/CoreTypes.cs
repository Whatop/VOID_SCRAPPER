using System;

public enum GameState
{
    Boot,
    Settlement,
    ExpeditionLoading,
    Expedition,
    BossBattle,
    ReturnChoice,
    RunResult,

    // Campaign extension. Appended to preserve existing serialized enum values.
    SettlementDefense,
    FinalBossBattle,

    // Tutorial extension. Appended to preserve existing serialized enum values.
    Tutorial
}

public enum RunEndReason
{
    None,
    SafeReturn,
    EmergencyReturn,
    Death,
    DebugAbort,

    // Final campaign clear. Uses the same full resource commitment rule as SafeReturn.
    FinalVictory
}

public enum ExpeditionDepth
{
    // Region 1: Outer Debris Sea.
    Normal = 0,

    // Region 2: Logistics Junction.
    DeepZone1 = 1,

    // Region 3: Central Lockdown Zone.
    DeepZone2 = 2,

    // Hand-authored final route opened from the settlement Route Heart.
    FinalNetwork = 3
}

public enum CampaignBossId
{
    None = 0,
    SectorAdministrator = 1,
    SalvageDevourer = 2,
    PhaseGatekeeper = 3,
    NullDispatcher = 4
}

public enum BossStoryPart
{
    None = 0,
    SectorStabilizer = 1,
    MatterCompressor = 2,
    PhaseNavigationLens = 3
}

public enum RouteCoreState
{
    MissingParts = 0,
    ReadyToAssemble = 1,
    Assembled = 2,
    Activated = 3
}

public enum SeaRegionType
{
    DenseDebris,
    ElectromagneticStorm,
    RaiderOccupied
}

public enum WeaponTreeType
{
    Shotgun,
    Sniper,
    MachineGun
}

public enum CurrencyType
{
    // Legacy value. 신규 보상에는 사용하지 않는다.
    Experience,
    Credits,
    ScrapParts,
    CoreShards,
    TuningChips,

    // Sector 1 optional permanent-growth cargo. Appended for serialized enum safety.
    StabilizedAlloy
}

public enum BuildingType
{
    Hangar,
    EngineWorkshop,
    WeaponLab,
    RecoveryProcessor
}

public enum RadarMarkerType
{
    Enemy,
    RewardObject,
    Meteor,
    Shop,
    Event,
    Boss,
    Core,
    ReturnBeacon,
    EnemyBase
}

public enum EnemyType
{
    Basic = 0,
    Shotgun = 1,
    Charging = 2,

    // 기존 에셋 호환용. 신규 엘리트 에셋은 아래 3개 타입을 사용한다.
    Elite = 3,

    Boss = 4,
    ShopDrone = 5,

    EliteMachineGun = 6,
    EliteShotgun = 7,
    EliteCharging = 8,

    // 경로 예고 후 직선 돌진하는 근접형 적.
    MeleeCharger = 9
}
