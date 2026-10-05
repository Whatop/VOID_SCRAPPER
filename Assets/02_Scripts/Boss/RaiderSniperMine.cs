using UnityEngine;

// No Update, reward, pickup, or movement component. Encounter advances all mines.
[DisallowMultipleComponent]
public sealed class RaiderSniperMine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer ring, machinery;
    [SerializeField] private CircleCollider2D trigger;
    [SerializeField] private Sprite[] warningFrames, impactFrames;
    [SerializeField] private Sprite machinerySprite;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private float age, warning, lifetime, damage, blastAge;
    private bool detonating, hit;
    public RaiderSniperCommanderBossController Owner {get;private set;}
    public bool IsArmed=>trigger!=null&&trigger.enabled&&!detonating;
    public bool IsDetonating=>detonating;
    public bool IsVisible=>ring!=null&&ring.enabled;
    public float Radius=>trigger.radius;
    public void Initialize(RaiderSniperCommanderBossController owner,PlayerHealth target,float warningTime,float activeLifetime,float amount)
    {
        Clear();Owner=owner;player=target;playerCollider=target!=null?target.GetComponentInChildren<Collider2D>():null;
        warning=warningTime;lifetime=activeLifetime;damage=amount;machinery.sprite=machinerySprite;machinery.transform.localScale=Vector3.one*.22f;ring.enabled=machinery.enabled=true;ShowWarning(0);
    }
    // False means release through the owning encounter's PoolManager contract.
    public bool Advance(float dt)
    {
        if(Owner==null)return false;
        age+=dt;
        if(detonating)
        {
            blastAge+=dt;
            if(blastAge>=.2f){Clear();return false;}
            ring.sprite=warningFrames[warningFrames.Length-1];ring.color=Color.white;
            machinery.sprite=impactFrames[blastAge<.035f?0:blastAge<.09f?1:blastAge<.16f?2:3];
            machinery.transform.localScale=Vector3.one*(Radius*2f*32f/28f);
            machinery.enabled=blastAge<.16f;trigger.enabled=blastAge<.09f;
            if(trigger.enabled&&!hit&&PlayerInRadius()){hit=true;player.TakeDamage(damage,transform.position,(Vector2)player.transform.position-(Vector2)transform.position);}
            return true;
        }
        if(age<warning){ShowWarning(age/warning);return true;}
        if(age>=warning+lifetime){Clear();return false;}
        ring.sprite=warningFrames[warningFrames.Length-1];ring.color=Color.white;trigger.enabled=true;
        if(PlayerInRadius()){detonating=true;blastAge=0;machinery.sprite=impactFrames[0];machinery.transform.localScale=Vector3.one*(Radius*2f*32f/28f);}
        return true;
    }
    private bool PlayerInRadius()
    {
        if(player==null||player.IsDead)return false;
        Vector2 p=playerCollider!=null?playerCollider.ClosestPoint(transform.position):(Vector2)player.transform.position;
        return (p-(Vector2)transform.position).sqrMagnitude<=Radius*Radius;
    }
    private void ShowWarning(float progress){trigger.enabled=false;ring.sprite=warningFrames[Mathf.Clamp((int)(progress*warningFrames.Length),0,warningFrames.Length-1)];ring.color=new Color(1,1,1,.55f);}
    public void Clear(){Owner=null;age=blastAge=0;detonating=hit=false;player=null;playerCollider=null;if(trigger!=null)trigger.enabled=false;if(ring!=null)ring.enabled=false;if(machinery!=null)machinery.enabled=false;}
    private void OnDisable()=>Clear();
}
