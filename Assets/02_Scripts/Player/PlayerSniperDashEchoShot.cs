using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSniperDashEchoShot : MonoBehaviour
{
    private const string EchoVisualResourcePath = "VFX/PF_Player_Sniper_DashEcho";
    private static GameObject cachedEchoVisualPrefab;

    [Header("Echo Shot")]
    [Min(0.1f)]
    [SerializeField] private float echoLifetime = 2.5f;
    [Range(0.05f, 1f)]
    [SerializeField] private float echoDamageRatio = 0.4f;
    [SerializeField] private Color echoProjectileColor = new Color(0.3f, 0.9f, 1f, 1f);

    [Header("Echo Presentation")]
    [SerializeField] private Color echoVisualColor = new Color(0.25f, 0.82f, 1f, 0.45f);
    [Min(0.05f)]
    [SerializeField] private float echoVisualScale = 0.7f;
    [Min(0.05f)]
    [SerializeField] private float echoPulseDuration = 0.45f;
    [Range(1f, 1.25f)]
    [SerializeField] private float echoPulseScale = 1.08f;
    [Min(0.01f)]
    [SerializeField] private float echoReleaseDuration = 0.1f;

    private readonly HashSet<UnityEngine.Object> activationSources =
        new HashSet<UnityEngine.Object>();

    private PlayerDash playerDash;
    private PlayerController2D playerController;
    private PlayerWeaponController weaponController;
    private PlayerHealth playerHealth;
    private PlayerVisualStateController visualStateController;
    private SniperWeapon subscribedSniperWeapon;
    private GameObject activeEchoVisual;
    private Vector2 echoOrigin;
    private float echoRemainingTime;
    private bool echoAvailable;
    private bool subscribed;
    private bool warnedMissingVisualPrefab;
    private bool warnedMissingPool;

    public static bool SetSourceEnabled(
        GameObject player,
        UnityEngine.Object source,
        bool value)
    {
        if (player == null || source == null)
        {
            return false;
        }

        PlayerSniperDashEchoShot echoShot = player.GetComponent<PlayerSniperDashEchoShot>();

        if (echoShot == null && value)
        {
            echoShot = player.AddComponent<PlayerSniperDashEchoShot>();
        }

        if (echoShot == null)
        {
            return false;
        }

        echoShot.SetActivationSource(source, value);
        return true;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (activationSources.Count > 0)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearEcho(true);
        activationSources.Clear();
    }

    private void Update()
    {
        if (!echoAvailable)
        {
            return;
        }

        echoRemainingTime -= Time.deltaTime;

        if (echoRemainingTime <= 0f)
        {
            ClearEcho(false);
        }
    }

    private void SetActivationSource(UnityEngine.Object source, bool value)
    {
        if (value)
        {
            activationSources.Add(source);
            CacheReferences();
            Subscribe();
            return;
        }

        activationSources.Remove(source);

        if (activationSources.Count == 0)
        {
            Unsubscribe();
            ClearEcho(false);
        }
    }

    private void HandleDashStarted(Vector2 dashDirection)
    {
        if (activationSources.Count == 0 ||
            playerHealth == null ||
            playerHealth.IsDead ||
            weaponController == null ||
            weaponController.CurrentWeaponTree != WeaponTreeType.Sniper ||
            !(weaponController.CurrentWeapon is SniperWeapon))
        {
            return;
        }

        ClearEcho(false);
        echoOrigin = transform.position;
        echoRemainingTime = Mathf.Max(0.1f, echoLifetime);
        echoAvailable = true;
        Quaternion visualRotation = playerController != null && playerController.AimVisualRoot != null
            ? playerController.AimVisualRoot.rotation
            : transform.rotation;
        SpawnEchoVisual(echoOrigin, visualRotation);
    }

    private void HandleSuccessfulSniperShot(SniperSuccessfulShotSnapshot shotSnapshot)
    {
        if (!echoAvailable ||
            activationSources.Count == 0 ||
            weaponController == null ||
            weaponController.CurrentWeaponTree != WeaponTreeType.Sniper)
        {
            return;
        }

        Vector2 origin = echoOrigin;
        echoAvailable = false;
        echoRemainingTime = 0f;

        Vector2 direction = shotSnapshot.AimWorldPosition - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = shotSnapshot.Projectile.Direction;
        }

        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
        SpawnEchoProjectile(origin, direction, shotSnapshot.Projectile);

        visualStateController?.PlayAuxiliaryLaunchPulse(
            origin,
            direction,
            new Color(0.35f, 0.9f, 1f, 1f),
            0.26f
        );
        ReleaseEchoVisual(false, true);
    }

    private void HandleWeaponEquipped(
        WeaponTreeType weaponTreeType,
        PlayerWeaponBase weapon)
    {
        BindSuccessfulShotEvent(weapon as SniperWeapon);

        if (weaponTreeType != WeaponTreeType.Sniper || !(weapon is SniperWeapon))
        {
            ClearEcho(false);
        }
    }

    private void HandlePlayerDied()
    {
        ClearEcho(true);
    }

    private void SpawnEchoProjectile(
        Vector2 origin,
        Vector2 direction,
        PlayerProjectileFireSnapshot projectileSnapshot)
    {
        if (projectileSnapshot.ProjectilePrefab == null || PoolManager.Instance == null)
        {
            if (PoolManager.Instance == null && !warnedMissingPool)
            {
                warnedMissingPool = true;
                Debug.LogWarning(
                    "Sniper Dash Echo Shot requires the scene PoolManager.",
                    this
                );
            }

            return;
        }

        GameObject projectileObject = PoolManager.Instance.Get(
            projectileSnapshot.ProjectilePrefab,
            origin,
            Quaternion.identity
        );

        if (projectileObject == null)
        {
            return;
        }

        Bullet bullet = projectileObject.GetComponent<Bullet>();

        if (bullet == null)
        {
            PoolManager.Instance.Release(projectileObject);
            return;
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Player,
            projectileSnapshot.ProjectileDefinition,
            projectileSnapshot.Damage * Mathf.Clamp01(echoDamageRatio),
            projectileSnapshot.Speed,
            projectileSnapshot.Range,
            projectileSnapshot.PierceCount,
            0f,
            0f,
            1f,
            false,
            gameObject,
            projectileSnapshot.PierceDamageRetention
        );
        bullet.ConfigureStandaloneHoming(null, 0f, 0f, false);
        bullet.ConfigureEquipmentWidth(projectileSnapshot.WidthMultiplier);
        bullet.ConfigureProjectileColor(echoProjectileColor);
    }

    private void SpawnEchoVisual(Vector2 position, Quaternion rotation)
    {
        if (!TryResolveEchoVisualPrefab(out GameObject echoVisualPrefab) ||
            PoolManager.Instance == null)
        {
            if (PoolManager.Instance == null && !warnedMissingPool)
            {
                warnedMissingPool = true;
                Debug.LogWarning(
                    "Sniper Dash Echo visual requires the scene PoolManager.",
                    this
                );
            }

            return;
        }

        activeEchoVisual = PoolManager.Instance.Get(echoVisualPrefab, position, rotation);

        if (activeEchoVisual == null)
        {
            return;
        }

        Transform visualTransform = activeEchoVisual.transform;
        SpriteRenderer renderer = activeEchoVisual.GetComponentInChildren<SpriteRenderer>(true);
        Vector3 baseScale = Vector3.one * Mathf.Max(0.05f, echoVisualScale);

        visualTransform.DOKill();
        visualTransform.localScale = baseScale;

        if (renderer != null)
        {
            renderer.DOKill();
            renderer.color = echoVisualColor;
            renderer.DOFade(echoVisualColor.a * 0.72f, echoPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(activeEchoVisual, LinkBehaviour.KillOnDisable);
        }

        visualTransform.DOScale(baseScale * echoPulseScale, echoPulseDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(activeEchoVisual, LinkBehaviour.KillOnDisable);
    }

    private bool TryResolveEchoVisualPrefab(out GameObject echoVisualPrefab)
    {
        if (cachedEchoVisualPrefab == null)
        {
            cachedEchoVisualPrefab = Resources.Load<GameObject>(EchoVisualResourcePath);
        }

        echoVisualPrefab = cachedEchoVisualPrefab;

        if (echoVisualPrefab == null && !warnedMissingVisualPrefab)
        {
            warnedMissingVisualPrefab = true;
            Debug.LogWarning(
                $"Sniper Dash Echo visual is missing from Resources/{EchoVisualResourcePath}.",
                this
            );
        }

        return echoVisualPrefab != null;
    }

    private void ClearEcho(bool immediate)
    {
        echoAvailable = false;
        echoRemainingTime = 0f;
        ReleaseEchoVisual(immediate, false);
    }

    private void ReleaseEchoVisual(bool immediate, bool consumed)
    {
        GameObject visual = activeEchoVisual;
        activeEchoVisual = null;

        if (visual == null)
        {
            return;
        }

        Transform visualTransform = visual.transform;
        SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>(true);
        visualTransform.DOKill();
        renderer?.DOKill();

        if (immediate || PoolManager.Instance == null)
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(visual);
            }
            else
            {
                Destroy(visual);
            }

            return;
        }

        float duration = Mathf.Max(0.01f, echoReleaseDuration);
        float scaleMultiplier = consumed ? 1.22f : 1.08f;
        visualTransform.DOScale(visualTransform.localScale * scaleMultiplier, duration)
            .SetLink(visual, LinkBehaviour.KillOnDisable);

        if (renderer != null)
        {
            if (consumed)
            {
                Color flashColor = new Color(0.7f, 0.96f, 1f, 0.85f);
                renderer.color = flashColor;
            }

            renderer.DOFade(0f, duration)
                .SetLink(visual, LinkBehaviour.KillOnDisable);
        }

        PoolManager.Instance.ReleaseAfter(visual, duration);
    }

    private void CacheReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController2D>();
        }

        if (playerDash == null)
        {
            playerDash = GetComponent<PlayerDash>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (visualStateController == null)
        {
            visualStateController = GetComponent<PlayerVisualStateController>();
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            BindSuccessfulShotEvent(
                weaponController != null
                    ? weaponController.CurrentWeapon as SniperWeapon
                    : null
            );
            return;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted += HandleDashStarted;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped += HandleWeaponEquipped;
        }

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
        }

        subscribed = true;
        BindSuccessfulShotEvent(
            weaponController != null
                ? weaponController.CurrentWeapon as SniperWeapon
                : null
        );
    }

    private void Unsubscribe()
    {
        BindSuccessfulShotEvent(null);

        if (!subscribed)
        {
            return;
        }

        if (playerDash != null)
        {
            playerDash.DashStarted -= HandleDashStarted;
        }

        if (weaponController != null)
        {
            weaponController.WeaponEquipped -= HandleWeaponEquipped;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }

        subscribed = false;
    }

    private void BindSuccessfulShotEvent(SniperWeapon sniperWeapon)
    {
        if (subscribedSniperWeapon == sniperWeapon)
        {
            return;
        }

        if (subscribedSniperWeapon != null)
        {
            subscribedSniperWeapon.SuccessfulShotFired -= HandleSuccessfulSniperShot;
        }

        subscribedSniperWeapon = sniperWeapon;

        if (subscribedSniperWeapon != null && activationSources.Count > 0)
        {
            subscribedSniperWeapon.SuccessfulShotFired += HandleSuccessfulSniperShot;
        }
    }
}
