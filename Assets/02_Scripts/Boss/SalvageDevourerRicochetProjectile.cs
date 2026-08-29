using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Bullet))]
public sealed class SalvageDevourerRicochetProjectile : MonoBehaviour,
    IBulletWorldCollisionHandler
{
    private int remainingBounces;
    private bool configured;

    public int RemainingBounces => remainingBounces;

    private void OnEnable()
    {
        remainingBounces = 0;
        configured = false;
    }

    private void OnDisable()
    {
        remainingBounces = 0;
        configured = false;
    }

    public void Configure(int maximumBounces)
    {
        remainingBounces = Mathf.Max(0, maximumBounces);
        configured = remainingBounces > 0;
    }

    public bool TryHandleWorldCollision(Bullet bullet, Collider2D other)
    {
        if (!configured || remainingBounces <= 0 || bullet == null || other == null)
        {
            return false;
        }

        BossArenaLaserWall wall = other.GetComponentInParent<BossArenaLaserWall>();
        if (wall == null ||
            !wall.TryReflectProjectile(
                bullet.transform.position,
                bullet.MoveDirection,
                out Vector2 reflectedDirection))
        {
            return false;
        }

        if (!bullet.TrySetTravelDirection(reflectedDirection))
        {
            return false;
        }

        remainingBounces--;
        configured = remainingBounces > 0;
        return true;
    }
}
