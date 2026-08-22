using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCargoController : MonoBehaviour
{
    [Header("Fallback")]
    [SerializeField] private int fallbackCapacity = 100;
    [Range(0f, 1f)]
    [SerializeField] private float fallbackEmergencyReturnRatio = 0.7f;

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

    private void OnEnable()
    {
        Subscribe();
        NotifyChanged();
    }

    private void OnDisable()
    {
        Unsubscribe();
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
        CargoChanged?.Invoke(CurrentLoad, MaxCapacity);
    }
}
