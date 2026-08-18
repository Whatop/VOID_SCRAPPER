using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSupportDroneController : MonoBehaviour, IPlayerOwnedAlly
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private PlayerProjectileInterceptor2D projectileInterceptor;

    [Header("Follow")]
    [Min(0.1f)]
    [SerializeField] private float orbitRadius = 0.7f;
    [SerializeField] private float orbitDegreesPerSecond = 120f;
    [Min(0.1f)]
    [SerializeField] private float followSharpness = 14f;

    private Transform owner;
    private float orbitAngle;
    private bool configured;

    public bool IsPlayerOwnedAlly => configured && owner != null;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        projectileInterceptor = GetComponentInChildren<PlayerProjectileInterceptor2D>(true);
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (projectileInterceptor == null)
        {
            projectileInterceptor = GetComponentInChildren<PlayerProjectileInterceptor2D>(true);
        }
    }

    private void OnEnable()
    {
        configured = false;
        owner = null;
        orbitAngle = 0f;
        projectileInterceptor?.SetInterceptionEnabled(false);
    }

    private void OnDisable()
    {
        configured = false;
        owner = null;
        projectileInterceptor?.SetInterceptionEnabled(false);

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = tint;
        }
    }

    private void LateUpdate()
    {
        if (!configured || owner == null)
        {
            return;
        }

        orbitAngle = Mathf.Repeat(
            orbitAngle + orbitDegreesPerSecond * Time.deltaTime,
            360f
        );

        float radians = orbitAngle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * orbitRadius;
        Vector3 targetPosition = owner.position + (Vector3)offset;
        float blend = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
    }

    public void Configure(
        GameObject playerOwner,
        ReinforcementDefinition definition,
        bool enableProjectileInterception = false)
    {
        if (playerOwner == null || definition == null)
        {
            Debug.LogWarning("지원 드론의 플레이어 소유자 또는 장비 정의가 비어 있습니다.", this);
            return;
        }

        owner = playerOwner.transform;
        orbitAngle = Mathf.Abs(GetInstanceID() * 37f) % 360f;
        configured = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = definition.Icon;
            spriteRenderer.color = tint;
        }

        projectileInterceptor?.SetInterceptionEnabled(enableProjectileInterception);
    }
}
