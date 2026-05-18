using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAnimationController : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsChargingHash = Animator.StringToHash("IsCharging");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeathHash = Animator.StringToHash("Death");

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private EnemyHealth enemyHealth;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        enemyAI = GetComponent<EnemyBaseAI>();
        attackController = GetComponent<EnemyAttackController>();
        enemyHealth = GetComponent<EnemyHealth>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyBaseAI>();
        }

        if (attackController == null)
        {
            attackController = GetComponent<EnemyAttackController>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }
    }

    private void OnEnable()
    {
        if (attackController != null)
        {
            attackController.AttackStarted += HandleAttackStarted;
            attackController.ChargeStarted += HandleChargeStarted;
            attackController.ChargeReleased += HandleChargeFinished;
            attackController.ChargeCanceled += HandleChargeFinished;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Damaged += HandleDamaged;
            enemyHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (attackController != null)
        {
            attackController.AttackStarted -= HandleAttackStarted;
            attackController.ChargeStarted -= HandleChargeStarted;
            attackController.ChargeReleased -= HandleChargeFinished;
            attackController.ChargeCanceled -= HandleChargeFinished;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Damaged -= HandleDamaged;
            enemyHealth.Died -= HandleDied;
        }
    }

    private void Update()
    {
        if (animator == null || enemyAI == null)
        {
            return;
        }

        animator.SetBool(IsMovingHash, enemyAI.IsMoving);

        if (attackController != null)
        {
            animator.SetBool(IsChargingHash, attackController.IsCharging);
        }
    }

    private void HandleAttackStarted(EnemyAttackController controller)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetTrigger(AttackHash);
    }

    private void HandleChargeStarted(EnemyAttackController controller)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsChargingHash, true);
    }

    private void HandleChargeFinished(EnemyAttackController controller)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsChargingHash, false);
    }

    private void HandleDamaged(EnemyHealth health)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetTrigger(HitHash);
    }

    private void HandleDied(EnemyHealth health)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsMovingHash, false);
        animator.SetBool(IsChargingHash, false);
        animator.SetTrigger(DeathHash);
    }
}