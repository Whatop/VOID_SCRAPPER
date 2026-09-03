using System;
using DG.Tweening;
using UnityEngine;

public enum FrigateBossPartId
{
    Left = 0,
    Center = 1,
    Right = 2
}

public enum FrigateBossPartState
{
    Alive = 0,
    Destroyed = 1,
    FinalSequencePending = 2,
    DestructionTransition = 3
}

[DisallowMultipleComponent]
public sealed class FrigateBossPart : MonoBehaviour, IDamageable
{
    [Header("Identity")]
    [SerializeField] private FrigateBossPartId partId;

    [Header("Local Health")]
    [SerializeField, Min(1f)] private float maxHealth = 55f;

    [Header("Authority")]
    [SerializeField] private FrigateTriadBossController owningController;
    [SerializeField] private EnemyHealth aggregateHealth;

    [Header("Part References")]
    [SerializeField] private Collider2D damageCollider;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private SpriteHitFlash2D hitFlash;
    [SerializeField] private GameObject firePointRoot;
    [SerializeField] private Transform primaryFirePoint;

    [Header("Destruction Presentation")]
    [SerializeField] private GameObject destructionVfxPrefab;
    [SerializeField, Min(0.05f)] private float destructionVfxLifetime = 0.65f;
    [SerializeField] private Color destroyedColor = new Color(0.22f, 0.2f, 0.2f, 0.72f);
    [SerializeField] private Color finalPendingColor = new Color(1f, 0.34f, 0.12f, 1f);
    [SerializeField] private float destroyedRotationDegrees = 8f;

    private float currentHealth;
    private FrigateBossPartState state;
    private Color originalColor = Color.white;
    private Quaternion originalLocalRotation = Quaternion.identity;
    private Quaternion originalPartLocalRotation = Quaternion.identity;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Vector3 originalLocalScale = Vector3.one;
    private int originalSiblingIndex;
    private Tween wreckFadeTween;
    private bool detachedWorldWreck;
    private bool presentationCached;

    public FrigateBossPartId PartId => partId;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public FrigateBossPartState State => state;
    public bool IsAlive => state == FrigateBossPartState.Alive;
    public bool IsDestroyed => state == FrigateBossPartState.Destroyed;
    public bool IsFinalSequencePending => state == FrigateBossPartState.FinalSequencePending;
    public bool IsDead => state != FrigateBossPartState.Alive;
    public bool CanReceiveDamage =>
        state == FrigateBossPartState.Alive &&
        owningController != null &&
        owningController.CanAcceptPartDamage;
    public bool CanParticipateInPatterns =>
        state == FrigateBossPartState.Alive &&
        firePointRoot != null &&
        firePointRoot.activeSelf &&
        primaryFirePoint != null;
    public Transform VisualRoot => visualRoot;
    public SpriteRenderer VisualRenderer => visualRenderer;
    public GameObject FirePointRoot => firePointRoot;
    public Transform PrimaryFirePoint => primaryFirePoint;
    public EnemyHealth AggregateHealth => aggregateHealth;

    public Vector2 ResolveHomingAimPoint(Vector2 seekerPosition)
    {
        if (damageCollider == null || !damageCollider.enabled ||
            !damageCollider.gameObject.activeInHierarchy)
        {
            return transform.position;
        }

        Vector2 colliderCenter = damageCollider.bounds.center;
        Vector2 closestPoint = damageCollider.ClosestPoint(seekerPosition);
        return (closestPoint - seekerPosition).sqrMagnitude <= 0.000001f
            ? colliderCenter
            : closestPoint;
    }

    public event Action<FrigateBossPart, float, float> HealthChanged;
    public event Action<FrigateBossPart, FrigateBossPartState> StateChanged;
    public event Action<FrigateBossPart> Destroyed;
    public event Action<FrigateBossPart> FinalSequencePendingEntered;

    private void Awake()
    {
        ResolveFirePoint();
        CachePresentationState();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        destructionVfxLifetime = Mathf.Max(0.05f, destructionVfxLifetime);
    }

