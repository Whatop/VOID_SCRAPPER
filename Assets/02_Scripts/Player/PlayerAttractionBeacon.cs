using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAttractionBeacon : MonoBehaviour, IPlayerOwnedAlly
{
    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform radiusPulseRoot;
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField] private SpriteRenderer radiusPulseRenderer;
    [SerializeField] private Color defensiveColor = new Color(0.45f, 1f, 1f, 1f);
    [SerializeField] private Color offensiveColor = new Color(1f, 0.35f, 0.12f, 1f);
    [SerializeField] private float spawnTweenDuration = 0.18f;
    [SerializeField] private float pulseDuration = 0.55f;
    [SerializeField] private float expiryFeedbackDuration = 0.16f;

    [Header("Attraction")]
    [Min(0.05f)]
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly Collider2D[] enemyBuffer = new Collider2D[96];
    private readonly List<EnemyBaseAI> trackedEnemies = new List<EnemyBaseAI>(32);
    private readonly HashSet<int> trackedEnemyIds = new HashSet<int>();

    private GameObject playerOwner;
    private ReinforcementDefinition definition;
    private LayerMask enemyLayer;
    private float attractionRadius;
    private float attractionDuration;
    private float nextRefreshAt;
    private float expiryFeedbackAt;
    private bool configured;
    private bool expiryFeedbackStarted;
    private Vector3 visualRestScale = Vector3.one;
    private Vector3 radiusPulseRestScale = Vector3.one;
    private Color activeColor = Color.white;
    private Tween spawnTween;
    private Sequence pulseTween;
    private Tween expiryTween;

    public bool IsPlayerOwnedAlly => configured && playerOwner != null;
    public float AttractionRadius => attractionRadius;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualRestScale = visualRoot.localScale;
        radiusPulseRestScale = radiusPulseRoot != null
            ? radiusPulseRoot.localScale
            : Vector3.one;
    }

    private void OnEnable()
    {
        configured = false;
        playerOwner = null;
        definition = null;
        trackedEnemies.Clear();
        trackedEnemyIds.Clear();
        StopVisualTweens();
        ResetVisualState();
    }

    private void OnDisable()
    {
        ClearOwnedAttractions();
        configured = false;
        playerOwner = null;
        definition = null;
        StopVisualTweens();
        ResetVisualState();
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        float now = Time.time;

        if (now >= nextRefreshAt)
        {
            nextRefreshAt = now + Mathf.Max(0.05f, refreshInterval);
            AcquireNewEnemies();
        }

        if (!expiryFeedbackStarted && now >= expiryFeedbackAt)
        {
            PlayExpiryFeedback();
        }
    }

    public void Configure(
        GameObject owner,
        ReinforcementDefinition beaconDefinition,
        float radius,
        float duration,
        float lifetime,
        LayerMask targetLayer)
    {
        ClearOwnedAttractions();

        if (owner == null || beaconDefinition == null || radius <= 0f || targetLayer.value == 0)
        {
            Debug.LogWarning("유인 비콘의 소유자, 정의, 반경 또는 적 레이어가 유효하지 않습니다.", this);
            configured = false;
            return;
        }

        playerOwner = owner;
        definition = beaconDefinition;
        attractionRadius = Mathf.Max(0.1f, radius);
        attractionDuration = Mathf.Max(0.1f, duration > 0f ? duration : lifetime);
        enemyLayer = targetLayer;
        trackedEnemies.Clear();
        trackedEnemyIds.Clear();
        configured = true;
        expiryFeedbackStarted = false;
        nextRefreshAt = Time.time;
        expiryFeedbackAt = Time.time + Mathf.Max(0.01f, lifetime - expiryFeedbackDuration);

        activeColor = definition.Availability == ReinforcementAvailability.ShotgunOnly
            ? offensiveColor
            : defensiveColor;

        ApplyVisualColor();
        PlaySpawnFeedback();
        AcquireNewEnemies();
    }

    private void AcquireNewEnemies()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            attractionRadius,
            enemyBuffer,
            enemyLayer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = enemyBuffer[i];

            if (hit == null)
            {
                continue;
            }

            IPlayerOwnedAlly playerOwnedAlly = hit.GetComponentInParent<IPlayerOwnedAlly>();
            if (playerOwnedAlly != null && playerOwnedAlly.IsPlayerOwnedAlly)
            {
                continue;
            }

            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            EnemyBaseAI enemyAI = enemyHealth != null
                ? enemyHealth.GetComponent<EnemyBaseAI>()
                : null;

            if (!IsEligible(enemyHealth, enemyAI))
            {
                continue;
            }

            int instanceId = enemyAI.GetInstanceID();
            if (trackedEnemyIds.Contains(instanceId))
            {
                continue;
            }

            if (!enemyAI.ApplyTemporaryAttraction(
                    this,
                    transform.position,
                    attractionDuration))
            {
                continue;
            }

            trackedEnemyIds.Add(instanceId);
            trackedEnemies.Add(enemyAI);
        }
    }

    private static bool IsEligible(EnemyHealth enemyHealth, EnemyBaseAI enemyAI)
    {
        return enemyHealth != null &&
               !enemyHealth.IsDead &&
               enemyAI != null &&
               enemyAI.gameObject.activeInHierarchy &&
               enemyAI.IsRadarTauntable &&
               !enemyAI.IsShopSecurityUnit &&
               !enemyAI.IsInsideActiveShopNeutralZone;
    }

    private void ClearOwnedAttractions()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            EnemyBaseAI enemyAI = trackedEnemies[i];

            if (enemyAI != null)
            {
                enemyAI.ClearTemporaryAttraction(this);
            }
        }

        trackedEnemies.Clear();
        trackedEnemyIds.Clear();
    }

    private void ApplyVisualColor()
    {
        if (coreRenderer != null)
        {
            coreRenderer.color = activeColor;
        }

        if (radiusPulseRenderer != null)
        {
            Color pulseColor = activeColor;
            pulseColor.a = 0.24f;
            radiusPulseRenderer.color = pulseColor;
        }
    }

    private void PlaySpawnFeedback()
    {
        StopVisualTweens();

        if (visualRoot != null)
        {
            visualRoot.localScale = visualRestScale * 0.55f;
            spawnTween = visualRoot
                .DOScale(visualRestScale, Mathf.Max(0.01f, spawnTweenDuration))
                .SetEase(Ease.OutBack)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        if (radiusPulseRoot != null)
        {
            float radiusScale = Mathf.Clamp(attractionRadius * 0.32f, 1f, 2.2f);
            radiusPulseRestScale = Vector3.one * radiusScale;
            radiusPulseRoot.localScale = radiusPulseRestScale;

            pulseTween = DOTween.Sequence()
                .Append(radiusPulseRoot.DOScale(radiusPulseRestScale * 1.08f, Mathf.Max(0.05f, pulseDuration)))
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void PlayExpiryFeedback()
    {
        expiryFeedbackStarted = true;
        pulseTween?.Kill();
        pulseTween = null;

        if (visualRoot != null)
        {
            expiryTween = visualRoot
                .DOScale(visualRestScale * 0.7f, Mathf.Max(0.01f, expiryFeedbackDuration))
                .SetEase(Ease.InBack)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        if (coreRenderer != null)
        {
            coreRenderer.DOFade(0f, Mathf.Max(0.01f, expiryFeedbackDuration))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        if (radiusPulseRenderer != null)
        {
            radiusPulseRenderer.DOFade(0f, Mathf.Max(0.01f, expiryFeedbackDuration))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void StopVisualTweens()
    {
        spawnTween?.Kill();
        pulseTween?.Kill();
        expiryTween?.Kill();
        spawnTween = null;
        pulseTween = null;
        expiryTween = null;
    }

    private void ResetVisualState()
    {
        expiryFeedbackStarted = false;

        if (visualRoot != null)
        {
            visualRoot.localScale = visualRestScale;
        }

        if (radiusPulseRoot != null)
        {
            radiusPulseRoot.localScale = radiusPulseRestScale;
        }

        ApplyVisualColor();
    }
}
