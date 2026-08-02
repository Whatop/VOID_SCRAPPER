using UnityEngine;

public enum MeteorMotionMode
{
    // 기존 프리팹 호환용. Transform 직접 이동이므로 새 프리팹에는 권장하지 않습니다.
    LegacyTransformDrift = 0,

    // 대형 운석/지형용. 판정 Root는 이동하지 않습니다.
    StaticTerrain = 1,

    // 소형 운석용. Rigidbody2D + SpaceDriftBody2D를 사용합니다.
    RigidbodyDrift = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MeteorObstacle : MonoBehaviour
{
    [Header("Meteor Settings")]
    [SerializeField] private int maxHp = 3;

    [Header("Projectile Interaction")]
    [SerializeField] private bool takeDamageFromPlayerProjectiles = true;
    [SerializeField] private bool takeDamageFromEnemyProjectiles;
    [SerializeField] private bool blockProjectileWhenDamageIgnored = true;

    [Header("Motion Mode")]
    [SerializeField] private MeteorMotionMode motionMode = MeteorMotionMode.LegacyTransformDrift;

    [Tooltip("Rigidbody Drift 모드에서 사용하는 컴포넌트입니다. 같은 오브젝트에서 자동 탐색합니다.")]
    [SerializeField] private SpaceDriftBody2D driftBody;

    [Tooltip("Static Terrain 모드에서는 Root 대신 이 자식 비주얼만 회전시킵니다.")]
    [SerializeField] private Transform visualRoot;

    [SerializeField] private bool allowRootVisualRotationWhenVisualRootMissing;

    [Header("Legacy / Rigidbody Drift Values")]
    [SerializeField] private float minDriftSpeed = 0.15f;
    [SerializeField] private float maxDriftSpeed = 0.45f;
    [SerializeField] private float minSpinSpeed = -25f;
    [SerializeField] private float maxSpinSpeed = 25f;
    [SerializeField] private float boundsPadding = 0.5f;
    [SerializeField] private float hitImpulse = 0.35f;

    [Header("Static Visual Rotation")]
    [SerializeField] private Vector2 staticVisualSpinRange = new Vector2(-3f, 3f);

    [Header("Break / Release Root")]
    [Tooltip("MeteorObstacle가 Collider 자식에 붙어 있을 때, 파괴 시 함께 제거할 운석 프리팹 Root를 연결합니다. 비우면 VisualRoot 구조를 기준으로 자동 탐색합니다.")]
    [SerializeField] private GameObject objectRoot;
    [SerializeField] private bool disableVisualsAndCollidersImmediatelyOnBreak = true;
    [SerializeField] private Renderer[] renderersToDisableOnBreak;
    [SerializeField] private Collider2D[] collidersToDisableOnBreak;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.12f;
    [SerializeField] private bool useProceduralHitEffectWhenPrefabMissing = true;
    [SerializeField] private float hitEffectIntensity = 0.75f;

    [Header("Camera Shake")]
    [SerializeField] private float hitShakeAmplitude = 0.018f;
    [SerializeField] private float hitShakeDuration = 0.045f;
    [SerializeField] private float breakShakeAmplitude = 0.065f;
    [SerializeField] private float breakShakeDuration = 0.09f;

    private Rigidbody2D body;
    private int currentHp;
    private Vector2 moveDirection;
    private float moveSpeed;
    private float spinSpeed;

    private Bounds roamingBounds;
    private bool hasRoamingBounds;

    public MeteorMotionMode MotionMode => motionMode;
    public bool BlocksProjectileWhenDamageIgnored => blockProjectileWhenDamageIgnored;

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
        driftBody = GetComponent<SpaceDriftBody2D>();

        Transform childVisual = transform.Find("VisualRoot");
        if (childVisual != null)
        {
            visualRoot = childVisual;
        }

        if (visualRoot != null && visualRoot.parent == transform)
        {
            objectRoot = gameObject;
        }

        CacheBreakObjects();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        if (driftBody == null)
        {
            driftBody = GetComponent<SpaceDriftBody2D>();
        }

        ResolveVisualRoot();
        CacheBreakObjects();
    }

    private void OnEnable()
    {
        SetBreakObjectsEnabled(true);
        currentHp = Mathf.Max(1, maxHp);
        InitializeMotion();
    }

    private void Update()
    {
        switch (motionMode)
        {
            case MeteorMotionMode.LegacyTransformDrift:
                UpdateLegacyTransformDrift();
                break;

            case MeteorMotionMode.StaticTerrain:
                UpdateStaticVisualRotation();
                break;
        }
    }

