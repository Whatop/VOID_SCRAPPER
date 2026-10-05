using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>One encounter-local component: combat defeat never grants campaign progress.</summary>
[DisallowMultipleComponent]
public sealed class SettlementDefenseCorruptedCore : MonoBehaviour, IInteractable
{
    public enum CoreState
    {
        Dormant,
        CombatActive,
        AwaitingReactivation,
        Fusing,
        Fused,
        Cancelled
    }

    [SerializeField] private EnemyHealth health;
    [SerializeField] private Collider2D combatCollider;
    [SerializeField] private Collider2D interactionCollider;
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private ProjectileDefinition projectile;
    [SerializeField] private Color corruptedColor = new Color(0.7f, 0.18f, 0.9f, 1f);

    [Header("Authored weapon telegraph")]
    [SerializeField] private LineRenderer telegraph;
    [Header("Orange - Shotgun")]
    [SerializeField, Min(2)] private int fanCount = 5;
    [SerializeField, Range(1f, 120f)] private float fanSpread = 64f;
    [SerializeField, Min(1)] private int volleyCount = 2;
    [SerializeField, Min(.1f)] private float volleyInterval = .24f;
    [SerializeField] private float secondVolleyOffset = 6f;
    [SerializeField, Min(.1f)] private float shotgunTelegraph = .65f;
    [SerializeField, Min(.1f)] private float shotgunRecovery = 1.4f;
    [SerializeField, Min(1f)] private float shotgunSpeed = 4.2f;
    [Header("Blue - Sniper")]
    [SerializeField, Min(1)] private int sniperShots = 3;
    [SerializeField, Min(.1f)] private float sniperAim = .65f;
    [SerializeField, Min(.1f)] private float sniperLock = .3f;
    [SerializeField, Min(.1f)] private float sniperRecovery = .35f;
    [SerializeField, Min(.1f)] private float sniperCycleRecovery = 1f;
    [SerializeField, Min(1f)] private float sniperSpeed = 14f;
    [Header("Green - Machine Gun")]
    [SerializeField, Range(8, 12)] private int machineGunShots = 10;
    [SerializeField, Min(.05f)] private float machineGunInterval = .13f;
    [SerializeField, Range(1f, 60f)] private float machineGunSweep = 24f;
    [SerializeField, Min(.1f)] private float machineGunWarmup = .65f;
    [SerializeField, Min(.1f)] private float machineGunRecovery = 1.8f;
    [SerializeField, Min(1f)] private float machineGunSpeed = 5.2f;

    public enum AttackStage { Idle, Aim, Locked, Firing, Recovery }
    public AttackStage Attack { get; private set; }
    public Vector2 CommittedDirection { get; private set; }
    public int ShotsFired { get; private set; }

    private SettlementDefenseEncounterController encounter;
    private Transform player;
    private Transform center;
    private Color cleanColor;
    private Coroutine combat;
    private Sequence fusion;
    private bool configured;

    public BossStoryPart Part { get; private set; }
    public CoreState State { get; private set; }
    public EnemyHealth Health => health;
    public SpriteRenderer Visual => visual;
    public bool HasAuthoredBindings => health != null && visual != null && combatCollider != null &&
        interactionCollider != null && interactionCollider != combatCollider &&
        projectile != null && projectile.ProjectilePrefab != null &&
        projectile.ProjectilePrefab.GetComponent<Bullet>() != null && telegraph != null;
    public string InteractionText
    {
        get
        {
            string nameKey = Part switch
            {
                BossStoryPart.SectorStabilizer => "ui.story_recovery.sector_stabilizer",
                BossStoryPart.PhaseNavigationLens => "ui.story_recovery.phase_navigation_lens",
                _ => "ui.story_recovery.matter_compressor"
            };
            string partName = SettlementDefenseEncounterController.Text(nameKey,
                CampaignProgressionCatalog.GetStoryPartDisplayName(Part));
            NamedPlaceholderUtility.TryFormat(
                SettlementDefenseEncounterController.Text("defense.core.reactivate", "{partName} 재활성화"),
                new Dictionary<string, string> { { "partName", partName } },
                out string message, out _);
            return message;
        }
    }

    public void Configure(SettlementDefenseEncounterController owner, BossStoryPart part,
        Color color, float hp, Transform target, Transform centralCore)
    {
        encounter = owner;
        Part = part;
        cleanColor = color;
        player = target;
        center = centralCore;
        State = CoreState.Dormant;
        ShotsFired = 0;
        HideTelegraph();
        health.SetRewardDropEnabled(false);
        health.ResetHealth();
        health.SetMaxHp(hp, true);
        health.Died += HandleDefeat;
        configured = true;
        combatCollider.enabled = false;
        interactionCollider.enabled = false;
        visual.color = cleanColor;
    }

