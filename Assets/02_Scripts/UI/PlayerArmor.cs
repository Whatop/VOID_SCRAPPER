using System;
using UnityEngine;

public class PlayerArmor : MonoBehaviour
{
    [Header("Armor")]
    [SerializeField] private float maxArmor = 0f;
    [SerializeField] private float currentArmor = 0f;

    public float CurrentArmor => currentArmor;
    public float MaxArmor => maxArmor;
    public float ArmorRatio => maxArmor <= 0f ? 0f : currentArmor / maxArmor;
    public bool HasArmor => currentArmor > 0f;

    public event Action<float, float> Changed;

    private void OnEnable()
    {
        currentArmor = Mathf.Clamp(currentArmor, 0f, maxArmor);
        Changed?.Invoke(currentArmor, maxArmor);
    }

    public void SetMaxArmor(float value, bool refill)
    {
        maxArmor = Mathf.Max(0f, value);

        if (refill)
        {
            currentArmor = maxArmor;
        }
        else
        {
            currentArmor = Mathf.Clamp(currentArmor, 0f, maxArmor);
        }

        Changed?.Invoke(currentArmor, maxArmor);
    }

    public void SetArmor(float value)
    {
        currentArmor = Mathf.Clamp(value, 0f, maxArmor);
        Changed?.Invoke(currentArmor, maxArmor);
    }

    public void RestoreCurrentArmor(float value)
    {
        currentArmor = Mathf.Clamp(value, 0f, maxArmor);
        Changed?.Invoke(currentArmor, maxArmor);
    }

    public void AddArmor(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        SetArmor(currentArmor + amount);
    }

    public float AbsorbDamage(float damage)
    {
        if (damage <= 0f)
        {
            return 0f;
        }

        if (currentArmor <= 0f)
        {
            return damage;
        }

        float absorbed = Mathf.Min(currentArmor, damage);
        currentArmor -= absorbed;
        Changed?.Invoke(currentArmor, maxArmor);

        return damage - absorbed;
    }
}
