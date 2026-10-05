using System.Collections;
using UnityEngine;

public sealed partial class PirateCommanderBossController
{
    public enum RaiderAttack { TrackingBurst, Scatter, ShieldRam }
    [Header("Raider protection and Ram")]
    [SerializeField] private RaiderShieldPresentation shieldPresentation;
    [SerializeField] private float shieldCapacity = 18f;
    [SerializeField] private float ramSpeed = 12f, ramDistance = 6.5f, ramDamage = 4f;
    [SerializeField] private float ramWarningDuration = .6f, ramPunishDuration = 1.35f;
    private float shieldHp, ramStageStarted;
    private bool introShield, ramStopped, ramHitPlayer;
    private Vector2 ramDirection, ramStart, ramEnd;
    private readonly RaycastHit2D[] ramHits = new RaycastHit2D[32];
    public RaiderAttack CurrentAttack { get; private set; }
    public float ShieldHp => shieldHp;
    public int AbsorbedHitSequence { get; private set; }
    public bool IsShieldProtected => shieldHp > 0 && (introShield || combatActive);
    public bool IsRamPunish => stage == AssaultStage.RamPunish;
    public Vector2 RamDirection => ramDirection;
    public Vector2 RamStart => ramStart;
    public Vector2 RamEnd => ramEnd;
    public bool RamHitPlayer => ramHitPlayer;
    public static RaiderAttack AttackForSlot(int slot) => (slot & 3) == 1 ? RaiderAttack.Scatter :
        (slot & 3) == 3 ? RaiderAttack.ShieldRam : RaiderAttack.TrackingBurst;
    public static int TrackingBursts(bool critical) => critical ? 3 : 2;
    public static int ScatterPellets(bool critical) => critical ? 7 : 5;
    public static float ScatterAngle(int index, bool critical) => Mathf.Lerp(critical ? -36 : -30,
        critical ? 36 : 30, index / (float)(ScatterPellets(critical) - 1));

