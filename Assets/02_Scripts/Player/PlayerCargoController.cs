using System;
using System.Collections.Generic;
using UnityEngine;

public enum CargoLoadState
{
    Normal,
    Overloaded,
    Critical,
    Full
}

[DisallowMultipleComponent]
public class PlayerCargoController : MonoBehaviour
{
    private static readonly CurrencyType[] SupportedCargoTypesInternal =
    {
        CurrencyType.ScrapParts,
        CurrencyType.CoreShards,
        CurrencyType.StabilizedAlloy
    };

    [Header("Fallback")]
    [SerializeField] private int fallbackCapacity = 100;
    [Range(0f, 1f)]
    [SerializeField] private float fallbackEmergencyReturnRatio = 0.7f;

    [Header("Load Decisions")]
    [SerializeField, Range(0f, 1f)] private float overloadedRatio = 0.8f;
    [SerializeField, Range(0f, 1f)] private float criticalRatio = 0.95f;
    [SerializeField, Range(0.1f, 1f)] private float overloadedMoveSpeedMultiplier = 0.95f;
    [SerializeField, Min(1f)] private float overloadedDashCooldownMultiplier = 1.1f;
    [SerializeField, Range(0.1f, 1f)] private float criticalMoveSpeedMultiplier = 0.92f;
    [SerializeField, Min(1f)] private float criticalDashCooldownMultiplier = 1.2f;

    private PlayerController2D playerController;
    private PlayerDash playerDash;
    private CargoLoadState loadState;
    private int exactOwnedRecollectionDepth;

    public int MaxCapacity
    {
        get
        {
            if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
            {
                return RunManager.Instance.CurrentRun.MaxCargoCapacity;
            }

            return Mathf.Max(1, fallbackCapacity);
        }
    }

    public int CurrentLoad
    {
        get
        {
            if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
            {
                return RunManager.Instance.CurrentRun.CurrentCargoLoad;
            }

            return 0;
        }
    }

    public int FreeCapacity => Mathf.Max(0, MaxCapacity - CurrentLoad);
    public float CargoRatio => MaxCapacity <= 0 ? 0f : Mathf.Clamp01(CurrentLoad / (float)MaxCapacity);
    public float OverloadedRatio => Mathf.Clamp01(overloadedRatio);
    public float CriticalRatio => Mathf.Clamp(criticalRatio, OverloadedRatio, 1f);
    public CargoLoadState LoadState => loadState;
    public bool RequiresManualPickup => loadState >= CargoLoadState.Overloaded;
    public bool IsApplyingExactOwnedRecollection => exactOwnedRecollectionDepth > 0;
    public static IReadOnlyList<CurrencyType> SupportedCargoTypes => SupportedCargoTypesInternal;

    public float EmergencyReturnRatio
    {
        get
        {
            if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
            {
                return RunManager.Instance.CurrentRun.EmergencyReturnCapacityRatio;
            }

            return Mathf.Clamp01(fallbackEmergencyReturnRatio);
        }
    }

    public int EmergencyReturnCapacityLimit => Mathf.FloorToInt(MaxCapacity * EmergencyReturnRatio);

    public event Action<int, int> CargoChanged;
    public event Action CargoFullRejected;
    public event Action<CargoLoadState, CargoLoadState> LoadStateChanged;
    public event Action<CurrencyType, bool> AutoPickupPreferenceChanged;

    private void OnValidate()
    {
        overloadedRatio = Mathf.Clamp01(overloadedRatio);
        criticalRatio = Mathf.Clamp(criticalRatio, overloadedRatio, 1f);
        overloadedMoveSpeedMultiplier = Mathf.Clamp(overloadedMoveSpeedMultiplier, 0.1f, 1f);
        criticalMoveSpeedMultiplier = Mathf.Clamp(criticalMoveSpeedMultiplier, 0.1f, overloadedMoveSpeedMultiplier);
        overloadedDashCooldownMultiplier = Mathf.Max(1f, overloadedDashCooldownMultiplier);
        criticalDashCooldownMultiplier = Mathf.Max(overloadedDashCooldownMultiplier, criticalDashCooldownMultiplier);
    }