    private void OnDestroy()
    {
        StopDetachedWreckPresentation(false);
        owningController = null;
        aggregateHealth = null;
        damageCollider = null;
        visualRoot = null;
        visualRenderer = null;
        hitFlash = null;
        firePointRoot = null;
        primaryFirePoint = null;
        HealthChanged = null;
        StateChanged = null;
        Destroyed = null;
        FinalSequencePendingEntered = null;
    }

    public void TakeDamage(float damage)
    {
        TryTakeDamage(damage, transform.position, Vector2.zero);
    }

    public bool TryTakeDamage(
        float damage,
        Vector2 hitPoint,
        Vector2 incomingDirection)
    {
        if (!CanReceiveDamage || damage <= 0f)
        {
            return false;
        }

        return owningController.TryApplyPartDamage(
            this,
            damage,
            hitPoint,
            incomingDirection
        );
    }

    internal void InitializeRuntime(
        FrigateTriadBossController controller,
        EnemyHealth rootHealth)
    {
        ResolveFirePoint();
        owningController = controller;
        aggregateHealth = rootHealth;
        ResetPartState();
    }

    internal void DetachRuntime(FrigateTriadBossController controller)
    {
        StopDetachedWreckPresentation(true);

        if (ReferenceEquals(owningController, controller))
        {
            owningController = null;
            aggregateHealth = null;
        }

        SetRuntimeDamageEnabled(false);
    }

    internal void ResetPartState()
    {
        CachePresentationState();
        StopDetachedWreckPresentation(true);
        currentHealth = Mathf.Max(1f, maxHealth);
        state = FrigateBossPartState.Alive;

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
            visualRoot.localRotation = originalLocalRotation;
        }

        transform.localRotation = originalPartLocalRotation;

        if (visualRenderer != null)
        {
            visualRenderer.enabled = true;
            visualRenderer.color = originalColor;
        }

