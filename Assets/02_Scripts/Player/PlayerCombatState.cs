using UnityEngine;

public class PlayerCombatState : MonoBehaviour
{
    [Header("Out Of Combat Rule")]
    [SerializeField] private float noAttackTime = 2f;
    [SerializeField] private float noHitTime = 2f;
    [SerializeField] private float threatCheckRadius = 12f;
    [SerializeField] private LayerMask enemyLayer;

    private float lastAttackTime = -999f;
    private float lastHitTime = -999f;

    private readonly Collider2D[] threatBuffer = new Collider2D[64];

    public float LastAttackTime => lastAttackTime;
    public float LastHitTime => lastHitTime;
    public float ThreatCheckRadius => threatCheckRadius;

    public bool RecentlyAttacked => Time.time - lastAttackTime < noAttackTime;
    public bool RecentlyHit => Time.time - lastHitTime < noHitTime;

    public void RegisterAttack()
    {
        lastAttackTime = Time.time;
    }

    public void RegisterHit()
    {
        lastHitTime = Time.time;
    }

    public bool IsOutOfCombat()
    {
        if (RecentlyAttacked)
        {
            return false;
        }

        if (RecentlyHit)
        {
            return false;
        }

        if (HasThreateningEnemyNearby())
        {
            return false;
        }

        return true;
    }

    public bool HasThreateningEnemyNearby()
    {
        if (enemyLayer.value == 0)
        {
            return false;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            threatCheckRadius,
            threatBuffer,
            enemyLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = threatBuffer[i];
            if (hit == null)
            {
                continue;
            }

            EnemyBaseAI enemy = hit.GetComponentInParent<EnemyBaseAI>();
            if (enemy == null)
            {
                continue;
            }

            if (enemy.CurrentState != EnemyState.Idle &&
                enemy.CurrentState != EnemyState.Dead)
            {
                return true;
            }
        }

        return false;
    }

    public void SetOutOfCombatRule(float attackTime, float hitTime, float enemyRadius)
    {
        noAttackTime = Mathf.Max(0f, attackTime);
        noHitTime = Mathf.Max(0f, hitTime);
        threatCheckRadius = Mathf.Max(0f, enemyRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, threatCheckRadius);
    }
}