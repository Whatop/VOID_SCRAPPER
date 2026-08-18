using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerPeriodicReflector2D : MonoBehaviour
{
    public enum ReflectorState
    {
        Disabled,
        Cooldown,
        Ready,
        Reflecting
    }

    private const string ReflectorResourcePath = "VFX/PF_PlayerPeriodicReflector";
    private static GameObject cachedReflectorPrefab;

    [Header("Reflection")]
    [SerializeField] private CircleCollider2D reflectTrigger;
    [Min(0.05f)]
    [SerializeField] private float reflectionRadius = 0.42f;
    [Min(0.05f)]
    [SerializeField] private float reflectionWindow = 1.25f;
    [Min(0.05f)]
    [SerializeField] private float defaultRechargeDuration = 15f;

    [Header("Reflected Projectile")]
    [SerializeField] private GameObject reflectedProjectilePrefab;
    [Min(0.1f)]
    [SerializeField] private float reflectedProjectileRange = 12f;
    [Min(0f)]
    [SerializeField] private float reflectedSpawnOffset = 0.04f;

    [Header("Presentation")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform ringRoot;
    [SerializeField] private SpriteRenderer ringRenderer;
    [SerializeField] private Color readyColor = new Color(1f, 0.85f, 0.18f, 0.5f);
    [SerializeField] private Color reflectingColor = new Color(1f, 0.98f, 0.65f, 0.95f);
    [Min(0.05f)]
    [SerializeField] private float readyPulseDuration = 0.8f;
    [Min(0.05f)]
    [SerializeField] private float reflectingPulseDuration = 0.18f;
    [Min(0.01f)]
    [SerializeField] private float contactPunchDuration = 0.11f;

    private readonly Dictionary<UnityEngine.Object, float> activationSources =
        new Dictionary<UnityEngine.Object, float>(2);

    private GameObject playerOwner;
    private ReflectorState currentState = ReflectorState.Disabled;
    private float stateTimer;
    private float rechargeDuration = 15f;
    private Vector3 ringActiveScale = Vector3.one;
    private Tween pulseTween;
    private Tween contactTween;

    public ReflectorState CurrentState => currentState;
    public float ReflectionRadius => reflectionRadius;
    public float ReflectionWindow => reflectionWindow;
    public float RechargeDuration => rechargeDuration;
    public float StateTimeRemaining => Mathf.Max(0f, stateTimer);

    public static bool SetSourceEnabled(
        GameObject player,
        UnityEngine.Object source,
        bool value,
        float rechargeSeconds = 15f)
    {
        if (player == null || source == null)
        {
            return false;
        }

        PlayerPeriodicReflector2D reflector =
            player.GetComponentInChildren<PlayerPeriodicReflector2D>(true);

        if (reflector == null && value)
        {
            reflector = CreateForPlayer(player);
        }

        if (reflector == null)
        {
            return false;
        }

        reflector.SetActivationSource(source, value, rechargeSeconds);
        return true;
    }

    private static PlayerPeriodicReflector2D CreateForPlayer(GameObject player)
    {
        if (cachedReflectorPrefab == null)
        {
            cachedReflectorPrefab = Resources.Load<GameObject>(ReflectorResourcePath);
        }

        if (cachedReflectorPrefab == null)
        {
            Debug.LogWarning(
                $"Periodic reflector prefab is missing from Resources/{ReflectorResourcePath}.",
                player
            );
            return null;
        }

        GameObject instance = Instantiate(
            cachedReflectorPrefab,
            player.transform.position,
            Quaternion.identity,
            player.transform
        );
        instance.name = "PeriodicReflectiveShield";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        PlayerPeriodicReflector2D reflector = instance.GetComponent<PlayerPeriodicReflector2D>();
        reflector?.AttachToPlayer(player);
        return reflector;
    }

    private void Reset()
    {
        reflectTrigger = GetComponent<CircleCollider2D>();
        visualRoot = transform;

        if (reflectTrigger != null)
        {
            reflectTrigger.isTrigger = true;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureWorldRadius();
        TransitionToDisabled();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureWorldRadius();

        if (activationSources.Count == 0)
        {
            TransitionToDisabled();
        }
    }

    private void OnDisable()
    {
        activationSources.Clear();
        playerOwner = null;
        TransitionToDisabled();
    }

    private void Update()
    {
        if (currentState != ReflectorState.Reflecting &&
            currentState != ReflectorState.Cooldown)
        {
            return;
        }

        stateTimer -= Time.deltaTime;

        if (stateTimer > 0f)
        {
            return;
        }

        if (currentState == ReflectorState.Reflecting)
        {
            TransitionToCooldown();
        }
        else
        {
            TransitionToReady();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if ((currentState != ReflectorState.Ready &&
             currentState != ReflectorState.Reflecting) ||
            other == null ||
            playerOwner == null)
        {
            return;
        }

        Bullet incomingBullet = other.GetComponentInParent<Bullet>();

        if (incomingBullet == null ||
            incomingBullet.Owner != ProjectileOwner.Enemy ||
            !incomingBullet.TryClaimReflection(out BulletReflectionSnapshot snapshot))
        {
            return;
        }

        Vector2 reflectionDirection = ResolveReflectionDirection(snapshot);
        Vector3 reflectionPosition = incomingBullet.transform.position;
        GameObject reflectedObject = SpawnReflectedProjectile(
            reflectionPosition + (Vector3)(reflectionDirection * reflectedSpawnOffset)
        );

        if (reflectedObject == null)
        {
            incomingBullet.CancelReflectionClaim();
            return;
        }

        Bullet reflectedBullet = reflectedObject.GetComponent<Bullet>();

        if (reflectedBullet == null)
        {
            ReleaseSpawnedProjectile(reflectedObject);
            incomingBullet.CancelReflectionClaim();
            return;
        }

        reflectedBullet.Initialize(
            reflectionDirection,
            ProjectileOwner.Player,
            null,
            snapshot.Damage,
            snapshot.Speed,
            reflectedProjectileRange,
            0,
            0f,
            0f,
            1f,
            false,
            playerOwner,
            1f
        );

        incomingBullet.ForceRelease(false);

        if (currentState == ReflectorState.Ready)
        {
            TransitionToReflecting();
        }

        PlayContactFeedback();
    }

    private void AttachToPlayer(GameObject player)
    {
        playerOwner = player;
        ConfigureWorldRadius();
    }

    private void SetActivationSource(
        UnityEngine.Object source,
        bool value,
        float rechargeSeconds)
    {
        bool wasActive = activationSources.Count > 0;

        if (value)
        {
            activationSources[source] = Mathf.Max(0.05f, rechargeSeconds);
        }
        else
        {
            activationSources.Remove(source);
        }

        RecalculateRechargeDuration();
        bool isActive = activationSources.Count > 0;

        if (!wasActive && isActive)
        {
            if (playerOwner == null)
            {
                playerOwner = transform.parent != null
                    ? transform.parent.gameObject
                    : null;
            }

            TransitionToReady();
        }
        else if (wasActive && !isActive)
        {
            TransitionToDisabled();
        }
        else if (isActive && currentState == ReflectorState.Cooldown)
        {
            stateTimer = Mathf.Min(stateTimer, rechargeDuration);
        }
    }

    private void RecalculateRechargeDuration()
    {
        float shortestRecharge = float.MaxValue;

        foreach (KeyValuePair<UnityEngine.Object, float> source in activationSources)
        {
            if (source.Key != null)
            {
                shortestRecharge = Mathf.Min(shortestRecharge, source.Value);
            }
        }

        rechargeDuration = shortestRecharge < float.MaxValue
            ? Mathf.Max(0.05f, shortestRecharge)
            : Mathf.Max(0.05f, defaultRechargeDuration);
    }

    private Vector2 ResolveReflectionDirection(BulletReflectionSnapshot snapshot)
    {
        Transform firingSource = snapshot.FiringSource;

        if (IsValidHostileFiringSource(firingSource))
        {
            Vector2 towardSource = (Vector2)firingSource.position - (Vector2)transform.position;

            if (towardSource.sqrMagnitude > 0.001f)
            {
                return towardSource.normalized;
            }
        }

        return snapshot.TravelDirection.sqrMagnitude > 0.001f
            ? -snapshot.TravelDirection.normalized
            : Vector2.up;
    }

    private static bool IsValidHostileFiringSource(Transform firingSource)
    {
        if (firingSource == null || !firingSource.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (firingSource.GetComponentInParent<PlayerHealth>() != null)
        {
            return false;
        }

        IPlayerOwnedAlly playerOwnedAlly =
            firingSource.GetComponentInParent<IPlayerOwnedAlly>();

        if (playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly)
        {
            return false;
        }

        EnemyHealth enemyHealth = firingSource.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null || enemyHealth.IsDead)
        {
            return false;
        }

        BaseTurretController turret = enemyHealth.GetComponentInParent<BaseTurretController>();

        if (turret != null)
        {
            if (turret.IsPlayerAllied)
            {
                return false;
            }

            if (turret.IsShopDefense && turret.ShopOwner != null && !turret.ShopOwner.IsHostile)
            {
                return false;
            }
        }

        EnemyBaseAI enemyAI = enemyHealth.GetComponent<EnemyBaseAI>();
        return enemyAI == null ||
               !enemyAI.IsShopSecurityUnit ||
               ShopRunBridge.IsShopHostileThisRun();
    }

    private GameObject SpawnReflectedProjectile(Vector3 position)
    {
        if (reflectedProjectilePrefab == null)
        {
            Debug.LogWarning("Periodic reflector has no reflected projectile prefab.", this);
            return null;
        }

        return PoolManager.Instance != null
            ? PoolManager.Instance.Get(reflectedProjectilePrefab, position, Quaternion.identity)
            : Instantiate(reflectedProjectilePrefab, position, Quaternion.identity);
    }

    private void ReleaseSpawnedProjectile(GameObject projectileObject)
    {
        if (projectileObject == null)
        {
            return;
        }

        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.Release(projectileObject);
        }
        else
        {
            Destroy(projectileObject);
        }
    }

    private void TransitionToDisabled()
    {
        currentState = ReflectorState.Disabled;
        stateTimer = 0f;
        SetTriggerEnabled(false);
        HideVisual();
    }

    private void TransitionToReady()
    {
        if (activationSources.Count == 0)
        {
            TransitionToDisabled();
            return;
        }

        currentState = ReflectorState.Ready;
        stateTimer = 0f;
        SetTriggerEnabled(true);
        ShowVisual(readyColor, readyPulseDuration, 0.96f, 1.04f);
    }

    private void TransitionToReflecting()
    {
        currentState = ReflectorState.Reflecting;
        stateTimer = Mathf.Max(0.05f, reflectionWindow);
        SetTriggerEnabled(true);
        ShowVisual(reflectingColor, reflectingPulseDuration, 0.9f, 1.08f);
    }

    private void TransitionToCooldown()
    {
        currentState = ReflectorState.Cooldown;
        stateTimer = Mathf.Max(0.05f, rechargeDuration);
        SetTriggerEnabled(false);
        HideVisual();
    }

    private void ResolveReferences()
    {
        if (reflectTrigger == null)
        {
            reflectTrigger = GetComponent<CircleCollider2D>();
        }

        if (reflectTrigger != null)
        {
            reflectTrigger.isTrigger = true;
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (ringRoot == null && ringRenderer != null)
        {
            ringRoot = ringRenderer.transform;
        }
    }

    private void ConfigureWorldRadius()
    {
        if (reflectTrigger != null)
        {
            Vector3 lossyScale = transform.lossyScale;
            float worldScale = Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                0.0001f
            );
            reflectTrigger.radius = reflectionRadius / worldScale;
        }

        if (ringRoot == null || ringRenderer == null || ringRenderer.sprite == null)
        {
            return;
        }

        Transform ringParent = ringRoot.parent;
        Vector3 parentLossyScale = ringParent != null
            ? ringParent.lossyScale
            : Vector3.one;
        float parentWorldScale = Mathf.Max(
            Mathf.Abs(parentLossyScale.x),
            Mathf.Abs(parentLossyScale.y),
            0.0001f
        );
        Vector3 spriteSize = ringRenderer.sprite.bounds.size;
        float spriteDiameter = Mathf.Max(spriteSize.x, spriteSize.y, 0.0001f);
        float localScale = reflectionRadius * 2f / (spriteDiameter * parentWorldScale);
        ringActiveScale = new Vector3(localScale, localScale, 1f);
        ringRoot.localScale = ringActiveScale;
    }

    private void SetTriggerEnabled(bool value)
    {
        if (reflectTrigger != null)
        {
            reflectTrigger.enabled = value;
        }
    }

    private void ShowVisual(
        Color color,
        float pulseDuration,
        float startScaleMultiplier,
        float endScaleMultiplier)
    {
        StopVisualTweens();

        if (ringRenderer == null || ringRoot == null)
        {
            return;
        }

        ringRenderer.enabled = true;
        ringRenderer.color = color;
        ringRoot.localScale = ringActiveScale * startScaleMultiplier;
        pulseTween = ringRoot
            .DOScale(ringActiveScale * endScaleMultiplier, Mathf.Max(0.05f, pulseDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
    }

    private void HideVisual()
    {
        StopVisualTweens();

        if (ringRoot != null)
        {
            ringRoot.localScale = ringActiveScale;
        }

        if (ringRenderer != null)
        {
            ringRenderer.enabled = false;
            ringRenderer.color = readyColor;
        }
    }

    private void PlayContactFeedback()
    {
        if (ringRoot == null || ringRenderer == null)
        {
            return;
        }

        pulseTween?.Kill(false);
        pulseTween = null;
        contactTween?.Kill(false);
        ringRenderer.color = reflectingColor;
        ringRoot.localScale = ringActiveScale;
        contactTween = ringRoot
            .DOPunchScale(ringActiveScale * 0.18f, contactPunchDuration, 1, 0.25f)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(RestartStatePulse);
    }

    private void RestartStatePulse()
    {
        contactTween = null;

        if (currentState == ReflectorState.Reflecting)
        {
            ShowVisual(reflectingColor, reflectingPulseDuration, 0.9f, 1.08f);
        }
        else if (currentState == ReflectorState.Ready)
        {
            ShowVisual(readyColor, readyPulseDuration, 0.96f, 1.04f);
        }
    }

    private void StopVisualTweens()
    {
        pulseTween?.Kill(false);
        contactTween?.Kill(false);
        pulseTween = null;
        contactTween = null;
    }
}