        if (firePointRoot != null)
        {
            firePointRoot.SetActive(true);
        }

        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }

        HealthChanged?.Invoke(this, currentHealth, maxHealth);
        StateChanged?.Invoke(this, state);
    }

    internal void BeginDetachedWreckFade(float fadeDelay, float fadeDuration)
    {
        if (state != FrigateBossPartState.Destroyed || visualRenderer == null)
        {
            return;
        }

        CachePresentationState();
        StopDetachedWreckPresentation(false);

        transform.SetParent(null, true);
        detachedWorldWreck = true;
        SetRuntimeDamageEnabled(false);

        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(true);
        }

        Color visibleColor = visualRenderer.color;
        visibleColor.a = Mathf.Max(0.01f, visibleColor.a);
        visualRenderer.color = visibleColor;
        visualRenderer.enabled = true;

        wreckFadeTween = visualRenderer
            .DOFade(0f, Mathf.Max(0.05f, fadeDuration))
            .SetDelay(Mathf.Max(0f, fadeDelay))
            .SetEase(Ease.InQuad)
            .SetLink(gameObject)
            .OnComplete(HideDetachedWreckVisual);
    }

    internal void StopDetachedWreckPresentation(bool restoreAuthoredHierarchy)
    {
        if (wreckFadeTween != null)
        {
            wreckFadeTween.Kill(false);
            wreckFadeTween = null;
        }

        if (restoreAuthoredHierarchy && detachedWorldWreck)
        {
            RestoreAuthoredHierarchy();
        }
    }

    internal float ApplyAcceptedDamage(float damage)
    {
        if (state != FrigateBossPartState.Alive || damage <= 0f)
        {
            return 0f;
        }

        float acceptedDamage = Mathf.Min(currentHealth, damage);
        currentHealth = Mathf.Max(0f, currentHealth - acceptedDamage);
        PlayLocalHitFlash(acceptedDamage);
        HealthChanged?.Invoke(this, currentHealth, maxHealth);
        return acceptedDamage;
    }

    internal void PlayPendingLethalHitFeedback(float damage)
    {
        PlayLocalHitFlash(Mathf.Max(0.01f, damage));
    }

    internal void EnterDestroyedState(bool playLocalDestructionVfx = true)
    {
        if (state == FrigateBossPartState.Destroyed)
        {
            return;
        }

        bool healthChanged = currentHealth > 0f;
        currentHealth = 0f;
        state = FrigateBossPartState.Destroyed;
        SetRuntimeDamageEnabled(false);
        ApplyDestroyedPresentation();

        if (playLocalDestructionVfx)
        {
            SpawnDestructionVfx();
        }

        if (healthChanged)
        {
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
        }

        StateChanged?.Invoke(this, state);
        Destroyed?.Invoke(this);
    }

    internal bool EnterDestructionTransitionState()
    {
        if (state != FrigateBossPartState.Alive)
        {
            return false;
        }

        bool healthChanged = currentHealth > 0f;
        currentHealth = 0f;
        state = FrigateBossPartState.DestructionTransition;
        SetRuntimeDamageEnabled(false);

        if (healthChanged)
        {
            HealthChanged?.Invoke(this, currentHealth, maxHealth);
        }

        StateChanged?.Invoke(this, state);
        return true;
    }

    internal void EnterFinalSequencePending()
    {
        if (state != FrigateBossPartState.Alive)
        {
            return;
        }

        state = FrigateBossPartState.FinalSequencePending;
        SetRuntimeDamageEnabled(false);

        if (visualRenderer != null)
        {
            visualRenderer.color = finalPendingColor;
        }

        StateChanged?.Invoke(this, state);
        FinalSequencePendingEntered?.Invoke(this);
    }

    internal void CompleteDeferredDestruction()
    {
        if (state != FrigateBossPartState.FinalSequencePending)
        {
            return;
        }

        EnterDestroyedState(false);
    }

    internal void SetRuntimeDamageEnabled(bool enabled)
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = enabled && state == FrigateBossPartState.Alive;
        }

        if (firePointRoot != null)
        {
            firePointRoot.SetActive(enabled && state == FrigateBossPartState.Alive);
        }
    }

    private void CachePresentationState()
    {
        if (presentationCached)
        {
            return;
        }

        if (visualRenderer != null)
        {
            originalColor = visualRenderer.color;
        }

        if (visualRoot != null)
        {
            originalLocalRotation = visualRoot.localRotation;
        }

        originalPartLocalRotation = transform.localRotation;
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalScale = transform.localScale;
        originalSiblingIndex = transform.GetSiblingIndex();

        presentationCached = true;
    }

    private void RestoreAuthoredHierarchy()
    {
        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalPartLocalRotation;
            transform.localScale = originalLocalScale;
            transform.SetSiblingIndex(
                Mathf.Clamp(originalSiblingIndex, 0, originalParent.childCount - 1)
            );
        }

        detachedWorldWreck = false;
    }

    private void HideDetachedWreckVisual()
    {
        wreckFadeTween = null;
        if (visualRoot != null)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    private void ResolveFirePoint()
    {
        if (primaryFirePoint != null || firePointRoot == null)
        {
            return;
        }

        Transform root = firePointRoot.transform;
        if (root.childCount > 0)
        {
            primaryFirePoint = root.GetChild(0);
        }
    }

    private void PlayLocalHitFlash(float damage)
    {
        float intensity = Mathf.Clamp(
            Mathf.Sqrt(Mathf.Max(0.01f, damage) / 2f),
            0.65f,
            1.75f
        );
        hitFlash?.Play(intensity);
    }

    private void ApplyDestroyedPresentation()
    {
        if (visualRenderer != null)
        {
            visualRenderer.color = destroyedColor;
        }

        if (visualRoot != null)
        {
            visualRoot.localRotation = originalLocalRotation *
                                       Quaternion.Euler(0f, 0f, destroyedRotationDegrees);
        }
    }

    private void SpawnDestructionVfx()
    {
        if (destructionVfxPrefab != null)
        {
            float lifetime = Mathf.Max(0.05f, destructionVfxLifetime);

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.SpawnAutoRelease(
                    destructionVfxPrefab,
                    transform.position,
                    lifetime
                );
            }
            else
            {
                GameObject effect = Instantiate(
                    destructionVfxPrefab,
                    transform.position,
                    Quaternion.identity
                );
                Destroy(effect, lifetime);
            }
        }

        AudioManager.PlayAt(SoundEventIds.ShipDeathBreakup, transform.position, 0.72f);
    }
}
