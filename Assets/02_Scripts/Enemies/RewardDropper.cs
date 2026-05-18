using System.Collections.Generic;
using UnityEngine;

public class RewardDropper : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private RewardDefinition rewardDefinition;

    [Header("Pickup Prefab")]
    [SerializeField] private GameObject rewardPickupPrefab;
    [SerializeField] private GameObject healPickupPrefab;

    [Header("Shard Split")]
    [SerializeField] private int amountPerShard = 1;
    [SerializeField] private int maxShardsPerCurrency = 24;

    [Header("Scatter")]
    [SerializeField] private float minScatterRadius = 0.15f;
    [SerializeField] private float maxScatterRadius = 0.75f;
    [SerializeField] private float minScatterSpeed = 1.2f;
    [SerializeField] private float maxScatterSpeed = 3.2f;
    [SerializeField] private float angleJitter = 15f;

    [Header("Debug")]
    [SerializeField] private bool logMissingPrefab = true;

    public void SetRewardDefinition(RewardDefinition definition)
    {
        rewardDefinition = definition;
    }

    public void Drop()
    {
        DropAt(transform.position);
    }

    public void DropAt(Vector3 origin)
    {
        if (rewardDefinition == null)
        {
            return;
        }

        List<CurrencyAmount> currencies = rewardDefinition.RollCurrencies();

        for (int i = 0; i < currencies.Count; i++)
        {
            CurrencyAmount reward = currencies[i];

            if (!reward.IsValid())
            {
                continue;
            }

            DropCurrency(origin, reward.CurrencyType, reward.Amount);
        }

        if (rewardDefinition.RollHealDrop())
        {
            DropHeal(origin, rewardDefinition.HealAmount);
        }
    }

    private void DropCurrency(Vector3 origin, CurrencyType currencyType, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameObject prefab = rewardPickupPrefab;

        if (prefab == null)
        {
            if (logMissingPrefab)
            {
                Debug.LogWarning("RewardPickup 프리팹이 없어 보상 조각을 드랍할 수 없습니다.", this);
            }

            return;
        }

        int shardSize = Mathf.Max(1, amountPerShard);
        int desiredShardCount = Mathf.CeilToInt((float)amount / shardSize);
        int shardCount = Mathf.Clamp(desiredShardCount, 1, Mathf.Max(1, maxShardsPerCurrency));

        int baseAmount = amount / shardCount;
        int remainder = amount % shardCount;

        for (int i = 0; i < shardCount; i++)
        {
            int shardAmount = baseAmount + (i < remainder ? 1 : 0);

            if (shardAmount <= 0)
            {
                continue;
            }

            Vector2 direction = GetCircularDirection(i, shardCount);
            Vector3 spawnPosition = origin + (Vector3)(direction * Random.Range(minScatterRadius, maxScatterRadius));
            Vector2 initialVelocity = direction * Random.Range(minScatterSpeed, maxScatterSpeed);

            GameObject pickupObject = SpawnPickupObject(prefab, spawnPosition);

            if (pickupObject == null)
            {
                continue;
            }

            RewardPickup pickup = pickupObject.GetComponent<RewardPickup>();

            if (pickup == null)
            {
                Debug.LogWarning("RewardPickup 프리팹에 RewardPickup 컴포넌트가 없습니다.", pickupObject);
                ReleaseOrDestroy(pickupObject);
                continue;
            }

            pickup.InitializeCurrency(currencyType, shardAmount, initialVelocity);
        }
    }

    private void DropHeal(Vector3 origin, float healAmount)
    {
        if (healAmount <= 0f)
        {
            return;
        }

        GameObject prefab = healPickupPrefab != null ? healPickupPrefab : rewardPickupPrefab;

        if (prefab == null)
        {
            if (logMissingPrefab)
            {
                Debug.LogWarning("HealPickup 프리팹이 없어 회복 조각을 드랍할 수 없습니다.", this);
            }

            return;
        }

        Vector2 direction = Random.insideUnitCircle.normalized;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        Vector3 spawnPosition = origin + (Vector3)(direction * Random.Range(minScatterRadius, maxScatterRadius));
        Vector2 initialVelocity = direction * Random.Range(minScatterSpeed, maxScatterSpeed);

        GameObject pickupObject = SpawnPickupObject(prefab, spawnPosition);

        if (pickupObject == null)
        {
            return;
        }

        RewardPickup pickup = pickupObject.GetComponent<RewardPickup>();

        if (pickup == null)
        {
            Debug.LogWarning("HealPickup 프리팹에 RewardPickup 컴포넌트가 없습니다.", pickupObject);
            ReleaseOrDestroy(pickupObject);
            return;
        }

        pickup.InitializeHeal(healAmount, initialVelocity);
    }

    private Vector2 GetCircularDirection(int index, int totalCount)
    {
        float baseAngle = totalCount <= 1 ? Random.Range(0f, 360f) : (360f / totalCount) * index;
        float finalAngle = baseAngle + Random.Range(-angleJitter, angleJitter);
        float radian = finalAngle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)).normalized;
    }

    private GameObject SpawnPickupObject(GameObject prefab, Vector3 position)
    {
        if (PoolManager.Instance != null)
        {
            return PoolManager.Instance.Get(prefab, position, Quaternion.identity);
        }

        return Instantiate(prefab, position, Quaternion.identity);
    }

    private void ReleaseOrDestroy(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(target);
        }
        else
        {
            Destroy(target);
        }
    }
}