using UnityEngine;

// One scheduler and one physics owner. Campaign/death/rewards remain BossDummyController-owned.
[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth), typeof(Rigidbody2D))]
public sealed class RaiderSalvageCarrierBossController : MonoBehaviour
{
    public enum CarrierStage { Stopped, Idle, SecondaryWarning, SecondaryFire, PullWarning, Collection, CargoReady, DischargeWarning, Discharge, Recovery, Reposition, OverloadTransition }
    [SerializeField] private Transform visualRoot, intake, dischargePort;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer[] cargoIndicators;
    private SpriteRenderer pullRenderer;
    [SerializeField] private Sprite idleSprite, salvageSprite, overloadSprite;
    [SerializeField] private CarrierCombatSalvage salvagePrefab;
    [SerializeField] private ProjectileDefinition projectileDefinition, dischargeDefinition;
    [SerializeField] private PhaseCombatVfx pullPrefab, muzzlePrefab, sparksPrefab, criticalPrefab;
    [SerializeField] private float maxHp = 125, escalationRatio = .5f;
    [SerializeField] private float pullWarning = 1f, escalatedPullWarning = .8f, collectionTimeout = 3.8f;
    [SerializeField] private float pullSpeed = 1.3f, escalatedPullSpeed = 1.6f, salvageSpeed = 1.45f;
    [SerializeField] private float laneLength = 6f, laneHalfWidth = 1.1f;
    [SerializeField] private int salvageCount = 5, escalatedSalvageCount = 6, cargoThreshold = 3;
    [SerializeField] private float dischargeWarning = .8f, dischargeCadence = .24f;
    [SerializeField] private float recovery = 2.4f, overloadRecovery = 3.2f, failedRecovery = 1.6f, transitionDuration = .65f;
    [SerializeField] private float movementSpeed = .9f, repositionDuration = 1.6f;
    private EnemyHealth health;
    private Rigidbody2D body;
    private PlayerHealth playerHealth;
    private PlayerController2D player;
    private Rigidbody2D playerBody;
    private ExpeditionHUD hud;
    private GungeonStyleCamera2D cameraOwner;
    private RunManager runManager;
    private MeteorObstacle[] meteors;
    private readonly CarrierCombatSalvage[] salvage = new CarrierCombatSalvage[6];
    private readonly PhaseCombatVfx[] views = new PhaseCombatVfx[4];
    private readonly float[] starts = new float[4];
    private Vector2 center, half = new Vector2(8.4f, 8.4f), laneDirection = Vector2.down, committedTarget, moveTarget;
    private bool configured, cameraHeld, pendingEscalation;
    private float remaining, shotTimer;
    private int sequenceShots, dischargeCount;
    public static RaiderSalvageCarrierBossController ActiveEncounter { get; private set; }
    public CarrierStage Stage { get; private set; }
    public bool IsCombatActive { get; private set; }
    public bool IsPhase2 { get; private set; }
    public bool IsPullActive => IsCombatActive && !pendingEscalation && Stage == CarrierStage.Collection && views[0] != null && views[0].IsVisible;
    public int Cargo { get; private set; }
    public int Cycle { get; private set; }
    public int ShotsFired { get; private set; }
    public int Discharges { get; private set; }
    public int DestroyedSalvage { get; private set; }
    public int EscalationCount { get; private set; }
    public Vector2 PullVelocity { get; private set; }
    public Vector2 CommittedTarget => committedTarget;
    public Bounds ArenaBounds => new Bounds(center, new Vector3(half.x * 2, half.y * 2, 20));
    public int LiveSalvageCount { get { int n = 0; for (int i = 0; i < salvage.Length; i++) if (salvage[i] != null) n++; return n; } }