    public void Corrupt()
    {
        visual.color = Color.Lerp(cleanColor, corruptedColor, 0.6f);
    }

    public bool ActivateCombat()
    {
        if (!configured || State != CoreState.Dormant)
        {
            return false;
        }
        State = CoreState.CombatActive;
        health.ResetHealth();
        combatCollider.enabled = true;
        visual.color = Color.Lerp(cleanColor, corruptedColor, 0.35f);
        if (Application.isPlaying && isActiveAndEnabled)
        {
            combat = StartCoroutine(CombatRoutine());
        }
        return true;
    }

    private IEnumerator CombatRoutine()
    {
        yield return WaitCombat(.75f);
        while (State == CoreState.CombatActive)
        {
            if (GameplayPauseManager.IsPaused || player == null)
            {
                yield return null;
                continue;
            }
            if (Part == BossStoryPart.SectorStabilizer) yield return ShotgunCycle();
            else if (Part == BossStoryPart.PhaseNavigationLens) yield return SniperCycle();
            else yield return MachineGunCycle();
        }
    }

    private Vector2 AimAtPlayer()
    {
        Vector2 delta = player != null ? (Vector2)(player.position - transform.position) : Vector2.down;
        return delta.sqrMagnitude > .001f ? delta.normalized : Vector2.down;
    }

