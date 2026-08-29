using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RaiderCoverBlastTelegraph))]
public sealed class SalvageDevourerWarningArea : MonoBehaviour
{
    [SerializeField] private RaiderCoverBlastTelegraph telegraph;
    [SerializeField] private SpriteRenderer warningIcon;
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField, Min(0.05f)] private float impactEffectLifetime = 0.3f;
    [SerializeField] private Color iconStartColor = new Color(1f, 0.35f, 0.05f, 0.82f);
    [SerializeField] private Color iconFinalColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField, Min(1f)] private float iconPulseScale = 1.22f;

    private Vector3 iconBaseScale = Vector3.one;
    private PlayerHealth targetPlayer;
    private Collider2D targetCollider;
    private float radius;
    private float damage;
    private bool armed;

    private void Awake()
    {
        if (telegraph == null)
        {
            telegraph = GetComponent<RaiderCoverBlastTelegraph>();
        }

        if (warningIcon != null)
        {
            iconBaseScale = warningIcon.transform.localScale;
        }
    }

    private void OnEnable()
    {
        armed = false;
        targetPlayer = null;
        targetCollider = null;

        if (warningIcon != null)
        {
            warningIcon.enabled = false;
            warningIcon.color = iconStartColor;
            warningIcon.transform.localScale = iconBaseScale;
        }
    }

    private void OnDisable()
    {
        armed = false;
        targetPlayer = null;
        targetCollider = null;

        if (warningIcon != null)
        {
            warningIcon.enabled = false;
            warningIcon.color = iconStartColor;
            warningIcon.transform.localScale = iconBaseScale;
        }
    }

    public void BeginWarning(
        Vector2 position,
        float worldRadius,
        float strikeDamage,
        PlayerHealth player)
    {
        transform.position = position;
        radius = Mathf.Max(0.1f, worldRadius);
        damage = Mathf.Max(0f, strikeDamage);
        targetPlayer = player;
        targetCollider = targetPlayer != null
            ? targetPlayer.GetComponentInChildren<Collider2D>()
            : null;
        armed = true;

        telegraph?.BeginCharge(position, radius);

        if (warningIcon != null)
        {
            warningIcon.enabled = true;
            warningIcon.color = iconStartColor;
            warningIcon.transform.localScale = iconBaseScale;
        }
    }

    public void SetWarningProgress(float normalizedProgress)
    {
        if (!armed)
        {
            return;
        }

        float progress = Mathf.Clamp01(normalizedProgress);
        telegraph?.SetChargeProgress(progress);

        if (warningIcon != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(progress * Mathf.PI * 5f);
            warningIcon.color = Color.Lerp(iconStartColor, iconFinalColor, progress);
            warningIcon.transform.localScale = iconBaseScale *
                Mathf.Lerp(1f, iconPulseScale, pulse * progress);
        }
    }

    public void Detonate()
    {
        if (!armed)
        {
            return;
        }

        armed = false;
        Vector2 center = transform.position;
        SpawnImpactEffect(center);

        if (targetPlayer == null || targetPlayer.IsDead || damage <= 0f)
        {
            return;
        }

        Vector2 closestPoint = targetCollider != null
            ? targetCollider.ClosestPoint(center)
            : (Vector2)targetPlayer.transform.position;
        if ((closestPoint - center).sqrMagnitude > radius * radius)
        {
            return;
        }

        Vector2 incomingDirection = (Vector2)targetPlayer.transform.position - center;
        targetPlayer.TakeDamage(damage, closestPoint, incomingDirection);
    }

    private void SpawnImpactEffect(Vector2 position)
    {
        if (impactEffectPrefab == null || PoolManager.Instance == null)
        {
            return;
        }

        PoolManager.Instance.SpawnAutoRelease(
            impactEffectPrefab,
            position,
            Mathf.Max(0.05f, impactEffectLifetime)
        );
    }
}
