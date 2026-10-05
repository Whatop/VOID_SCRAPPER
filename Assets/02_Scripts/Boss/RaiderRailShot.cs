using UnityEngine;

// Passive pooled pulse: the boss owns the warning, visible frames and damage clock.
[DisallowMultipleComponent]
public sealed class RaiderRailShot : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private BoxCollider2D damageCollider;
    [SerializeField] private Sprite[] frames;
    private PlayerHealth player;
    private Collider2D playerCollider;
    private float damage, length;
    private bool hit;
    public const float Duration = .13f;
    public const float DamageDuration = .09f; // The approved final 40 ms frame is transparent.
    public bool IsDamaging => damageCollider != null && damageCollider.enabled;
    public bool IsVisible => visual != null && visual.enabled;
    public Vector2 Direction => transform.right;
    public float Length => length;

    public void Configure(Vector2 origin, Vector2 end, float amount, PlayerHealth target)
    {
        Clear(); player=target; playerCollider=target!=null?target.GetComponentInChildren<Collider2D>():null; damage=amount;
        Vector2 delta=end-origin;length=delta.magnitude;
        transform.SetPositionAndRotation(origin,Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg));
        transform.localScale=Vector3.one;
        visual.drawMode=SpriteDrawMode.Sliced; visual.size=new Vector2(delta.magnitude,1);
        // The approved shot's bright center is six source pixels; keep collision within it.
        damageCollider.offset=new Vector2(delta.magnitude*.5f,0); damageCollider.size=new Vector2(delta.magnitude,.16f);
        Warn(0);
    }
    public void Warn(float progress)
    {
        damageCollider.enabled=false; visual.sprite=frames[Mathf.Clamp((int)(progress*3),0,2)]; visual.enabled=true;
    }
    public void Fire(float elapsed)
    {
        if(elapsed>=DamageDuration){Clear();return;}
        visual.sprite=frames[elapsed<.035f?3:4]; visual.enabled=true;
        // Approved frames have different transparent muzzle padding. Never damage that padding.
        float first=elapsed<.035f?2f/96f:10f/96f, last=94f/96f;
        damageCollider.offset=new Vector2(length*(first+last)*.5f,0);
        damageCollider.size=new Vector2(length*(last-first),.16f);damageCollider.enabled=true;
        TryHit();
    }
    private void TryHit()
    {
        if(hit||!IsDamaging||player==null||player.IsDead)return;
        // Collider distance includes the player's actual footprint, even for a short pulse
        // whose first rendered frame occurs between physics steps.
        if(playerCollider==null||!damageCollider.Distance(playerCollider).isOverlapped)return;
        hit=true; player.TakeDamage(damage,player.transform.position,Direction);
    }
    public void Clear(){if(visual!=null)visual.enabled=false;if(damageCollider!=null)damageCollider.enabled=false;player=null;playerCollider=null;hit=false;}
    private void OnDisable()=>Clear();
}
