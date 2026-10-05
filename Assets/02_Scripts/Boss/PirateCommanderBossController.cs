using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed partial class PirateCommanderBossController : MonoBehaviour
{

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

    public enum AssaultStage { Stopped, Idle, AimLeft, LeftBurst, Gap, AimRight, RightBurst, Recovery, MovementTell, Reposition, CriticalTransition, HotTell, HotRecovery, ScatterTell, ScatterFire, RamAim, RamWarning, RamActive, RamPunish, ShieldRecover }
    [Header("Assault sequence")]
    [SerializeField] private Transform leftMuzzle, rightMuzzle;
    [SerializeField] private Sprite idleSprite, weaponsHotSprite, criticalSprite;
    [SerializeField] private PhaseCombatVfx heavyMuzzlePrefab, weaponsHotPrefab, damageSparksPrefab, criticalLoopPrefab;
    [SerializeField, Min(.1f)] private float burstTellDuration = .38f;
    [SerializeField, Min(.01f)] private float hotShotInterval = .06f;
    [SerializeField, Min(.01f)] private float hotBurstGap = .16f;
    [SerializeField, Min(.1f)] private float hotTellDuration = .6f;
    [SerializeField, Min(.1f)] private float repositionDuration = 1.25f;
    [SerializeField, Min(.1f)] private float movementTellDuration = .32f;
    [SerializeField, Min(.1f)] private float recoveryDuration = 1.05f;
    [SerializeField, Min(.1f)] private float postHotRecoveryDuration = 3.2f;

    private Coroutine combatRoutine;
    private PlayerHealth playerHealth;
    private Rigidbody2D playerBody;
    private ExpeditionHUD expeditionHud;
    private GungeonStyleCamera2D gameplayCamera;
    private RunManager observedRunManager;
    private Vector2 arenaCenter, moveTarget, committedTarget, facingDirection = Vector2.down;
    private SpriteRenderer visualRenderer;
    private bool encounterConfigured, combatActive, phase2, phaseTransitionPending, battleCameraProfileHeld, weaponsHot;
    private int assaultCycle, escalationCount, shotCount, lastMount, repositionIndex;
    private AssaultStage stage;
    private MeteorObstacle[] encounterMeteors;
    // Retained cleanup contract for any escorts owned before a script reload. The
    // redesigned scheduler never creates escorts or invokes legacy cover/fan attacks.
    private readonly GameObject[] spawnedEscorts = new GameObject[2];
    private readonly PhaseCombatVfx[] views = new PhaseCombatVfx[5];
    private readonly float[] viewStarts = new float[5];
    public static PirateCommanderBossController ActiveAssaultEncounter { get; private set; }
    public bool IsCombatActive => combatActive;
    public bool IsPhase2 => phase2;
    public bool IsWeaponsHot => weaponsHot;
    public AssaultStage Stage => stage;
    public int AssaultCycle => assaultCycle;
    public int EscalationCount => escalationCount;
    public int ShotsFired => shotCount;
    public int LastMount => lastMount;
    public Vector2 CommittedTarget => committedTarget;
    public Vector2 RepositionTarget => moveTarget;
    public Bounds ArenaBounds => new Bounds(arenaCenter, new Vector3(arenaHalfExtents.x * 2, arenaHalfExtents.y * 2, 20));

    private void Awake() { ResolveReferences(); enemyHealth.SetMaxHp(maxHp, true); }
    private void OnEnable()
    {
        ResolveReferences(); enemyHealth.SetMaxHp(maxHp, true);
        enemyHealth.HealthChanged += HandleHealthChanged; enemyHealth.Died += HandleDied; enemyHealth.Damaged += HandleDamaged;
        observedRunManager = RunManager.Instance;
        if (observedRunManager != null) observedRunManager.RunEnded += HandleRunEnded;
    }
    private void OnDisable()
    {
        StopCombat();
        if (enemyHealth != null) { enemyHealth.HealthChanged -= HandleHealthChanged; enemyHealth.Died -= HandleDied; enemyHealth.Damaged -= HandleDamaged; }
        if (observedRunManager != null) observedRunManager.RunEnded -= HandleRunEnded;
        observedRunManager = null;
    }
    private void ResolveReferences()
    {
        enemyHealth ??= GetComponent<EnemyHealth>(); body ??= GetComponent<Rigidbody2D>();
        visualRoot ??= transform.Find("BossVisualRoot");
        if (visualRoot != null) visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
        expeditionHud ??= FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        gameplayCamera ??= GungeonStyleCamera2D.Instance;
    }
    public void ConfigureEncounter(Vector2 resolvedArenaCenter, Vector2 resolvedArenaHalfExtents, GameObject playerObject)
    {
        arenaCenter = resolvedArenaCenter;
        arenaHalfExtents = new Vector2(Mathf.Max(.1f, resolvedArenaHalfExtents.x), Mathf.Max(.1f, resolvedArenaHalfExtents.y));
        encounterConfigured = true;
        playerHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : FindFirstObjectByType<PlayerHealth>();
        playerBody = playerHealth != null ? playerHealth.GetComponent<Rigidbody2D>() : null;
    }
    public void PrepareBattleCameraProfile()
    {
        ResolveReferences();
        if (!battleCameraProfileHeld && gameplayCamera != null)
            battleCameraProfileHeld = gameplayCamera.AcquireGameplayFramingProfile(this, battleCameraWorldOffset, .35f, battleCameraZoomMultiplier, true);
    }
    public void ReleaseBattleCameraProfile(bool immediate)
    {
        if (!battleCameraProfileHeld) return;
        battleCameraProfileHeld = false;
        if (gameplayCamera != null) gameplayCamera.ReleaseGameplayFramingProfile(this, immediate);
    }
    public void BeginCombat()
    {
        if (combatActive || enemyHealth == null || enemyHealth.IsDead || IsRunEnding()) return;
        if (!encounterConfigured) ConfigureEncounter(transform.position, arenaHalfExtents, null);
        PrepareBattleCameraProfile();
        ActiveAssaultEncounter = this; // Core publishes BossBattle immediately after this handoff.
        expeditionHud?.SetRegionBossPresentation(this, true);
        encounterMeteors = FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);
        Bounds reserved = ArenaBounds; reserved.Expand(2f);
        for (int i = 0; i < encounterMeteors.Length; i++) encounterMeteors[i].PauseForSectorEncounter(reserved);
        combatActive = true; phase2 = false; phaseTransitionPending = enemyHealth.HpRatio <= phase2HpRatio;
        assaultCycle = escalationCount = shotCount = repositionIndex = 0;
        if (playerHealth != null) playerHealth.Died += HandlePlayerDied;
        AcquireViews(); BeginShieldCombat(); SetStage(AssaultStage.Idle);
        combatRoutine = StartCoroutine(CombatLoopRoutine());
    }
    public void StopCombat()
    {
        if (playerHealth != null) playerHealth.Died -= HandlePlayerDied;
        ClearRaiderProtection();
        combatActive = false; phaseTransitionPending = false; weaponsHot = false; stage = AssaultStage.Stopped;
        if (combatRoutine != null) { StopCoroutine(combatRoutine); combatRoutine = null; }
        if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
        Bullet.ReleaseAllActiveFromSource(transform); RetireSpawnedEscorts(); ReleaseViews();
        if (ActiveAssaultEncounter == this) ActiveAssaultEncounter = null;
        if (encounterMeteors != null)
            for (int i = 0; i < encounterMeteors.Length; i++) if (encounterMeteors[i] != null) encounterMeteors[i].ResumeAfterSectorEncounter();
        encounterMeteors = null;
        expeditionHud?.SetRegionBossPresentation(this, false); ReleaseBattleCameraProfile(true);
    }
    private bool CanContinueCombat() => combatActive && isActiveAndEnabled && enemyHealth != null && !enemyHealth.IsDead && !IsRunEnding() && (playerHealth == null || !playerHealth.IsDead);
    private bool Interrupted => !CanContinueCombat();
    private static bool IsRunEnding() => RunManager.Instance != null && RunManager.Instance.IsCompletingRun;

    private IEnumerator CombatLoopRoutine()
    {
        yield return WaitGameplaySeconds(battleStartDelay);
        while (CanContinueCombat())
        {
            assaultCycle++;
            for (int slot = 0; slot < 4 && CanContinueCombat(); slot++)
            {
                // Critical is an escalation at an attack boundary, never an attack cancellation.
                if (phaseTransitionPending)
                {
                    phaseTransitionPending = false; phase2 = true; escalationCount++;
                    viewStarts[3] = Time.time; ApplyBody();
                }
                CurrentAttack = AttackForSlot(slot);
                switch (CurrentAttack)
                {
                    case RaiderAttack.TrackingBurst: yield return TrackingBurstRoutine(); break;
                    case RaiderAttack.Scatter: yield return ScatterRoutine(); break;
                    case RaiderAttack.ShieldRam: yield return ShieldRamRoutine(); break;
                }
                if (Interrupted) break;
                SetHot(false); SetStage(AssaultStage.Recovery);
                yield return WaitGameplaySeconds(.55f);
                if (slot == 1 && !Interrupted) yield return RepositionRoutine();
            }
        }
        combatRoutine = null;
    }
    private void CommitAim()
    {
        Vector2 target = playerHealth != null ? (Vector2)playerHealth.transform.position : body.position + Vector2.down;
        committedTarget = EnemyAttackController.PredictTargetPosition(body.position, target,
            playerBody != null ? playerBody.linearVelocity : Vector2.zero, projectileDefinition != null ? projectileDefinition.Speed : 0, suppressivePredictionTime);
        facingDirection = (committedTarget - body.position).normalized;
        if (facingDirection.sqrMagnitude < .001f) facingDirection = Vector2.down;
    }
    private IEnumerator RepositionRoutine()
    {
        float side = (repositionIndex++ & 1) == 0 ? -1 : 1;
        Vector2 playerPosition = playerHealth != null ? (Vector2)playerHealth.transform.position : arenaCenter;
        Vector2 toward = playerPosition - body.position;
        float distance = toward.magnitude;
        toward = distance > .01f ? toward / distance : Vector2.down;
        Vector2 lateral = new Vector2(-toward.y, toward.x) * side;
        // Commit a short dogleg: advance when distant, re-angle/retreat when close.
        // The destination never follows subsequent player movement.
        moveTarget = ClampToArena(body.position + lateral * 2.2f + toward * Mathf.Clamp(distance - 4f, -1.2f, 1.4f));
        if (!HasClearRun(body.position, moveTarget, playerPosition, 2f))
            moveTarget = ClampToArena(body.position - lateral * 2.2f - toward);
        if (!HasClearRun(body.position, moveTarget, playerPosition, 2f)) moveTarget = body.position;
        CommitAim(); SetStage(AssaultStage.MovementTell); yield return WaitGameplaySeconds(movementTellDuration);
        if (Interrupted) yield break;
        SetStage(AssaultStage.Reposition); yield return WaitGameplaySeconds(repositionDuration);
        if (!Interrupted) SetStage(AssaultStage.Recovery);
    }
    public static bool HasClearRun(Vector2 start, Vector2 end, Vector2 player, float clearance)
    {
        Vector2 delta = end - start;
        float t = delta.sqrMagnitude > .001f ? Mathf.Clamp01(Vector2.Dot(player - start, delta) / delta.sqrMagnitude) : 0;
        return (start + delta * t - player).sqrMagnitude >= clearance * clearance;
    }
    private void FixedUpdate()
    {
        if (!CanContinueCombat() || body == null || enemyHealth.IsKnockbackActive) return;
        if (stage == AssaultStage.RamActive) { StepRam(Time.fixedDeltaTime); return; }
        if (stage == AssaultStage.Reposition)
        {
            Vector2 next = Vector2.MoveTowards(body.position, moveTarget, movementSpeed * Time.fixedDeltaTime);
            if (playerHealth == null || HasClearRun(body.position, next, playerHealth.transform.position, 1.9f)) body.MovePosition(ClampToArena(next));
        }
    }
    private void Update()
    {
        if (!CanContinueCombat()) return;
        UpdateRaiderCombatFraming();
        SampleRamPresentation();
        // This is the sole facing owner. Aim states turn; committed bursts hold.
        if (visualRoot != null && stage != AssaultStage.LeftBurst && stage != AssaultStage.RightBurst && stage != AssaultStage.RamWarning && stage != AssaultStage.RamActive)
            visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation,
                Quaternion.Euler(0, 0, Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg - 90), 540f * Time.deltaTime);
        for (int i = 0; i < views.Length; i++)
        {
            var view = views[i]; if (view == null) continue;
            bool loop = i == 2 ? weaponsHot : i == 3 && phase2;
            if ((i == 2 && !weaponsHot) || (i == 3 && !phase2)) { view.Clear(); continue; }
            Transform anchor = i < 2 ? Muzzle(i) : visualRoot;
            view.transform.position = anchor.position;
            if (i >= 2) view.transform.rotation = visualRoot.rotation;
            float elapsed = Time.time - viewStarts[i];
            view.Sample(loop ? elapsed % (i == 2 ? .64f : 1f) : elapsed);
        }
    }
    private void Fire(int mount, Vector2 direction)
    {
        if (projectileDefinition == null || projectileDefinition.ProjectilePrefab == null || PoolManager.Instance == null) return;
        Transform muzzle = Muzzle(mount);
        muzzle.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        var shot = PoolManager.Instance.Get(projectileDefinition.ProjectilePrefab, muzzle.position, Quaternion.identity);
        var bullet = shot.GetComponent<Bullet>();
        if (bullet == null) { PoolManager.Instance.Release(shot); return; }
        bullet.Initialize(direction, ProjectileOwner.Enemy, projectileDefinition, damageOverride: suppressiveDamage, projectileSource: gameObject);
        lastMount = mount; shotCount++;
        if (views[mount] != null)
        {
            views[mount].transform.SetPositionAndRotation(muzzle.position, Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            viewStarts[mount] = Time.time; views[mount].Sample(0);
        }
    }
    private Transform Muzzle(int mount) => (mount == 0 ? leftMuzzle : rightMuzzle) ?? firePoint ?? transform;
    private IEnumerator WaitGameplaySeconds(float duration)
    {
        float end = Time.time + Mathf.Max(0, duration);
        while (Time.time < end && !Interrupted) yield return null;
    }
    private void SetStage(AssaultStage next)
    {
        if (stage == AssaultStage.Reposition && next != stage && body != null && !enemyHealth.IsKnockbackActive)
            body.linearVelocity = Vector2.zero;
        stage = next; ApplyBody();
    }
    private void SetHot(bool value) { weaponsHot = value; if (value) viewStarts[2] = Time.time; ApplyBody(); }
    private void ApplyBody()
    {
        if (visualRenderer == null) return;
        Sprite state = phase2 ? criticalSprite : weaponsHot ? weaponsHotSprite : idleSprite;
        if (state != null) visualRenderer.sprite = state;
    }
    private Vector2 ClampToArena(Vector2 p)
    {
        Vector2 half = new Vector2(Mathf.Max(0, arenaHalfExtents.x - 1.5f), Mathf.Max(0, arenaHalfExtents.y - 1.5f));
        return new Vector2(Mathf.Clamp(p.x, arenaCenter.x-half.x, arenaCenter.x+half.x), Mathf.Clamp(p.y, arenaCenter.y-half.y, arenaCenter.y+half.y));
    }
    private static Vector2 Rotate(Vector2 v, float angle) => Quaternion.Euler(0, 0, angle) * v;
    private void AcquireViews()
    {
        if (PoolManager.Instance == null) return;
        for (int i = 0; i < views.Length; i++)
        {
            PhaseCombatVfx prefab = i < 2 ? heavyMuzzlePrefab : i == 2 ? weaponsHotPrefab : i == 3 ? criticalLoopPrefab : damageSparksPrefab;
            if (prefab == null) continue;
            views[i] = PoolManager.Instance.Get(prefab.gameObject, transform.position, Quaternion.identity).GetComponent<PhaseCombatVfx>();
            viewStarts[i] = Time.time - 10; views[i].Clear();
        }
    }
    private void ReleaseViews()
    {
        for (int i = 0; i < views.Length; i++)
        {
            var view = views[i]; views[i] = null; if (view == null) continue;
            view.Clear(); if (PoolManager.Instance != null) PoolManager.Instance.Release(view.gameObject); else view.gameObject.SetActive(false);
        }
    }
    private void RetireSpawnedEscorts()
    {
        for (int i = 0; i < spawnedEscorts.Length; i++)
        {
            var escort = spawnedEscorts[i]; spawnedEscorts[i] = null; if (escort == null) continue;
            Bullet.ReleaseAllActiveFromSource(escort.transform);
            if (PoolManager.Instance != null) PoolManager.Instance.Release(escort); else escort.SetActive(false);
        }
    }
    private void HandleHealthChanged(EnemyHealth _, float current, float maximum)
    { if (combatActive && !phase2 && maximum > 0 && current > 0 && current / maximum <= phase2HpRatio) phaseTransitionPending = true; }
    private void HandleDamaged(EnemyHealth _) { if (combatActive && Time.time - viewStarts[4] > .2f) viewStarts[4] = Time.time; }
    private void HandlePlayerDied() => StopCombat();
    private void HandleDied(EnemyHealth _) => StopCombat();
    private void HandleRunEnded(RunResultData _) => StopCombat();
}
