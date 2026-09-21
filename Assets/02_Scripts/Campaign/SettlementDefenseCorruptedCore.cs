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
        projectile.ProjectilePrefab.GetComponent<Bullet>() != null;
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
        var spacing = new WaitForSeconds(0.18f);
        var recovery = new WaitForSeconds(Part == BossStoryPart.SectorStabilizer ? 1.4f : 0.9f);
        yield return recovery;
        float radialOffset = 0f;
        while (State == CoreState.CombatActive)
        {
            if (GameplayPauseManager.IsPaused || player == null)
            {
                yield return spacing;
                continue;
            }
            int volleys = Part == BossStoryPart.PhaseNavigationLens ? 3 :
                Part == BossStoryPart.MatterCompressor ? 2 : 1;
            for (int volley = 0; volley < volleys && State == CoreState.CombatActive; volley++)
            {
                Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
                if (Part == BossStoryPart.SectorStabilizer)
                {
                    for (int shot = 0; shot < 10; shot++)
                    {
                        Fire(Quaternion.Euler(0f, 0f, radialOffset + shot * 36f) * Vector2.up, 4.2f);
                    }
                    radialOffset += 18f;
                }
                else if (Part == BossStoryPart.PhaseNavigationLens)
                {
                    Fire(direction, 6f); // Each shot reacquires the player, with readable spacing.
                }
                else
                {
                    for (int shot = -2; shot <= 2; shot++)
                    {
                        Fire(Quaternion.Euler(0f, 0f, shot * 13f) * direction, 7f);
                    }
                }
                yield return spacing;
            }
            yield return recovery;
        }
    }

    private void Fire(Vector2 direction, float speed)
    {
        if (State != CoreState.CombatActive || GameplayPauseManager.IsPaused ||
            projectile == null || projectile.ProjectilePrefab == null)
        {
            return;
        }
        GameObject shot = PoolManager.Instance != null
            ? PoolManager.Instance.Get(projectile.ProjectilePrefab, transform.position, Quaternion.identity)
            : Instantiate(projectile.ProjectilePrefab, transform.position, Quaternion.identity);
        if (shot == null)
        {
            return;
        }
        Bullet bullet = shot.GetComponent<Bullet>();
        bullet.Initialize(direction, ProjectileOwner.Enemy, projectile, speedOverride: speed,
            rangeOverride: 16f, projectileSource: gameObject);
        bullet.ConfigureProjectileColor(cleanColor);
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
