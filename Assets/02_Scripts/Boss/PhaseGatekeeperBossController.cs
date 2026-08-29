using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class PhaseGatekeeperBossController : MonoBehaviour
{
    public enum EncounterState
    {
        Dormant,
        Intro,
        Stealth,
        Decloaking,
        Charging,
        Firing,
        Exposed,
        DeadOrCleanup
    }

    private const int LasersPerCycle = 3;
    private const int MaximumLaserSegments = 4;
    private const int MaximumReflections = 3;
    private const int MaximumRaycastHits = 32;
    private const int MaximumRepositionOverlaps = 16;
    private const float ReflectionRayOffset = 0.04f;

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Collider2D damageCollider;
    [SerializeField] private Collider2D proximityTrigger;
    [SerializeField] private Transform laserOrigin;
    [SerializeField] private BossLaserHazard laserHazardPrefab;
    [SerializeField] private Material laserMaterial;

    [Header("Intro")]
    [SerializeField, Min(0.1f)] private float decloakDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float combatChargeDuration = 0.7f;
    [SerializeField] private Color stealthColor = new Color(0.25f, 0.75f, 1f, 0.08f);
    [SerializeField] private Color revealedColor = Color.white;
    [SerializeField] private Color chargingColor = new Color(0.45f, 0.9f, 1f, 1f);

    [Header("Reflected Laser")]
    [SerializeField, Range(0, MaximumReflections)] private int maxReflections = 3;
    [SerializeField] private LayerMask laserBlockingMask;
    [SerializeField, Min(0.1f)] private float laserChargeDuration = 1f;
    [SerializeField, Range(0.1f, 0.5f)] private float laserLockDuration = 0.25f;
    [SerializeField, Range(1f, 90f)] private float preLockAimTurnRate = 32f;
    [SerializeField, Range(0f, 20f)] private float attackAimBiasDegrees = 6f;
    [SerializeField, Min(0.05f)] private float laserFireDuration = 0.8f;
    [SerializeField, Min(0f)] private float laserRecoveryDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float laserRange = 48f;
    [SerializeField, Min(0.05f)] private float initialLaserWidth = 0.8f;
    [SerializeField, Min(0f)] private float laserDamage = 4f;
    [SerializeField, Min(0.05f)] private float laserDamageInterval = 0.5f;
    [SerializeField] private Color telegraphColor = new Color(0.25f, 0.9f, 1f, 0.55f);
    [SerializeField] private Color lockedTelegraphColor = new Color(0.65f, 0.95f, 1f, 0.95f);
    [SerializeField] private Color activeLaserColor = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private string laserSortingLayerName = "Default";
    [SerializeField] private int laserSortingOrder = 24;

    [Header("Stealth Reposition")]
    [SerializeField, Min(0.05f)] private float cloakDuration = 0.3f;
    [SerializeField, Min(0f)] private float hiddenRepositionHold = 0.15f;
    [SerializeField, Range(0.5f, 0.8f)] private float decloakPreparationDuration = 0.65f;
    [SerializeField, Range(4f, 20f)] private float minimumPlayerRepositionDistance = 10f;
    [SerializeField, Min(1f)] private float mapEdgePadding = 4f;
    [SerializeField, Min(0.25f)] private float repositionClearanceRadius = 1.75f;
    [SerializeField, Range(1, 24)] private int repositionCandidateAttempts = 12;
    [SerializeField] private LayerMask repositionBlockingMask;

    [Header("Exposure Cycle")]
    [SerializeField, Min(0.25f)] private float exposedDuration = 5f;
    [SerializeField] private Color exposedColor = new Color(1f, 0.75f, 0.2f, 1f);

    [Header("Development Visualization")]
    [SerializeField] private bool drawReflectionChainGizmos = true;
    [SerializeField] private bool drawReflectorNormalGizmos = true;

    private readonly Vector2[] segmentStarts = new Vector2[MaximumLaserSegments];
    private readonly Vector2[] segmentEnds = new Vector2[MaximumLaserSegments];
    private readonly float[] segmentWidths = new float[MaximumLaserSegments];
    private readonly PhaseReflectorPlate[] segmentReflectors =
        new PhaseReflectorPlate[MaximumLaserSegments];
    private readonly LineRenderer[] telegraphLines = new LineRenderer[MaximumLaserSegments];
    private readonly BossLaserHazard[] activeLaserHazards =
        new BossLaserHazard[MaximumLaserSegments];
    private readonly RaycastHit2D[] laserRaycastHits = new RaycastHit2D[MaximumRaycastHits];
    private readonly Collider2D[] repositionOverlapResults =
        new Collider2D[MaximumRepositionOverlaps];

    private PhaseReflectorPlate[] reflectorPlates;
    private Bounds encounterBounds;
    private Vector3 bodyBaseScale;
    private PlayerHealth playerHealth;
    private PlayerController2D playerController;
    private GungeonStyleCamera2D gameplayCamera;
    private ExpeditionHUD expeditionHud;
    private RunManager observedRunManager;
    private ContactFilter2D laserContactFilter;
    private ContactFilter2D repositionContactFilter;
    private Coroutine encounterRoutine;
    private EncounterState state = EncounterState.Dormant;
    private int segmentCount;
    private int reflectionCount;
    private int attackSequenceIndex;
    private int lasersCompletedInCycle;
    private int repositionSequenceIndex;
    private bool encounterConfigured;
    private bool introStarted;
    private bool cleanupComplete;
    private bool introLocksHeld;

    public EncounterState State => state;
    public bool IsExposed => state == EncounterState.Exposed;
    public bool RejectsIncomingDamage => state != EncounterState.Exposed || cleanupComplete;
    public int ReflectorPlateCount => reflectorPlates != null ? reflectorPlates.Length : 0;
    public int LasersCompletedInCycle => lasersCompletedInCycle;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        damageCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (laserOrigin == null)
        {
            laserOrigin = transform;
        }

        bodyBaseScale = bodyRenderer != null
            ? bodyRenderer.transform.localScale
            : Vector3.one;
        ConfigurePhysicsFilters();
        EnsureTelegraphLines();
        ApplyDormantState();
    }

    private void OnEnable()
    {
        cleanupComplete = false;
        state = EncounterState.Dormant;
        attackSequenceIndex = 0;
        lasersCompletedInCycle = 0;
        repositionSequenceIndex = 0;
        introStarted = false;
        playerHealth = null;
        playerController = null;
        ApplyDormantState();

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = true;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += HandleBossDied;
        }

        SubscribeRunEnd();
    }

    private void OnDisable()
    {
        CleanupEncounter(true);
    }

    private void OnDestroy()
    {
        CleanupEncounter(true);
    }

    private void OnValidate()
    {
        maxReflections = Mathf.Clamp(maxReflections, 0, MaximumReflections);
        laserLockDuration = Mathf.Clamp(laserLockDuration, 0.1f, 0.5f);
        repositionCandidateAttempts = Mathf.Clamp(repositionCandidateAttempts, 1, 24);

        if (Application.isPlaying)
        {
            ConfigurePhysicsFilters();
        }
    }

    public void ConfigureEncounter(
        Bounds mapBounds,
        Vector2 bossAnchor,
        PhaseReflectorPlate[] plates)
    {
        encounterBounds = mapBounds;
        reflectorPlates = plates;
        encounterConfigured = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (introStarted || cleanupComplete || other == null)
        {
            return;
        }

        PlayerHealth candidate = other.GetComponentInParent<PlayerHealth>();

        if (candidate == null || candidate.IsDead || !IsRegion3EncounterActive())
        {
            return;
        }

        BeginEncounter(candidate);
    }

    private void BeginEncounter(PlayerHealth candidate)
    {
        if (introStarted || !encounterConfigured || candidate == null)
        {
            return;
        }

        introStarted = true;
        playerHealth = candidate;
        playerController = candidate.GetComponent<PlayerController2D>();
        playerHealth.Died += HandlePlayerDied;

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = false;
        }

        encounterRoutine = StartCoroutine(EncounterRoutine());
    }

    private IEnumerator EncounterRoutine()
    {
        state = EncounterState.Intro;
        SetDamageWindow(false);
        AcquireIntroLocks();
        GameAudioLoopController.EnterBossIntroMusic();
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);

        EventTitleDirector titleDirector = EventTitleDirector.Instance;
        if (titleDirector != null)
        {
            titleDirector.ShowBossEncounter(
                CampaignProgressionCatalog.GetBossDisplayName(CampaignBossId.PhaseGatekeeper),
                CampaignProgressionCatalog.GetBossSubtitle(CampaignBossId.PhaseGatekeeper)
            );
        }

        yield return FadeBodyColor(stealthColor, revealedColor, decloakDuration);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position);
        yield return PulseChargingPresentation(combatChargeDuration);

        while (titleDirector != null && titleDirector.IsPlaying && !ShouldStopCombat())
        {
            yield return null;
        }

        if (ShouldStopCombat())
        {
            CleanupEncounter(true);
            yield break;
        }

        ReleaseIntroLocks(false);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.BossBattle);
        }

        BossHealthBarUI.Instance?.ShowBossAnimated(
            enemyHealth,
            CampaignProgressionCatalog.GetBossDisplayName(CampaignBossId.PhaseGatekeeper)
        );

        while (!ShouldStopCombat())
        {
            yield return RunReflectedLaserAttack();

            if (ShouldStopCombat())
            {
                encounterRoutine = null;
                CleanupEncounter(true);
                yield break;
            }

            lasersCompletedInCycle++;
            attackSequenceIndex++;

            if (lasersCompletedInCycle >= LasersPerCycle)
            {
                yield return RunExposedWindow();

                if (ShouldStopCombat())
                {
                    encounterRoutine = null;
                    CleanupEncounter(true);
                    yield break;
                }

                lasersCompletedInCycle = 0;
            }

            yield return RunStealthReposition();
        }

        encounterRoutine = null;
        CleanupEncounter(true);
    }

    private IEnumerator RunReflectedLaserAttack()
    {
        state = EncounterState.Charging;
        SetDamageWindow(false);
        ClearLaserPresentation();

        Vector2 origin = ResolveLaserOrigin();
        float aimAngle = ResolveBiasedPlayerAimAngle(origin);
        BuildLaserChain(AngleToDirection(aimAngle));
        ShowTelegraphChain(false);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position);

        float timer = 0f;
        float previewDuration = Mathf.Max(0.1f, laserChargeDuration);
        while (timer < previewDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            origin = ResolveLaserOrigin();
            float targetAngle = ResolveBiasedPlayerAimAngle(origin);
            aimAngle = Mathf.MoveTowardsAngle(
                aimAngle,
                targetAngle,
                preLockAimTurnRate * Time.deltaTime
            );
            BuildLaserChain(AngleToDirection(aimAngle));
            ShowTelegraphChain(false);

            float pulse = 0.78f + Mathf.Sin(timer * 16f) * 0.22f;
            SetTelegraphAlpha(telegraphColor.a * pulse);
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        ShowTelegraphChain(true);
        PulseLockedReflectors();
        timer = 0f;
        float lockDuration = Mathf.Max(0.1f, laserLockDuration);
        while (timer < lockDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            SetTelegraphAlpha(lockedTelegraphColor.a);
            ApplyChargingBodyPulse(previewDuration + timer);
            yield return null;
        }

        if (ShouldStopCombat())
        {
            ClearLaserPresentation();
            yield break;
        }

        HideTelegraphChain();
        state = EncounterState.Firing;
        SpawnLaserHazards();
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, transform.position);

        timer = 0f;
        while (timer < laserFireDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        DeactivateLaserHazards();
        RestoreRevealedPresentation();

        if (ShouldStopCombat())
        {
            yield break;
        }

        timer = 0f;
        while (timer < laserRecoveryDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator RunExposedWindow()
    {
        ClearLaserPresentation();
        state = EncounterState.Exposed;
        RestoreRevealedPresentation();
        SetDamageWindow(true);
        AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);

        float timer = 0f;
        while (timer < exposedDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(timer * 7f) * 0.06f;

            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(revealedColor, exposedColor, 0.72f);
                bodyRenderer.transform.localScale = bodyBaseScale * pulse;
            }

            yield return null;
        }

        RestoreRevealedPresentation();
        SetDamageWindow(false);
    }

    private IEnumerator RunStealthReposition()
    {
        ClearLaserPresentation();
        SetDamageWindow(false);
        state = EncounterState.Stealth;

        Color currentColor = bodyRenderer != null ? bodyRenderer.color : revealedColor;
        yield return FadeBodyColor(currentColor, stealthColor, cloakDuration);

        if (ShouldStopCombat())
        {
            yield break;
        }

        if (TryResolveRepositionTarget(out Vector2 target))
        {
            transform.position = target;
            Physics2D.SyncTransforms();
        }

        float timer = 0f;
        while (timer < hiddenRepositionHold && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (ShouldStopCombat())
        {
            yield break;
        }

        state = EncounterState.Decloaking;
        AudioManager.PlayAt(SoundEventIds.BossSpawn, transform.position);
        yield return FadeBodyColor(
            stealthColor,
            revealedColor,
            Mathf.Max(0.5f, decloakPreparationDuration)
        );
    }

    private void BuildLaserChain(Vector2 initialDirection)
    {
        segmentCount = 0;
        reflectionCount = 0;

        for (int i = 0; i < MaximumLaserSegments; i++)
        {
            segmentReflectors[i] = null;
        }

        Vector2 segmentOrigin = ResolveLaserOrigin();
        Vector2 castOrigin = segmentOrigin;
        Vector2 direction = initialDirection.sqrMagnitude > 0.0001f
            ? initialDirection.normalized
            : Vector2.down;
        float width = Mathf.Max(0.05f, initialLaserWidth);
        float remainingDistance = Mathf.Max(0.1f, laserRange);
        int reflectionLimit = Mathf.Clamp(maxReflections, 0, MaximumReflections);

        while (segmentCount < MaximumLaserSegments && remainingDistance > 0.05f)
        {
            float maximumDistance = ResolveDistanceToBounds(
                castOrigin,
                direction,
                remainingDistance
            );
            Vector2 segmentEnd = segmentOrigin + direction * maximumDistance;
            PhaseReflectorPlate hitReflector = null;
            Vector2 hitPoint = segmentEnd;

            if (TryFindNearestLaserHit(
                castOrigin,
                direction,
                maximumDistance,
                out RaycastHit2D nearestHit))
            {
                hitPoint = nearestHit.point;
                hitReflector = ResolveReflector(nearestHit.collider);
            }

            segmentStarts[segmentCount] = segmentOrigin;
            segmentEnds[segmentCount] = hitPoint;
            segmentWidths[segmentCount] = width;

            bool canReflect = hitReflector != null && reflectionCount < reflectionLimit;
            segmentReflectors[segmentCount] = canReflect ? hitReflector : null;
            float traveledDistance = Vector2.Distance(segmentOrigin, hitPoint);
            segmentCount++;
            remainingDistance = Mathf.Max(0f, remainingDistance - traveledDistance);

            if (!canReflect || segmentCount >= MaximumLaserSegments)
            {
                break;
            }

            direction = hitReflector.Reflect(direction);
            segmentOrigin = hitPoint;
            castOrigin = hitPoint + direction * ReflectionRayOffset;
            remainingDistance = Mathf.Max(0f, remainingDistance - ReflectionRayOffset);
            reflectionCount++;
            width *= 0.5f;
        }
    }

    private bool TryFindNearestLaserHit(
        Vector2 origin,
        Vector2 direction,
        float maximumDistance,
        out RaycastHit2D nearestHit)
    {
        nearestHit = default;
        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            laserContactFilter,
            laserRaycastHits,
            maximumDistance
        );
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = laserRaycastHits[i].collider;
            if (!IsValidLaserBlockingCollider(collider) ||
                laserRaycastHits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = laserRaycastHits[i].distance;
            nearestHit = laserRaycastHits[i];
            found = true;
        }

        return found;
    }

    private bool IsValidLaserBlockingCollider(Collider2D collider)
    {
        if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        return playerHealth == null || !collider.transform.IsChildOf(playerHealth.transform);
    }

    private PhaseReflectorPlate ResolveReflector(Collider2D collider)
    {
        if (collider == null || reflectorPlates == null)
        {
            return null;
        }

        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate != null && plate.OwnsCollider(collider))
            {
                return plate;
            }
        }

        return null;
    }

    private float ResolveBiasedPlayerAimAngle(Vector2 origin)
    {
        Vector2 direction = Vector2.down;
        if (playerHealth != null)
        {
            Vector2 toPlayer = (Vector2)playerHealth.transform.position - origin;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                direction = toPlayer.normalized;
            }
        }

        float bias;
        switch (attackSequenceIndex % LasersPerCycle)
        {
            case 0:
                bias = -attackAimBiasDegrees;
                break;
            case 1:
                bias = attackAimBiasDegrees;
                break;
            default:
                bias = 0f;
                break;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + bias;
    }

    private float ResolveDistanceToBounds(
        Vector2 origin,
        Vector2 direction,
        float distanceLimit)
    {
        direction.Normalize();
        float distance = Mathf.Max(0.1f, distanceLimit);

        if (direction.x > 0.0001f)
        {
            distance = Mathf.Min(distance, (encounterBounds.max.x - origin.x) / direction.x);
        }
        else if (direction.x < -0.0001f)
        {
            distance = Mathf.Min(distance, (encounterBounds.min.x - origin.x) / direction.x);
        }

        if (direction.y > 0.0001f)
        {
            distance = Mathf.Min(distance, (encounterBounds.max.y - origin.y) / direction.y);
        }
        else if (direction.y < -0.0001f)
        {
            distance = Mathf.Min(distance, (encounterBounds.min.y - origin.y) / direction.y);
        }

        return Mathf.Max(0.1f, distance);
    }

    private Vector2 ResolveLaserOrigin()
    {
        return laserOrigin != null ? laserOrigin.position : transform.position;
    }

    private static Vector2 AngleToDirection(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void ShowTelegraphChain(bool locked)
    {
        Color color = locked ? lockedTelegraphColor : telegraphColor;
        float widthScale = locked ? 0.72f : 0.55f;

        for (int i = 0; i < telegraphLines.Length; i++)
        {
            LineRenderer line = telegraphLines[i];
            bool visible = i < segmentCount;
            line.enabled = visible;

            if (!visible)
            {
                continue;
            }

            line.SetPosition(0, segmentStarts[i]);
            line.SetPosition(1, segmentEnds[i]);
            line.startWidth = segmentWidths[i] * widthScale;
            line.endWidth = segmentWidths[i] * widthScale;
            line.startColor = color;
            line.endColor = color;
        }
    }

    private void SetTelegraphAlpha(float alpha)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            LineRenderer line = telegraphLines[i];
            Color color = line.startColor;
            color.a = Mathf.Clamp01(alpha);
            line.startColor = color;
            line.endColor = color;
        }
    }

    private void PulseLockedReflectors()
    {
        for (int i = 0; i < segmentCount; i++)
        {
            segmentReflectors[i]?.PlayLaserHitPulse();
        }
    }

    private void HideTelegraphChain()
    {
        for (int i = 0; i < telegraphLines.Length; i++)
        {
            telegraphLines[i].enabled = false;
        }
    }

    private void SpawnLaserHazards()
    {
        DeactivateLaserHazards();

        if (laserHazardPrefab == null)
        {
            Debug.LogWarning("Phase Gatekeeper laser hazard prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject instance = PoolManager.Instance != null
                ? PoolManager.Instance.Get(
                    laserHazardPrefab.gameObject,
                    (segmentStarts[i] + segmentEnds[i]) * 0.5f,
                    Quaternion.identity
                )
                : Instantiate(
                    laserHazardPrefab.gameObject,
                    (segmentStarts[i] + segmentEnds[i]) * 0.5f,
                    Quaternion.identity
                );

            if (instance == null || !instance.TryGetComponent(out BossLaserHazard hazard))
            {
                continue;
            }

            activeLaserHazards[i] = hazard;
            hazard.InitializeBetween(
                segmentStarts[i],
                segmentEnds[i],
                segmentWidths[i],
                laserFireDuration,
                laserDamage,
                laserDamageInterval,
                laserMaterial,
                activeLaserColor,
                laserSortingLayerName,
                laserSortingOrder,
                0.12f
            );
        }
    }

    private void DeactivateLaserHazards()
    {
        for (int i = 0; i < activeLaserHazards.Length; i++)
        {
            BossLaserHazard hazard = activeLaserHazards[i];
            activeLaserHazards[i] = null;

            if (hazard != null && hazard.gameObject.activeSelf)
            {
                hazard.Deactivate();
            }
        }
    }

    private void ClearLaserPresentation()
    {
        HideTelegraphChain();
        DeactivateLaserHazards();
    }

    private void EnsureTelegraphLines()
    {
        for (int i = 0; i < telegraphLines.Length; i++)
        {
            GameObject lineObject = new GameObject($"ReflectedLaserTelegraph_{i:00}");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.sortingLayerName = laserSortingLayerName;
            line.sortingOrder = laserSortingOrder - 1;
            line.sharedMaterial = laserMaterial;
            telegraphLines[i] = line;
        }
    }

    private void ConfigurePhysicsFilters()
    {
        int laserMask = laserBlockingMask.value != 0
            ? laserBlockingMask.value
            : Physics2D.AllLayers;
        laserContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        laserContactFilter.SetLayerMask(laserMask);

        int repositionMask = repositionBlockingMask.value != 0
            ? repositionBlockingMask.value
            : Physics2D.AllLayers;
        repositionContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        repositionContactFilter.SetLayerMask(repositionMask);
    }

    private bool TryResolveRepositionTarget(out Vector2 target)
    {
        float minX = encounterBounds.min.x + mapEdgePadding;
        float maxX = encounterBounds.max.x - mapEdgePadding;
        float minY = encounterBounds.min.y + mapEdgePadding;
        float maxY = encounterBounds.max.y - mapEdgePadding;

        if (minX >= maxX || minY >= maxY)
        {
            target = transform.position;
            return false;
        }

        Vector2 playerPosition = playerHealth != null
            ? playerHealth.transform.position
            : encounterBounds.center;
        float minimumDistanceSqr = minimumPlayerRepositionDistance *
                                   minimumPlayerRepositionDistance;
        int attempts = Mathf.Clamp(repositionCandidateAttempts, 1, 24);

        for (int i = 0; i < attempts; i++)
        {
            int sample = repositionSequenceIndex * attempts + i + 1;
            float xRatio = Mathf.Repeat(sample * 0.6180339f, 1f);
            float yRatio = Mathf.Repeat(sample * 0.4142136f + 0.2718281f, 1f);
            Vector2 candidate = new Vector2(
                Mathf.Lerp(minX, maxX, xRatio),
                Mathf.Lerp(minY, maxY, yRatio)
            );

            if ((candidate - playerPosition).sqrMagnitude < minimumDistanceSqr ||
                !IsRepositionLocationClear(candidate))
            {
                continue;
            }

            repositionSequenceIndex++;
            target = candidate;
            return true;
        }

        repositionSequenceIndex++;
        target = transform.position;
        return false;
    }

    private bool IsRepositionLocationClear(Vector2 position)
    {
        int count = Physics2D.OverlapCircle(
            position,
            repositionClearanceRadius,
            repositionContactFilter,
            repositionOverlapResults
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = repositionOverlapResults[i];
            if (collider != null &&
                !collider.isTrigger &&
                !collider.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator FadeBodyColor(Color from, Color to, float duration)
    {
        if (bodyRenderer == null)
        {
            float waitTimer = 0f;
            while (waitTimer < duration && !ShouldStopCombat())
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            yield break;
        }

        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (timer < safeDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            bodyRenderer.color = Color.Lerp(from, to, Mathf.Clamp01(timer / safeDuration));
            yield return null;
        }

        if (!ShouldStopCombat())
        {
            bodyRenderer.color = to;
        }
    }

    private IEnumerator PulseChargingPresentation(float duration)
    {
        float timer = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);

        while (timer < safeDuration && !ShouldStopCombat())
        {
            timer += Time.deltaTime;
            ApplyChargingBodyPulse(timer);
            yield return null;
        }

        RestoreRevealedPresentation();
    }

    private void ApplyChargingBodyPulse(float timer)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        float pulse = 0.35f + Mathf.PingPong(timer * 3.5f, 0.65f);
        bodyRenderer.color = Color.Lerp(revealedColor, chargingColor, pulse);
    }

    private void RestoreRevealedPresentation()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.transform.localScale = bodyBaseScale;
        bodyRenderer.color = revealedColor;
    }

    private void AcquireIntroLocks()
    {
        gameplayCamera = FindFirstObjectByType<GungeonStyleCamera2D>();
        expeditionHud = FindFirstObjectByType<ExpeditionHUD>();

        playerController?.SetExternalControlLocked(this, true);
        playerHealth?.AddInvincibleTime(decloakDuration + combatChargeDuration + 0.5f);
        expeditionHud?.SetCinematicMode(this, true);

        if (gameplayCamera != null)
        {
            gameplayCamera.SetCinematicInputOffsetLocked(true);
            gameplayCamera.SetCinematicFocus(transform.position, false);
        }

        introLocksHeld = true;
    }

    private void ReleaseIntroLocks(bool immediateCameraReset)
    {
        if (!introLocksHeld)
        {
            return;
        }

        playerController?.SetExternalControlLocked(this, false);
        expeditionHud?.ReleaseCinematicMode(this);

        if (gameplayCamera != null)
        {
            gameplayCamera.SetCinematicInputOffsetLocked(false);
            gameplayCamera.ClearCinematicFocus(immediateCameraReset);
        }

        introLocksHeld = false;
    }

    private void ApplyDormantState()
    {
        ClearLaserPresentation();
        SetDamageWindow(false);

        if (bodyRenderer != null)
        {
            bodyRenderer.transform.localScale = bodyBaseScale;
            bodyRenderer.color = stealthColor;
        }
    }

    private void SetDamageWindow(bool enabled)
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = enabled;
        }
    }

    private bool ShouldStopCombat()
    {
        return cleanupComplete ||
               enemyHealth == null ||
               enemyHealth.IsDead ||
               playerHealth == null ||
               playerHealth.IsDead ||
               RunManager.Instance == null ||
               !RunManager.Instance.HasActiveRun ||
               RunManager.Instance.IsCompletingRun;
    }

    private bool IsRegion3EncounterActive()
    {
        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               !RunManager.Instance.IsCompletingRun &&
               RunManager.Instance.CurrentRun.ExpeditionDepth == ExpeditionDepth.DeepZone2 &&
               CampaignProgressionCatalog.GetBossId(ExpeditionDepth.DeepZone2) ==
               CampaignBossId.PhaseGatekeeper;
    }

    private void HandleBossDied(EnemyHealth _)
    {
        CleanupEncounter(false);
    }

    private void HandlePlayerDied()
    {
        CleanupEncounter(true);
    }

    private void SubscribeRunEnd()
    {
        observedRunManager = RunManager.Instance;

        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnsubscribeRunEnd()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        CleanupEncounter(true);
    }

    private void CleanupEncounter(bool resetCameraImmediately)
    {
        if (cleanupComplete)
        {
            return;
        }

        cleanupComplete = true;
        state = EncounterState.DeadOrCleanup;

        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died -= HandleBossDied;
        }

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
        }

        UnsubscribeRunEnd();
        ReleaseIntroLocks(resetCameraImmediately);
        GameAudioLoopController.CancelBossIntroMusic();
        ClearLaserPresentation();
        SetDamageWindow(false);
        BossHealthBarUI.Instance?.Hide();

        if (proximityTrigger != null)
        {
            proximityTrigger.enabled = false;
        }

        RestoreRevealedPresentation();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Region 3 Boss/Begin Encounter")]
    private void DebugBeginEncounter()
    {
        EnsureDebugPlayer();
        if (playerHealth != null)
        {
            BeginEncounter(playerHealth);
        }
    }

    [ContextMenu("Region 3 Boss/Force Stealth")]
    private void DebugForceStealth()
    {
        StopDebugRoutine();
        ClearLaserPresentation();
        state = EncounterState.Stealth;
        SetDamageWindow(false);
        if (bodyRenderer != null)
        {
            bodyRenderer.color = stealthColor;
        }
    }

    [ContextMenu("Region 3 Boss/Force Reposition")]
    private void DebugForceReposition()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunStealthReposition());
    }

    [ContextMenu("Region 3 Boss/Force Decloak And Charge")]
    private void DebugForceDecloakAndCharge()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(DebugDecloakAndChargeRoutine());
    }

    [ContextMenu("Region 3 Boss/Preview Next Reflection Chain")]
    private void DebugPreviewNextReflectionChain()
    {
        EnsureDebugPlayer();
        BuildLaserChain(AngleToDirection(ResolveBiasedPlayerAimAngle(ResolveLaserOrigin())));
        ShowTelegraphChain(true);
        PulseLockedReflectors();
    }

    [ContextMenu("Region 3 Boss/Fire Reflected Laser")]
    private void DebugFireReflectedLaser()
    {
        DebugPreviewNextReflectionChain();
        HideTelegraphChain();
        state = EncounterState.Firing;
        SetDamageWindow(false);
        SpawnLaserHazards();
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 1")]
    private void DebugForceLaserCountOne()
    {
        lasersCompletedInCycle = 1;
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 2")]
    private void DebugForceLaserCountTwo()
    {
        lasersCompletedInCycle = 2;
    }

    [ContextMenu("Region 3 Boss/Force Laser Count 3")]
    private void DebugForceLaserCountThree()
    {
        lasersCompletedInCycle = 3;
    }

    [ContextMenu("Region 3 Boss/Force Exposed")]
    private void DebugForceExposed()
    {
        EnsureDebugPlayer();
        StartDebugRoutine(RunExposedWindow());
    }

    [ContextMenu("Region 3 Boss/End Exposed")]
    private void DebugEndExposed()
    {
        StopDebugRoutine();
        RestoreRevealedPresentation();
        state = EncounterState.Stealth;
        SetDamageWindow(false);
    }

    [ContextMenu("Region 3 Boss/Toggle Reflection Chain Gizmos")]
    private void DebugToggleReflectionChainGizmos()
    {
        drawReflectionChainGizmos = !drawReflectionChainGizmos;
    }

    [ContextMenu("Region 3 Boss/Toggle Reflector Normal Gizmos")]
    private void DebugToggleReflectorNormalGizmos()
    {
        drawReflectorNormalGizmos = !drawReflectorNormalGizmos;
    }

    [ContextMenu("Region 3 Boss/Log State And Reflection Chain")]
    private void DebugLogState()
    {
        string widths = segmentCount > 0
            ? $"{segmentWidths[0]:0.###}, {segmentWidths[1]:0.###}, " +
              $"{segmentWidths[2]:0.###}, {segmentWidths[3]:0.###}"
            : "none";
        Debug.Log(
            $"Phase Gatekeeper state={state}, cycleLasers={lasersCompletedInCycle}/3, " +
            $"totalLasers={attackSequenceIndex}, segments={segmentCount}, " +
            $"reflections={reflectionCount}, widths=[{widths}], " +
            $"reflectors={reflectorPlates?.Length ?? 0}",
            this
        );
    }

    [ContextMenu("Region 3 Boss/Cleanup Encounter")]
    private void DebugCleanupEncounter()
    {
        CleanupEncounter(true);
    }

    private IEnumerator DebugDecloakAndChargeRoutine()
    {
        state = EncounterState.Decloaking;
        yield return FadeBodyColor(stealthColor, revealedColor, decloakPreparationDuration);
        yield return RunReflectedLaserAttack();
        encounterRoutine = null;
    }

    private void EnsureDebugPlayer()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerController = playerHealth != null
                ? playerHealth.GetComponent<PlayerController2D>()
                : null;
        }
    }

    private void StartDebugRoutine(IEnumerator routine)
    {
        StopDebugRoutine();
        cleanupComplete = false;
        encounterRoutine = StartCoroutine(routine);
    }

    private void StopDebugRoutine()
    {
        if (encounterRoutine != null)
        {
            StopCoroutine(encounterRoutine);
            encounterRoutine = null;
        }
    }

    private void OnDrawGizmos()
    {
        if (drawReflectionChainGizmos)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                Gizmos.color = Color.Lerp(Color.cyan, Color.blue, i / 3f);
                Gizmos.DrawLine(segmentStarts[i], segmentEnds[i]);
            }
        }

        if (!drawReflectorNormalGizmos || reflectorPlates == null)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        for (int i = 0; i < reflectorPlates.Length; i++)
        {
            PhaseReflectorPlate plate = reflectorPlates[i];
            if (plate == null)
            {
                continue;
            }

            Vector3 origin = plate.transform.position;
            Gizmos.DrawLine(origin, origin + (Vector3)plate.SurfaceNormal * 1.5f);
        }
    }
#endif
}
