using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PixelCrushers.DialogueSystem;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public sealed class NullDispatcherBossController : MonoBehaviour, IFinalBossTreatmentAuthority
{
    public enum EncounterPhase { Intro, Phase1, TreatmentDialogue, PolarityPhase, Dead, ReclaimBeam, RejectTransition, SettlementIntervention, FinalTransition, FinalPhase }
    public enum Polarity { White, Black }
    public enum TreatmentChoice { None, Accept, Reject }
    public enum FinalCombination { PartitionPackets, RedirectCompression }
    public enum Pattern { RoutePartition, CompressionDispatch, PhaseRedirect }

    [SerializeField] private EnemyHealth health;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private BossLaserHazard laserPrefab;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private ProjectileDefinition projectile;
    [SerializeField] private SpriteRenderer[] relays;
    [SerializeField] private DialogueStoryEntryPoint treatmentEntry;
    [SerializeField, Min(0.5f)] private float telegraphSeconds = 0.8f;
    [SerializeField, Min(0.7f)] private float recoverySeconds = 0.9f;

    [Header("Reclaim and Settlement intervention")]
    [SerializeField] private LineRenderer reclaimBeam;
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private SpriteRenderer breakPulse;
    [SerializeField] private SettlementFinalSupportController settlementSupport;
    [SerializeField, Min(0.1f)] private float reclaimDuration = 4.5f;
    [SerializeField, Min(0f)] private float pullSpeed = 4f;
    [SerializeField, Min(0f)] private float drainMaxHpFractionPerSecond = 0.111112f;
    [SerializeField, Min(0.1f)] private float rejectDuration = 0.75f;
    [SerializeField, Min(0.1f)] private float interventionDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float breakPushDuration = 0.3f;

    [Header("Phase 2 polarity")]
    [SerializeField] private NullDispatcherPolarityPacket hostilePacketPrefab;
    [SerializeField] private NullDispatcherPolarityPacket supportPacketPrefab;
    [SerializeField, Min(1)] private int patternsPerPolarity = 2;
    [SerializeField, Min(0.1f)] private float switchSeconds = 0.5f;
    [SerializeField, Min(1f)] private float supportPacketInterval = 5f;
    [SerializeField, Min(0f)] private float supportPacketHeal = 1f;
    [SerializeField, Min(0f)] private float hostilePacketDamage = 1f;
    [Header("Final shell break")]
    [SerializeField] private GameObject outerShellRoot;
    [SerializeField] private SpriteRenderer innerCore;
    [SerializeField] private SpriteRenderer[] shellPieces;
    [SerializeField] private BossDeathPresentation deathPresentation;
    [SerializeField, Min(0.1f)] private float finalTransitionSeconds = 2f;
    [SerializeField, Min(0.1f)] private float finalRecoverySeconds = 0.8f;
    [SerializeField, Min(1f)] private float finalSupportPacketInterval = 3.5f;
    private readonly object finalPhaseOwner = new object();
    private Coroutine finalTransition;
    private Tween shellTween;
    private Vector3[] shellPositions;
    private Quaternion[] shellRotations;
    private Color[] shellColors;
    private Vector3 innerScale;
    private Color innerColor;
    private float finalTransitionElapsed;
    private int lastCombination = -1;
    private bool lanesDamaging;
    public int FinalTransitionCount { get; private set; }
    public bool InnerCoreExposed { get; private set; }
    public int ActiveMajorMechanics { get; private set; }
    public bool PolarityCombatActive => Phase == EncounterPhase.PolarityPhase || Phase == EncounterPhase.FinalPhase;
    public float CurrentSupportPacketInterval => Phase == EncounterPhase.FinalPhase ? finalSupportPacketInterval : supportPacketInterval;
    public static FinalCombination NextFinalCombination(int previous) => (FinalCombination)((previous + 1) % 2);
    // Explicit budgets for the two intentionally reduced final combinations.
    public const int PartitionPacketCount = 4;
    public const int RedirectBurstCount = 2;
    public const int RedirectPacketsPerBurst = 2;
    public const int CompressionFanCount = 3;

    private const int MaxPolarityPackets = 26;
    private readonly List<NullDispatcherPolarityPacket> packets = new List<NullDispatcherPolarityPacket>(MaxPolarityPackets);
    private Coroutine polarityRuntime;
    private bool mainPatternActive;
    private int completedPolarityPatterns;
    private float supportElapsed;
    private float switchElapsed;
    public Polarity PlayerPolarity { get; private set; } = Polarity.White;
    public bool PolaritySwitching { get; private set; }
    public int ActivePolarityPacketCount => packets.Count;
    public bool PolarityPacketsAllowed => PolarityCombatActive && !PolaritySwitching &&
        health != null && !health.IsDead && (!Application.isPlaying || CanContinue());
    public static Polarity PacketPolarity(Polarity playerPolarity, NullDispatcherPolarityPacket.PacketKind kind) =>
        kind == NullDispatcherPolarityPacket.PacketKind.Support ? playerPolarity :
        playerPolarity == Polarity.White ? Polarity.Black : Polarity.White;

    private readonly List<BossLaserHazard> lasers = new List<BossLaserHazard>(3);
    private readonly List<Bullet> shots = new List<Bullet>(40);
    private PlayerController2D player;
    private PlayerWeaponController weapon;
    private PlayerHealth playerHealth;
    private GameStateManager state;
    private GungeonStyleCamera2D cameraRig;
    private DialoguePixelCrushersBridge bridge;
    private RunManager run;
    private Coroutine patterns;
    private Coroutine treatment;
    private Tween visualTween;
    private Vector3 bodyScale;
    private Color bodyColor;
    private bool configured;
    private bool ownsInput;
    private bool offering;
    private TreatmentChoice pendingChoice;
    private int lastPattern = -1;
    private Bounds arena;
    private readonly object branchOwner = new object();
    private Coroutine branchRoutine;
    private Tween branchVisualTween;
    private ExpeditionHUD hud;
    private float branchElapsed;
    private float beamHpFloor;
    private float beamMaxHp;
    private float beamWidth;
    private Vector3 pulseScale;
    private Color pulseColor;
    private bool supportRequested;
    private bool requirePlayer;

    public bool SupportRequested => supportRequested;

    public EncounterPhase Phase { get; private set; } = EncounterPhase.Intro;
    public TreatmentChoice Choice { get; private set; }
    public bool PatternRunning => patterns != null;
    public bool PatternDamageEnabled { get; private set; }
    public int TreatmentGateCount { get; private set; }
    public static Pattern NextPattern(int previous) => (Pattern)((previous + 1) % 3);

    public bool ConfigureEncounter(ExpeditionDepth depth, GameObject actor)
    {
        if (configured || depth != ExpeditionDepth.FinalNetwork)
        {
            return false;
        }
        health = health != null ? health : GetComponent<EnemyHealth>();
        player = actor != null ? actor.GetComponent<PlayerController2D>() : null;
        weapon = actor != null ? actor.GetComponent<PlayerWeaponController>() : null;
        playerHealth = actor != null ? actor.GetComponent<PlayerHealth>() : null;
        requirePlayer = actor != null;
        hud = Application.isPlaying ? FindFirstObjectByType<ExpeditionHUD>() : null;
        if (reclaimBeam != null) beamWidth = reclaimBeam.widthMultiplier;
        if (breakPulse != null)
        {
            pulseScale = breakPulse.transform.localScale;
            pulseColor = breakPulse.color;
        }
        if (settlementSupport != null)
        {
            settlementSupport.ConfigureFinalBoss(health);
            settlementSupport.Completed += HandleSupportCompleted;
        }
        if (playerHealth != null) playerHealth.Died += CancelEncounter;
        state = GameStateManager.Instance;
        if (state != null) state.StateChanged += HandleStateChanged;
        cameraRig = GungeonStyleCamera2D.Instance;
        CacheFinalVisuals();
        configured = true;
        Phase = EncounterPhase.Intro;
        Choice = pendingChoice = TreatmentChoice.None;
        TreatmentGateCount = 0;
        lastPattern = -1;
        if (body != null)
        {
            bodyScale = body.transform.localScale;
            bodyColor = body.color;
        }
        health.SetDamageFloor(this, 1f);
        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDeath;
        run = RunManager.Instance;
        if (run != null)
        {
            run.RunEnded += HandleRunEnded;
        }
        if (treatmentEntry != null)
        {
            treatmentEntry.Completed += HandleConversationEnded;
        }
        return true;
    }

    public bool BeginCombat()
    {
        if (!configured || Phase != EncounterPhase.Intro || health.IsDead)
        {
            return false;
        }
        Phase = EncounterPhase.Phase1;
        health.SetDamageFloor(this, 0.5f);
        StartPatterns();
        return true;
    }

    private void StartPatterns()
    {
        if (patterns == null && Application.isPlaying && isActiveAndEnabled)
        {
            patterns = StartCoroutine(PatternLoop());
        }
    }

    private bool CanContinue()
    {
        return configured && Phase != EncounterPhase.Dead && health != null && !health.IsDead &&
            run != null && run.HasActiveRun && !run.IsCompletingRun &&
            run.CurrentRun.ExpeditionDepth == ExpeditionDepth.FinalNetwork &&
            (playerHealth == null || !playerHealth.IsDead) &&
            (!requirePlayer || (player != null && playerHealth != null)) &&
            (SceneFlowManager.Instance == null || !SceneFlowManager.Instance.IsLoading);
    }

    private IEnumerator PatternLoop()
    {
        // A single scheduler owns every nested pattern; no parallel attack loops.
        yield return new WaitForSeconds(recoverySeconds);
        while (CanContinue() && (Phase == EncounterPhase.Phase1 || PolarityCombatActive))
        {
            if (GameplayPauseManager.IsPaused)
            {
                yield return null;
                continue;
            }
            ResolveArena();
            if (Phase == EncounterPhase.FinalPhase)
            {
                lastCombination = (int)NextFinalCombination(lastCombination);
                mainPatternActive = true;
                yield return FinalCombinationRoutine((FinalCombination)lastCombination);
                mainPatternActive = false;
                ClearPatternObjects();
                ClearPolarityPackets();
                CompleteMainPattern();
                while (PolaritySwitching && CanContinue()) yield return null;
                yield return new WaitForSeconds(finalRecoverySeconds);
                continue;
            }
            Pattern next = NextPattern(lastPattern);
            lastPattern = (int)next;
            mainPatternActive = true;
            switch (next)
            {
                case Pattern.RoutePartition: yield return RoutePartition(); break;
                case Pattern.CompressionDispatch: yield return CompressionDispatch(); break;
                case Pattern.PhaseRedirect: yield return PhaseRedirect(); break;
            }
            mainPatternActive = false;
            ClearPatternObjects();
            CompleteMainPattern();
            while (PolaritySwitching && CanContinue()) yield return null;
            yield return new WaitForSeconds(recoverySeconds);
        }
        patterns = null;
    }

    private void ResolveArena()
    {
        Camera view = Camera.main;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(12f, 7f, 1f);
        if (view != null && view.orthographic)
        {
            center = view.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, -view.transform.position.z));
            size = new Vector3(view.orthographicSize * view.aspect * 2f - 1f,
                view.orthographicSize * 2f - 1f, 1f);
        }
        center.z = transform.position.z;
        arena = new Bounds(center, size);
    }

    private IEnumerator RoutePartition()
    {
        CreateLanes(false);
        yield return WaitForTelegraph();
        if (!CanContinue()) yield break;
        ClearLasers();
        CreateLanes(true);
        yield return new WaitForSeconds(2.4f);
    }

    private void CreateLanes(bool damaging)
    {
        if (damaging && !PatternDamageEnabled) return;
        lanesDamaging = damaging;
        for (int lane = -1; lane <= 1; lane++)
        {
            float y = arena.center.y + lane * arena.extents.y * 0.6f;
            GameObject instance = PoolManager.Instance != null
                ? PoolManager.Instance.Get(laserPrefab.gameObject, Vector3.zero, Quaternion.identity)
                : Instantiate(laserPrefab.gameObject);
            var laser = instance.GetComponent<BossLaserHazard>();
            lasers.Add(laser);
            // Three narrow horizontal lanes leave generous, visible escape gaps.
            laser.InitializeBetween(new Vector2(arena.min.x, y), new Vector2(arena.max.x, y),
                damaging ? 0.18f : 0.07f, 10f, damaging ? 2f : 0f, 0.4f,
                lineMaterial, damaging ? new Color(0.8f, 0.2f, 1f) : new Color(0.5f, 1f, 0.65f, 0.65f),
                "Default", 70);
        }
    }

    private void ShowRelays(Color color)
    {
        for (int i = 0; i < 2; i++)
        {
            relays[i].transform.position = arena.center + new Vector3((i == 0 ? -1f : 1f) * arena.extents.x * 0.7f,
                arena.extents.y * 0.5f, 0f);
            relays[i].color = color;
            relays[i].enabled = true;
        }
    }

    private IEnumerator CompressionDispatch()
    {
        ShowRelays(new Color(1f, 0.55f, 0.15f));
        yield return WaitForTelegraph();
        if (!CanContinue()) yield break;
        for (int group = 0; group < 3; group++)
        {
            if (!CanContinue()) yield break;
            Vector2 sample = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
            for (int side = 0; side < 2; side++)
            {
                FireFan(relays[side].transform.position, sample, 5, 18f, 4.5f, new Color(1f, 0.55f, 0.15f));
            }
            yield return new WaitForSeconds(0.55f);
        }
        Vector2 finalAim = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
        yield return new WaitForSeconds(0.4f);
        FireFan(transform.position, finalAim, 5, 12f, 6f, new Color(1f, 0.55f, 0.15f));
        yield return new WaitForSeconds(0.7f);
    }

    private IEnumerator PhaseRedirect()
    {
        ShowRelays(new Color(0.35f, 0.75f, 1f));
        yield return WaitForTelegraph();
        if (!CanContinue()) yield break;
        for (int burst = 0; burst < 4; burst++)
        {
            if (!CanContinue()) yield break;
            Vector2 entry = relays[0].transform.position;
            Vector2 delta = entry - (Vector2)transform.position;
            if (PolarityCombatActive)
            {
                // Relay charge travels through the network; only its exit emits a hostile packet.
                yield return new WaitForSeconds(delta.magnitude / 10f);
                Vector2 aim = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
                FireFan(relays[1].transform.position, aim, 3, 16f, 5f, Color.white);
                yield return new WaitForSeconds(0.4f);
                continue;
            }
            Bullet routed = Fire(transform.position, delta.normalized, 10f, Color.cyan, 0f);
            yield return new WaitForSeconds(delta.magnitude / 10f);
            if (CanContinue() && routed != null && routed.isActiveAndEnabled && routed.SourceRoot == transform)
            {
                // Reuse the same projectile; its entry leg is harmless. No portal framework.
                routed.transform.position = relays[1].transform.position;
                Vector2 target = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
                routed.Initialize((target - (Vector2)routed.transform.position).normalized,
                    ProjectileOwner.Enemy, projectile, damageOverride: 2f, speedOverride: 6f,
                    rangeOverride: 16f, projectileSource: gameObject);
                routed.ConfigureProjectileColor(Color.cyan);
            }
            yield return new WaitForSeconds(0.4f);
        }
    }

    private void FireFan(Vector2 origin, Vector2 target, int count, float spacing, float speed, Color color)
    {
        Vector2 direction = (target - origin).normalized;
        for (int i = 0; i < count; i++)
        {
            Fire(origin, Quaternion.Euler(0f, 0f, (i - (count - 1) * 0.5f) * spacing) * direction, speed, color, 2f);
        }
    }

    private Bullet Fire(Vector2 origin, Vector2 direction, float speed, Color color, float damage)
    {
        if (!PatternDamageEnabled || !CanContinue())
        {
            return null;
        }
        if (PolarityCombatActive)
        {
            SpawnPolarityPacket(NullDispatcherPolarityPacket.PacketKind.Hostile, origin, direction * speed);
            return null;
        }
        GameObject shot = PoolManager.Instance != null
            ? PoolManager.Instance.Get(projectile.ProjectilePrefab, origin, Quaternion.identity)
            : Instantiate(projectile.ProjectilePrefab, origin, Quaternion.identity);
        var bullet = shot.GetComponent<Bullet>();
        shots.Add(bullet);
        bullet.Initialize(direction, ProjectileOwner.Enemy, projectile, damageOverride: damage,
            speedOverride: speed, rangeOverride: 16f, projectileSource: gameObject);
        bullet.ConfigureProjectileColor(color);
        return bullet;
    }

    private IEnumerator FinalCombinationRoutine(FinalCombination combination)
    {
        if (combination == FinalCombination.PartitionPackets) CreateLanes(false);
        else ShowRelays(new Color(0.7f, 0.45f, 1f));
        yield return WaitForTelegraph();
        if (!CanContinue() || Phase != EncounterPhase.FinalPhase || !PatternDamageEnabled) yield break;
        ActiveMajorMechanics = 2;
        if (combination == FinalCombination.PartitionPackets)
        {
            ClearLasers();
            CreateLanes(true);
            // Four isolated shots across 2.4 seconds, rather than the full compression fans.
            for (int i = 0; i < PartitionPacketCount; i++)
            {
                if (!CanContinue() || Phase != EncounterPhase.FinalPhase) yield break;
                Vector2 target = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
                FireFan(transform.position, target, 1, 0f, 3.5f, Color.white);
                yield return new WaitForSeconds(0.6f);
            }
        }
        else
        {
            // Two reduced relay bursts and ONE small compression fan; no lasers in this combination.
            for (int i = 0; i < RedirectBurstCount; i++)
            {
                yield return new WaitForSeconds(0.4f);
                if (!CanContinue() || Phase != EncounterPhase.FinalPhase) yield break;
                Vector2 target = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
                FireFan(relays[1].transform.position, target, RedirectPacketsPerBurst, 22f, 4f, Color.white);
                if (i == 0) FireFan(transform.position, target, CompressionFanCount, 25f, 3.5f, Color.white);
                yield return new WaitForSeconds(0.6f);
            }
        }
    }

    private void CacheFinalVisuals()
    {
        if (shellPositions != null) return;
        int count = shellPieces != null ? shellPieces.Length : 0;
        shellPositions = new Vector3[count];
        shellRotations = new Quaternion[count];
        shellColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            if (shellPieces[i] == null) continue;
            shellPositions[i] = shellPieces[i].transform.localPosition;
            shellRotations[i] = shellPieces[i].transform.localRotation;
            shellColors[i] = shellPieces[i].color;
        }
        if (innerCore != null) { innerScale = innerCore.transform.localScale; innerColor = innerCore.color; }
    }

    private void BeginFinalTransition()
    {
        if (Phase != EncounterPhase.PolarityPhase || FinalTransitionCount != 0) return;
        Phase = EncounterPhase.FinalTransition; // Revoke all spawn/collision authority first.
        FinalTransitionCount++;
        StopPatterns();
        if (polarityRuntime != null) { StopCoroutine(polarityRuntime); polarityRuntime = null; }
        PolaritySwitching = false;
        ClearPolarityPackets();
        StopBranchVisuals();
        finalTransitionElapsed = 0f;
        weapon?.SetExternalInputLocked(finalPhaseOwner, true);
        ShowBranchMessage("final.phase.shell_break");
        PlayShellBreak();
        if (Application.isPlaying && isActiveAndEnabled)
            finalTransition = StartCoroutine(FinalTransitionRoutine());
    }

    private IEnumerator FinalTransitionRoutine()
    {
        while (Phase == EncounterPhase.FinalTransition)
        {
            if (!CanContinue()) { CancelEncounter(); yield break; }
            AdvanceFinalTransition(Time.deltaTime);
            yield return null;
        }
        finalTransition = null;
    }

    private void AdvanceFinalTransition(float deltaTime)
    {
        if (Phase != EncounterPhase.FinalTransition || deltaTime <= 0f || GameplayPauseManager.IsPaused) return;
        finalTransitionElapsed += deltaTime;
        if (finalTransitionElapsed >= finalTransitionSeconds) CompleteFinalTransition();
    }

    private void PlayShellBreak()
    {
        CacheFinalVisuals();
        if (!Application.isPlaying) return;
        try
        {
            var sequence = DOTween.Sequence(); // Scaled visual timing; gameplay completion is independent.
            shellTween = sequence;
            float duration = Mathf.Max(0.1f, finalTransitionSeconds);
            if (body != null)
            {
                sequence.Insert(0f, body.transform.DOPunchScale(bodyScale * 0.2f, duration * 0.4f, 6));
                sequence.Insert(duration * 0.35f, body.DOFade(0f, duration * 0.45f));
            }
            for (int i = 0; i < shellPositions.Length; i++)
            {
                var piece = shellPieces[i];
                if (piece == null) continue;
                Vector3 outward = shellPositions[i].sqrMagnitude > 0.001f ? shellPositions[i].normalized : Vector3.up;
                sequence.Insert(duration * 0.3f, piece.transform.DOLocalMove(shellPositions[i] + outward * 2f, duration * 0.6f).SetEase(Ease.OutCubic));
                sequence.Insert(duration * 0.3f, piece.transform.DOLocalRotate(new Vector3(0f, 0f, i % 2 == 0 ? 80f : -80f), duration * 0.6f));
                sequence.Insert(duration * 0.5f, piece.DOFade(0f, duration * 0.4f));
            }
            if (innerCore != null)
            {
                innerCore.gameObject.SetActive(true);
                innerCore.color = new Color(innerColor.r, innerColor.g, innerColor.b, 0f);
                sequence.Insert(duration * 0.4f, innerCore.DOFade(innerColor.a, duration * 0.4f));
                sequence.Insert(duration * 0.55f, innerCore.transform.DOPunchScale(innerScale * 0.25f, duration * 0.4f, 4));
            }
            AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);
        }
        catch (System.Exception) { StopShellPresentation(); } // The independent timer still exposes the core.
    }

    private void StopShellPresentation()
    {
        shellTween?.Kill();
        shellTween = null;
        if (shellPositions == null) return;
        for (int i = 0; i < shellPositions.Length; i++)
        {
            if (shellPieces[i] == null) continue;
            shellPieces[i].transform.localPosition = shellPositions[i];
            shellPieces[i].transform.localRotation = shellRotations[i];
            shellPieces[i].color = shellColors[i];
        }
        if (innerCore != null) { innerCore.transform.localScale = innerScale; innerCore.color = innerColor; }
    }

    private void CompleteFinalTransition()
    {
        if (Phase != EncounterPhase.FinalTransition) return;
        if (Application.isPlaying && !CanContinue()) { CancelEncounter(); return; }
        StopShellPresentation();
        if (outerShellRoot != null) outerShellRoot.SetActive(false);
        if (innerCore != null)
        {
            innerCore.gameObject.SetActive(true);
            body = innerCore;
            bodyScale = innerScale;
            bodyColor = innerColor;
            deathPresentation?.SetBodyRenderer(innerCore);
        }
        InnerCoreExposed = true;
        health.RemoveDamageFloor(finalPhaseOwner);
        weapon?.SetExternalInputLocked(finalPhaseOwner, false);
        Phase = EncounterPhase.FinalPhase;
        completedPolarityPatterns = 0;
        supportElapsed = 0f;
        lastCombination = -1;
        UpdatePolarityHud();
        ShowBranchMessage("final.phase.inner_core");
        if (Application.isPlaying && isActiveAndEnabled) polarityRuntime = StartCoroutine(PolarityRuntime());
        StartPatterns();
    }

    private void HandleHealthChanged(EnemyHealth source, float hp, float maximum)
    {
        if (Phase == EncounterPhase.PolarityPhase && maximum > 0f && hp <= maximum * 0.2f)
        {
            BeginFinalTransition();
            return;
        }
        if (Phase != EncounterPhase.Phase1 || maximum <= 0f || hp > maximum * 0.5f)
        {
            return;
        }
        Phase = EncounterPhase.TreatmentDialogue;
        TreatmentGateCount++;
        StopPatterns();
        if (Application.isPlaying && isActiveAndEnabled)
        {
            treatment = StartCoroutine(TreatmentRoutine());
        }
    }

    private IEnumerator WaitForTelegraph()
    {
        PatternDamageEnabled = false;
        yield return new WaitForSeconds(telegraphSeconds);
        PatternDamageEnabled = CanContinue() &&
            (Phase == EncounterPhase.Phase1 || PolarityCombatActive);
    }

    public bool TryRequestTreatmentChoice(bool accept)
    {
        if (Phase != EncounterPhase.TreatmentDialogue || !offering ||
            Choice != TreatmentChoice.None || pendingChoice != TreatmentChoice.None)
        {
            return false;
        }
        // A response is provisional until the graph reaches its natural END marker.
        pendingChoice = accept ? TreatmentChoice.Accept : TreatmentChoice.Reject;
        return true;
    }

    private IEnumerator TreatmentRoutine()
    {
        var retryDelay = new WaitForSecondsRealtime(0.4f);
        bool warned = false;
        while (CanContinue() && Phase == EncounterPhase.TreatmentDialogue)
        {
            if (offering || DialogueManager.isConversationActive || GameplayPauseManager.IsPaused)
            {
                yield return retryDelay;
                continue;
            }
            if (!DialogueManager.hasInstance || treatmentEntry == null ||
                DialogueManager.MasterDatabase == null ||
                DialogueManager.MasterDatabase.GetConversation(NullDispatcherDialogueIds.Conversation) == null)
            {
                if (!warned)
                {
                    Debug.LogError("Install and validate FINAL_NullDispatcherTreatmentOffer before testing the final encounter.", this);
                    warned = true;
                }
                yield return retryDelay;
                continue;
            }
            bridge = DialogueManager.instance.GetComponent<DialoguePixelCrushersBridge>();
            if (bridge == null || !bridge.isActiveAndEnabled || !bridge.RegisterFinalBossAuthority(this))
            {
                yield return retryDelay;
                continue;
            }
            AcquireCinematic();
            yield return new WaitForSecondsRealtime(0.35f);
            if (!CanContinue())
            {
                break;
            }
            offering = true;
            if (!treatmentEntry.TryStartConversation(NullDispatcherDialogueIds.Conversation,
                player != null ? player.transform : null, transform))
            {
                offering = false;
                ReleaseCinematic();
            }
            yield return retryDelay;
        }
        treatment = null;
        if (Phase == EncounterPhase.TreatmentDialogue)
        {
            CancelEncounter();
        }
    }

    private void HandleConversationEnded(DialogueStoryEntryPoint entry)
    {
        FinishTreatmentDialogue(bridge != null && bridge.WasConversationCompletedNaturally(NullDispatcherDialogueIds.Conversation));
    }

    public void FinishTreatmentDialogue(bool naturalCompletion)
    {
        if (Phase != EncounterPhase.TreatmentDialogue || !offering)
        {
            return;
        }
        offering = false;
        ReleaseCinematic();
        if (!naturalCompletion || pendingChoice == TreatmentChoice.None || (Application.isPlaying && !CanContinue()))
        {
            pendingChoice = TreatmentChoice.None;
            return; // The one treatment coroutine re-offers; interruption is never Reject.
        }
        Choice = pendingChoice;
        pendingChoice = TreatmentChoice.None;
        bridge?.UnregisterFinalBossAuthority(this);
        BeginChoiceBranch();
    }

    public static float CalculateReclaimFloor(float startHp, float maximumHp)
    {
        return Mathf.Min(startHp, Mathf.Max(1f, maximumHp * 0.5f));
    }

    public static float CalculateDrainedHp(float currentHp, float floor, float amount)
    {
        // Never heal, including after unrelated damage lowered HP beneath the captured floor.
        return Mathf.Min(currentHp, Mathf.Max(floor, currentHp - Mathf.Max(0f, amount)));
    }

    private void BeginChoiceBranch()
    {
        StopPatterns();
        branchElapsed = 0f;
        weapon?.SetExternalInputLocked(branchOwner, true);
        Phase = Choice == TreatmentChoice.Accept ? EncounterPhase.ReclaimBeam : EncounterPhase.RejectTransition;
        if (Phase == EncounterPhase.ReclaimBeam)
        {
            beamMaxHp = playerHealth != null ? playerHealth.MaxHp : 1f;
            beamHpFloor = CalculateReclaimFloor(playerHealth != null ? playerHealth.CurrentHp : 1f, beamMaxHp);
            if (reclaimBeam != null)
            {
                reclaimBeam.enabled = true;
                UpdateBeamVisual();
            }
            ShowBranchMessage("final.reclaim.resist");
            if (Application.isPlaying) AudioManager.PlayAt(SoundEventIds.BossLaserWarning, transform.position);
        }
        else ShowBranchMessage("final.reclaim.reject");
        PlayBranchPulse(false);
        if (Application.isPlaying && isActiveAndEnabled && branchRoutine == null)
        {
            Coroutine started = StartCoroutine(ChoiceBranchRoutine());
            branchRoutine = Phase == EncounterPhase.Dead ? null : started;
        }
    }

    private IEnumerator ChoiceBranchRoutine()
    {
        // A single loop also observes cancellation while paused. Gameplay uses scaled delta only.
        while (Phase == EncounterPhase.ReclaimBeam || Phase == EncounterPhase.RejectTransition ||
            Phase == EncounterPhase.SettlementIntervention)
        {
            if (!CanContinue())
            {
                branchRoutine = null;
                CancelEncounter();
                yield break;
            }
            if (!GameplayPauseManager.IsPaused) AdvanceChoiceBranch(Time.deltaTime);
            if (Phase == EncounterPhase.ReclaimBeam) UpdateBeamVisual();
            yield return null;
        }
        branchRoutine = null;
    }

    private void AdvanceChoiceBranch(float deltaTime)
    {
        if (deltaTime <= 0f || GameplayPauseManager.IsPaused) return;
        if (Phase == EncounterPhase.ReclaimBeam)
        {
            float step = Mathf.Min(deltaTime, Mathf.Max(0f, reclaimDuration - branchElapsed));
            if (player != null && playerHealth != null && !playerHealth.IsDead)
            {
                Vector2 inward = (Vector2)transform.position - (Vector2)player.transform.position;
                player.SetExternalPushVelocity(branchOwner, inward.normalized * Mathf.Max(0f, pullSpeed));
                float nextHp = CalculateDrainedHp(playerHealth.CurrentHp, beamHpFloor,
                    beamMaxHp * drainMaxHpFractionPerSecond * step);
                if (nextHp < playerHealth.CurrentHp) playerHealth.RestoreCurrentHp(nextHp);
            }
            branchElapsed += step;
            if (branchElapsed >= reclaimDuration) BeginSettlementIntervention();
        }
        else if (Phase == EncounterPhase.RejectTransition)
        {
            branchElapsed += deltaTime;
            if (branchElapsed >= rejectDuration) BeginSettlementIntervention();
        }
        else if (Phase == EncounterPhase.SettlementIntervention && !supportRequested)
        {
            branchElapsed += deltaTime;
            if (branchElapsed >= breakPushDuration) player?.ClearExternalPush(branchOwner);
            if (branchElapsed >= Mathf.Max(interventionDuration, breakPushDuration)) RequestSettlementSupport();
        }
    }

    private void BeginSettlementIntervention()
    {
        if (Phase != EncounterPhase.ReclaimBeam && Phase != EncounterPhase.RejectTransition) return;
        StopBeam();
        Phase = EncounterPhase.SettlementIntervention;
        branchElapsed = 0f;
        ShowBranchMessage(Choice == TreatmentChoice.Accept ? "final.reclaim.severed" : "final.reclaim.intervention");
        PlayBranchPulse(true);
        if (Application.isPlaying) AudioManager.PlayAt(SoundEventIds.BossPhase2, transform.position);
        if (Choice == TreatmentChoice.Accept) PushPlayerAway();
    }

    private void PushPlayerAway()
    {
        if (player == null) return;
        Vector2 offset = (Vector2)player.transform.position - (Vector2)transform.position;
        float distance = Mathf.Clamp(3.2f - offset.magnitude, 0f, 2f);
        if (distance <= 0.05f) return;
        Vector2 away = offset.sqrMagnitude > 0.001f ? offset.normalized : Vector2.down;
        // Bounded, one-time destination checks; physics/normal player constraints own the travel.
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * ((i + 1) / 2) * 30f;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * away;
            if (player.IsRepositionDestinationValid((Vector2)player.transform.position + direction * distance))
            {
                player.SetExternalPushVelocity(branchOwner, direction * (distance / Mathf.Max(0.1f, breakPushDuration)));
                return;
            }
        }
    }

    private void RequestSettlementSupport()
    {
        if (Phase != EncounterPhase.SettlementIntervention || supportRequested) return;
        if (Application.isPlaying && !CanContinue()) { CancelEncounter(); return; }
        supportRequested = true;
        player?.ClearExternalPush(branchOwner);
        // Firing remains source-locked; remove old player shots before opening the support damage window.
        if (Application.isPlaying) Bullet.ReleaseAllActiveOwnedBy(ProjectileOwner.Player);
        // Reserve the final gate before Weapon Lab can cross it. The 50% owner is separate.
        health.SetDamageFloor(finalPhaseOwner, 0.2f);
        health.RemoveDamageFloor(this);
        if (settlementSupport == null)
        {
            Debug.LogError("NULL DISPATCHER requires its authored SettlementFinalSupportController binding.", this);
            CancelEncounter();
            return;
        }
        if (!settlementSupport.TriggerFullSupport() && settlementSupport.SequenceCompleted)
            HandleSupportCompleted();
    }

    private void HandleSupportCompleted()
    {
        if (Phase != EncounterPhase.SettlementIntervention || !supportRequested) return;
        if (Application.isPlaying && !CanContinue()) { CancelEncounter(); return; }
        Phase = EncounterPhase.PolarityPhase;
        weapon?.SetExternalInputLocked(branchOwner, false);
        StopBranchVisuals();
        PlayerPolarity = Polarity.White;
        completedPolarityPatterns = 0;
        supportElapsed = 0f;
        UpdatePolarityHud();
        if (health.HpRatio <= 0.2f) { BeginFinalTransition(); return; }
        if (Application.isPlaying && isActiveAndEnabled)
            polarityRuntime = StartCoroutine(PolarityRuntime());
        StartPatterns();
    }

    public bool RegisterPolarityPacket(NullDispatcherPolarityPacket packet)
    {
        if (!PolarityPacketsAllowed || packet == null || packets.Contains(packet) || packets.Count >= MaxPolarityPackets) return false;
        int sameKind = 0;
        foreach (var active in packets) if (active.Kind == packet.Kind) sameKind++;
        if (sameKind >= (packet.Kind == NullDispatcherPolarityPacket.PacketKind.Support ? 2 : 24)) return false;
        packets.Add(packet);
        return true;
    }

    public void UnregisterPolarityPacket(NullDispatcherPolarityPacket packet) => packets.Remove(packet);

    private void SpawnPolarityPacket(NullDispatcherPolarityPacket.PacketKind kind, Vector2 position, Vector2 velocity)
    {
        if (!PolarityPacketsAllowed || PoolManager.Instance == null || playerHealth == null || packets.Count >= MaxPolarityPackets) return;
        var prefab = kind == NullDispatcherPolarityPacket.PacketKind.Support ? supportPacketPrefab : hostilePacketPrefab;
        if (prefab == null) return; // Required authored references are covered by authoring validation.
        var instance = PoolManager.Instance.Get(prefab.gameObject, position, Quaternion.identity);
        var packet = instance.GetComponent<NullDispatcherPolarityPacket>();
        if (!packet.Initialize(this, playerHealth, PacketPolarity(PlayerPolarity, kind), velocity,
            kind == NullDispatcherPolarityPacket.PacketKind.Support ? supportPacketHeal : hostilePacketDamage,
            kind == NullDispatcherPolarityPacket.PacketKind.Support ? 8f : 4f))
            PoolManager.Instance.Release(instance);
    }

    private IEnumerator PolarityRuntime()
    {
        var fixedStep = new WaitForFixedUpdate();
        while (PolarityCombatActive)
        {
            yield return fixedStep;
            if (!CanContinue()) { CancelEncounter(); yield break; }
            AdvancePolarity(Time.fixedDeltaTime);
        }
        polarityRuntime = null;
    }

    private void AdvancePolarity(float deltaTime)
    {
        if (!PolarityCombatActive || deltaTime <= 0f || GameplayPauseManager.IsPaused) return;
        if (PolaritySwitching)
        {
            switchElapsed += deltaTime;
            if (switchElapsed < switchSeconds) return;
            // All packet colliders were disabled before the warning began.
            PlayerPolarity = PlayerPolarity == Polarity.White ? Polarity.Black : Polarity.White;
            completedPolarityPatterns = 0;
            PolaritySwitching = false;
            UpdatePolarityHud();
            return;
        }
        for (int i = packets.Count - 1; i >= 0; i--) packets[i].AdvanceFlight(deltaTime);
        supportElapsed += deltaTime;
        if (supportElapsed >= CurrentSupportPacketInterval)
        {
            // Defer supply until partition lanes are gone; never launch support into a live lane.
            if (lanesDamaging) return;
            supportElapsed = 0f;
            ResolveArena();
            Vector2 origin = new Vector2(arena.min.x + 0.3f, arena.center.y);
            Vector2 target = player != null ? (Vector2)player.transform.position : (Vector2)arena.center;
            SpawnPolarityPacket(NullDispatcherPolarityPacket.PacketKind.Support, origin, (target - origin).normalized * 2.8f);
        }
    }

    private void CompleteMainPattern()
    {
        if (!PolarityCombatActive || mainPatternActive || PolaritySwitching) return;
        completedPolarityPatterns++;
        if (completedPolarityPatterns >= patternsPerPolarity) TryBeginPolaritySwitch();
    }

    private bool TryBeginPolaritySwitch()
    {
        if (!PolarityCombatActive || mainPatternActive || PolaritySwitching ||
            completedPolarityPatterns < patternsPerPolarity) return false;
        PolaritySwitching = true; // Stops admission and collision authority before clearing either kind.
        ClearPolarityPackets();
        switchElapsed = 0f;
        supportElapsed = 0f;
        ShowBranchMessage("final.polarity.switch_warning");
        PlayBranchPulse(true);
        return true;
    }

    private void ClearPolarityPackets()
    {
        while (packets.Count > 0)
        {
            var packet = packets[packets.Count - 1];
            packets.RemoveAt(packets.Count - 1);
            if (packet != null) packet.Release();
        }
    }

    private void UpdatePolarityHud()
    {
        if (hud != null) hud.ShowPolarity(this, PlayerPolarity == Polarity.White ? "final.polarity.white" : "final.polarity.black");
    }

    private void ShowBranchMessage(string key)
    {
        if (Application.isPlaying && hud != null && VoidScrapperLocalizationService.Instance != null)
            hud.ShowWarning(VoidScrapperLocalizationService.Instance.GetText(key));
    }

    private void UpdateBeamVisual()
    {
        if (reclaimBeam == null || beamOrigin == null || player == null) return;
        reclaimBeam.SetPosition(0, beamOrigin.position);
        reclaimBeam.SetPosition(1, player.transform.position);
    }

    private void PlayBranchPulse(bool connectionBreak)
    {
        StopBranchVisuals();
        if (!Application.isPlaying) return;
        try
        {
            var sequence = DOTween.Sequence(); // Scaled presentation pauses with playable resistance.
            branchVisualTween = sequence;
            if (body != null) sequence.Append(body.transform.DOPunchScale(bodyScale * 0.12f, 0.5f, 3));
            if (connectionBreak && breakPulse != null)
            {
                breakPulse.enabled = true;
                sequence.Join(breakPulse.transform.DOScale(pulseScale * 3f, 0.5f));
                sequence.Join(breakPulse.DOFade(0f, 0.5f));
            }
            else if (reclaimBeam != null && Phase == EncounterPhase.ReclaimBeam)
            {
                sequence.Join(DOTween.To(() => reclaimBeam.widthMultiplier,
                    value => reclaimBeam.widthMultiplier = value, beamWidth * 1.4f, 0.5f));
                sequence.SetLoops(-1, LoopType.Yoyo);
            }
        }
        catch (System.Exception) { StopBranchVisuals(); }
    }

    private void StopBranchVisuals()
    {
        branchVisualTween?.Kill();
        branchVisualTween = null;
        if (!configured) return;
        if (body != null && configured) body.transform.localScale = bodyScale;
        if (reclaimBeam != null) reclaimBeam.widthMultiplier = beamWidth;
        if (breakPulse != null)
        {
            breakPulse.enabled = false;
            breakPulse.transform.localScale = pulseScale;
            breakPulse.color = pulseColor;
        }
    }

    private void StopBeam()
    {
        player?.ClearExternalPush(branchOwner);
        if (reclaimBeam != null) reclaimBeam.enabled = false;
        StopBranchVisuals();
    }

    private void AcquireCinematic()
    {
        if (ownsInput)
        {
            return;
        }
        ownsInput = true;
        player?.GetComponent<PlayerDash>()?.CancelActiveDash();
        player?.SetExternalControlLocked(this, true);
        weapon?.SetExternalInputLocked(this, true);
        if (cameraRig != null)
        {
            cameraRig.SetCinematicInputOffsetLocked(this, true);
            Vector3 focus = player != null ? (player.transform.position + transform.position) * 0.5f : transform.position;
            cameraRig.TryBeginOwnedCinematicFocusBlend(this, focus, 0.3f, null, out _);
        }
        if (body != null)
        {
            try
            {
                var pulse = DOTween.Sequence().SetUpdate(true);
                visualTween = pulse;
                pulse.Append(body.transform.DOPunchScale(bodyScale * 0.18f, 0.35f, 4));
                pulse.Join(body.DOColor(new Color(1f, 0.2f, 1f), 0.17f).SetLoops(2, LoopType.Yoyo));
            }
            catch (System.Exception)
            {
                visualTween?.Kill();
                visualTween = null; // Dialogue/control ownership does not depend on VFX availability.
            }
        }
    }

    private void ReleaseCinematic()
    {
        if (!ownsInput && visualTween == null)
        {
            return;
        }
        visualTween?.Kill();
        visualTween = null;
        if (body != null)
        {
            body.transform.localScale = bodyScale;
            body.color = bodyColor;
        }
        player?.SetExternalControlLocked(this, false);
        weapon?.SetExternalInputLocked(this, false);
        if (cameraRig != null)
        {
            cameraRig.ReleaseOwnedCinematicFocus(this, true);
            cameraRig.SetCinematicInputOffsetLocked(this, false);
        }
        ownsInput = false;
    }

    private void ClearLasers()
    {
        lanesDamaging = false;
        foreach (var laser in lasers)
        {
            if (laser != null)
            {
                laser.Deactivate();
            }
        }
        lasers.Clear();
    }

    private void ClearPatternObjects()
    {
        PatternDamageEnabled = false;
        ActiveMajorMechanics = 0;
        ClearLasers();
        foreach (var shot in shots)
        {
            // A pooled projectile may already have been released/reassigned by a hit.
            if (shot != null && shot.gameObject.activeInHierarchy && shot.SourceRoot == transform)
            {
                shot.ForceRelease(false);
            }
        }
        shots.Clear();
        if (relays != null)
        {
            foreach (var relay in relays)
            {
                if (relay != null) relay.enabled = false;
            }
        }
    }

    private void StopPatterns()
    {
        mainPatternActive = false;
        if (patterns != null)
        {
            StopCoroutine(patterns);
            patterns = null;
        }
        ClearPatternObjects();
    }

    private void HandleDeath(EnemyHealth _) => CancelEncounter();
    private void HandleRunEnded(RunResultData _) => CancelEncounter();
    private void HandleStateChanged(GameState previous, GameState current)
    {
        if (current == GameState.Boot || current == GameState.Settlement ||
            current == GameState.ExpeditionLoading || current == GameState.RunResult)
        {
            CancelEncounter();
        }
    }
    private void OnDisable() => CancelEncounter();
    private void OnDestroy() => CancelEncounter();

    public void CancelEncounter()
    {
        Phase = EncounterPhase.Dead;
        if (finalTransition != null) { StopCoroutine(finalTransition); finalTransition = null; }
        StopShellPresentation();
        weapon?.SetExternalInputLocked(finalPhaseOwner, false);
        if (health != null) health.RemoveDamageFloor(finalPhaseOwner);
        if (polarityRuntime != null) { StopCoroutine(polarityRuntime); polarityRuntime = null; }
        PolaritySwitching = false;
        ClearPolarityPackets();
        hud?.HidePolarity(this);
        pendingChoice = TreatmentChoice.None;
        offering = false;
        StopPatterns();
        if (branchRoutine != null)
        {
            StopCoroutine(branchRoutine);
            branchRoutine = null;
        }
        StopBeam();
        weapon?.SetExternalInputLocked(branchOwner, false);
        if (settlementSupport != null)
        {
            settlementSupport.Completed -= HandleSupportCompleted;
            if (supportRequested) settlementSupport.CancelSupportSequence();
        }
        if (treatment != null)
        {
            StopCoroutine(treatment);
            treatment = null;
        }
        if (treatmentEntry != null)
        {
            treatmentEntry.Completed -= HandleConversationEnded;
            if (treatmentEntry.IsRunning && DialogueManager.isConversationActive &&
                DialogueManager.lastConversationStarted == NullDispatcherDialogueIds.Conversation)
            {
                DialogueManager.StopConversation();
            }
        }
        bridge?.UnregisterFinalBossAuthority(this);
        ReleaseCinematic();
        if (health != null)
        {
            health.RemoveDamageFloor(this);
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDeath;
        }
        if (run != null) run.RunEnded -= HandleRunEnded;
        if (playerHealth != null) playerHealth.Died -= CancelEncounter;
        if (state != null) state.StateChanged -= HandleStateChanged;
    }
}
