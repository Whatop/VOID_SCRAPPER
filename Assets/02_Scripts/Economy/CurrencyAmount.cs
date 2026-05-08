using System;
using UnityEngine;

[Serializable]
public struct CurrencyAmount
{
    [SerializeField] private CurrencyType currencyType;
    [SerializeField] private int amount;

    public CurrencyType CurrencyType => currencyType;
    public int Amount => amount;

    public CurrencyAmount(CurrencyType currencyType, int amount)
    {
        this.currencyType = currencyType;
        this.amount = Mathf.Max(0, amount);
    }

    public bool IsValid()
    {
        return amount > 0;
    }
}