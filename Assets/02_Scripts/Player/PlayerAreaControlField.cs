using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public class PlayerAreaControlField : MonoBehaviour, IPlayerOwnedAlly
{
    [Header("Detection")]
    [SerializeField] private CircleCollider2D effectTrigger;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform coreRoot;
    [SerializeField] private Transform radiusRingRoot;
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private SpriteRenderer radiusRingRenderer;
    [SerializeField] private Color gravityColor = new Color(0.42f, 0.28f, 1f, 1f);
    [SerializeField] private Color stasisColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private float spawnDuration = 0.18f;
    [SerializeField] private float pulseDuration = 0.65f;
    [SerializeField] private float expiryDuration = 0.16f;

    private readonly Dictionary<EnemyBaseAI, int> trackedEnemies =
        new Dictionary<EnemyBaseAI, int>(32);

    private GameObject playerOwner;
    private ReinforcementDefinition definition;
    private float movementMultiplier = 1f;
    private float fieldRadius;
    private float lifetime;
    private bool configured;
    private Vector3 visualAuthoredScale = Vector3.one;
    private Vector3 coreAuthoredScale = Vector3.one;
    private Vector3 radiusAuthoredScale = Vector3.one;
    private Vector3 radiusActiveScale = Vector3.one;
    private Color activeColor = Color.white;
    private Sequence visualSequence;
    private Tween pulseTween;

    public bool IsPlayerOwnedAlly => configured && playerOwner != null;
    public float FieldRadius => fieldRadius;
    public float MovementMultiplier => movementMultiplier;

    private void Reset()
    {
        effectTrigger = GetComponent<CircleCollider2D>();
        effectTrigger.isTrigger = true;
        visualRoot = transform;
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureAuthoredVisualState();
        SetTriggerEnabled(false);
    }

    private void OnEnable()
    {
        configured = false;
        playerOwner = null;
        definition = null;
        trackedEnemies.Clear();
        StopVisualTweens();
        ResetVisualState();
        SetTriggerEnabled(false);
    }

    private void OnDisable()
    {
        ClearTrackedEnemies();
        configured = false;
        playerOwner = null;
        definition = null;
        movementMultiplier = 1f;
        fieldRadius = 0f;
        lifetime = 0f;
        SetTriggerEnabled(false);
        StopVisualTweens();
        ResetVisualState();
    }

    public void Configure(
        GameObject owner,
        ReinforcementDefinition fieldDefinition,
        float radius,
        float speedMultiplier,
        float activeLifetime)
    {
        ClearTrackedEnemies();
        StopVisualTweens();

        if (owner == null || fieldDefinition == null || radius <= 0f || activeLifetime <= 0f)
        {
            Debug.LogWarning(
                "Player area-control field requires a valid owner, definition, radius, and lifetime.",
                this
            );
            configured = false;
            SetTriggerEnabled(false);
            return;
        }

        playerOwner = owner;
        definition = fieldDefinition;
        fieldRadius = Mathf.Max(0.1f, radius);
        movementMultiplier = Mathf.Clamp(speedMultiplier, 0.01f, 1f);
        lifetime = Mathf.Max(0.01f, activeLifetime);
        configured = true;

        bool isStasis = definition.Availability == ReinforcementAvailability.SniperOnly;
        activeColor = isStasis ? stasisColor : gravityColor;

        ConfigureTriggerRadius();
        ConfigureVisualRadius();
        ApplyVisualColor();
        SetTriggerEnabled(true);
        PlayVisualSequence(isStasis);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!configured || other == null)
        {
            return;
        }

        IPlayerOwnedAlly playerOwnedAlly = other.GetComponentInParent<IPlayerOwnedAlly>();
        if (playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly)
        {
            return;
        }

        EnemyBaseAI enemyAI = other.GetComponentInParent<EnemyBaseAI>();
        if (!IsEligible(enemyAI))
        {
            return;
        }

        if (trackedEnemies.TryGetValue(enemyAI, out int overlapCount))
        {
            trackedEnemies[enemyAI] = overlapCount + 1;
            return;
        }

        if (!enemyAI.SetExternalMoveSpeedMultiplier(this, movementMultiplier))
        {
            return;
        }

        trackedEnemies.Add(enemyAI, 1);

        if (enemyAI.Health != null)
        {
            enemyAI.Health.Died += HandleTrackedEnemyDied;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        EnemyBaseAI enemyAI = other.GetComponentInParent<EnemyBaseAI>();
        if (enemyAI == null || !trackedEnemies.TryGetValue(enemyAI, out int overlapCount))
        {
            return;
        }

        if (overlapCount > 1)
        {
            trackedEnemies[enemyAI] = overlapCount - 1;
            return;
        }

        RemoveTrackedEnemy(enemyAI, true);
    }

    private static bool IsEligible(EnemyBaseAI enemyAI)
    {
        return enemyAI != null &&
               enemyAI.gameObject.activeInHierarchy &&
               enemyAI.Health != null &&
               !enemyAI.Health.IsDead &&
               enemyAI.CanReceiveExternalMovementControl &&
               !enemyAI.IsInsideActiveShopNeutralZone;
    }

    private void HandleTrackedEnemyDied(EnemyHealth enemyHealth)
    {
        EnemyBaseAI enemyAI = enemyHealth != null
            ? enemyHealth.GetComponentInParent<EnemyBaseAI>()
            : null;

        if (enemyAI != null)
        {
            RemoveTrackedEnemy(enemyAI, true);
        }
    }

    private void RemoveTrackedEnemy(EnemyBaseAI enemyAI, bool clearModifier)
    {
        if (enemyAI == null || !trackedEnemies.Remove(enemyAI))
        {
            return;
        }

        if (enemyAI.Health != null)
        {
            enemyAI.Health.Died -= HandleTrackedEnemyDied;
        }

        if (clearModifier)
        {
            enemyAI.ClearExternalMoveSpeedMultiplier(this);
        }
    }

    private void ClearTrackedEnemies()
    {
        foreach (KeyValuePair<EnemyBaseAI, int> tracked in trackedEnemies)
        {
            EnemyBaseAI enemyAI = tracked.Key;

            if (enemyAI == null)
            {
                continue;
            }

            if (enemyAI.Health != null)
            {
                enemyAI.Health.Died -= HandleTrackedEnemyDied;
            }

            enemyAI.ClearExternalMoveSpeedMultiplier(this);
        }

        trackedEnemies.Clear();
    }

    private void ResolveReferences()
    {
        if (effectTrigger == null)
        {
            effectTrigger = GetComponent<CircleCollider2D>();
        }

        if (effectTrigger != null)
        {
            effectTrigger.isTrigger = true;
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }
    }

    private void CaptureAuthoredVisualState()
    {
        visualAuthoredScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        coreAuthoredScale = coreRoot != null ? coreRoot.localScale : Vector3.one;
        radiusAuthoredScale = radiusRingRoot != null ? radiusRingRoot.localScale : Vector3.one;
        radiusActiveScale = radiusAuthoredScale;
    }

    private void ConfigureTriggerRadius()
    {
        if (effectTrigger == null)
        {
            return;
        }

        Vector3 lossyScale = transform.lossyScale;
        float worldScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), 0.0001f);
        effectTrigger.radius = fieldRadius / worldScale;
    }

    private void ConfigureVisualRadius()
    {
        if (radiusRingRoot == null || radiusRingRenderer == null || radiusRingRenderer.sprite == null)
        {
            radiusActiveScale = radiusAuthoredScale;
            return;
        }

        Transform parent = radiusRingRoot.parent;
        Vector3 parentLossyScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentWorldScale = Mathf.Max(
            Mathf.Abs(parentLossyScale.x),
            Mathf.Abs(parentLossyScale.y),
            0.0001f
        );
        Vector3 spriteSize = radiusRingRenderer.sprite.bounds.size;
        float spriteDiameter = Mathf.Max(spriteSize.x, spriteSize.y, 0.0001f);
        float localScale = fieldRadius * 2f / (spriteDiameter * parentWorldScale);
        radiusActiveScale = new Vector3(localScale, localScale, radiusAuthoredScale.z);
        radiusRingRoot.localScale = radiusActiveScale;
    }

    private void ApplyVisualColor()
    {
        if (coreRenderer != null)
        {
            coreRenderer.color = activeColor;
        }

        if (radiusRingRenderer != null)
        {
            Color ringColor = activeColor;
            ringColor.a = 0.28f;
            radiusRingRenderer.color = ringColor;
        }
    }

    private void PlayVisualSequence(bool isStasis)
    {
        ResetVisualState();
        ApplyVisualColor();

        if (coreRenderer != null)
        {
            coreRenderer.color = WithAlpha(coreRenderer.color, 0f);
        }

        if (radiusRingRenderer != null)
        {
            radiusRingRenderer.color = WithAlpha(radiusRingRenderer.color, 0f);
        }

        if (visualRoot != null)
        {
            visualRoot.localScale = visualAuthoredScale * 0.35f;
        }

        visualSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        if (visualRoot != null)
        {
            visualSequence.Append(
                visualRoot
                    .DOScale(visualAuthoredScale, Mathf.Max(0.01f, spawnDuration))
                    .SetEase(isStasis ? Ease.OutExpo : Ease.OutBack)
            );
        }

        if (coreRenderer != null)
        {
            visualSequence.Join(coreRenderer.DOFade(activeColor.a, Mathf.Max(0.01f, spawnDuration)));
        }

        if (radiusRingRenderer != null)
        {
            visualSequence.Join(radiusRingRenderer.DOFade(0.28f, Mathf.Max(0.01f, spawnDuration)));
        }

        float collapseDelay = Mathf.Max(
            0f,
            lifetime - Mathf.Max(0.01f, spawnDuration) - Mathf.Max(0.01f, expiryDuration)
        );
        visualSequence.AppendInterval(collapseDelay);

        if (visualRoot != null)
        {
            visualSequence.Append(
                visualRoot
                    .DOScale(visualAuthoredScale * 0.35f, Mathf.Max(0.01f, expiryDuration))
                    .SetEase(Ease.InBack)
            );
        }

        if (coreRenderer != null)
        {
            visualSequence.Join(coreRenderer.DOFade(0f, Mathf.Max(0.01f, expiryDuration)));
        }

        if (radiusRingRenderer != null)
        {
            visualSequence.Join(radiusRingRenderer.DOFade(0f, Mathf.Max(0.01f, expiryDuration)));
        }

        if (radiusRingRoot != null)
        {
            radiusRingRoot.localScale = radiusActiveScale * (isStasis ? 1.02f : 1.08f);
            pulseTween = radiusRingRoot
                .DOScale(radiusActiveScale * (isStasis ? 0.94f : 0.88f), Mathf.Max(0.05f, pulseDuration))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void StopVisualTweens()
    {
        visualSequence?.Kill();
        pulseTween?.Kill();
        visualSequence = null;
        pulseTween = null;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void ResetVisualState()
    {
        if (visualRoot != null)
        {
            visualRoot.localScale = visualAuthoredScale;
        }

        if (coreRoot != null)
        {
            coreRoot.localScale = coreAuthoredScale;
        }

        if (radiusRingRoot != null)
        {
            radiusRingRoot.localScale = radiusAuthoredScale;
        }

        ApplyVisualColor();
    }

    private void SetTriggerEnabled(bool value)
    {
        if (effectTrigger != null)
        {
            effectTrigger.enabled = value;
        }
    }

    private void OnDrawGizmosSelected()
    {
        float radius = fieldRadius > 0f
            ? fieldRadius
            : effectTrigger != null
                ? effectTrigger.radius
                : 1f;
        Gizmos.color = new Color(0.35f, 0.55f, 1f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
