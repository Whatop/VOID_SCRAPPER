using UnityEngine;

/// <summary>
/// 경로 예고 후 직선 돌진하는 근접 적 전용 공격 컨트롤러.
/// EnemyBaseAI의 이동을 재사용하고, 차징 중에는 확대/페이드 잔상과
/// 실제 돌진 폭에 가까운 직사각형 경고 경로를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyMeleeChargeController2D : MonoBehaviour
{
    private enum ChargeState
    {
        Ready,
        Charging,
        Dashing,
        Recovering
    }

    [Header("References")]
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer bodySprite;

    [Header("Approach")]
    [Min(0.1f)]
    [SerializeField] private float chargeTriggerRange = 7.5f;
    [Min(0f)]
    [SerializeField] private float minimumChargeRange = 1.1f;
    [Min(0.1f)]
    [SerializeField] private float approachSpeedMultiplier = 1.15f;

    [Header("Charge")]
    [Min(0.05f)]
    [SerializeField] private float chargeDuration = 0.85f;
    [Tooltip("돌진 직전 이 시간 동안은 방향을 더 이상 추적하지 않습니다.")]
    [Min(0f)]
    [SerializeField] private float directionLockBeforeDash = 0.2f;

    [Header("Dash")]
    [Min(0.1f)]
    [SerializeField] private float dashDistance = 7f;
    [Min(0.05f)]
    [SerializeField] private float dashDuration = 0.36f;
    [Min(0f)]
    [SerializeField] private float dashDamage = 5f;
    [Min(0.05f)]
    [SerializeField] private float dashHitRadius = 0.55f;
    [Min(0f)]
    [SerializeField] private float recoveryDuration = 0.7f;
    [SerializeField] private LayerMask dashObstacleMask;
    [Min(0.01f)]
    [SerializeField] private float obstacleProbeRadius = 0.3f;

    [Header("Path Warning")]
    [SerializeField] private LineRenderer pathRenderer;
    [SerializeField] private bool autoCreatePathRenderer = true;
    [SerializeField] private Material pathMaterial;
    [SerializeField] private Color pathColor = new Color(1f, 0.08f, 0.04f, 0.72f);
    [Min(0.01f)]
    [SerializeField] private float pathLineWidth = 0.045f;
    [Min(0.05f)]
    [SerializeField] private float pathWidth = 0.9f;
    [SerializeField] private string pathSortingLayer = "Default";
    [SerializeField] private int pathSortingOrder = 12;

    [Header("Charge Pulse")]
    [SerializeField] private SpriteRenderer chargePulseRenderer;
    [SerializeField] private bool autoCreatePulseRenderer = true;
    [Min(0.05f)]
    [SerializeField] private float pulseCycleDuration = 0.36f;
    [Min(1f)]
    [SerializeField] private float pulseEndScale = 1.55f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseStartAlpha = 0.52f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.2f, 0.12f, 1f);
    [SerializeField] private int pulseSortingOrderOffset = -1;

    [Header("Debug")]
    [SerializeField] private bool drawDashGizmo;

    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[12];

    private ChargeState state;
    private Transform target;
    private Vector2 lockedDirection = Vector2.up;
    private float stateTimer;
    private float chargeElapsed;
    private float pulseElapsed;
    private float dashSpeed;
    private bool directionLocked;
    private bool damagedTargetThisDash;

    private Vector3 pulseBaseLocalPosition;
    private Quaternion pulseBaseLocalRotation = Quaternion.identity;
    private Vector3 pulseBaseLocalScale = Vector3.one;

    public bool IsBusy => state != ChargeState.Ready;
    public bool IsCharging => state == ChargeState.Charging;
    public bool IsDashing => state == ChargeState.Dashing;
    public bool LocksFacing => state == ChargeState.Charging || state == ChargeState.Dashing;

    private void Reset()
    {
        enemyAI = GetComponent<EnemyBaseAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        bodySprite = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsurePathRenderer();
        EnsurePulseRenderer();
        HideWarningVisuals();
        state = ChargeState.Ready;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    public bool TryHandleCombat(EnemyBaseAI ownerAI, Transform playerTarget, float deltaTime)
    {
        if (ownerAI != null)
        {
            enemyAI = ownerAI;
        }

        if (enemyHealth != null && enemyHealth.IsDead)
        {
            CancelAttack();
            return true;
        }

        target = playerTarget;

        if (target == null)
        {
            CancelAttack();
            return true;
        }

        switch (state)
        {
            case ChargeState.Charging:
                UpdateCharging(deltaTime);
                return true;

            case ChargeState.Dashing:
                UpdateDashing(deltaTime);
                return true;

            case ChargeState.Recovering:
                UpdateRecovery(deltaTime);
                return true;
        }

        float distance = Vector2.Distance(transform.position, target.position);

        if (distance > chargeTriggerRange)
        {
            enemyAI?.CommandMoveTo(target.position, approachSpeedMultiplier, target);
            enemyAI?.CommandFaceTo(target.position);
            return true;
        }

        if (distance < minimumChargeRange)
        {
            Vector2 retreatDirection = (Vector2)transform.position - (Vector2)target.position;

            if (retreatDirection.sqrMagnitude <= 0.001f)
            {
                retreatDirection = enemyAI != null
                    ? -enemyAI.FacingDirection
                    : Vector2.down;
            }

            float retreatSpeed = enemyAI != null ? enemyAI.BaseMoveSpeed : 2.5f;
            enemyAI?.CommandSetVelocity(retreatDirection.normalized * retreatSpeed);
            return true;
        }

        BeginCharge(target);
        return true;
    }

    public void CancelAttack()
    {
        state = ChargeState.Ready;
        stateTimer = 0f;
        chargeElapsed = 0f;
        pulseElapsed = 0f;
        directionLocked = false;
        damagedTargetThisDash = false;
        target = null;

        HideWarningVisuals();
        enemyAI?.CommandStopMoving();
    }

    private void BeginCharge(Transform playerTarget)
    {
        target = playerTarget;
        state = ChargeState.Charging;
        stateTimer = Mathf.Max(0.05f, chargeDuration);
        chargeElapsed = 0f;
        pulseElapsed = 0f;
        directionLocked = false;
        damagedTargetThisDash = false;

        lockedDirection = ResolveDirectionToTarget();
        enemyAI?.CommandStopMoving();
        enemyAI?.CommandSetFacingDirection(lockedDirection);

        SetPathVisible(true);
        SetPulseVisible(true);
        UpdatePathVisual();
        UpdatePulseVisual(0f);

        AudioManager.PlayAt(SoundEventIds.EnemyAlert, transform.position, 0.7f);
    }

    private void UpdateCharging(float deltaTime)
    {
        enemyAI?.CommandStopMoving();

        chargeElapsed += Mathf.Max(0f, deltaTime);
        stateTimer -= Mathf.Max(0f, deltaTime);

        float lockMoment = Mathf.Max(0f, chargeDuration - directionLockBeforeDash);

        if (!directionLocked && chargeElapsed < lockMoment)
        {
            lockedDirection = ResolveDirectionToTarget();
        }
        else
        {
            directionLocked = true;
        }

        enemyAI?.CommandSetFacingDirection(lockedDirection);
        UpdatePathVisual();

        pulseElapsed += Mathf.Max(0f, deltaTime);
        float pulseT = Mathf.Repeat(pulseElapsed, Mathf.Max(0.05f, pulseCycleDuration)) /
                       Mathf.Max(0.05f, pulseCycleDuration);
        UpdatePulseVisual(pulseT);

        if (stateTimer > 0f)
        {
            return;
        }

        BeginDash();
    }

    private void BeginDash()
    {
        state = ChargeState.Dashing;
        stateTimer = Mathf.Max(0.05f, dashDuration);
        dashSpeed = Mathf.Max(0.1f, dashDistance) / Mathf.Max(0.05f, dashDuration);
        damagedTargetThisDash = false;

        SetPathVisible(false);
        SetPulseVisible(false);
        enemyAI?.CommandSetFacingDirection(lockedDirection);
        enemyAI?.CommandSetVelocity(lockedDirection * dashSpeed, false);

        AudioManager.PlayAt(SoundEventIds.EnemyChargerFire, transform.position, 0.8f);
    }

    private void UpdateDashing(float deltaTime)
    {
        float stepDistance = dashSpeed * Mathf.Max(0f, deltaTime);

        if (WillHitObstacle(stepDistance))
        {
            BeginRecovery();
            return;
        }

        enemyAI?.CommandSetVelocity(lockedDirection * dashSpeed, false);
        enemyAI?.CommandSetFacingDirection(lockedDirection);

        TryDamageTarget();

        stateTimer -= Mathf.Max(0f, deltaTime);

        if (stateTimer <= 0f)
        {
            BeginRecovery();
        }
    }

    private void BeginRecovery()
    {
        state = ChargeState.Recovering;
        stateTimer = Mathf.Max(0f, recoveryDuration);
        enemyAI?.CommandStopMoving();
    }

    private void UpdateRecovery(float deltaTime)
    {
        enemyAI?.CommandStopMoving();
        stateTimer -= Mathf.Max(0f, deltaTime);

        if (stateTimer > 0f)
        {
            return;
        }

        state = ChargeState.Ready;
        damagedTargetThisDash = false;
    }

    private Vector2 ResolveDirectionToTarget()
    {
        if (target == null)
        {
            return lockedDirection.sqrMagnitude > 0.001f
                ? lockedDirection.normalized
                : Vector2.up;
        }

        Vector2 direction = (Vector2)target.position - (Vector2)transform.position;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
    }

    private bool WillHitObstacle(float stepDistance)
    {
        if (dashObstacleMask.value == 0 || stepDistance <= 0f)
        {
            return false;
        }

        int count = Physics2D.CircleCastNonAlloc(
            transform.position,
            Mathf.Max(0.01f, obstacleProbeRadius),
            lockedDirection,
            obstacleHits,
            stepDistance + Mathf.Max(0.02f, obstacleProbeRadius),
            dashObstacleMask
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = obstacleHits[i].collider;

            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            Transform hitTransform = hit.transform;

            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform) ||
                transform.IsChildOf(hitTransform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void TryDamageTarget()
    {
        if (damagedTargetThisDash || target == null || dashDamage <= 0f)
        {
            return;
        }

        PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        float hitDistance = Mathf.Max(0.05f, dashHitRadius);

        if (((Vector2)target.position - (Vector2)transform.position).sqrMagnitude >
            hitDistance * hitDistance)
        {
            return;
        }

        damagedTargetThisDash = true;
        playerHealth.TakeDamage(dashDamage, transform.position);
    }

    private void EnsurePathRenderer()
    {
        if (pathRenderer == null && autoCreatePathRenderer)
        {
            GameObject pathObject = new GameObject("DashPathWarning");
            pathObject.transform.SetParent(transform, false);
            pathRenderer = pathObject.AddComponent<LineRenderer>();
        }

        if (pathRenderer == null)
        {
            return;
        }

        pathRenderer.useWorldSpace = true;
        pathRenderer.loop = true;
        pathRenderer.positionCount = 5;
        pathRenderer.startWidth = pathLineWidth;
        pathRenderer.endWidth = pathLineWidth;
        pathRenderer.numCapVertices = 1;
        pathRenderer.numCornerVertices = 1;
        pathRenderer.sortingLayerName = pathSortingLayer;
        pathRenderer.sortingOrder = pathSortingOrder;
        pathRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pathRenderer.receiveShadows = false;

        if (pathRenderer.sharedMaterial == null)
        {
            if (pathMaterial != null)
            {
                pathRenderer.sharedMaterial = pathMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    pathRenderer.sharedMaterial = new Material(shader);
                }
            }
        }
    }

    private void EnsurePulseRenderer()
    {
        if (bodySprite == null)
        {
            bodySprite = GetComponentInChildren<SpriteRenderer>();
        }

        if (chargePulseRenderer == null && autoCreatePulseRenderer && bodySprite != null)
        {
            GameObject pulseObject = new GameObject("ChargePulseSprite");
            Transform parent = bodySprite.transform.parent != null
                ? bodySprite.transform.parent
                : transform;

            pulseObject.transform.SetParent(parent, false);
            pulseObject.transform.localPosition = bodySprite.transform.localPosition;
            pulseObject.transform.localRotation = bodySprite.transform.localRotation;
            pulseObject.transform.localScale = bodySprite.transform.localScale;

            chargePulseRenderer = pulseObject.AddComponent<SpriteRenderer>();
            chargePulseRenderer.sprite = bodySprite.sprite;
            chargePulseRenderer.sharedMaterial = bodySprite.sharedMaterial;
            chargePulseRenderer.sortingLayerID = bodySprite.sortingLayerID;
            chargePulseRenderer.sortingOrder = bodySprite.sortingOrder + pulseSortingOrderOffset;
            chargePulseRenderer.flipX = bodySprite.flipX;
            chargePulseRenderer.flipY = bodySprite.flipY;
        }

        if (chargePulseRenderer == null)
        {
            return;
        }

        pulseBaseLocalPosition = chargePulseRenderer.transform.localPosition;
        pulseBaseLocalRotation = chargePulseRenderer.transform.localRotation;
        pulseBaseLocalScale = chargePulseRenderer.transform.localScale;
    }

    private void UpdatePathVisual()
    {
        if (pathRenderer == null)
        {
            return;
        }

        Vector2 direction = lockedDirection.sqrMagnitude > 0.001f
            ? lockedDirection.normalized
            : Vector2.up;
        Vector2 normal = new Vector2(-direction.y, direction.x);
        Vector2 start = transform.position;
        Vector2 end = start + direction * Mathf.Max(0.1f, dashDistance);
        float halfWidth = Mathf.Max(0.025f, pathWidth * 0.5f);

        pathRenderer.SetPosition(0, start + normal * halfWidth);
        pathRenderer.SetPosition(1, end + normal * halfWidth);
        pathRenderer.SetPosition(2, end - normal * halfWidth);
        pathRenderer.SetPosition(3, start - normal * halfWidth);
        pathRenderer.SetPosition(4, start + normal * halfWidth);

        float chargeRatio = Mathf.Clamp01(chargeElapsed / Mathf.Max(0.05f, chargeDuration));
        float alphaPulse = Mathf.Lerp(0.35f, pathColor.a, 0.55f + 0.45f * Mathf.Sin(Time.time * 16f));
        Color color = pathColor;
        color.a = Mathf.Clamp01(alphaPulse * Mathf.Lerp(0.65f, 1f, chargeRatio));
        pathRenderer.startColor = color;
        pathRenderer.endColor = color;
    }

    private void UpdatePulseVisual(float normalized)
    {
        if (chargePulseRenderer == null)
        {
            return;
        }

        normalized = Mathf.Clamp01(normalized);
        chargePulseRenderer.sprite = bodySprite != null ? bodySprite.sprite : chargePulseRenderer.sprite;

        Transform pulseTransform = chargePulseRenderer.transform;
        pulseTransform.localPosition = pulseBaseLocalPosition;
        pulseTransform.localRotation = pulseBaseLocalRotation;
        pulseTransform.localScale = pulseBaseLocalScale * Mathf.Lerp(1f, pulseEndScale, normalized);

        Color color = pulseColor;
        color.a = Mathf.Lerp(pulseStartAlpha, 0f, normalized);
        chargePulseRenderer.color = color;
    }

    private void HideWarningVisuals()
    {
        SetPathVisible(false);
        SetPulseVisible(false);
    }

    private void SetPathVisible(bool visible)
    {
        if (pathRenderer != null)
        {
            pathRenderer.enabled = visible;
        }
    }

    private void SetPulseVisible(bool visible)
    {
        if (chargePulseRenderer == null)
        {
            return;
        }

        chargePulseRenderer.enabled = visible;

        if (!visible)
        {
            chargePulseRenderer.transform.localPosition = pulseBaseLocalPosition;
            chargePulseRenderer.transform.localRotation = pulseBaseLocalRotation;
            chargePulseRenderer.transform.localScale = pulseBaseLocalScale;
        }
    }

    private void ResolveReferences()
    {
        if (enemyAI == null)
        {
            enemyAI = GetComponent<EnemyBaseAI>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodySprite == null)
        {
            bodySprite = GetComponentInChildren<SpriteRenderer>();
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDashGizmo)
        {
            return;
        }

        Vector2 direction = Application.isPlaying && lockedDirection.sqrMagnitude > 0.001f
            ? lockedDirection.normalized
            : transform.up;

        Gizmos.color = pathColor;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + direction * dashDistance);
        Gizmos.DrawWireSphere((Vector2)transform.position + direction * dashDistance, obstacleProbeRadius);
    }
#endif
}
