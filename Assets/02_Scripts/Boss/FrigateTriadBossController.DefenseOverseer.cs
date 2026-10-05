using System.Collections;
using UnityEngine;

public sealed partial class FrigateTriadBossController
{
    public enum DefenseStage { Stopped, Recovery, BatteryTell, LeftBattery, BatteryGap, RightBattery, BarrageWarning, BarrageImpact, BarrageGap, ArmorBreak, Reformation }
    [Header("Defense Overseer — authored Region B sequence")]
    [SerializeField] private bool useDefenseOverseerSequence;
    [SerializeField] private Sprite defenseIdleSprite, defenseChargedSprite, defenseBrokenSprite;
    [SerializeField] private Transform[] defenseLeftMuzzles = new Transform[3];
    [SerializeField] private Transform[] defenseRightMuzzles = new Transform[3];
    [SerializeField] private DefenseBarrageZone defenseBarragePrefab;
    [SerializeField] private GameObject defenseBatteryFlashPrefab;
    [SerializeField, Min(.05f)] private float defenseArmorBreakDuration = .9f;
    [SerializeField, Min(.05f)] private float defenseBatteryCadence = .38f;
    [SerializeField, Min(.05f)] private float defenseBrokenCadence = .28f;
    [SerializeField, Min(.05f)] private float defenseFinalCadence = .22f;
    [SerializeField, Min(.05f)] private float defenseBatteryGap = .32f;
    [SerializeField, Min(.05f)] private float defenseBrokenGap = .2f;
    [SerializeField, Min(.05f)] private float defenseFinalGap = .12f;
    [SerializeField, Min(.05f)] private float defenseBarrageGap = .22f;
    [SerializeField, Min(.05f)] private float defenseBrokenBarrageGap = .12f;
    [SerializeField, Min(.05f)] private float defenseFinalBarrageGap = .08f;
    private readonly Vector2[] defenseLockedDirections = new Vector2[3];
    private readonly GameObject[] defenseFlashes = new GameObject[6];
    private readonly float[] defenseFlashEnds = new float[6];
    private readonly Quaternion[] defenseFlashRotations = new Quaternion[6];
    private DefenseBarrageZone defenseZone;
    private MeteorObstacle[] defenseMeteors;
    private bool defenseArmorBroken;
    private int defenseArmorBreakCount, defenseCycle, defenseShots;
    private DefenseStage defenseStage;
    public static FrigateTriadBossController ActiveDefenseEncounter { get; private set; }
    public bool UsesDefenseOverseerSequence => useDefenseOverseerSequence;
    public DefenseStage CurrentDefenseStage => defenseStage;
    public bool IsArmorBroken => defenseArmorBroken;
    public int ArmorBreakCount => defenseArmorBreakCount;
    public int DefenseCycle => defenseCycle;
    public int DefenseShots => defenseShots;
    public FrigateBossPart LastBatteryOwner { get; private set; }
    public bool LastBatteryWasRight { get; private set; }
    public Vector2 LastBatteryOrigin { get; private set; }
    public Vector2 LastBatteryDirection { get; private set; }
    public float BatteryCadence(int count) => count >= 3 ? defenseBatteryCadence : count == 2 ? defenseBrokenCadence : defenseFinalCadence;
    public float BatteryGap(int count) => count >= 3 ? defenseBatteryGap : count == 2 ? defenseBrokenGap : defenseFinalGap;
    public float BarrageGap(int count) => count >= 3 ? defenseBarrageGap : count == 2 ? defenseBrokenBarrageGap : defenseFinalBarrageGap;

