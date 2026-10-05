using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Encounter-local scan/exposure target. Never owns campaign progress or rewards.</summary>
[DisallowMultipleComponent]
public sealed class SettlementDefensePurpleCore : MonoBehaviour
{
    public enum Phase { Dormant, Hidden, Exposed, Cleansed, Cancelled }
    [SerializeField] private EnemyHealth health;
    [SerializeField] private Collider2D damageCollider;
    [SerializeField] private Collider2D radarCollider;
    [SerializeField] private RadarTarget radarTarget;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private RouteCoreCorruptionVisual corruptionPresentation;
    [SerializeField] private ProjectileDefinition projectile;
    [SerializeField, Min(1)] private float maxHp = 50f;
    [SerializeField, Min(1)] private float exposureDuration = 4f;
    [SerializeField, Min(.75f)] private float openingGrace = 1f;
    [SerializeField, Min(.1f)] private float pulseDelay = .55f;
    [SerializeField, Range(4, 12)] private int pulseCount = 8;
    [SerializeField, Min(1)] private float pulseSpeed = 3f;
    [SerializeField] private Color exposedColor = new Color(.8f, .3f, 1f, 1f);

    private SettlementDefenseEncounterController encounter;
    private PlayerRadarScanner scanner;
    private Coroutine exposure;
    private float startedAt;
    private bool warnedMissingPresentation;
    public Phase State { get; private set; }
    public EnemyHealth Health => health;
    public RadarTarget Target => radarTarget;
    public int ExposureCount { get; private set; }
    public int ShotsFired { get; private set; }
    public bool HasAuthoredBindings => health != null && damageCollider != null &&
        damageCollider.GetComponentInParent<EnemyHealth>(true) == health && radarCollider != null &&
        radarCollider != damageCollider && radarCollider.isTrigger &&
        radarCollider.GetComponentInParent<EnemyHealth>(true) == null && radarTarget != null &&
        radarCollider.GetComponentInParent<RadarTarget>(true) == radarTarget && visual != null &&
        projectile != null && projectile.ProjectilePrefab != null &&
        projectile.ProjectilePrefab.GetComponent<Bullet>() != null;

    public bool Begin(SettlementDefenseEncounterController owner, PlayerRadarScanner source)
    {
        if (!HasAuthoredBindings || owner == null || source == null ||
            State == Phase.Hidden || State == Phase.Exposed) return false;
        encounter = owner;
        scanner = source;
        gameObject.SetActive(true);
        health.ResetHealth();
        health.SetMaxHp(maxHp, true);
        health.SetRewardDropEnabled(false);
        health.Died += HandleDeath;
        scanner.ScanCompleted += HandleScan;
        radarCollider.enabled = true;
        radarTarget.enabled = true;
        radarTarget.SetVisible(true);
        ExposureCount = ShotsFired = 0;
        startedAt = Time.time;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (corruptionPresentation == null && !warnedMissingPresentation)
        {
            warnedMissingPresentation = true;
            Debug.LogWarning("RouteCoreDeck/PurpleCorruptionTarget: optional corruption presentation is not authored; using original sprite presentation.", this);
        }
#endif
        Hide(false);
        return true;
    }

    private void HandleScan(Vector2 origin, float radius, IReadOnlyList<RadarTarget> targets)
    {
        // Only the scanner's active-scan event calls this. Passive discovery/opening never do.
        // Ignore scans while exposed: one fixed window, no timer stacking or refresh.
        if (State != Phase.Hidden || targets == null || !radarTarget.IsRadarVisible) return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != radarTarget) continue;
            State = Phase.Exposed;
            ExposureCount++;
            health.RemoveDamageFloor(this);
            damageCollider.enabled = true;
            visual.color = exposedColor;
            corruptionPresentation?.Exposed();
            encounter.PurpleExposureChanged(this, true);
            if (Application.isPlaying && isActiveAndEnabled) exposure = StartCoroutine(ExposureRoutine());
            return;
        }
    }

    private IEnumerator ExposureRoutine()
    {
        float elapsed = 0;
        bool fired = false;
        while (State == Phase.Exposed && elapsed < exposureDuration)
        {
            if (!GameplayPauseManager.IsPaused)
            {
                elapsed += Time.deltaTime;
                if (!fired && elapsed >= pulseDelay && Time.time - startedAt >= openingGrace)
                {
                    fired = true;
                    FirePulse();
                }
            }
            yield return null;
        }
        exposure = null;
        if (State == Phase.Exposed) Hide(true);
    }

    private void Hide(bool notify)
    {
        State = Phase.Hidden;
        damageCollider.enabled = false;
        // Protect direct damage paths too, without changing EnemyHealth or restoring lost HP.
        health.SetDamageFloor(this, 1f);
        visual.color = new Color(exposedColor.r * .2f, exposedColor.g * .2f, exposedColor.b * .2f, .08f);
        corruptionPresentation?.Hidden(!notify);
        Bullet.ReleaseAllActiveFromSource(transform);
        if (notify) encounter.PurpleExposureChanged(this, false);
    }

    private void FirePulse()
    {
        if (State != Phase.Exposed || PoolManager.Instance == null) return;
        for (int i = 0; i < pulseCount; i++)
        {
            var shot = PoolManager.Instance.Get(projectile.ProjectilePrefab, transform.position, Quaternion.identity);
            if (shot == null) continue;
            var bullet = shot.GetComponent<Bullet>();
            bullet.Initialize(Quaternion.Euler(0, 0, i * 360f / pulseCount) * Vector2.up,
                ProjectileOwner.Enemy, projectile, speedOverride: pulseSpeed, rangeOverride: 16f,
                projectileSource: gameObject);
            bullet.ConfigureProjectileColor(exposedColor);
            ShotsFired++;
        }
    }

    private void HandleDeath(EnemyHealth dead)
    {
        if (dead != health || !health.IsDead || State != Phase.Exposed) return;
        State = Phase.Cleansed;
        Cleanup();
        encounter.PurpleCleansed(this);
    }

    public void Cancel()
    {
        if (State != Phase.Cleansed) State = Phase.Cancelled;
        Cleanup();
        corruptionPresentation?.ResetPresentation();
    }

    // Optional post-save visual tail. Missing/unsupported polish returns immediately to facilities.
    public bool TryPlayPurification(out float duration)
    {
        duration = 0;
        if (State != Phase.Cleansed || !gameObject.activeInHierarchy || corruptionPresentation == null || !Application.isPlaying) return false;
        if (!corruptionPresentation.Purify()) return false;
        duration = corruptionPresentation.PurificationDuration;
        return true;
    }

    private void Cleanup()
    {
        if (exposure != null) StopCoroutine(exposure);
        exposure = null;
        if (scanner != null) scanner.ScanCompleted -= HandleScan;
        scanner = null;
        if (health != null) { health.Died -= HandleDeath; health.RemoveDamageFloor(this); }
        if (damageCollider != null) damageCollider.enabled = false;
        if (radarCollider != null) radarCollider.enabled = false;
        if (radarTarget != null) { radarTarget.SetVisible(false); radarTarget.enabled = false; }
        if (visual != null) visual.color = Color.clear;
        Bullet.ReleaseAllActiveFromSource(transform);
    }

    private void OnDisable()
    {
        bool interrupted = State == Phase.Hidden || State == Phase.Exposed;
        Cancel();
        if (interrupted && encounter != null) encounter.FailEncounter();
    }
}
