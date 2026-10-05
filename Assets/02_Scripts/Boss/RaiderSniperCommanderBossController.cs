using UnityEngine;

// Region C revisit only. BossDummyController retains death, rewards and progression.
[DisallowMultipleComponent, RequireComponent(typeof(EnemyHealth),typeof(Rigidbody2D))]
public sealed class RaiderSniperCommanderBossController : MonoBehaviour
{
    public enum SniperStage { Stopped, Idle, MineDeployment, Reposition, Acquisition, CommittedCharge, RailShot, Recovery, CriticalTransition }
    [SerializeField] private Transform visualRoot, railMuzzle;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite idleSprite, lockSprite, criticalSprite;
    [SerializeField] private RaiderRailShot railPrefab;
    [SerializeField] private RaiderSniperMine minePrefab;
    [SerializeField] private PhaseCombatVfx sparksPrefab, criticalPrefab;
    [SerializeField] private ProjectileDefinition aimDefinition;
    [SerializeField] private float maxHp=125, escalationRatio=.5f;
    [SerializeField] private float acquisitionDuration=.9f, criticalAcquisition=.7f, commitDuration=.65f, maxLeadTime=.25f;
    [SerializeField] private float recovery=2.2f, criticalRecovery=2f, transitionDuration=.65f;
    [SerializeField] private float preferredDistance=4.8f, closeDistance=3.5f, movementSpeed=1.8f, repositionDuration=1.2f;
    [SerializeField] private float mineWarning=1.1f, mineLifetime=8f;
    [SerializeField,Range(1,3)] private int mineCap=3;
    private EnemyHealth health;
    private Rigidbody2D body,playerBody;
    private PlayerHealth playerHealth;
    private ExpeditionHUD hud;
    private GungeonStyleCamera2D cameraOwner;
    private RunManager runManager;
    private MeteorObstacle[] meteors;
    private RaiderRailShot rail;
    private readonly RaiderSniperMine[] mines=new RaiderSniperMine[3];
    private readonly PhaseCombatVfx[] views=new PhaseCombatVfx[2];
    private Vector2 center,half=new Vector2(8.4f,8.4f),committedTarget,committedOrigin,committedDirection,moveTarget;
    private bool configured,cameraHeld,pendingEscalation;
    private float remaining,stateAge,sparkTime=-10;
    public static RaiderSniperCommanderBossController ActiveEncounter {get;private set;}
    public SniperStage Stage {get;private set;}
    public bool IsCombatActive {get;private set;}
    public bool IsPhase2 {get;private set;}
    public int ShotsFired {get;private set;}
    public int Cycle {get;private set;}
    public int EscalationCount {get;private set;}
    public Vector2 CommittedTarget=>committedTarget;
    public Vector2 CommittedDirection=>committedDirection;
    public bool IsRailDamaging=>rail!=null&&rail.IsDamaging;
    public Bounds ArenaBounds=>new Bounds(center,new Vector3(half.x*2,half.y*2,20));
    public int LiveMineCount {get{int n=0;for(int i=0;i<mines.Length;i++)if(mines[i]!=null)n++;return n;}}
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
        playerBody = playerHealth != null ? playerHealth.GetComponent<Rigidbody2D>() : null;
    }
    public void PrepareBattleCameraProfile()
    {
        Resolve();
        if (!cameraHeld && cameraOwner != null) cameraHeld = cameraOwner.AcquireGameplayFramingProfile(this, new Vector2(0,1.6f), .35f, 1.1f, true);
    }
    public void ReleaseBattleCameraProfile(bool immediate)
    { if (cameraHeld && cameraOwner != null) cameraOwner.ReleaseGameplayFramingProfile(this, immediate); cameraHeld = false; }
    public void BeginCombat()
    {
        if(IsCombatActive||health==null||health.IsDead||(RunManager.Instance!=null&&RunManager.Instance.IsCompletingRun))return;
        if(!configured)ConfigureEncounter(transform.position,half,null);
        IsCombatActive=true;IsPhase2=false;pendingEscalation=health.HpRatio<=escalationRatio;
        ShotsFired=Cycle=EscalationCount=0;ActiveEncounter=this;
        PrepareBattleCameraProfile();hud?.SetRegionBossPresentation(this,true);
        if(playerHealth!=null)playerHealth.Died+=PlayerDied;
        meteors=FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);Bounds reserved=ArenaBounds;reserved.Expand(2);
        for(int i=0;i<meteors.Length;i++)meteors[i].PauseForSectorEncounter(reserved);
        AcquireViews();Enter(SniperStage.Idle,.65f);
    }
    public void StopCombat()
    {
        IsCombatActive=false;pendingEscalation=false;Stage=SniperStage.Stopped;
        if(playerHealth!=null)playerHealth.Died-=PlayerDied;
        ReleaseMines();ReleaseViews();Bullet.ReleaseAllActiveFromSource(transform);
        if(body!=null){body.linearVelocity=Vector2.zero;body.angularVelocity=0;}
        if(ActiveEncounter==this)ActiveEncounter=null;
        if(meteors!=null)for(int i=0;i<meteors.Length;i++)if(meteors[i]!=null)meteors[i].ResumeAfterSectorEncounter();
        meteors=null;hud?.SetRegionBossPresentation(this,false);ReleaseBattleCameraProfile(true);
    }
    private void Update()
    {
        if(!IsCombatActive||GameplayPauseManager.IsPaused)return;
        if(health.IsDead||(playerHealth!=null&&playerHealth.IsDead)||(runManager!=null&&runManager.IsCompletingRun)){StopCombat();return;}
        AdvanceScheduler(Time.deltaTime);UpdateViews();
    }
    private void AdvanceScheduler(float dt)
    {
        if(pendingEscalation)
        {
            pendingEscalation=false;IsPhase2=true;EscalationCount++;rail?.Clear();ReleaseMines();
            sparkTime=Time.time;Enter(SniperStage.CriticalTransition,transitionDuration);return;
        }
        for(int i=0;i<mines.Length;i++)if(mines[i]!=null&&!mines[i].Advance(dt))ReleaseMine(i);
        remaining-=dt;stateAge+=dt;
        if(Stage==SniperStage.Acquisition)UpdateAcquisition();
        if(Stage==SniperStage.CommittedCharge)rail?.Warn(1);
        if(Stage==SniperStage.RailShot&&remaining>0)rail?.Fire(stateAge);
        if(remaining>0)return;
        switch(Stage)
        {
            case SniperStage.Idle:case SniperStage.CriticalTransition:case SniperStage.Recovery:
                Cycle++;DeployMines();Enter(SniperStage.MineDeployment,mineWarning);break;
            case SniperStage.MineDeployment:BeginReposition();break;
            case SniperStage.Reposition:Enter(SniperStage.Acquisition,IsPhase2?criticalAcquisition:acquisitionDuration);UpdateAcquisition();break;
            case SniperStage.Acquisition:CommitAim();Enter(SniperStage.CommittedCharge,commitDuration);break;
            case SniperStage.CommittedCharge:ShotsFired++;Enter(SniperStage.RailShot,RaiderRailShot.Duration);rail?.Fire(0);break;
            case SniperStage.RailShot:rail?.Clear();Enter(SniperStage.Recovery,IsPhase2?criticalRecovery:recovery);break;
        }
    }
    private Vector2 TargetPosition=>playerHealth!=null?(Vector2)playerHealth.transform.position:body.position+Vector2.down*preferredDistance;
    private void UpdateAcquisition()
    {
        Vector2 direction=(TargetPosition-body.position).normalized;Face(direction);
        if(rail!=null)rail.Configure(railMuzzle.position,RayEnd(railMuzzle.position,direction),Damage,playerHealth);
        rail?.Warn(Mathf.Clamp01(stateAge/(IsPhase2?criticalAcquisition:acquisitionDuration)));
    }
    private float Damage=>aimDefinition!=null?aimDefinition.Damage:2;
    private void CommitAim()
    {
        committedTarget=EnemyAttackController.PredictTargetPosition(body.position,TargetPosition,playerBody!=null?playerBody.linearVelocity:Vector2.zero,aimDefinition!=null?aimDefinition.Speed:0,maxLeadTime);
        committedDirection=(committedTarget-body.position).normalized;if(committedDirection.sqrMagnitude<.001f)committedDirection=Vector2.down;
        Face(committedDirection);committedOrigin=railMuzzle.position;
        // Collinear muzzle inherits the one visual rotation; world-space line is now frozen.
        rail?.Configure(committedOrigin,RayEnd(committedOrigin,committedDirection),Damage,playerHealth);rail?.Warn(1);
    }
    private Vector2 RayEnd(Vector2 origin,Vector2 direction)
    {
        float x=Mathf.Abs(direction.x)>.0001f?(center.x+(direction.x>0?half.x:-half.x)-origin.x)/direction.x:float.PositiveInfinity;
        float y=Mathf.Abs(direction.y)>.0001f?(center.y+(direction.y>0?half.y:-half.y)-origin.y)/direction.y:float.PositiveInfinity;
        return origin+direction*Mathf.Max(.1f,Mathf.Min(x,y));
    }
    private void Face(Vector2 direction)
    {if(visualRoot!=null&&direction.sqrMagnitude>.001f)visualRoot.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90);}
    private Vector2 ClampToArena(Vector2 p)=>new Vector2(Mathf.Clamp(p.x,center.x-half.x+1.5f,center.x+half.x-1.5f),Mathf.Clamp(p.y,center.y-half.y+1.5f,center.y+half.y-1.5f));
    private void BeginReposition()
    {
        Vector2 away=body.position-TargetPosition;float distance=away.magnitude;
        if(distance<.01f)away=Vector2.up;else away/=distance;
        Vector2 lateral=new Vector2(-away.y,away.x)*((Cycle&1)==0?1:-1);
        Vector2 step=distance<closeDistance?away*2:distance>preferredDistance+1?lateral: lateral*1.4f;
        moveTarget=ClampToArena(body.position+step);
        // Never cross the player while re-angling, including clamped corner retreats.
        Vector2 delta=moveTarget-body.position;float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(TargetPosition-body.position,delta)/delta.sqrMagnitude):0;
        if((body.position+delta*t-TargetPosition).sqrMagnitude<1.5f*1.5f)moveTarget=body.position;
        Enter(SniperStage.Reposition,IsPhase2?repositionDuration*.85f:repositionDuration);
    }
    private void FixedUpdate()
    {
        if(!IsCombatActive||GameplayPauseManager.IsPaused||Stage!=SniperStage.Reposition||health.IsKnockbackActive)return;
        body.MovePosition(Vector2.MoveTowards(body.position,moveTarget,movementSpeed*Time.fixedDeltaTime));Face(TargetPosition-body.position);
    }
    private void DeployMines()
    {
        if(minePrefab==null||PoolManager.Instance==null)return;
        Vector2 forward=(TargetPosition-body.position).normalized;if(forward.sqrMagnitude<.01f)forward=Vector2.down;
        Vector2 side=new Vector2(-forward.y,forward.x), anchor=TargetPosition;
        int wanted=IsPhase2?3:2;
        for(int n=0;n<wanted&&LiveMineCount<Mathf.Clamp(mineCap,1,mines.Length);n++)
        {
            Vector2 offset=n==0?side*1.8f:n==1?-side*1.8f:forward*2.4f;
            Vector2 position=ClampToArena(anchor+offset);
            // Skip unsafe/clamped overlap rather than force a closed layout.
            if((position-anchor).sqrMagnitude<1.6f*1.6f||(position-body.position).sqrMagnitude<2.25f)continue;
            bool clear=true;for(int i=0;i<mines.Length;i++)if(mines[i]!=null&&((Vector2)mines[i].transform.position-position).sqrMagnitude<2.25f)clear=false;
            if(!clear)continue;
            for(int i=0;i<mines.Length;i++)if(mines[i]==null)
            {
                var mine=PoolManager.Instance.Get(minePrefab.gameObject,position,Quaternion.identity).GetComponent<RaiderSniperMine>();
                mines[i]=mine;mine.Initialize(this,playerHealth,mineWarning,mineLifetime,Damage);break;
            }
        }
    }
    private void ReleaseMine(int i)
    {var m=mines[i];mines[i]=null;if(m==null)return;m.Clear();if(PoolManager.Instance!=null)PoolManager.Instance.Release(m.gameObject);else m.gameObject.SetActive(false);}
    private void ReleaseMines(){for(int i=0;i<mines.Length;i++)ReleaseMine(i);}
    private void Enter(SniperStage next,float duration)
    {
        // One cue per semantic boundary, never from the continuously sampled rail visual.
        if (Stage != next)
        {
            if (next == SniperStage.Acquisition) AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position);
            else if (next == SniperStage.RailShot) AudioManager.PlayAt(SoundEventIds.BossChargeFire, committedOrigin);
            else if (next == SniperStage.CriticalTransition) AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);
        }
        if(Stage==SniperStage.Reposition&&body!=null&&!health.IsKnockbackActive)body.linearVelocity=Vector2.zero;
        Stage=next;remaining=duration;stateAge=0;
        // Refresh at sequence boundaries through existing camera ownership, never zoom polling.
        if(cameraHeld&&cameraOwner!=null&&playerHealth!=null)
        {
            Vector2 offset=Vector2.ClampMagnitude((body.position-TargetPosition)*.5f+Vector2.up*.4f,2.9f);
            cameraOwner.AcquireGameplayFramingProfile(this,offset,.35f,1.1f,false);
        }
        if(bodyRenderer!=null)bodyRenderer.sprite=IsPhase2?criticalSprite:next==SniperStage.Acquisition||next==SniperStage.CommittedCharge?lockSprite:idleSprite;
    }
    private void AcquireViews()
    {
        if(PoolManager.Instance==null)return;
        if(railPrefab!=null)rail=PoolManager.Instance.Get(railPrefab.gameObject,transform.position,Quaternion.identity).GetComponent<RaiderRailShot>();rail?.Clear();
        for(int i=0;i<views.Length;i++){var p=i==0?sparksPrefab:criticalPrefab;if(p!=null)views[i]=PoolManager.Instance.Get(p.gameObject,transform.position,Quaternion.identity).GetComponent<PhaseCombatVfx>();views[i]?.Clear();}
    }
    private void UpdateViews()
    {
        for(int i=0;i<views.Length;i++)
        {
            var v=views[i];if(v==null)continue;v.transform.SetPositionAndRotation(visualRoot.position,visualRoot.rotation);
            if(i==0)v.Sample(Time.time-sparkTime);else if(IsPhase2)v.Sample(Time.time%1);else v.Clear();
        }
    }
    private void ReleaseViews()
    {
        if(rail!=null){rail.Clear();if(PoolManager.Instance!=null)PoolManager.Instance.Release(rail.gameObject);else rail.gameObject.SetActive(false);rail=null;}
        for(int i=0;i<views.Length;i++){var v=views[i];views[i]=null;if(v==null)continue;v.Clear();if(PoolManager.Instance!=null)PoolManager.Instance.Release(v.gameObject);else v.gameObject.SetActive(false);}
    }
    private void HealthChanged(EnemyHealth _,float current,float maximum)
    {if(IsCombatActive&&!IsPhase2&&current>0&&current/maximum<=escalationRatio){pendingEscalation=true;rail?.Clear();ReleaseMines();}}
    private void Damaged(EnemyHealth _){if(IsCombatActive&&Time.time-sparkTime>.2f)sparkTime=Time.time;}
    private void Died(EnemyHealth _)=>StopCombat();
    private void PlayerDied()=>StopCombat();
    private void RunEnded(RunResultData _)=>StopCombat();
}