    private void ResetDefenseSequence()
    {
        defenseArmorBroken = false; defenseArmorBreakCount = 0; defenseCycle = 0; defenseShots = 0;
        defenseStage = DefenseStage.Stopped; LastBatteryOwner = null;
        if (useDefenseOverseerSequence) SetDefenseBody(false);
    }
    private void SetDefenseBody(bool charged)
    {
        Sprite sprite = defenseArmorBroken ? defenseBrokenSprite : charged ? defenseChargedSprite : defenseIdleSprite;
        if (sprite == null || parts == null) return;
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] != null && parts[i].IsAlive && parts[i].VisualRenderer != null)
                parts[i].VisualRenderer.sprite = sprite;
    }
    private IEnumerator DefenseSequenceRoutine(int token, int count)
    {
        while (CanContinuePattern(token, count))
        {
            defenseCycle++; defenseShots = 0;
            SetDefenseBody(false);
            activePattern = SalvageDevourerCombatPattern.BatterySuppression;
            yield return DefenseBatteryBurst(token, count, false);
            if (!CanContinuePattern(token, count)) yield break;
            defenseStage = DefenseStage.BatteryGap;
            yield return WaitPatternSeconds(BatteryGap(count), token, count);
            yield return DefenseBatteryBurst(token, count, true);
            if (!CanContinuePattern(token, count)) yield break;
            yield return DefenseRecovery(token, count);
            if (!CanContinuePattern(token, count)) yield break;
            activePattern = SalvageDevourerCombatPattern.HeavyBarrage;
            SetDefenseBody(true);
            int zones = count >= 3 ? 2 : 3;
            for (int i = 0; i < zones && CanContinuePattern(token, count); i++)
            {
                yield return DefenseBarrageRoutine(token, count, i);
                if (!CanContinuePattern(token, count)) yield break;
                if (i + 1 < zones)
                {
                    defenseStage = DefenseStage.BarrageGap;
                    yield return WaitPatternSeconds(BarrageGap(count), token, count);
                }
            }
            SetDefenseBody(false);
            yield return DefenseRecovery(token, count);
        }
    }
    private IEnumerator DefenseRecovery(int token, int count)
    {
        activePattern = SalvageDevourerCombatPattern.None;
        defenseStage = DefenseStage.Recovery;
        yield return WaitPatternSeconds(count >= 3 ? threeAliveRecovery : count == 2 ? twoAliveRecovery : oneAliveRecovery, token, count);
    }
    private Vector2 DefenseTargetSample()
    {
        Vector2 target = observedPlayerHealth != null ? (Vector2)observedPlayerHealth.transform.position : CurrentScrollAnchor + Vector2.down * 3;
        Vector2 velocity = observedPlayerRigidbody != null ? observedPlayerRigidbody.linearVelocity : Vector2.zero;
        return target + Vector2.ClampMagnitude(velocity * aimedPredictionTime, aimedMaximumLeadDistance);
    }
    public Transform DefenseMuzzle(int index, bool right) => right ? defenseRightMuzzles[index] : defenseLeftMuzzles[index];
    private void CommitBatteryAim(bool right, Vector2 target)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            Transform muzzle = DefenseMuzzle(i, right);
            Vector2 delta = muzzle != null ? target - (Vector2)muzzle.position : Vector2.down;
            defenseLockedDirections[i] = delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.down;
        }
    }
    private int LivingPartIndex(int ordinal)
    {
        int living = 0;
        for (int i = 0; i < parts.Length; i++) if (CanPartFire(parts[i])) living++;
        if (living == 0) return -1;
        ordinal %= living;
        for (int i = 0; i < parts.Length; i++) if (CanPartFire(parts[i]) && ordinal-- == 0) return i;
        return -1;
    }
    private IEnumerator DefenseBatteryBurst(int token, int count, bool right)
    {
        if (!CanContinuePattern(token, count)) yield break;
        CommitBatteryAim(right, DefenseTargetSample());
        defenseStage = DefenseStage.BatteryTell;
        AudioManager.PlayAt(SoundEventIds.BossChargeAim, transform.position, .4f);
        yield return WaitPatternSeconds(aimedLockDuration, token, count);
        // Three single shots distributed over surviving modules, never simultaneous fans.
        for (int shot = 0; shot < aimedBurstCount && CanContinuePattern(token, count); shot++)
        {
            defenseStage = right ? DefenseStage.RightBattery : DefenseStage.LeftBattery;
            int index = LivingPartIndex(shot);
            if (index >= 0) FireDefenseBattery(index, right, defenseLockedDirections[index]);
            if (shot + 1 < aimedBurstCount) yield return WaitPatternSeconds(BatteryCadence(count), token, count);
        }
    }
    private void FireDefenseBattery(int index, bool right, Vector2 direction)
    {
        if (!CanPartFire(parts[index])) return;
        Transform muzzle = DefenseMuzzle(index, right);
        if (muzzle == null) return;
        Bullet bullet = SpawnBossProjectile(spreadProjectilePrefab, muzzle.position, direction,
            bossProjectileDefinition != null ? bossProjectileDefinition.Damage : 3,
            bossProjectileDefinition != null ? bossProjectileDefinition.Speed : 9,
            bossProjectileDefinition != null ? bossProjectileDefinition.Range : 12, aimedProjectileColor);
        if (bullet == null) return;
        LastBatteryOwner = parts[index]; LastBatteryWasRight = right;
        LastBatteryOrigin = muzzle.position; LastBatteryDirection = direction; defenseShots++;
        AudioManager.PlayAt(SoundEventIds.BossSpreadFire, muzzle.position, .5f);
        if (defenseBatteryFlashPrefab == null || PoolManager.Instance == null) return;
        int slot = index * 2 + (right ? 1 : 0);
        ReleaseDefenseFlash(slot);
        Quaternion rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        GameObject flash = PoolManager.Instance.Get(defenseBatteryFlashPrefab, muzzle.position, rotation);
        defenseFlashes[slot] = flash; defenseFlashEnds[slot] = Time.time + .21f; defenseFlashRotations[slot] = rotation;
        if (flash != null)
        {
            flash.transform.localScale = defenseBatteryFlashPrefab.transform.localScale;
            var animator = flash.GetComponent<Animator>();
            if (animator != null) animator.Play(0, 0, 0);
        }
    }
    // Called by the existing encounter LateUpdate, not by individual battery components.
    private void TickDefenseFlashes()
    {
        for (int i = 0; i < defenseFlashes.Length; i++)
        {
            if (defenseFlashes[i] == null) continue;
            if (Time.time >= defenseFlashEnds[i] || !CanPartFire(parts[i / 2])) ReleaseDefenseFlash(i);
            else defenseFlashes[i].transform.SetPositionAndRotation(DefenseMuzzle(i / 2, i % 2 == 1).position, defenseFlashRotations[i]);
        }
    }
    private void ReleaseDefenseFlash(int index)
    {
        var flash = defenseFlashes[index]; defenseFlashes[index] = null;
        if (flash == null) return;
        if (PoolManager.Instance != null) PoolManager.Instance.Release(flash); else flash.SetActive(false);
    }
    public static Vector2 ResolveBarrageTarget(Vector2 sampled, Bounds bounds, float radius, int index)
    {
        float offset = index % 3 == 0 ? 0 : index % 3 == 1 ? -2.8f : 2.8f;
        // One bounded circle at a time. Leave the opposing half of the corridor open.
        return new Vector2(Mathf.Clamp(sampled.x + offset, bounds.min.x + radius + .2f, bounds.max.x - radius - .2f),
            Mathf.Clamp(sampled.y, bounds.min.y + radius + .2f, bounds.max.y - radius - 1.8f));
    }
    private IEnumerator DefenseBarrageRoutine(int token, int count, int index)
    {
        if (defenseBarragePrefab == null || PoolManager.Instance == null ||
            !corridorController.TryGetConstrainedPlayerViewportBounds(out Bounds bounds)) yield break;
        Vector2 sampled = DefenseTargetSample();
        Vector2 target = ResolveBarrageTarget(sampled, bounds, warningAreaRadius, index);
        bool right = index % 2 == 1;
        CommitBatteryAim(right, sampled);
        var g = PoolManager.Instance.Get(defenseBarragePrefab.gameObject, target, Quaternion.identity);
        defenseZone = g.GetComponent<DefenseBarrageZone>();
        defenseZone.BeginWarning(target, warningAreaRadius, warningAreaDamage, observedPlayerHealth);
        defenseStage = DefenseStage.BarrageWarning;
        AudioManager.PlayAt(SoundEventIds.BossLaserWarning, target, .65f);
        float elapsed = 0;
        while (elapsed < warningAreaDuration && CanContinuePattern(token, count))
        {
            defenseZone.SetWarningProgress(elapsed / warningAreaDuration);
            yield return null; elapsed += Time.deltaTime;
        }
        if (!CanContinuePattern(token, count)) yield break;
        defenseStage = DefenseStage.BarrageImpact;
        AudioManager.PlayAt(SoundEventIds.BossChargeFire, target, .7f);
        GungeonStyleCamera2D.RequestShake(.055f, .12f);
        // Every other escalated sequence permits one light, already committed shot per zone.
        if (count < 3 && defenseCycle % 2 == 0)
        {
            int owner = LivingPartIndex(index);
            if (owner >= 0) FireDefenseBattery(owner, right, defenseLockedDirections[owner]);
        }
        elapsed = 0;
        while (elapsed < DefenseBarrageZone.ImpactDuration && CanContinuePattern(token, count))
        {
            defenseZone.SetImpactElapsed(elapsed);
            yield return null; elapsed += Time.deltaTime;
        }
        ReleaseDefenseZone();
    }
    private IEnumerator DefenseTransitionRoutine(int token, int count)
    {
        bool firstBreak = count == 2 && defenseArmorBreakCount == 0;
        if (firstBreak) defenseArmorBreakCount++;
        defenseStage = firstBreak ? DefenseStage.ArmorBreak : DefenseStage.Reformation;
        float duration = firstBreak ? defenseArmorBreakDuration : aliveCountTransitionDelay;
        float elapsed = 0;
        while (elapsed < duration && CanContinueTransition(token, count))
        {
            if (firstBreak && elapsed >= duration * .5f && !defenseArmorBroken)
            { defenseArmorBroken = true; SetDefenseBody(false); }
            yield return null; elapsed += Time.deltaTime;
        }
        if (!CanContinueTransition(token, count)) yield break;
        if (firstBreak) { defenseArmorBroken = true; SetDefenseBody(false); }
        patternRoutine = null;
        StartPatternScheduler();
    }
    private void ReleaseDefenseZone()
    {
        var zone = defenseZone; defenseZone = null;
        if (zone == null) return;
        zone.Clear();
        if (PoolManager.Instance != null) PoolManager.Instance.Release(zone.gameObject); else zone.gameObject.SetActive(false);
    }
    private void ClearDefenseAttacks()
    {
        ReleaseDefenseZone();
        for (int i = 0; i < defenseFlashes.Length; i++) ReleaseDefenseFlash(i);
        defenseStage = DefenseStage.Stopped;
        if (useDefenseOverseerSequence) SetDefenseBody(false);
    }
    private void BeginDefenseArenaIsolation()
    {
        if (!useDefenseOverseerSequence || corridorController == null) return;
        ActiveDefenseEncounter = this;
        if (defenseMeteors != null) return;
        // Once at intro handoff, including the whole scrolling corridor footprint.
        defenseMeteors = FindObjectsByType<MeteorObstacle>(FindObjectsSortMode.None);
        Bounds bounds = corridorController.EncounterReservedBounds;
        bounds.Expand(new Vector3(2, 12, 20));
        for (int i = 0; i < defenseMeteors.Length; i++) defenseMeteors[i].PauseForSectorEncounter(bounds);
    }
    private void EndDefenseArenaIsolation()
    {
        if (ActiveDefenseEncounter == this) ActiveDefenseEncounter = null;
        if (defenseMeteors == null) return;
        for (int i = 0; i < defenseMeteors.Length; i++) if (defenseMeteors[i] != null) defenseMeteors[i].ResumeAfterSectorEncounter();
        defenseMeteors = null;
    }
}