    private IEnumerator WaitCombat(float duration)
    {
        float elapsed = 0;
        while (elapsed < duration && State == CoreState.CombatActive)
        {
            if (!GameplayPauseManager.IsPaused) elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShotgunCycle()
    {
        CommittedDirection = AimAtPlayer();
        Attack = AttackStage.Locked;
        ShowTelegraph(CommittedDirection, fanSpread, 4.5f, .045f, cleanColor);
        yield return WaitCombat(shotgunTelegraph);
        HideTelegraph();
        Attack = AttackStage.Firing;
        for (int volley = 0; volley < volleyCount; volley++)
        {
            for (int pellet = 0; pellet < fanCount; pellet++)
            {
                float angle = Mathf.Lerp(-fanSpread / 2, fanSpread / 2, pellet / (float)Mathf.Max(1, fanCount - 1));
                Fire(Quaternion.Euler(0, 0, angle + (volley == 0 ? 0 : secondVolleyOffset)) * CommittedDirection, shotgunSpeed);
            }
            if (volley + 1 < volleyCount) yield return WaitCombat(volleyInterval);
        }
        Attack = AttackStage.Recovery;
        yield return WaitCombat(shotgunRecovery);
    }

    private IEnumerator SniperCycle()
    {
        for (int shot = 0; shot < sniperShots; shot++)
        {
            Attack = AttackStage.Aim;
            float elapsed = 0;
            while (elapsed < sniperAim && State == CoreState.CombatActive)
            {
                if (!GameplayPauseManager.IsPaused)
                {
                    CommittedDirection = AimAtPlayer();
                    ShowTelegraph(CommittedDirection, 0, 16f, .025f, cleanColor);
                    elapsed += Time.deltaTime;
                }
                yield return null;
            }
            Attack = AttackStage.Locked;
            ShowTelegraph(CommittedDirection, 0, 16f, .07f, Color.Lerp(cleanColor, Color.white, .65f));
            yield return WaitCombat(sniperLock);
            HideTelegraph();
            Attack = AttackStage.Firing;
            Fire(CommittedDirection, sniperSpeed);
            Attack = AttackStage.Recovery;
            yield return WaitCombat(sniperRecovery);
        }
        yield return WaitCombat(sniperCycleRecovery);
    }

    private IEnumerator MachineGunCycle()
    {
        CommittedDirection = AimAtPlayer();
        Attack = AttackStage.Locked;
        ShowTelegraph(CommittedDirection, machineGunSweep, 4.5f, .035f, cleanColor);
        yield return WaitCombat(machineGunWarmup);
        HideTelegraph();
        Attack = AttackStage.Firing;
        visual.color = Color.Lerp(cleanColor, Color.white, .25f);
        for (int shot = 0; shot < machineGunShots; shot++)
        {
            float angle = Mathf.Lerp(-machineGunSweep / 2, machineGunSweep / 2, shot / (float)Mathf.Max(1, machineGunShots - 1));
            Fire(Quaternion.Euler(0, 0, angle) * CommittedDirection, machineGunSpeed);
            yield return WaitCombat(machineGunInterval);
        }
        Attack = AttackStage.Recovery;
        visual.color = new Color(cleanColor.r * .5f, cleanColor.g * .5f, cleanColor.b * .5f, 1);
        yield return WaitCombat(machineGunRecovery);
        visual.color = cleanColor;
    }

    private void ShowTelegraph(Vector2 direction, float spread, float length, float width, Color color)
    {
        if (telegraph == null || State != CoreState.CombatActive) return;
        telegraph.enabled = true;
        telegraph.startWidth = telegraph.endWidth = width;
        telegraph.startColor = telegraph.endColor = color;
        telegraph.positionCount = spread > 0 ? 4 : 2;
        Vector3 origin = transform.position;
        telegraph.SetPosition(0, origin);
        telegraph.SetPosition(1, origin + Quaternion.Euler(0, 0, -spread / 2) * (Vector3)direction * length);
        if (spread > 0)
        {
            telegraph.SetPosition(2, origin);
            telegraph.SetPosition(3, origin + Quaternion.Euler(0, 0, spread / 2) * (Vector3)direction * length);
        }
    }

    private void HideTelegraph()
    {
        if (telegraph != null) telegraph.enabled = false;
    }

    private void Fire(Vector2 direction, float speed)
    {
        if (State != CoreState.CombatActive || GameplayPauseManager.IsPaused ||
            projectile == null || projectile.ProjectilePrefab == null || PoolManager.Instance == null)
        {
            return;
        }
        GameObject shot = PoolManager.Instance.Get(projectile.ProjectilePrefab, transform.position, Quaternion.identity);
        if (shot == null)
        {
            return;
        }
        Bullet bullet = shot.GetComponent<Bullet>();
        bullet.Initialize(direction, ProjectileOwner.Enemy, projectile, speedOverride: speed,
            rangeOverride: 16f, projectileSource: gameObject);
        bullet.ConfigureProjectileColor(cleanColor);
        ShotsFired++;
    }

    private void HandleDefeat(EnemyHealth defeated)
    {
        if (State != CoreState.CombatActive || defeated != health || !health.IsDead)
        {
            return;
        }
        State = CoreState.AwaitingReactivation;
        StopCombat();
        combatCollider.enabled = false;
        interactionCollider.enabled = true;
        visual.color = new Color(cleanColor.r * 0.4f, cleanColor.g * 0.4f, cleanColor.b * 0.4f, 0.8f);
        encounter.CoreDefeated(this);
    }

    public bool CanInteract(GameObject interactor)
    {
        return State == CoreState.AwaitingReactivation && interactor != null &&
            encounter != null && encounter.IsActive && !GameplayPauseManager.IsPaused &&
            !SettlementExpeditionLaunchGuard.IsDialogueActive;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }
        State = CoreState.Fusing; // Claim before any tween or callback can reenter.
        interactionCollider.enabled = false;
        try
        {
            fusion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            fusion.Append(visual.DOColor(cleanColor, 0.2f));
            fusion.Join(visual.transform.DOPunchScale(Vector3.one * 0.18f, 0.2f, 2));
            fusion.Append(transform.DOMoveY(transform.position.y + 0.5f, 0.18f));
            fusion.AppendCallback(() => transform.SetParent(center, true));
            // Created after reparenting so its starting local position is resolved correctly.
            fusion.AppendCallback(BeginFlight);
        }
        catch (System.Exception)
        {
            FinishFusion(); // Visual failure cannot strand an explicitly reactivated core.
        }
    }

    private void BeginFlight()
    {
        if (State != CoreState.Fusing)
        {
            return;
        }
        try
        {
            fusion = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            fusion.Append(transform.DOLocalMove(Vector3.zero, 0.45f).SetEase(Ease.InQuad));
            fusion.Join(visual.transform.DOScale(Vector3.zero, 0.45f));
            fusion.Join(visual.DOFade(0f, 0.45f));
            fusion.OnComplete(FinishFusion);
        }
        catch (System.Exception)
        {
            FinishFusion();
        }
    }

    private void FinishFusion()
    {
        if (State != CoreState.Fusing)
        {
            return;
        }
        fusion?.Kill();
        fusion = null;
        State = CoreState.Fused;
        visual.enabled = false;
        encounter.CoreFused(this);
    }

    private void StopCombat()
    {
        Attack = AttackStage.Idle;
        HideTelegraph();
        if (combat != null)
        {
            StopCoroutine(combat);
            combat = null;
        }
        Bullet.ReleaseAllActiveFromSource(transform);
    }

    public void Cancel()
    {
        State = CoreState.Cancelled;
        StopCombat();
        fusion?.Kill();
        fusion = null;
        if (configured)
        {
            health.Died -= HandleDefeat;
        }
        configured = false;
        combatCollider.enabled = false;
        interactionCollider.enabled = false;
    }

    private void OnDisable()
    {
        bool interrupted = configured && State != CoreState.Fused && State != CoreState.Cancelled;
        Cancel();
        if (interrupted && encounter != null)
        {
            encounter.FailEncounter();
        }
    }
}