    private void Awake() { Resolve(); health.SetMaxHp(maxHp, true); }
    private void OnEnable()
    {
        Resolve(); health.SetMaxHp(maxHp, true);
        health.HealthChanged += HealthChanged; health.Died += Died; health.Damaged += Damaged;
        runManager = RunManager.Instance; if (runManager != null) runManager.RunEnded += RunEnded;
    }
    private void OnDisable()
    {
        StopCombat();
        if (health != null) { health.HealthChanged -= HealthChanged; health.Died -= Died; health.Damaged -= Damaged; }
        if (runManager != null) runManager.RunEnded -= RunEnded;
    }
    private void Resolve()
    {
        health ??= GetComponent<EnemyHealth>(); body ??= GetComponent<Rigidbody2D>();
        hud ??= FindFirstObjectByType<ExpeditionHUD>(FindObjectsInactive.Include);
        cameraOwner ??= GungeonStyleCamera2D.Instance;
    }
    public void ConfigureEncounter(Vector2 arenaCenter, Vector2 arenaHalf, GameObject target)
    {
        center = arenaCenter; half = arenaHalf; configured = true;
        playerHealth = target != null ? target.GetComponent<PlayerHealth>() : FindFirstObjectByType<PlayerHealth>();
        player = playerHealth != null ? playerHealth.GetComponent<PlayerController2D>() : null;
        playerBody = playerHealth != null ? playerHealth.GetComponent<Rigidbody2D>() : null;
    }
    public void PrepareBattleCameraProfile()
    {
        Resolve();
        if (!cameraHeld && cameraOwner != null) cameraHeld = cameraOwner.AcquireGameplayFramingProfile(this, new Vector2(0,1.2f), .35f, 1f, true);
    }
    public void ReleaseBattleCameraProfile(bool immediate)
    { if (cameraHeld && cameraOwner != null) cameraOwner.ReleaseGameplayFramingProfile(this, immediate); cameraHeld = false; }
    public void BeginCombat()
    {
        if (IsCombatActive || health == null || health.IsDead || (RunManager.Instance != null && RunManager.Instance.IsCompletingRun)) return;
        if (!configured) ConfigureEncounter(transform.position, half, null);
        IsCombatActive = true; IsPhase2 = false; pendingEscalation = health.HpRatio <= escalationRatio;
        Cargo = Cycle = ShotsFired = Discharges = DestroyedSalvage = EscalationCount = 0;
        ActiveEncounter = this; PrepareBattleCameraProfile(); hud?.SetRegionBossPresentation(this, true);
        if (playerHealth != null) playerHealth.Died += PlayerDied;
        meteors = FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);
        Bounds reserved = ArenaBounds; reserved.Expand(2);
        for (int i = 0; i < meteors.Length; i++) meteors[i].PauseForSectorEncounter(reserved);
        AcquireViews(); Enter(CarrierStage.Idle, .65f);
    }
    public void StopCombat()
    {
        IsCombatActive = false; pendingEscalation = false; Stage = CarrierStage.Stopped; Cargo = 0; UpdateViews();
        if (playerHealth != null) playerHealth.Died -= PlayerDied;
        ClearPull(); ReleaseSalvage(); Bullet.ReleaseAllActiveFromSource(transform); ReleaseViews();
        if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
        if (ActiveEncounter == this) ActiveEncounter = null;
        if (meteors != null) for (int i = 0; i < meteors.Length; i++) if (meteors[i] != null) meteors[i].ResumeAfterSectorEncounter();
        meteors = null; hud?.SetRegionBossPresentation(this, false); ReleaseBattleCameraProfile(true);
    }
    private void Update()
    {
        if (!IsCombatActive || GameplayPauseManager.IsPaused) return;
        if (health.IsDead || (playerHealth != null && playerHealth.IsDead) || (runManager != null && runManager.IsCompletingRun)) { StopCombat(); return; }
        AdvanceScheduler(Time.deltaTime); UpdateViews();
    }
    private void AdvanceScheduler(float dt)
    {
        if (pendingEscalation)
        {
            pendingEscalation = false; IsPhase2 = true; EscalationCount++;
            ClearPull(); ReleaseSalvage(); Cargo = 0; Bullet.ReleaseAllActiveFromSource(transform);
            Enter(CarrierStage.OverloadTransition, transitionDuration); starts[2] = Time.time; return;
        }
        remaining -= dt;
        if (Stage == CarrierStage.SecondaryFire || Stage == CarrierStage.Discharge)
        {
            shotTimer -= dt;
            if (shotTimer <= 0 && sequenceShots < (Stage == CarrierStage.Discharge ? dischargeCount : 2))
            {
                Fire(Stage == CarrierStage.Discharge, sequenceShots++);
                shotTimer = Stage == CarrierStage.Discharge ? dischargeCadence : .22f;
            }
            if (sequenceShots >= (Stage == CarrierStage.Discharge ? dischargeCount : 2) && shotTimer <= 0)
            {
                if (Stage == CarrierStage.SecondaryFire) BeginCollection();
                else { Cargo = 0; Enter(CarrierStage.Recovery, IsPhase2 ? overloadRecovery : recovery); }
            }
            return;
        }
        if (Stage == CarrierStage.Collection && (remaining <= 0 || LiveSalvageCount == 0))
        {
            ClearPull(); ReleaseSalvage();
            if (Cargo >= cargoThreshold) { Discharges++; Enter(CarrierStage.CargoReady, .35f); }
            else { Cargo = 0; Enter(CarrierStage.Recovery, failedRecovery); }
            return;
        }
        if (remaining > 0) return;
        switch (Stage)
        {
            case CarrierStage.Idle: case CarrierStage.Reposition: case CarrierStage.OverloadTransition:
                Cycle++; CommitAim(); Enter(CarrierStage.SecondaryWarning, .45f); break;
            case CarrierStage.SecondaryWarning: Enter(CarrierStage.SecondaryFire, 0); break;
            case CarrierStage.PullWarning: Enter(CarrierStage.Collection, collectionTimeout); break;
            case CarrierStage.CargoReady:
                CommitAim(); dischargeCount = Mathf.Clamp(Cargo, 3, 5); Enter(CarrierStage.DischargeWarning, dischargeWarning); break;
            case CarrierStage.DischargeWarning: Enter(CarrierStage.Discharge, 0); break;
            case CarrierStage.Recovery: BeginReposition(); break;
        }
    }
    private void BeginCollection()
    {
        Cargo = 0; CommitAim(); laneDirection = (committedTarget - (Vector2)intake.position).normalized;
        if (laneDirection.sqrMagnitude < .01f) laneDirection = Vector2.down;
        // Hull is held throughout collection; only this visual child owns facing.
        Face(laneDirection); laneDirection = visualRoot.up;
        SpawnSalvage(); Enter(CarrierStage.PullWarning, IsPhase2 ? escalatedPullWarning : pullWarning);
    }
    private void SpawnSalvage()
    {
        ReleaseSalvage(); if (salvagePrefab == null || PoolManager.Instance == null) return;
        int count = Mathf.Clamp(IsPhase2 ? escalatedSalvageCount : salvageCount, 0, salvage.Length);
        Vector2 origin = intake.position, lateral = new Vector2(-laneDirection.y, laneDirection.x);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = ClampToArena(origin + laneDirection * (3.4f + (i / 2) * .8f) + lateral * ((i % 2 == 0 ? -1 : 1) * .75f));
            var item = PoolManager.Instance.Get(salvagePrefab.gameObject, p, Quaternion.Euler(0,0,i*31)).GetComponent<CarrierCombatSalvage>();
            salvage[i] = item; item.Initialize(this);
        }
    }
    public void RetireSalvage(CarrierCombatSalvage item, bool collected)
    {
        for (int i = 0; i < salvage.Length; i++)
        {
            if (salvage[i] != item || item == null || item.Owner != this) continue;
            salvage[i] = null;
            if (collected && !item.IsDead && Stage == CarrierStage.Collection) Cargo++;
            else if (item.IsDead) DestroyedSalvage++;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(item.gameObject); else item.gameObject.SetActive(false);
            return;
        }
    }
    private void ReleaseSalvage()
    {
        for (int i = 0; i < salvage.Length; i++)
        {
            var item = salvage[i]; salvage[i] = null; if (item == null) continue;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(item.gameObject); else item.gameObject.SetActive(false);
        }
    }
    private void FixedUpdate()
    {
        if (!IsCombatActive || GameplayPauseManager.IsPaused) return;
        if (Stage == CarrierStage.Reposition && !health.IsKnockbackActive)
        {
            Vector2 next = Vector2.MoveTowards(body.position, moveTarget, movementSpeed * Time.fixedDeltaTime);
            if (playerHealth == null || ClearPath(body.position, next, playerHealth.transform.position)) body.MovePosition(next);
        }
        if (!IsPullActive) { ClearPull(); return; }
        Vector2 origin = intake.position;
        PullVelocity = player != null ? CalculatePull(origin, laneDirection, player.transform.position, laneLength, laneHalfWidth, IsPhase2 ? escalatedPullSpeed : pullSpeed) : Vector2.zero;
        player?.SetExternalPushVelocity(this, PullVelocity);
        for (int i = 0; i < salvage.Length; i++)
        {
            var item = salvage[i]; if (item == null) continue;
            if (Vector2.Distance(item.transform.position, origin) < .35f) RetireSalvage(item, true);
            else item.Advance(origin, salvageSpeed * (IsPhase2 ? 1.2f : 1f) * Time.fixedDeltaTime);
        }
    }
    public static Vector2 CalculatePull(Vector2 origin, Vector2 direction, Vector2 target, float length, float width, float speed)
    {
        Vector2 delta = target - origin; float along = Vector2.Dot(delta, direction);
        if (along < 1.1f || along > length || Mathf.Abs(delta.x * direction.y - delta.y * direction.x) > width) return Vector2.zero;
        return -delta.normalized * Mathf.Max(0, speed);
    }
    private void ClearPull() { player?.ClearExternalPush(this); PullVelocity = Vector2.zero; }
    private void CommitAim()
    {
        Vector2 target = playerHealth != null ? (Vector2)playerHealth.transform.position : body.position + Vector2.down*4;
        committedTarget = EnemyAttackController.PredictTargetPosition(body.position, target, playerBody != null ? playerBody.linearVelocity : Vector2.zero, projectileDefinition != null ? projectileDefinition.Speed : 0, .18f);
        Face((committedTarget - body.position).normalized);
    }
    private void Face(Vector2 direction)
    { if (visualRoot != null && direction.sqrMagnitude > .001f) visualRoot.rotation = Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90); }
    private void Fire(bool heavy, int index)
    {
        var definition = heavy ? dischargeDefinition : projectileDefinition;
        if (PoolManager.Instance == null || definition == null || definition.ProjectilePrefab == null) return;
        Vector2 origin = dischargePort.position;
        Vector2 forward = (committedTarget-origin).normalized, lateral = new Vector2(-forward.y,forward.x);
        // Sequential committed lanes, not a simultaneous fan or homing burst.
        Vector2 destination = committedTarget + (heavy ? lateral * ((index%3)-1)*.65f : Vector2.zero);
        Vector2 direction = (destination-origin).normalized;
        var shot = PoolManager.Instance.Get(definition.ProjectilePrefab, origin, Quaternion.identity);
        shot.GetComponent<Bullet>().Initialize(direction, ProjectileOwner.Enemy, definition, projectileSource:gameObject);
        ShotsFired++; starts[1] = Time.time;
        if (views[1] != null) { views[1].transform.SetPositionAndRotation(origin, Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg)); views[1].Sample(0); }
    }
    private void BeginReposition()
    {
        float side = (Cycle&1)==0 ? -1 : 1;
        moveTarget = ClampToArena(body.position + new Vector2(side*1.4f,.35f));
        if (playerHealth != null && !ClearPath(body.position, moveTarget, playerHealth.transform.position)) moveTarget = body.position;
        Enter(CarrierStage.Reposition, repositionDuration);
    }
    private static bool ClearPath(Vector2 from, Vector2 to, Vector2 playerPosition)
    { Vector2 d=to-from; float t=d.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(playerPosition-from,d)/d.sqrMagnitude):0; return (from+d*t-playerPosition).sqrMagnitude>=4; }
    private Vector2 ClampToArena(Vector2 p) => new Vector2(Mathf.Clamp(p.x,center.x-half.x+1.5f,center.x+half.x-1.5f),Mathf.Clamp(p.y,center.y-half.y+1.5f,center.y+half.y-1.5f));
    private void Enter(CarrierStage next, float duration)
    {
        // The controller owns this one transition cue; pooled views only sample visuals.
        if (next == CarrierStage.OverloadTransition && Stage != next)
            AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);
        if (next != CarrierStage.Collection) ClearPull();
        if (Stage == CarrierStage.Reposition && body != null && !health.IsKnockbackActive) body.linearVelocity=Vector2.zero;
        Stage=next; remaining=duration; sequenceShots=0; shotTimer=0;
        if (bodyRenderer != null) bodyRenderer.sprite = next == CarrierStage.OverloadTransition || (next == CarrierStage.Recovery && IsPhase2) ? overloadSprite :
            next == CarrierStage.PullWarning || next == CarrierStage.Collection || next == CarrierStage.CargoReady || next == CarrierStage.DischargeWarning ? salvageSprite : IsPhase2 ? overloadSprite : idleSprite;
        UpdateViews(); // warning and force share the same state boundary, including the final visible frame.
    }
    private void AcquireViews()
    {
        if (PoolManager.Instance == null) return;
        for(int i=0;i<views.Length;i++)
        {
            var prefab=i==0?pullPrefab:i==1?muzzlePrefab:i==2?sparksPrefab:criticalPrefab;
            if(prefab==null)continue;
            views[i]=PoolManager.Instance.Get(prefab.gameObject,transform.position,Quaternion.identity).GetComponent<PhaseCombatVfx>(); starts[i]=Time.time-10; views[i].Clear();
            if(i==0)pullRenderer=views[i].GetComponent<SpriteRenderer>();
        }
    }
    private void UpdateViews()
    {
        if(cargoIndicators!=null)for(int i=0;i<cargoIndicators.Length;i++)
        {
            if(cargoIndicators[i]==null)continue;
            cargoIndicators[i].enabled=IsCombatActive&&i<Cargo;
            float pulse=Stage==CarrierStage.DischargeWarning ? .65f+.35f*Mathf.Sin(Time.time*24f) : 1f;
            cargoIndicators[i].color=new Color(1,.65f,.25f,pulse);
        }
        if(views[0]!=null)
        {
            bool warning=Stage==CarrierStage.PullWarning;
            if(warning||Stage==CarrierStage.Collection)
            {
                views[0].transform.SetPositionAndRotation(intake.position,Quaternion.Euler(0,0,Mathf.Atan2(laneDirection.y,laneDirection.x)*Mathf.Rad2Deg));
                views[0].transform.localScale=new Vector3(laneLength/3f,laneHalfWidth*2,1);
                pullRenderer.color=new Color(1,1,1,warning?.3f:.55f);
                views[0].Sample(Time.time%.54f);
            } else views[0].Clear();
        }
        for(int i=1;i<views.Length;i++)
        {
            var view=views[i];if(view==null)continue;
            if(i>=2)view.transform.SetPositionAndRotation(visualRoot.position,visualRoot.rotation);
            if(i==3){if(IsPhase2)view.Sample(Time.time%1f);else view.Clear();}
            else view.Sample(Time.time-starts[i]);
        }
    }
    private void ReleaseViews()
    {
        for(int i=0;i<views.Length;i++){var v=views[i];views[i]=null;if(v==null)continue;v.Clear();if(PoolManager.Instance!=null)PoolManager.Instance.Release(v.gameObject);else v.gameObject.SetActive(false);}
    }
    private void HealthChanged(EnemyHealth _,float current,float maximum)
    { if(IsCombatActive&&!IsPhase2&&current>0&&current/maximum<=escalationRatio){pendingEscalation=true;ClearPull();} }
    private void Damaged(EnemyHealth _) { if(IsCombatActive&&Time.time-starts[2]>.2f)starts[2]=Time.time; }
    private void Died(EnemyHealth _) => StopCombat();
    private void PlayerDied() => StopCombat();
    private void RunEnded(RunResultData _) => StopCombat();
}