    public void SetRoamingBounds(Bounds bounds)
    {
        roamingBounds = bounds;
        hasRoamingBounds = true;

        if (driftBody != null)
        {
            driftBody.SetRoamingBounds(bounds);
        }
    }

    public bool CanReceiveProjectileDamage(ProjectileOwner projectileOwner)
    {
        return projectileOwner == ProjectileOwner.Player
            ? takeDamageFromPlayerProjectiles
            : takeDamageFromEnemyProjectiles;
    }

    public void SetEnemyProjectileDamageEnabled(bool enabled, bool blockProjectileWhenDisabled = true)
    {
        takeDamageFromEnemyProjectiles = enabled;

        if (!enabled)
        {
            blockProjectileWhenDamageIgnored = blockProjectileWhenDisabled;
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, transform.position, Vector2.zero);
    }

    public void TakeDamage(int damage, Vector2 hitPoint, Vector2 incomingDirection)
    {
        if (damage <= 0)
        {
            return;
        }

        currentHp -= damage;

        if (motionMode == MeteorMotionMode.RigidbodyDrift && incomingDirection.sqrMagnitude > 0.001f)
        {
            if (driftBody != null)
            {
                driftBody.ApplyImpulse(incomingDirection, hitImpulse);
            }
            else if (body != null && body.bodyType == RigidbodyType2D.Dynamic)
            {
                body.AddForce(incomingDirection.normalized * hitImpulse, ForceMode2D.Impulse);
            }
        }

        bool customEffectSpawned = SpawnHitEffect(hitPoint);
        float damageScale = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1, damage)), 0.8f, 1.6f);

        CombatFeedbackManager.PlayHit(
            hitPoint,
            incomingDirection,
            CombatFeedbackKind.Meteor,
            hitEffectIntensity * damageScale,
            hitShakeAmplitude * damageScale,
            hitShakeDuration,
            useProceduralHitEffectWhenPrefabMissing && !customEffectSpawned
        );

        AudioManager.PlayAt(SoundEventIds.ObjectMeteorHit, hitPoint, 0.65f);

        if (currentHp <= 0)
        {
            ReleaseSelf();
        }
    }

    private void InitializeMotion()
    {
        ResolveVisualRoot();

        switch (motionMode)
        {
            case MeteorMotionMode.LegacyTransformDrift:
                RandomizeLegacyMovement();
                ConfigureNonDynamicBody(RigidbodyType2D.Kinematic);
                break;

            case MeteorMotionMode.StaticTerrain:
                spinSpeed = Random.Range(
                    Mathf.Min(staticVisualSpinRange.x, staticVisualSpinRange.y),
                    Mathf.Max(staticVisualSpinRange.x, staticVisualSpinRange.y)
                );
                ConfigureNonDynamicBody(RigidbodyType2D.Static);
                break;

            case MeteorMotionMode.RigidbodyDrift:
                if (body == null)
                {
                    Debug.LogWarning($"[{name}] Rigidbody Drift 모드인데 Rigidbody2D가 없습니다. Static Terrain으로 동작합니다.", this);
                    spinSpeed = 0f;
                    return;
                }

                body.simulated = true;
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;

                if (driftBody != null)
                {
                    if (hasRoamingBounds)
                    {
                        driftBody.SetRoamingBounds(roamingBounds);
                    }
                }
                else
                {
                    RandomizeRigidbodyVelocity();
                }
                break;
        }
    }

    private void ConfigureNonDynamicBody(RigidbodyType2D bodyType)
    {
        if (body == null)
        {
            return;
        }

        body.simulated = true;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = 0f;
        body.bodyType = bodyType;
    }

    private void RandomizeLegacyMovement()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        moveSpeed = Random.Range(
            Mathf.Max(0f, Mathf.Min(minDriftSpeed, maxDriftSpeed)),
            Mathf.Max(0f, Mathf.Max(minDriftSpeed, maxDriftSpeed))
        );
        spinSpeed = Random.Range(
            Mathf.Min(minSpinSpeed, maxSpinSpeed),
            Mathf.Max(minSpinSpeed, maxSpinSpeed)
        );
    }

    private void RandomizeRigidbodyVelocity()
    {
        Vector2 direction = Random.insideUnitCircle;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        float minSpeed = Mathf.Max(0f, Mathf.Min(minDriftSpeed, maxDriftSpeed));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(minDriftSpeed, maxDriftSpeed));

        body.linearVelocity = direction.normalized * Random.Range(minSpeed, maxSpeed);
        body.angularVelocity = Random.Range(
            Mathf.Min(minSpinSpeed, maxSpinSpeed),
            Mathf.Max(minSpinSpeed, maxSpinSpeed)
        );
    }

    private void UpdateLegacyTransformDrift()
    {
        transform.position += (Vector3)(moveDirection * moveSpeed * Time.deltaTime);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        HandleLegacyBoundsBounce();
    }

    private void UpdateStaticVisualRotation()
    {
        if (Mathf.Abs(spinSpeed) <= 0.001f)
        {
            return;
        }

        Transform target = visualRoot;

        if (target == null && allowRootVisualRotationWhenVisualRootMissing)
        {
            target = transform;
        }

        if (target == null)
        {
            return;
        }

        target.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void HandleLegacyBoundsBounce()
    {
        if (!hasRoamingBounds)
        {
            return;
        }

        Vector3 position = transform.position;
        bool bounced = false;

        if (position.x <= roamingBounds.min.x + boundsPadding && moveDirection.x < 0f)
        {
            moveDirection.x *= -1f;
            position.x = roamingBounds.min.x + boundsPadding;
            bounced = true;
        }
        else if (position.x >= roamingBounds.max.x - boundsPadding && moveDirection.x > 0f)
        {
            moveDirection.x *= -1f;
            position.x = roamingBounds.max.x - boundsPadding;
            bounced = true;
        }

        if (position.y <= roamingBounds.min.y + boundsPadding && moveDirection.y < 0f)
        {
            moveDirection.y *= -1f;
            position.y = roamingBounds.min.y + boundsPadding;
            bounced = true;
        }
        else if (position.y >= roamingBounds.max.y - boundsPadding && moveDirection.y > 0f)
        {
            moveDirection.y *= -1f;
            position.y = roamingBounds.max.y - boundsPadding;
            bounced = true;
        }

        if (bounced)
        {
            transform.position = position;
        }
    }

    private void ResolveVisualRoot()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform child = transform.Find("VisualRoot");
        if (child != null)
        {
            visualRoot = child;
        }
    }

    private bool SpawnHitEffect(Vector2 position)
    {
        if (hitEffectPrefab == null)
        {
            return false;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.SpawnAutoRelease(hitEffectPrefab, position, hitEffectDuration);
        }
        else
        {
            GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(effect, hitEffectDuration);
        }

        return true;
    }

    private void ReleaseSelf()
    {
        CombatFeedbackManager.PlayBreak(
            transform.position,
            CombatFeedbackKind.Meteor,
            1.05f,
            breakShakeAmplitude,
            breakShakeDuration,
            true
        );

        AudioManager.PlayAt(SoundEventIds.ObjectMeteorBreak, transform.position);

        GameObject releaseTarget = ResolveObjectRoot();

        if (disableVisualsAndCollidersImmediatelyOnBreak)
        {
            SetBreakObjectsEnabled(false);
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(releaseTarget);
        }
        else
        {
            Destroy(releaseTarget);
        }
    }

    private GameObject ResolveObjectRoot()
    {
        if (objectRoot != null)
        {
            return objectRoot;
        }

        if (visualRoot != null && visualRoot.parent != null)
        {
            Transform candidate = visualRoot.parent;
            MeteorObstacle linkedMeteor = candidate.GetComponentInChildren<MeteorObstacle>(true);

            if (linkedMeteor == this)
            {
                return candidate.gameObject;
            }
        }

        return gameObject;
    }

    private void CacheBreakObjects()
    {
        GameObject root = ResolveObjectRoot();

        if (root == null)
        {
            root = gameObject;
        }

        if (renderersToDisableOnBreak == null || renderersToDisableOnBreak.Length == 0)
        {
            renderersToDisableOnBreak = root.GetComponentsInChildren<Renderer>(true);
        }

        if (collidersToDisableOnBreak == null || collidersToDisableOnBreak.Length == 0)
        {
            collidersToDisableOnBreak = root.GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void SetBreakObjectsEnabled(bool value)
    {
        if (renderersToDisableOnBreak != null)
        {
            for (int i = 0; i < renderersToDisableOnBreak.Length; i++)
            {
                if (renderersToDisableOnBreak[i] != null)
                {
                    renderersToDisableOnBreak[i].enabled = value;
                }
            }
        }

        if (collidersToDisableOnBreak != null)
        {
            for (int i = 0; i < collidersToDisableOnBreak.Length; i++)
            {
                if (collidersToDisableOnBreak[i] != null)
                {
                    collidersToDisableOnBreak[i].enabled = value;
                }
            }
        }
    }
}
