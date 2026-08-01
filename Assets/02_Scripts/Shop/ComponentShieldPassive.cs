using UnityEngine;

[DisallowMultipleComponent]
public class ComponentShieldPassive : MonoBehaviour
{
    [Header("Charge")]
    [SerializeField] private float rechargeInterval = 18f;
    [SerializeField] private bool startCharged = true;

    [Header("Burst")]
    [SerializeField] private LayerMask projectileClearLayer;
    [SerializeField] private float projectileClearRadius = 2.5f;
    [SerializeField] private LayerMask knockbackLayer;
    [SerializeField] private float knockbackRadius = 2f;
    [SerializeField] private float knockbackDistance = 2f;

    [Header("VFX")]
    [SerializeField] private GameObject blockEffectPrefab;
    [SerializeField] private float blockEffectLifetime = 0.4f;
    [SerializeField] private bool useProceduralHitEffectWhenPrefabMissing = true;
    [SerializeField] private float blockEffectIntensity = 1.35f;

    [Header("Camera Shake")]
    [SerializeField] private float blockShakeAmplitude = 0.13f;
    [SerializeField] private float blockShakeDuration = 0.12f;

    private float rechargeTimer;
    private bool charged;

    public bool IsCharged => charged;
    public float RechargeRatio => rechargeInterval <= 0f ? 1f : Mathf.Clamp01(rechargeTimer / rechargeInterval);

    private readonly Collider2D[] projectileBuffer = new Collider2D[96];
    private readonly Collider2D[] knockbackBuffer = new Collider2D[64];

    private void OnEnable()
    {
        charged = startCharged;
        rechargeTimer = charged ? rechargeInterval : 0f;
    }

    private void Update()
    {
        if (charged)
        {
            return;
        }

        rechargeTimer += Time.deltaTime;

        if (rechargeTimer >= rechargeInterval)
        {
            RechargeNow();
        }
    }

    public void ConfigureRuntime(float rechargeSeconds, float clearRadius, bool rechargeImmediately)
    {
        rechargeInterval = Mathf.Max(0.1f, rechargeSeconds);
        projectileClearRadius = Mathf.Max(0f, clearRadius);

        if (rechargeImmediately)
        {
            RechargeNow();
        }
        else
        {
            rechargeTimer = Mathf.Clamp(rechargeTimer, 0f, rechargeInterval);
        }
    }

    public void RechargeNow()
    {
        charged = true;
        rechargeTimer = rechargeInterval;
    }

    public bool TryBlockDamage(Vector2 hitPoint)
    {
        if (!enabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!charged)
        {
            return false;
        }

        charged = false;
        rechargeTimer = 0f;

        Vector2 center = transform.position;

        SpawnEffect(hitPoint);
        CombatFeedbackManager.PlayHit(
            hitPoint,
            center - hitPoint,
            CombatFeedbackKind.Shield,
            blockEffectIntensity,
            blockShakeAmplitude,
            blockShakeDuration,
            useProceduralHitEffectWhenPrefabMissing && blockEffectPrefab == null
        );
        ClearProjectiles(center);
        PushNearbyObjects(center);

        return true;
    }

    private void ClearProjectiles(Vector2 center)
    {
        if (projectileClearRadius <= 0f)
        {
            return;
        }

        int layerMask = projectileClearLayer.value == 0
            ? Physics2D.AllLayers
            : projectileClearLayer.value;

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            projectileClearRadius,
            projectileBuffer,
            layerMask
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = projectileBuffer[i];

            if (hit == null)
            {
                continue;
            }

            ShopProjectile shopProjectile = hit.GetComponentInParent<ShopProjectile>();
            if (shopProjectile != null)
            {
                shopProjectile.ReleaseSelf();
                continue;
            }

            Bullet bullet = hit.GetComponentInParent<Bullet>();
            if (bullet != null && bullet.Owner == ProjectileOwner.Enemy)
            {
                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(bullet.gameObject);
                }
                else
                {
                    Destroy(bullet.gameObject);
                }
            }
        }
    }

    private void PushNearbyObjects(Vector2 center)
    {
        if (knockbackLayer.value == 0 || knockbackRadius <= 0f || knockbackDistance <= 0f)
        {
            return;
        }

        int count = Physics2D.OverlapCircleNonAlloc(
            center,
            knockbackRadius,
            knockbackBuffer,
            knockbackLayer
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = knockbackBuffer[i];

            if (hit == null)
            {
                continue;
            }

            if (hit.transform == transform || hit.GetComponentInParent<PlayerHealth>() != null)
            {
                continue;
            }

            IKnockbackReceiver receiver = hit.GetComponentInParent<IKnockbackReceiver>();

            if (receiver != null)
            {
                receiver.ApplyKnockback(center, knockbackDistance);
                continue;
            }

            Rigidbody2D targetRb = hit.attachedRigidbody;

            if (targetRb == null)
            {
                continue;
            }

            Vector2 direction = targetRb.position - center;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Random.insideUnitCircle;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            targetRb.position += direction.normalized * knockbackDistance;
        }
    }

    private void SpawnEffect(Vector2 position)
    {
        if (blockEffectPrefab == null)
        {
            return;
        }

        GameObject effect;

        if (PoolManager.Instance != null)
        {
            effect = PoolManager.Instance.Get(blockEffectPrefab, position, Quaternion.identity);
            PoolManager.Instance.ReleaseAfter(effect, blockEffectLifetime);
        }
        else
        {
            effect = Instantiate(blockEffectPrefab, position, Quaternion.identity);
            Destroy(effect, blockEffectLifetime);
        }
    }
}