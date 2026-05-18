using UnityEngine;

public class SimpleEnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private EnemyDefinition enemyDefinition;
    [SerializeField] private int spawnCount = 5;
    [SerializeField] private float spawnRadius = 10f;

    [Header("Spawn")]
    [SerializeField] private bool spawnOnStart = true;

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
            Debug.LogWarning("EnemyDefinition 또는 EnemyPrefab이 없습니다.", this);
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 position = transform.position + (Vector3)offset;

            GameObject enemyObject;

            if (PoolManager.Instance != null)
            {
                enemyObject = PoolManager.Instance.Get(enemyDefinition.EnemyPrefab, position, Quaternion.identity);
            }
            else
            {
                enemyObject = Instantiate(enemyDefinition.EnemyPrefab, position, Quaternion.identity);
            }

            EnemyBaseAI ai = enemyObject.GetComponent<EnemyBaseAI>();

            if (ai != null)
            {
                ai.ApplyDefinition(enemyDefinition);
            }

            RewardDropper rewardDropper = enemyObject.GetComponent<RewardDropper>();

            if (rewardDropper != null)
            {
                rewardDropper.SetRewardDefinition(enemyDefinition.RewardDefinition);
            }
        }
    }
}