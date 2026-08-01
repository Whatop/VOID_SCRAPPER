using UnityEngine;

public class SimpleEnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private EnemyDefinition enemyDefinition;
    [SerializeField] private int spawnCount = 5;
    [SerializeField] private float spawnRadius = 10f;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool alertAfterArrival;

    private void Start()
    {
        if (spawnOnStart)
        {
            Spawn();
        }
    }

    public void Spawn()
    {
        if (enemyDefinition == null || enemyDefinition.EnemyPrefab == null)
        {
            Debug.LogWarning("EnemyDefinition 또는 EnemyPrefab이 비어 있습니다.", this);
            return;
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        Transform target = playerHealth != null ? playerHealth.transform : null;

        for (int i = 0; i < Mathf.Max(0, spawnCount); i++)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, spawnRadius);
            Vector2 arrivalPosition = (Vector2)transform.position + offset;

            GameObject enemyObject;

            if (PoolManager.Instance != null)
            {
                enemyObject = PoolManager.Instance.Get(
                    enemyDefinition.EnemyPrefab,
                    arrivalPosition,
                    Quaternion.identity
                );
            }
            else
            {
                enemyObject = Instantiate(
                    enemyDefinition.EnemyPrefab,
                    arrivalPosition,
                    Quaternion.identity
                );
            }

            if (enemyObject == null)
            {
                continue;
            }

            EnemyBaseAI ai = enemyObject.GetComponentInChildren<EnemyBaseAI>(true);

            if (ai != null)
            {
                ai.ApplyDefinition(enemyDefinition);

                if (target != null)
                {
                    ai.SetTarget(target);
                }
            }

            RewardDropper rewardDropper = enemyObject.GetComponentInChildren<RewardDropper>(true);

            if (rewardDropper != null)
            {
                rewardDropper.SetRewardDefinition(enemyDefinition.RewardDefinition);
            }

            bool arrivalStarted = EnemyArrivalSpawnUtility.BeginArrival(
                enemyObject,
                arrivalPosition,
                target,
                alertAfterArrival,
                transform.position
            );

            if (!arrivalStarted && alertAfterArrival && ai != null && target != null)
            {
                ai.AlertTo(target.position);
            }
        }
    }
}
