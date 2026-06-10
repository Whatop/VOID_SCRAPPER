using System;

public enum GameState
{
    Boot,
    Settlement,
    ExpeditionLoading,
    Expedition,
    BossBattle,
    ReturnChoice,
    RunResult
}

public enum RunEndReason
{
    None,
    SafeReturn,
    EmergencyReturn,
    Death,
    DebugAbort
}

public enum ExpeditionDepth
{
    Normal,
    DeepZone1
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
    Experience,
    Credits,
    ScrapParts,
    CoreShards
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
    ReturnBeacon
}

public enum EnemyType
{
    Basic,
    Shotgun,
    Charging,
    Elite,
    Boss,
    ShopDrone
}