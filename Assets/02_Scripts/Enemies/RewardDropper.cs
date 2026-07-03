using System.Collections.Generic;
using UnityEngine;

public class RewardDropper : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private RewardDefinition rewardDefinition;

    [Header("Pickup Prefab")]
    [SerializeField] private GameObject rewardPickupPrefab;
    [SerializeField] private GameObject healPickupPrefab;

    [Header("Reinforcement Item Drop")]
    [Tooltip("RewardDefinition에 Catalog가 비어 있으면 이 Catalog에서 랜덤 장비를 뽑습니다.")]
    [SerializeField] private ReinforcementCatalog fallbackReinforcementCatalog;

    [Tooltip("필드에 떨어지는 ReinforcementPickup 프리팹. 반드시 ReinforcementPickup 컴포넌트가 있어야 합니다.")]
    [SerializeField] private ReinforcementPickup reinforcementPickupPrefab;

    [Tooltip("장비 아이템이 드랍될 때 중심에서 흩어지는 최소 거리")]
    [SerializeField] private float reinforcementMinScatterRadius = 0.35f;

    [Tooltip("장비 아이템이 드랍될 때 중심에서 흩어지는 최대 거리")]
    [SerializeField] private float reinforcementMaxScatterRadius = 1.25f;

    [Tooltip("Rigidbody2D가 있으면 이 속도로 살짝 튀어나갑니다.")]
    [SerializeField] private float reinforcementScatterSpeed = 1.5f;

    [Header("Trait Item Drop")]
    [Tooltip("RewardDefinition에 Catalog가 비어 있으면 이 Catalog에서 랜덤 패시브를 뽑습니다.")]
    [SerializeField] private TraitCatalog fallbackTraitCatalog;

    [Tooltip("필드에 떨어지는 TraitPickup 프리팹. 반드시 TraitPickup 컴포넌트가 있어야 합니다.")]
    [SerializeField] private TraitPickup traitPickupPrefab;

    [SerializeField] private float traitMinScatterRadius = 0.25f;
    [SerializeField] private float traitMaxScatterRadius = 1.05f;
    [SerializeField] private float traitScatterSpeed = 1.25f;

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

        DropReinforcementItems(origin);
        DropTraitItems(origin);
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

    private void DropReinforcementItems(Vector3 origin)
    {
        if (rewardDefinition == null)
        {
            return;
        }

        if (reinforcementPickupPrefab == null)
        {
            if (rewardDefinition.EnableReinforcementDrop && logMissingPrefab)
            {
                Debug.LogWarning("ReinforcementPickup 프리팹이 없어 액티브 장비를 드랍할 수 없습니다.", this);
            }

            return;
        }

        List<ReinforcementDefinition> drops = rewardDefinition.RollReinforcementDrops(fallbackReinforcementCatalog);

        if (drops == null || drops.Count == 0)
        {
            return;
        }

        for (int i = 0; i < drops.Count; i++)
        {
            ReinforcementDefinition definition = drops[i];

            if (definition == null)
            {
                continue;
            }

            DropReinforcementItem(origin, definition, i, drops.Count);
        }
    }

    private void DropTraitItems(Vector3 origin)
    {
        if (rewardDefinition == null)
        {
            return;
        }

        if (traitPickupPrefab == null)
        {
            if (rewardDefinition.EnableTraitDrop && logMissingPrefab)
            {
                Debug.LogWarning("TraitPickup 프리팹이 없어 패시브 특성을 드랍할 수 없습니다.", this);
            }

            return;
        }

        List<TraitDefinition> drops = rewardDefinition.RollTraitDrops(fallbackTraitCatalog);

        if (drops == null || drops.Count == 0)
        {
            return;
        }

        for (int i = 0; i < drops.Count; i++)
        {
            TraitDefinition trait = drops[i];

            if (trait == null)
            {
                continue;
            }

            DropTraitItem(origin, trait, i, drops.Count);
        }
    }

    private void DropReinforcementItem(Vector3 origin, ReinforcementDefinition definition, int index, int totalCount)
    {
        Vector2 direction = GetCircularDirection(index, Mathf.Max(1, totalCount));

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle.normalized;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        float minRadius = Mathf.Min(reinforcementMinScatterRadius, reinforcementMaxScatterRadius);
        float maxRadius = Mathf.Max(reinforcementMinScatterRadius, reinforcementMaxScatterRadius);
        Vector3 spawnPosition = origin + (Vector3)(direction * Random.Range(minRadius, maxRadius));

        ReinforcementPickup pickup = SpawnReinforcementPickup(reinforcementPickupPrefab, spawnPosition);

        if (pickup == null)
        {
            return;
        }

        pickup.Initialize(definition, -1, 0.5f);
        ApplyScatterVelocity(pickup.GetComponent<Rigidbody2D>(), direction, reinforcementScatterSpeed);
    }

    private void DropTraitItem(Vector3 origin, TraitDefinition trait, int index, int totalCount)
    {
        Vector2 direction = GetCircularDirection(index + 3, Mathf.Max(1, totalCount + 3));

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle.normalized;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        float minRadius = Mathf.Min(traitMinScatterRadius, traitMaxScatterRadius);
        float maxRadius = Mathf.Max(traitMinScatterRadius, traitMaxScatterRadius);
        Vector3 spawnPosition = origin + (Vector3)(direction * Random.Range(minRadius, maxRadius));

        TraitPickup pickup = SpawnTraitPickup(traitPickupPrefab, spawnPosition);

        if (pickup == null)
        {
            return;
        }

        pickup.Initialize(trait, 0.5f);
        ApplyScatterVelocity(pickup.GetComponent<Rigidbody2D>(), direction, traitScatterSpeed);
    }

    private void ApplyScatterVelocity(Rigidbody2D pickupRigidbody, Vector2 direction, float speed)
    {
        if (pickupRigidbody != null && pickupRigidbody.bodyType != RigidbodyType2D.Static)
        {
            pickupRigidbody.linearVelocity = direction * Mathf.Max(0f, speed);
        }
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

    private ReinforcementPickup SpawnReinforcementPickup(ReinforcementPickup prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return null;
        }

        if (PoolManager.Instance != null)
        {
            GameObject pickupObject = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity);
            return pickupObject != null ? pickupObject.GetComponent<ReinforcementPickup>() : null;
        }

        return Instantiate(prefab, position, Quaternion.identity);
    }

    private TraitPickup SpawnTraitPickup(TraitPickup prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return null;
        }

        if (PoolManager.Instance != null)
        {
            GameObject pickupObject = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity);
            return pickupObject != null ? pickupObject.GetComponent<TraitPickup>() : null;
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
