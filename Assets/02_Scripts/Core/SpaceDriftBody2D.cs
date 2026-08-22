using UnityEngine;

/// <summary>
/// 소형 운석/작은 잔해 전용 무중력 물리 이동 컴포넌트입니다.
/// Transform 직접 이동 대신 Rigidbody2D 속도를 사용하며, 지정된 맵 Bounds 안에서 반사됩니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class SpaceDriftBody2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;

    [Header("Rigidbody Setup")]
    [SerializeField] private bool configureRigidbodyOnEnable = true;
    [SerializeField] private float mass = 1f;
    [SerializeField] private float linearDamping;
    [SerializeField] private float angularDamping;
    [SerializeField] private CollisionDetectionMode2D collisionDetection = CollisionDetectionMode2D.Continuous;
    [SerializeField] private RigidbodyInterpolation2D interpolation = RigidbodyInterpolation2D.Interpolate;

    [Header("Initial Drift")]
    [SerializeField] private bool randomizeOnEnable = true;
    [Range(0f, 1f)]
    [SerializeField] private float movementChance = 0.4f;
    [SerializeField] private Vector2 speedRange = new Vector2(0.15f, 0.4f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(-12f, 12f);
    [SerializeField] private Vector2 idleAngularSpeedRange = new Vector2(-2f, 2f);

    [Header("Limits")]
    [Min(0.01f)]
    [SerializeField] private float maxLinearSpeed = 0.6f;
    [Min(0.01f)]
    [SerializeField] private float maxAngularSpeed = 30f;

    [Header("Bounds")]
    [SerializeField] private bool useRoamingBounds;
    [SerializeField] private Bounds roamingBounds;
    [Min(0f)]
    [SerializeField] private float boundsPadding = 0.5f;
    [Range(0f, 1f)]
    [SerializeField] private float bounceVelocityRetention = 0.9f;

    private bool initialized;
    private bool hasRuntimeMovementOverride;
    private bool runtimeMovementEnabled;

    public Rigidbody2D Body => body;
    public bool HasRoamingBounds => useRoamingBounds;

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Awake()
    {
        ResolveBody();
    }

    private void OnEnable()
    {
        ResolveBody();
        ConfigureBody();

        if (randomizeOnEnable)
        {
            RandomizeVelocity();
        }

        initialized = true;
    }

    private void OnDisable()
    {
        initialized = false;
        hasRuntimeMovementOverride = false;
        runtimeMovementEnabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || body == null)
        {
            return;
        }

        ClampVelocity();

        if (useRoamingBounds)
        {
            HandleBoundsBounce();
        }
    }

    public void SetRoamingBounds(Bounds bounds)
    {
        roamingBounds = bounds;
        useRoamingBounds = true;
    }

    public void ClearRoamingBounds()
    {
        useRoamingBounds = false;
    }

    public void SetRuntimeMovementEnabled(bool enabled, bool rerandomize = true)
    {
        hasRuntimeMovementOverride = true;
        runtimeMovementEnabled = enabled;

        if (rerandomize && isActiveAndEnabled)
        {
            ApplyRandomizedVelocity(enabled);
        }
    }

    public void RandomizeVelocity()
    {
        if (body == null)
        {
            return;
        }

        bool shouldMove = hasRuntimeMovementOverride
            ? runtimeMovementEnabled
            : Random.value < Mathf.Clamp01(movementChance);
        ApplyRandomizedVelocity(shouldMove);
    }

    private void ApplyRandomizedVelocity(bool shouldMove)
    {
        if (body == null)
        {
            return;
        }

        if (!shouldMove)
        {
            float minIdleAngular = Mathf.Min(idleAngularSpeedRange.x, idleAngularSpeedRange.y);
            float maxIdleAngular = Mathf.Max(idleAngularSpeedRange.x, idleAngularSpeedRange.y);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = Random.Range(minIdleAngular, maxIdleAngular);
            return;
        }

        Vector2 direction = Random.insideUnitCircle;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        direction.Normalize();

        float minSpeed = Mathf.Max(0f, Mathf.Min(speedRange.x, speedRange.y));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(speedRange.x, speedRange.y));
        float minAngular = Mathf.Min(angularSpeedRange.x, angularSpeedRange.y);
        float maxAngular = Mathf.Max(angularSpeedRange.x, angularSpeedRange.y);

        body.linearVelocity = direction * Random.Range(minSpeed, maxSpeed);
        body.angularVelocity = Random.Range(minAngular, maxAngular);
    }

    public void ApplyImpulse(Vector2 direction, float impulse)
    {
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic || impulse <= 0f)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        body.AddForce(direction.normalized * impulse, ForceMode2D.Impulse);
        ClampVelocity();
    }

    private void ResolveBody()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void ConfigureBody()
    {
        if (!configureRigidbodyOnEnable || body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0f;
        body.mass = Mathf.Max(0.01f, mass);
        body.linearDamping = Mathf.Max(0f, linearDamping);
        body.angularDamping = Mathf.Max(0f, angularDamping);
        body.collisionDetectionMode = collisionDetection;
        body.interpolation = interpolation;
        body.freezeRotation = false;
    }

    private void ClampVelocity()
    {
        if (body == null)
        {
            return;
        }

        float maxSpeed = Mathf.Max(0.01f, maxLinearSpeed);

        if (body.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
        }

        body.angularVelocity = Mathf.Clamp(
            body.angularVelocity,
            -Mathf.Max(0.01f, maxAngularSpeed),
            Mathf.Max(0.01f, maxAngularSpeed)
        );
    }

    private void HandleBoundsBounce()
    {
        Vector2 position = body.position;
        Vector2 velocity = body.linearVelocity;
        bool bounced = false;

        float minX = roamingBounds.min.x + boundsPadding;
        float maxX = roamingBounds.max.x - boundsPadding;
        float minY = roamingBounds.min.y + boundsPadding;
        float maxY = roamingBounds.max.y - boundsPadding;

        if (position.x < minX)
        {
            position.x = minX;
            velocity.x = Mathf.Abs(velocity.x);
            bounced = true;
        }
        else if (position.x > maxX)
        {
            position.x = maxX;
            velocity.x = -Mathf.Abs(velocity.x);
            bounced = true;
        }

        if (position.y < minY)
        {
            position.y = minY;
            velocity.y = Mathf.Abs(velocity.y);
            bounced = true;
        }
        else if (position.y > maxY)
        {
            position.y = maxY;
            velocity.y = -Mathf.Abs(velocity.y);
            bounced = true;
        }

        if (!bounced)
        {
            return;
        }

        body.position = position;
        body.linearVelocity = velocity * Mathf.Clamp01(bounceVelocityRetention);
    }
}