    private void Awake()
    {
        CacheMovementReferences();
    }

    private void OnEnable()
    {
        Subscribe();
        NotifyChanged();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearLoadModifiers();
    }

    public void SetRuntimeCargoRule(int maxCapacity, float emergencyReturnRatio)
    {
        fallbackCapacity = Mathf.Max(1, maxCapacity);
        fallbackEmergencyReturnRatio = Mathf.Clamp01(emergencyReturnRatio);

        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            RunContext runContext = RunManager.Instance.CurrentRun;
            runContext.SetCargoRule(
                fallbackCapacity,
                fallbackEmergencyReturnRatio,
                runContext.ScrapCargoWeight,
                runContext.CoreShardCargoWeight,
                runContext.StabilizedAlloyCargoWeight
            );
        }

        NotifyChanged();
    }

    public bool CanAdd(CurrencyType currencyType, int amount = 1)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        return RunManager.Instance.CurrentRun.GetAcceptedAmountByCargo(currencyType, amount) > 0;
    }

    public int GetAcceptedAmount(CurrencyType currencyType, int requestedAmount)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return 0;
        }

        return RunManager.Instance.CurrentRun.GetAcceptedAmountByCargo(currencyType, requestedAmount);
    }

    public int GetCargoWeight(CurrencyType currencyType)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return 0;
        }

        return RunManager.Instance.CurrentRun.GetCargoWeight(currencyType);
    }

    public int GetCargoAmount(CurrencyType currencyType)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return 0;
        }

        return RunManager.Instance.CurrentRun.Wallet.GetAmount(currencyType);
    }

    public bool UsesCargo(CurrencyType currencyType)
    {
        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               RunManager.Instance.CurrentRun.UsesCargo(currencyType);
    }

    public bool CanJettison(CurrencyType currencyType)
    {
        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               (RunManager.Instance.CurrentRun.UsesCargo(currencyType) ||
                currencyType == CurrencyType.Credits);
    }

    public int GetCargoContribution(CurrencyType currencyType)
    {
        return GetCargoAmount(currencyType) * GetCargoWeight(currencyType);
    }

    public bool IsAutoPickupEnabled(CurrencyType currencyType)
    {
        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               RunManager.Instance.CurrentRun.IsCargoAutoPickupEnabled(currencyType);
    }

    public bool SetAutoPickupEnabled(CurrencyType currencyType, bool enabled)
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return false;
        }

        if (!RunManager.Instance.CurrentRun.SetCargoAutoPickupEnabled(currencyType, enabled))
        {
            return false;
        }

        AutoPickupPreferenceChanged?.Invoke(currencyType, enabled);
        return true;
    }

    public bool TryRemoveCargo(CurrencyType currencyType, int amount)
    {
        return RunManager.Instance != null &&
               RunManager.Instance.TryRemoveCargoCurrency(currencyType, amount);
    }

    public bool TryJettison(
        CurrencyType currencyType,
        int amount,
        RewardPickup pickupPrefab,
        Vector2 position,
        Vector2 initialVelocity,
        float recollectionBlockSeconds,
        out RewardPickup spawnedPickup)
    {
        spawnedPickup = null;

        if (amount <= 0 ||
            pickupPrefab == null ||
            !CanJettison(currencyType) ||
            GetCargoAmount(currencyType) < amount)
        {
            return false;
        }

        GameObject pickupObject = PoolManager.Instance != null
            ? PoolManager.Instance.Get(pickupPrefab.gameObject, position, Quaternion.identity)
            : Instantiate(pickupPrefab.gameObject, position, Quaternion.identity);
        spawnedPickup = pickupObject != null ? pickupObject.GetComponent<RewardPickup>() : null;

        if (spawnedPickup == null)
        {
            ReleasePickupObject(pickupObject);
            return false;
        }

        spawnedPickup.InitializeOwnedCurrency(
            currencyType,
            amount,
            initialVelocity,
            recollectionBlockSeconds
        );

        if (TryRemoveJettisonCurrency(currencyType, amount))
        {
            return true;
        }

        ReleasePickupObject(pickupObject);
        spawnedPickup = null;
        return false;
    }

    public int AddExactOwnedCurrency(CurrencyType currencyType, int amount)
    {
        if (amount <= 0 || !CanJettison(currencyType) || RunManager.Instance == null)
        {
            return 0;
        }

        exactOwnedRecollectionDepth++;

        try
        {
            return RunManager.Instance.AddCurrencyRespectingCargo(currencyType, amount);
        }
        finally
        {
            exactOwnedRecollectionDepth = Mathf.Max(0, exactOwnedRecollectionDepth - 1);
        }
    }

    private bool TryRemoveJettisonCurrency(CurrencyType currencyType, int amount)
    {
        if (UsesCargo(currencyType))
        {
            return TryRemoveCargo(currencyType, amount);
        }

        return currencyType == CurrencyType.Credits &&
               RunManager.Instance != null &&
               RunManager.Instance.TrySpendCredits(amount);
    }

    public void NotifyCargoFullRejected()
    {
        CargoFullRejected?.Invoke();
    }

    private void Subscribe()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged += HandleWalletChanged;
            RunManager.Instance.RunStarted += HandleRunStarted;
        }
    }

    private void Unsubscribe()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.WalletChanged -= HandleWalletChanged;
            RunManager.Instance.RunStarted -= HandleRunStarted;
        }
    }

    private void HandleWalletChanged(RunWallet wallet)
    {
        NotifyChanged();
    }

    private void HandleRunStarted(RunContext runContext)
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        CargoLoadState previousState = loadState;
        loadState = ResolveLoadState();
        ApplyLoadModifiers();
        CargoChanged?.Invoke(CurrentLoad, MaxCapacity);

        if (previousState != loadState)
        {
            LoadStateChanged?.Invoke(previousState, loadState);
        }
    }

    private CargoLoadState ResolveLoadState()
    {
        float ratio = CargoRatio;

        if (ratio >= 0.999f)
        {
            return CargoLoadState.Full;
        }

        if (ratio >= CriticalRatio)
        {
            return CargoLoadState.Critical;
        }

        return ratio >= OverloadedRatio
            ? CargoLoadState.Overloaded
            : CargoLoadState.Normal;
    }

    private void CacheMovementReferences()
    {
        playerController ??= GetComponent<PlayerController2D>();
        playerDash ??= GetComponent<PlayerDash>();
    }

    private void ApplyLoadModifiers()
    {
        CacheMovementReferences();

        float moveMultiplier = 1f;
        float dashMultiplier = 1f;

        if (loadState >= CargoLoadState.Critical)
        {
            moveMultiplier = criticalMoveSpeedMultiplier;
            dashMultiplier = criticalDashCooldownMultiplier;
        }
        else if (loadState >= CargoLoadState.Overloaded)
        {
            moveMultiplier = overloadedMoveSpeedMultiplier;
            dashMultiplier = overloadedDashCooldownMultiplier;
        }

        playerController?.SetExternalMoveSpeedMultiplier(this, moveMultiplier);
        playerDash?.SetExternalCooldownMultiplier(this, dashMultiplier);
    }

    private void ClearLoadModifiers()
    {
        playerController?.ClearExternalMoveSpeedMultiplier(this);
        playerDash?.ClearExternalCooldownMultiplier(this);
    }

    private static void ReleasePickupObject(GameObject pickupObject)
    {
        if (pickupObject == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(pickupObject);
        }
        else
        {
            Destroy(pickupObject);
        }
    }
}
