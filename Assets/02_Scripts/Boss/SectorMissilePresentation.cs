using UnityEngine;

// Enemy variant only. Bullet remains the sole movement/contact/guidance owner.
[DisallowMultipleComponent]
public sealed class SectorMissilePresentation : MonoBehaviour
{
    [SerializeField] private Collider2D contact;
    [SerializeField] private SectorSupportVfx propulsion;
    public bool Deployed { get; private set; }
    public void BeginDeployment()
    {
        Deployed = false;
        if (contact != null) contact.enabled = false;
        propulsion?.Sample(0, true);
    }
    public void Sample(float age)
    {
        Deployed = age >= BossPatternController.SectorMissileDeploymentTime;
        if (contact != null) contact.enabled = Deployed;
        propulsion?.Sample(age, true);
    }
    private void OnDisable()
    {
        Deployed = false; propulsion?.Clear();
        // Bullet.Initialize restores the authored contact state on the next rental.
    }
}
