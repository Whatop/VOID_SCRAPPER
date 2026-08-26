using System.Collections;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PirateCommanderBossController : MonoBehaviour
{
    private enum CommanderPattern
    {
        SuppressiveBurst = 0,
        SpreadBarrage = 1,
        CoverBlast = 2
    }

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform blastOrigin;
    [SerializeField] private ProjectileDefinition projectileDefinition;
    [SerializeField] private RaiderCoverBlastTelegraph coverBlastTelegraphPrefab;

    [Header("Boss Tuning")]
    [SerializeField, Min(1f)] private float maxHp = 125f;
    [SerializeField, Range(0.1f, 0.9f)] private float phase2HpRatio = 0.5f;
    [SerializeField, Min(0f)] private float battleStartDelay = 0.65f;
    [SerializeField, Min(0.1f)] private float movementSpeed = 2.8f;
    [SerializeField, Min(0.01f)] private float anchorArrivalDistance = 0.08f;
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(8.4f, 8.4f);
    [SerializeField] private Vector2 upperAnchorOffset = new Vector2(0f, 4.25f);
    [SerializeField] private Vector2 leftAnchorOffset = new Vector2(-4.25f, 3.1f);
    [SerializeField] private Vector2 rightAnchorOffset = new Vector2(4.25f, 3.1f);

    [Header("Suppressive Burst")]
    [SerializeField, Min(1)] private int suppressiveBurstCountPhase1 = 3;
    [SerializeField, Min(1)] private int suppressiveBurstCountPhase2 = 4;
    [SerializeField, Range(1, 12)] private int suppressiveShotsPerBurst = 6;
    [SerializeField, Min(0.01f)] private float suppressiveShotInterval = 0.075f;
    [SerializeField, Min(0.01f)] private float suppressiveBurstInterval = 0.28f;
    [SerializeField, Range(0f, 12f)] private float suppressiveSpreadDegrees = 4f;
    [SerializeField, Min(0f)] private float suppressivePredictionTime = 0.22f;
    [SerializeField, Min(0f)] private float suppressiveDamage = 2f;
    [SerializeField, Min(0f)] private float suppressiveCooldown = 3.2f;

    [Header("Spread Barrage")]
    [SerializeField, Min(1)] private int spreadVolleyCountPhase1 = 2;
    [SerializeField, Min(1)] private int spreadVolleyCountPhase2 = 3;
    [SerializeField, Range(3, 15)] private int spreadProjectileCount = 7;
    [SerializeField, Range(10f, 140f)] private float spreadDegrees = 68f;
    [SerializeField, Range(0f, 30f)] private float spreadAlternateAngle = 6f;
    [SerializeField, Min(0.01f)] private float spreadVolleyInterval = 0.42f;
    [SerializeField, Min(0f)] private float spreadDamage = 2f;
    [SerializeField, Min(0f)] private float spreadCooldown = 4f;

    [Header("Cover Blast")]
    [SerializeField, Min(0.1f)] private float coverBlastChargePhase1 = 2.1f;
    [SerializeField, Min(0.1f)] private float coverBlastChargePhase2 = 1.75f;
    [SerializeField, Min(0f)] private float coverBlastDamage = 6f;
    [SerializeField, Min(0f)] private float coverBlastRecovery = 1.05f;
    [SerializeField] private LayerMask coverBlastOcclusionMask;
    [SerializeField] private string coverWarningText = "엄폐물 뒤로 이동하십시오.";
    [SerializeField] private Color coverChargeColor = new Color(1f, 0.24f, 0.08f, 1f);

    [Header("Phase 2 Escort")]
    [SerializeField] private GameObject pirateBasicPrefab;
    [SerializeField] private GameObject pirateShotgunPrefab;
    [SerializeField] private Vector2 escortLeftOffset = new Vector2(-5.25f, 1.8f);
    [SerializeField] private Vector2 escortRightOffset = new Vector2(5.25f, 1.8f);
    [SerializeField, Min(0.1f)] private float escortClearRadius = 1.25f;
    [SerializeField] private LayerMask escortBlockingMask;
    [SerializeField] private EnemyArrivalSpawnSettings escortArrivalSettings =
        new EnemyArrivalSpawnSettings();

    [Header("Presentation")]
    [SerializeField] private Color phase2PulseColor = new Color(1f, 0.2f, 0.08f, 1f);
    [SerializeField, Min(0.01f)] private float phase2TransitionDuration = 0.42f;

    [Header("Boss Battle Camera")]
    [SerializeField] private Vector2 battleCameraWorldOffset = new Vector2(0f, 0.8f);
    [SerializeField, Range(0.75f, 1.25f)] private float battleCameraZoomMultiplier = 1f;

    private readonly RaycastHit2D[] coverRaycastResults = new RaycastHit2D[16];
    private readonly Collider2D[] escortOverlapResults = new Collider2D[16];
    private readonly BossArenaCover[] arenaCovers = new BossArenaCover[2];
    private readonly GameObject[] spawnedEscorts = new GameObject[2];
    private readonly WaitForFixedUpdate fixedUpdateWait = new WaitForFixedUpdate();

    private Coroutine combatRoutine;
    private PlayerHealth playerHealth;
    private Rigidbody2D playerBody;
    private ExpeditionHUD expeditionHud;
    private GungeonStyleCamera2D gameplayCamera;
    private RaiderCoverBlastTelegraph coverBlastTelegraph;
    private RunManager observedRunManager;
    private Vector2 arenaCenter;
    private Vector3 visualBaseScale = Vector3.one;
    private Color visualBaseColor = Color.white;
    private SpriteRenderer visualRenderer;
    private bool encounterConfigured;
    private bool combatActive;
    private bool phase2;
    private bool phaseTransitionPending;
    private bool escortWaveSpawned;
    private bool nextCoverBlastUsesRight;
    private bool battleCameraProfileHeld;

    public bool IsCombatActive => combatActive;
    public bool IsPhase2 => phase2;

    private void Reset()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        visualRoot = transform.Find("BossVisualRoot");
        firePoint = transform.Find("FirePoint");
        blastOrigin = transform.Find("BlastOrigin");
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyHealthTuning();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyHealthTuning();
        BindHealth();
        BindRunEnd();
    }

    private void OnDisable()
    {
        StopCombat();
        RetireSpawnedEscorts();
        ReleaseBattleCameraProfile(true);
        DestroyCoverBlastTelegraph();
        UnbindHealth();
        UnbindRunEnd();
        RestoreVisualPresentation();
        ClearRuntimeReferences();
    }

    public void ConfigureEncounter(
        Vector2 resolvedArenaCenter,
        Vector2 resolvedArenaHalfExtents,
        GameObject playerObject)
    {
        arenaCenter = resolvedArenaCenter;
        arenaHalfExtents = new Vector2(
            Mathf.Max(0.1f, resolvedArenaHalfExtents.x),
            Mathf.Max(0.1f, resolvedArenaHalfExtents.y)
        );
        encounterConfigured = true;
        ResolvePlayer(playerObject);
        CacheArenaCovers();
        EnsureCoverBlastTelegraph();
        ApplyHealthTuning();
    }

    public void PrepareBattleCameraProfile()
    {
        if (battleCameraProfileHeld)
        {
            return;
        }

        ResolveReferences();
        if (gameplayCamera == null)
        {
            return;
        }

        battleCameraProfileHeld = gameplayCamera.AcquireGameplayFramingProfile(
            this,
            battleCameraWorldOffset,
            1f,
            battleCameraZoomMultiplier,
            true
        );
    }

    public void ReleaseBattleCameraProfile(bool immediate)
    {
        if (!battleCameraProfileHeld)
        {
            return;
        }

        battleCameraProfileHeld = false;
        if (gameplayCamera != null)
        {
            gameplayCamera.ReleaseGameplayFramingProfile(this, immediate);
        }
    }

    public void BeginCombat()
    {
        if (combatActive || enemyHealth == null || enemyHealth.IsDead || IsRunEnding())
        {
            return;
        }

        if (!encounterConfigured)
        {
            arenaCenter = transform.position;
            encounterConfigured = true;
        }

        if (playerHealth == null)
        {
            ResolvePlayer(null);
        }

        CacheArenaCovers();
        EnsureCoverBlastTelegraph();
        PrepareBattleCameraProfile();
        combatActive = true;
        phase2 = false;
        phaseTransitionPending = false;
        escortWaveSpawned = false;
        nextCoverBlastUsesRight = false;
        combatRoutine = StartCoroutine(CombatLoopRoutine());
    }

    public void StopCombat()
    {
        combatActive = false;
        phaseTransitionPending = false;

        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        SetCoverHighlights(false);
        if (coverBlastTelegraph != null)
        {
            coverBlastTelegraph.HideImmediate();
        }
        visualRoot?.DOKill();
        visualRenderer?.DOKill();
    }

    private IEnumerator CombatLoopRoutine()
    {
        yield return WaitGameplaySeconds(battleStartDelay, false);

        CommanderPattern[] patternOrder =
        {
            CommanderPattern.SuppressiveBurst,
            CommanderPattern.SpreadBarrage,
            CommanderPattern.CoverBlast
        };

        int patternIndex = 0;
        while (CanContinueCombat())
        {
            if (phaseTransitionPending)
            {
                yield return EnterPhase2Routine();
                continue;
            }

            CommanderPattern pattern = patternOrder[patternIndex];
            patternIndex = (patternIndex + 1) % patternOrder.Length;

            switch (pattern)
            {
                case CommanderPattern.SuppressiveBurst:
                    yield return SuppressiveBurstRoutine();
                    break;

                case CommanderPattern.SpreadBarrage:
                    yield return SpreadBarrageRoutine();
                    break;

                case CommanderPattern.CoverBlast:
                    yield return CoverBlastRoutine();
                    break;
            }

            if (!CanContinueCombat() || phaseTransitionPending)
            {
                continue;
            }

            float cooldown = ResolvePatternCooldown(pattern);
            yield return WaitGameplaySeconds(cooldown, true);
        }

        combatRoutine = null;
    }

    private IEnumerator SuppressiveBurstRoutine()
    {
        yield return MoveToAnchorRoutine(ResolveAnchor(upperAnchorOffset));

        int burstCount = phase2 ? suppressiveBurstCountPhase2 : suppressiveBurstCountPhase1;
        for (int burst = 0; burst < burstCount; burst++)
        {
            for (int shot = 0; shot < suppressiveShotsPerBurst; shot++)
            {
                if (ShouldInterruptPattern())
                {
                    yield break;
                }

                Vector2 direction = ResolvePredictedDirection(suppressivePredictionTime);
                float spreadT = suppressiveShotsPerBurst <= 1
                    ? 0f
                    : shot / (float)(suppressiveShotsPerBurst - 1);
                float angle = Mathf.Lerp(
                    -suppressiveSpreadDegrees,
                    suppressiveSpreadDegrees,
                    spreadT
                );
                SpawnProjectile(Rotate(direction, angle), suppressiveDamage);
                yield return WaitGameplaySeconds(suppressiveShotInterval, true);
            }

            if (burst + 1 < burstCount)
            {
                yield return WaitGameplaySeconds(suppressiveBurstInterval, true);
            }
        }
    }

    private IEnumerator SpreadBarrageRoutine()
    {
        Vector2 anchor = nextCoverBlastUsesRight
            ? ResolveAnchor(leftAnchorOffset)
            : ResolveAnchor(rightAnchorOffset);
        yield return MoveToAnchorRoutine(anchor);

        int volleyCount = phase2 ? spreadVolleyCountPhase2 : spreadVolleyCountPhase1;
        for (int volley = 0; volley < volleyCount; volley++)
        {
            if (ShouldInterruptPattern())
            {
                yield break;
            }

            Vector2 baseDirection = ResolveDirectionToPlayer();
            float volleyOffset = (volley & 1) == 0
                ? -spreadAlternateAngle
                : spreadAlternateAngle;
            SpawnSpread(baseDirection, volleyOffset);

            if (volley + 1 < volleyCount)
            {
                yield return WaitGameplaySeconds(spreadVolleyInterval, true);
            }
        }
    }

    private IEnumerator CoverBlastRoutine()
    {
        Vector2 anchor = nextCoverBlastUsesRight
            ? ResolveAnchor(rightAnchorOffset)
            : ResolveAnchor(leftAnchorOffset);
        nextCoverBlastUsesRight = !nextCoverBlastUsesRight;
        yield return MoveToAnchorRoutine(anchor);

        if (ShouldInterruptPattern())
        {
            yield break;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        SetCoverHighlights(true);
        if (expeditionHud != null)
        {
            expeditionHud.ShowWarning(coverWarningText);
        }

        Vector2 attackCenter = ResolveBlastOrigin();
        float attackRadius = ResolveCoverBlastRadius(attackCenter);
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, attackCenter);
        PlayChargePulse();

        float chargeTime = phase2 ? coverBlastChargePhase2 : coverBlastChargePhase1;
        if (coverBlastTelegraph != null)
        {
            coverBlastTelegraph.BeginCharge(attackCenter, attackRadius);
        }
        yield return RunCoverBlastChargeRoutine(chargeTime);

        if (ShouldInterruptPattern())
        {
            if (coverBlastTelegraph != null)
            {
                coverBlastTelegraph.HideImmediate();
            }
            SetCoverHighlights(false);
            yield break;
        }

        Vector2 finalPlayerPosition = playerHealth != null
            ? (Vector2)playerHealth.transform.position
            : attackCenter;
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, attackCenter);

        bool playerInsideBlast =
            (finalPlayerPosition - attackCenter).sqrMagnitude <= attackRadius * attackRadius;
        if (playerInsideBlast &&
            TryResolveBlockingCover(attackCenter, finalPlayerPosition, out BossArenaCover cover))
        {
            cover.PlayBlockedImpact(finalPlayerPosition);
        }
        else if (playerInsideBlast && playerHealth != null)
        {
            playerHealth.TakeDamage(
                coverBlastDamage,
                attackCenter,
                finalPlayerPosition - attackCenter
            );
        }

        if (coverBlastTelegraph != null)
        {
            coverBlastTelegraph.HideImmediate();
        }
        SetCoverHighlights(false);
        yield return WaitGameplaySeconds(coverBlastRecovery, true);
    }

    private IEnumerator RunCoverBlastChargeRoutine(float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration && CanContinueCombat())
        {
            if (phaseTransitionPending)
            {
                yield break;
            }

            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (coverBlastTelegraph != null)
            {
                coverBlastTelegraph.SetChargeProgress(elapsed / safeDuration);
            }
            RotateVisualTowardPlayer();
            yield return null;
        }

        if (coverBlastTelegraph != null)
        {
            coverBlastTelegraph.SetChargeProgress(1f);
        }
    }

    private IEnumerator EnterPhase2Routine()
    {
        phaseTransitionPending = false;
        phase2 = true;
        SetCoverHighlights(false);

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);
        PlayPhase2Pulse();
        SpawnPhase2Escorts();
        yield return WaitGameplaySeconds(phase2TransitionDuration, false);
    }

    private IEnumerator MoveToAnchorRoutine(Vector2 target)
    {
        target = ClampToArena(target);

        while (CanContinueCombat())
        {
            if (phaseTransitionPending)
            {
                yield break;
            }

            Vector2 current = body != null ? body.position : (Vector2)transform.position;
            Vector2 delta = target - current;
            if (delta.sqrMagnitude <= anchorArrivalDistance * anchorArrivalDistance)
            {
                if (body != null)
                {
                    body.MovePosition(target);
                    body.linearVelocity = Vector2.zero;
                }
                else
                {
                    transform.position = target;
                }

                yield break;
            }

            Vector2 next = Vector2.MoveTowards(current, target, movementSpeed * Time.fixedDeltaTime);
            if (body != null)
            {
                body.MovePosition(next);
            }

            RotateVisualTowardPlayer();
            yield return fixedUpdateWait;
        }
    }

    private IEnumerator WaitGameplaySeconds(float duration, bool interruptForPhase)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f && CanContinueCombat())
        {
            if (interruptForPhase && phaseTransitionPending)
            {
                yield break;
            }

            remaining -= Time.deltaTime;
            RotateVisualTowardPlayer();
            yield return null;
        }
    }

    private void SpawnSpread(Vector2 baseDirection, float angleOffset)
    {
        int count = Mathf.Max(1, spreadProjectileCount);
        float step = count <= 1 ? 0f : spreadDegrees / (count - 1);
        float start = -spreadDegrees * 0.5f + angleOffset;

        for (int i = 0; i < count; i++)
        {
            SpawnProjectile(Rotate(baseDirection, start + step * i), spreadDamage);
        }
    }

    private void SpawnProjectile(Vector2 direction, float damage)
    {
        if (projectileDefinition == null || projectileDefinition.ProjectilePrefab == null)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.down;
        }

        direction.Normalize();
        Vector2 origin = firePoint != null ? firePoint.position : (Vector2)transform.position;
        GameObject prefab = projectileDefinition.ProjectilePrefab;
        GameObject projectileObject = PoolManager.Instance != null
            ? PoolManager.Instance.Get(prefab, origin, Quaternion.identity)
            : Instantiate(prefab, origin, Quaternion.identity);

        if (projectileObject == null)
        {
            return;
        }

        Bullet bullet = projectileObject.GetComponent<Bullet>();
        if (bullet == null)
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(projectileObject);
            }
            else
            {
                Destroy(projectileObject);
            }

            return;
        }

        bullet.Initialize(
            direction,
            ProjectileOwner.Enemy,
            projectileDefinition,
            damageOverride: damage,
            projectileSource: gameObject
        );
    }

    private bool TryResolveBlockingCover(
        Vector2 origin,
        Vector2 playerPosition,
        out BossArenaCover blockingCover)
    {
        blockingCover = null;
        Vector2 delta = playerPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int mask = coverBlastOcclusionMask.value != 0
            ? coverBlastOcclusionMask.value
            : Physics2D.AllLayers;
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(mask);

        int count = Physics2D.Raycast(
            origin,
            delta / distance,
            filter,
            coverRaycastResults,
            distance
        );

        float nearestCoverDistance = float.PositiveInfinity;
        float playerHitDistance = distance + 0.01f;

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = coverRaycastResults[i].collider;
            if (collider == null || collider.isTrigger || collider.transform.IsChildOf(transform))
            {
                continue;
            }

            PlayerHealth hitPlayer = collider.GetComponentInParent<PlayerHealth>();
            if (hitPlayer != null && hitPlayer == playerHealth)
            {
                playerHitDistance = Mathf.Min(playerHitDistance, coverRaycastResults[i].distance);
                continue;
            }

            BossArenaCover cover = collider.GetComponentInParent<BossArenaCover>();
            if (IsEncounterCover(cover) &&
                coverRaycastResults[i].distance < nearestCoverDistance)
            {
                nearestCoverDistance = coverRaycastResults[i].distance;
                blockingCover = cover;
            }
        }

        return blockingCover != null && nearestCoverDistance < playerHitDistance;
    }

    private void SpawnPhase2Escorts()
    {
        if (escortWaveSpawned)
        {
            return;
        }

        escortWaveSpawned = true;
        if (TryResolveEscortPosition(escortLeftOffset, out Vector2 leftPosition))
        {
            spawnedEscorts[0] = SpawnEscort(pirateBasicPrefab, leftPosition);
        }

        if (TryResolveEscortPosition(escortRightOffset, out Vector2 rightPosition))
        {
            spawnedEscorts[1] = SpawnEscort(pirateShotgunPrefab, rightPosition);
        }
    }

    private GameObject SpawnEscort(GameObject prefab, Vector2 arrivalPosition)
    {
        if (prefab == null || IsRunEnding())
        {
            return null;
        }

        GameObject escort = PoolManager.Instance != null
            ? PoolManager.Instance.Get(prefab, arrivalPosition, Quaternion.identity)
            : Instantiate(prefab, arrivalPosition, Quaternion.identity);
        if (escort == null)
        {
            return null;
        }

        Transform playerTarget = playerHealth != null ? playerHealth.transform : null;
        EnemyBaseAI enemyAI = escort.GetComponent<EnemyBaseAI>();
        enemyAI?.SetTarget(playerTarget);

        EnemyArrivalSpawnUtility.BeginArrival(
            escort,
            arrivalPosition,
            playerTarget,
            true,
            arenaCenter,
            escortArrivalSettings
        );

        return escort;
    }

    private void RetireSpawnedEscorts()
    {
        for (int i = 0; i < spawnedEscorts.Length; i++)
        {
            GameObject escort = spawnedEscorts[i];
            spawnedEscorts[i] = null;

            if (escort == null)
            {
                continue;
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(escort);
            }
            else
            {
                escort.SetActive(false);
                Destroy(escort);
            }
        }
    }

    private bool TryResolveEscortPosition(Vector2 preferredOffset, out Vector2 position)
    {
        Vector2 preferred = ClampToArena(arenaCenter + preferredOffset);
        if (IsEscortPositionClear(preferred))
        {
            position = preferred;
            return true;
        }

        Vector2 mirrored = ClampToArena(arenaCenter + new Vector2(-preferredOffset.x, preferredOffset.y));
        if (IsEscortPositionClear(mirrored))
        {
            position = mirrored;
            return true;
        }

        Vector2 inner = ClampToArena(arenaCenter + new Vector2(preferredOffset.x * 0.6f, 0.5f));
        if (IsEscortPositionClear(inner))
        {
            position = inner;
            return true;
        }

        position = default;
        return false;
    }

    private bool IsEscortPositionClear(Vector2 position)
    {
        int mask = escortBlockingMask.value != 0
            ? escortBlockingMask.value
            : Physics2D.AllLayers;
        ContactFilter2D filter = new ContactFilter2D
        {
            useTriggers = false
        };
        filter.SetLayerMask(mask);
        int count = Physics2D.OverlapCircle(
            position,
            escortClearRadius,
            filter,
            escortOverlapResults
        );

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = escortOverlapResults[i];
            if (collider != null && !collider.isTrigger && !collider.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsEncounterCover(BossArenaCover cover)
    {
        return cover != null &&
               cover.IsValid &&
               (cover == arenaCovers[0] || cover == arenaCovers[1]);
    }

    private void CacheArenaCovers()
    {
        arenaCovers[0] = null;
        arenaCovers[1] = null;
        BossArenaCover[] covers = FindObjectsByType<BossArenaCover>(FindObjectsSortMode.None);

        for (int i = 0; i < covers.Length; i++)
        {
            BossArenaCover cover = covers[i];
            if (cover == null || !cover.IsValid)
            {
                continue;
            }

            int index = cover.Side == BossArenaCoverSide.Left ? 0 : 1;
            BossArenaCover current = arenaCovers[index];
            if (current == null ||
                Vector2.SqrMagnitude((Vector2)cover.transform.position - arenaCenter) <
                Vector2.SqrMagnitude((Vector2)current.transform.position - arenaCenter))
            {
                arenaCovers[index] = cover;
            }
        }
    }

    private void SetCoverHighlights(bool visible)
    {
        for (int i = 0; i < arenaCovers.Length; i++)
        {
            BossArenaCover cover = arenaCovers[i];
            if (cover != null)
            {
                cover.SetPatternHighlight(visible);
            }
        }
    }

    private void PlayChargePulse()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.DOKill();
        visualRoot.localScale = visualBaseScale;
        visualRoot.DOPunchScale(Vector3.one * 0.1f, 0.34f, 4, 0.3f)
            .SetLoops(3, LoopType.Restart)
            .SetLink(gameObject);

        if (visualRenderer != null)
        {
            visualRenderer.DOKill();
            visualRenderer.DOColor(coverChargeColor, 0.17f)
                .SetLoops(6, LoopType.Yoyo)
                .SetLink(gameObject);
        }
    }

    private void PlayPhase2Pulse()
    {
        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = visualBaseScale;
            visualRoot.DOPunchScale(Vector3.one * 0.16f, phase2TransitionDuration, 6, 0.45f)
                .SetLink(gameObject);
        }

        if (visualRenderer != null)
        {
            visualRenderer.DOKill();
            visualRenderer.DOColor(phase2PulseColor, phase2TransitionDuration * 0.5f)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(gameObject);
        }
    }

    private float ResolvePatternCooldown(CommanderPattern pattern)
    {
        float cooldown = pattern switch
        {
            CommanderPattern.SuppressiveBurst => suppressiveCooldown,
            CommanderPattern.SpreadBarrage => spreadCooldown,
            CommanderPattern.CoverBlast => coverBlastRecovery,
            _ => 1f
        };

        return phase2 ? cooldown * 0.88f : cooldown;
    }

    private Vector2 ResolvePredictedDirection(float predictionTime)
    {
        if (playerHealth == null)
        {
            return Vector2.down;
        }

        Vector2 predicted = playerHealth.transform.position;
        if (playerBody != null)
        {
            predicted += playerBody.linearVelocity * Mathf.Clamp(predictionTime, 0f, 0.4f);
        }

        Vector2 direction = predicted - ResolveFireOrigin();
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
    }

    private Vector2 ResolveDirectionToPlayer()
    {
        if (playerHealth == null)
        {
            return Vector2.down;
        }

        Vector2 direction = (Vector2)playerHealth.transform.position - ResolveFireOrigin();
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
    }

    private Vector2 ResolveFireOrigin()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private Vector2 ResolveBlastOrigin()
    {
        return blastOrigin != null ? blastOrigin.position : transform.position;
    }

    private float ResolveCoverBlastRadius(Vector2 attackCenter)
    {
        Vector2 bottomLeft = arenaCenter - arenaHalfExtents;
        Vector2 topRight = arenaCenter + arenaHalfExtents;
        float farthestSqr = 0f;
        farthestSqr = Mathf.Max(
            farthestSqr,
            (new Vector2(bottomLeft.x, bottomLeft.y) - attackCenter).sqrMagnitude
        );
        farthestSqr = Mathf.Max(
            farthestSqr,
            (new Vector2(bottomLeft.x, topRight.y) - attackCenter).sqrMagnitude
        );
        farthestSqr = Mathf.Max(
            farthestSqr,
            (new Vector2(topRight.x, bottomLeft.y) - attackCenter).sqrMagnitude
        );
        farthestSqr = Mathf.Max(
            farthestSqr,
            (new Vector2(topRight.x, topRight.y) - attackCenter).sqrMagnitude
        );
        return Mathf.Sqrt(farthestSqr) + 0.01f;
    }

    private Vector2 ResolveAnchor(Vector2 offset)
    {
        return ClampToArena(arenaCenter + offset);
    }

    private Vector2 ClampToArena(Vector2 position)
    {
        float padding = 1.25f;
        return new Vector2(
            Mathf.Clamp(
                position.x,
                arenaCenter.x - Mathf.Max(padding, arenaHalfExtents.x - padding),
                arenaCenter.x + Mathf.Max(padding, arenaHalfExtents.x - padding)
            ),
            Mathf.Clamp(
                position.y,
                arenaCenter.y - Mathf.Max(padding, arenaHalfExtents.y - padding),
                arenaCenter.y + Mathf.Max(padding, arenaHalfExtents.y - padding)
            )
        );
    }

    private void RotateVisualTowardPlayer()
    {
        if (visualRoot == null || playerHealth == null)
        {
            return;
        }

        Vector2 direction = (Vector2)playerHealth.transform.position - (Vector2)transform.position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        visualRoot.rotation = Quaternion.RotateTowards(
            visualRoot.rotation,
            Quaternion.Euler(0f, 0f, angle),
            420f * Time.deltaTime
        );
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos
        ).normalized;
    }

    private bool ShouldInterruptPattern()
    {
        return !CanContinueCombat() || phaseTransitionPending;
    }

    private bool CanContinueCombat()
    {
        return combatActive &&
               isActiveAndEnabled &&
               enemyHealth != null &&
               !enemyHealth.IsDead &&
               !IsRunEnding();
    }

    private static bool IsRunEnding()
    {
        return RunManager.Instance != null && RunManager.Instance.IsCompletingRun;
    }

    private void ResolveReferences()
    {
        enemyHealth ??= GetComponent<EnemyHealth>();
        body ??= GetComponent<Rigidbody2D>();
        visualRoot ??= transform.Find("BossVisualRoot");
        firePoint ??= transform.Find("FirePoint");
        blastOrigin ??= transform.Find("BlastOrigin");
        expeditionHud ??= FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        gameplayCamera ??= GungeonStyleCamera2D.Instance != null
            ? GungeonStyleCamera2D.Instance
            : FindFirstObjectByType<GungeonStyleCamera2D>(FindObjectsInactive.Include);

        if (visualRoot != null)
        {
            visualBaseScale = visualRoot.localScale;
            visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (visualRenderer != null)
            {
                visualBaseColor = visualRenderer.color;
            }
        }
    }

    private void EnsureCoverBlastTelegraph()
    {
        if (coverBlastTelegraph != null)
        {
            return;
        }

        if (coverBlastTelegraphPrefab == null)
        {
            return;
        }

        coverBlastTelegraph = Instantiate(
            coverBlastTelegraphPrefab,
            arenaCenter,
            Quaternion.identity
        );
        coverBlastTelegraph.name = "RaiderCoverBlastTelegraph";
        Vector2 attackCenter = ResolveBlastOrigin();
        coverBlastTelegraph.Configure(
            attackCenter,
            ResolveCoverBlastRadius(attackCenter)
        );
    }

    private void DestroyCoverBlastTelegraph()
    {
        if (coverBlastTelegraph == null)
        {
            return;
        }

        coverBlastTelegraph.HideImmediate();
        Destroy(coverBlastTelegraph.gameObject);
        coverBlastTelegraph = null;
    }

    private void ResolvePlayer(GameObject playerObject)
    {
        playerHealth = playerObject != null
            ? playerObject.GetComponent<PlayerHealth>()
            : FindFirstObjectByType<PlayerHealth>();
        playerBody = playerHealth != null ? playerHealth.GetComponent<Rigidbody2D>() : null;
    }

    private void ApplyHealthTuning()
    {
        enemyHealth?.SetMaxHp(maxHp, true);
    }

    private void BindHealth()
    {
        if (enemyHealth == null)
        {
            return;
        }

        enemyHealth.HealthChanged -= HandleHealthChanged;
        enemyHealth.HealthChanged += HandleHealthChanged;
        enemyHealth.Died -= HandleDied;
        enemyHealth.Died += HandleDied;
    }

    private void UnbindHealth()
    {
        if (enemyHealth == null)
        {
            return;
        }

        enemyHealth.HealthChanged -= HandleHealthChanged;
        enemyHealth.Died -= HandleDied;
    }

    private void HandleHealthChanged(EnemyHealth _, float current, float maximum)
    {
        if (!phase2 && !phaseTransitionPending && maximum > 0f && current / maximum <= phase2HpRatio)
        {
            phaseTransitionPending = true;
        }
    }

    private void HandleDied(EnemyHealth _)
    {
        StopCombat();
        RetireSpawnedEscorts();
        Bullet.ReleaseAllActiveOwnedBy(ProjectileOwner.Enemy);
        ReleaseBattleCameraProfile(false);
        DestroyCoverBlastTelegraph();
    }

    private void BindRunEnd()
    {
        RunManager manager = RunManager.Instance;
        if (observedRunManager == manager)
        {
            return;
        }

        UnbindRunEnd();
        observedRunManager = manager;
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnbindRunEnd()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        StopCombat();
        RetireSpawnedEscorts();
        Bullet.ReleaseAllActiveOwnedBy(ProjectileOwner.Enemy);
        ReleaseBattleCameraProfile(true);
        DestroyCoverBlastTelegraph();
    }

    private void RestoreVisualPresentation()
    {
        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = visualBaseScale;
        }

        if (visualRenderer != null)
        {
            visualRenderer.DOKill();
            visualRenderer.color = visualBaseColor;
        }
    }

    private void ClearRuntimeReferences()
    {
        playerHealth = null;
        playerBody = null;
        expeditionHud = null;
        gameplayCamera = null;
        arenaCovers[0] = null;
        arenaCovers[1] = null;
    }
}