    public void BeginIntroShield()
    {
        ResolveReferences(); introShield = true; shieldHp = shieldCapacity; AbsorbedHitSequence = 0;
        shieldPresentation?.Clear(); shieldPresentation?.ShowNormal(true);
    }
    private void BeginShieldCombat()
    {
        if (!introShield) shieldHp = shieldCapacity;
        AbsorbedHitSequence = 0;
        introShield = false; ramHitPlayer = ramStopped = false;
        shieldPresentation?.Clear(); shieldPresentation?.ShowNormal(IsShieldProtected);
    }
    private void ClearRaiderProtection()
    {
        introShield = false; shieldHp = 0; ramStopped = true; ramHitPlayer = false;
        shieldPresentation?.Clear();
    }
    public bool TryAbsorbDamage(float damage, Vector2 point, Vector2 incoming)
    {
        if (damage <= 0 || !IsShieldProtected || enemyHealth == null || enemyHealth.IsDead) return false;
        // Intro hits are absorbed without allowing a cinematic kill. Combat has a finite pool.
        if (!introShield) shieldHp = Mathf.Max(0, shieldHp - damage);
        AbsorbedHitSequence++;
        CombatFeedbackManager.PlayHit(point, incoming, CombatFeedbackKind.Shield, 1, 0, 0, true);
        shieldPresentation?.ShowNormal(IsShieldProtected && !IsRamShieldStage(stage));
        if (!IsShieldProtected) shieldPresentation?.BreakField();
        return true;
    }
    private static bool IsRamShieldStage(AssaultStage s) => s == AssaultStage.RamWarning || s == AssaultStage.RamActive || s == AssaultStage.RamPunish || s == AssaultStage.ShieldRecover;
    private IEnumerator AimFor(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end && !Interrupted) { CommitAim(); yield return null; }
        if (!Interrupted) CommitAim();
    }
    private IEnumerator TrackingBurstRoutine()
    {
        int bursts = TrackingBursts(phase2);
        for (int burst = 0; burst < bursts && !Interrupted; burst++)
        {
            int mount = burst & 1;
            SetHot(false); SetStage(mount == 0 ? AssaultStage.AimLeft : AssaultStage.AimRight);
            yield return AimFor(.4f);
            if (Interrupted) yield break;
            SetHot(true); SetStage(mount == 0 ? AssaultStage.LeftBurst : AssaultStage.RightBurst);
            Vector2 basis = (committedTarget - (Vector2)Muzzle(mount).position).normalized;
            float correction = 0;
            for (int shot = 0; shot < 5 && !Interrupted; shot++)
            {
                if (playerHealth != null)
                {
                    Vector2 live = (Vector2)playerHealth.transform.position - (Vector2)Muzzle(mount).position;
                    correction = Mathf.MoveTowards(correction, Mathf.Clamp(Vector2.SignedAngle(basis, live), -3, 3), 1.5f);
                }
                Fire(mount, Rotate(basis, correction));
                yield return WaitGameplaySeconds(.10f);
            }
            SetHot(false);
            if (burst + 1 < bursts) { SetStage(AssaultStage.Gap); yield return WaitGameplaySeconds(.30f); }
        }
    }
    private IEnumerator ScatterRoutine()
    {
        SetStage(AssaultStage.ScatterTell); SetHot(true);
        yield return AimFor(.5f);
        if (Interrupted) yield break;
        SetStage(AssaultStage.ScatterFire);
        Vector2 basis = (committedTarget - (Vector2)Muzzle(1).position).normalized;
        for (int i = 0; i < ScatterPellets(phase2); i++) Fire(1, Rotate(basis, ScatterAngle(i, phase2)));
        yield return WaitGameplaySeconds(.22f); SetHot(false);
    }
    private IEnumerator ShieldRamRoutine()
    {
        SetHot(false); SetStage(AssaultStage.RamAim);
        yield return AimFor(.4f);
        if (Interrupted) yield break;
        ramDirection = facingDirection.normalized;
        if (visualRoot != null) visualRoot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(ramDirection.y, ramDirection.x) * Mathf.Rad2Deg - 90);
        ramStart = body.position;
        ramEnd = ResolveRamEnd(ramStart, ramDirection, arenaCenter, arenaHalfExtents, ramDistance);
        shieldHp = shieldCapacity; ramHitPlayer = false; ramStopped = false;
        shieldPresentation?.ShowNormal(false);
        shieldPresentation?.ShowLane(body.position, ramEnd, 2.6f);
        SetStage(AssaultStage.RamWarning); ramStageStarted = Time.time;
        yield return WaitGameplaySeconds(ramWarningDuration);
        if (Interrupted) yield break;
        shieldPresentation?.HideLane();
        SetStage(AssaultStage.RamActive); ramStageStarted = Time.time;
        float deadline = Time.time + ramDistance / Mathf.Max(1, ramSpeed) + .3f;
        while (!ramStopped && Time.time < deadline && !Interrupted) yield return null;
        if (Interrupted) yield break;
        body.linearVelocity = Vector2.zero;
        shieldHp = 0; shieldPresentation?.ShowNormal(false);
        SetStage(AssaultStage.RamPunish); ramStageStarted = Time.time;
        yield return WaitGameplaySeconds(ramPunishDuration);
        if (Interrupted) yield break;
        // This deliberately remains unprotected until recovery is visibly complete.
        SetStage(AssaultStage.ShieldRecover); ramStageStarted = Time.time;
        yield return WaitGameplaySeconds(.28f);
        if (Interrupted) yield break;
        shieldHp = shieldCapacity; shieldPresentation?.ClearRam(); shieldPresentation?.ShowNormal(true);
    }
    public static Vector2 ResolveRamEnd(Vector2 start, Vector2 direction, Vector2 center, Vector2 half, float distance)
    {
        direction.Normalize(); half = Vector2.Max(Vector2.zero, half - Vector2.one * 1.5f);
        float length = Mathf.Max(0, distance);
        if (Mathf.Abs(direction.x) > .0001f) length = Mathf.Min(length, ((direction.x > 0 ? center.x + half.x : center.x - half.x) - start.x) / direction.x);
        if (Mathf.Abs(direction.y) > .0001f) length = Mathf.Min(length, ((direction.y > 0 ? center.y + half.y : center.y - half.y) - start.y) / direction.y);
        return start + direction * Mathf.Max(0, length);
    }
    private void StepRam(float dt)
    {
        if (ramStopped) return;
        Vector2 start = body.position;
        float step = Mathf.Min(ramSpeed * dt, Vector2.Distance(start, ramEnd));
        int count = Physics2D.CircleCastNonAlloc(start, 1.12f, ramDirection, ramHits, step, ~0);
        float travel = step;
        for (int i = 0; i < count; i++)
        {
            var hit = ramHits[i]; if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (!hit.collider.isTrigger && (hit.collider.GetComponentInParent<BossArenaLaserWall>() != null || hit.collider.GetComponentInParent<MeteorObstacle>() != null))
            { travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - .03f)); ramStopped = true; }
        }
        // Query the swept hull only as far as the wall permits. One hit per Ram.
        if (!ramHitPlayer && playerHealth != null && !playerHealth.IsDead)
        {
            int hits = Physics2D.CircleCastNonAlloc(start, 1.12f, ramDirection, ramHits, travel, ~0);
            for (int i = 0; i < hits; i++)
                if (ramHits[i].collider != null && ramHits[i].collider.GetComponentInParent<PlayerHealth>() == playerHealth)
                { ramHitPlayer = true; playerHealth.TakeDamage(ramDamage, ramHits[i].point, ramDirection); break; }
        }
        if (!CanContinueCombat()) return; // A lethal impact may synchronously tear down the encounter.
        body.MovePosition(start + ramDirection * travel);
        if (step <= .01f || Vector2.Distance(start + ramDirection * travel, ramEnd) < .04f) ramStopped = true;
    }
    private void SampleRamPresentation()
    {
        if (shieldPresentation == null) return;
        shieldPresentation.Sample(stage, Time.time - ramStageStarted, IsShieldProtected, visualRenderer);
    }
    private void UpdateRaiderCombatFraming()
    {
        if (!battleCameraProfileHeld || gameplayCamera == null || playerHealth == null) return;
        Vector2 player = playerHealth.transform.position;
        Vector2 focus = Vector2.Lerp(player, body.position, .45f) + Vector2.up * .3f;
        bool frameRam = stage == AssaultStage.RamAim || stage == AssaultStage.RamWarning || stage == AssaultStage.RamActive;
        if (frameRam)
        {
            Vector2 start = stage == AssaultStage.RamAim ? body.position : ramStart;
            Vector2 end = stage == AssaultStage.RamAim
                ? ResolveRamEnd(start, facingDirection, arenaCenter, arenaHalfExtents, ramDistance) : ramEnd;
            focus = (Vector2.Min(player, Vector2.Min(start, end)) + Vector2.Max(player, Vector2.Max(start, end))) * .5f + Vector2.up * .25f;
        }
        // Reuse the existing smooth gameplay profile. A small pullback begins during
        // aim, so both ends of the committed lane fit before the Ram becomes active.
        gameplayCamera.AcquireGameplayFramingProfile(this, focus - player, .01f,
            battleCameraZoomMultiplier * (frameRam ? 1.2f : 1.08f), false);
    }
}
