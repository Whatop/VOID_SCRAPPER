using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>The authored Route Core deck and its sequential corrupted-component defense encounter.</summary>
[DisallowMultipleComponent]
public sealed class SettlementDefenseEncounterController : MonoBehaviour
{
    [Header("Authored deck")]
    [SerializeField] private SettlementRouteCoreController routeCore;
    [SerializeField] private SettlementHUD hud;
    [SerializeField] private GameObject managementCanvas;
    [SerializeField] private GameObject deckRoot;
    [SerializeField] private GameObject deckCanvas;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private PlayerRuntimeStatApplier playerStats;
    [SerializeField] private Camera deckCamera;
    [SerializeField] private Transform playerStart;
    [SerializeField] private TMP_Text vitalsText;
    [SerializeField] private TMP_Text interactionText;
    [SerializeField] private ShipDefinition[] ships;
    [SerializeField] private BuildingDefinition[] buildings;
    [SerializeField] private TraitCatalog traitCatalog;

    [System.Serializable]
    public struct ComponentCore
    {
        public BossStoryPart part;
        public Transform combatPoint;
        public Color color;
        public float hp;
    }

    [Header("Authored corrupted component encounter")]
    [SerializeField] private SettlementDefenseCorruptedCore corePrefab;
    [SerializeField] private ComponentCore[] componentCores;
    [SerializeField] private SpriteRenderer centralVisual;
    [SerializeField] private SpriteRenderer corruptionWave;
    [SerializeField] private GameObject facilityNavigation;

    [Header("Authored Purple finale")]
    [SerializeField] private SettlementDefensePurpleCore purpleCore;
    [SerializeField] private PlayerRadarScanner deckRadar;
    [SerializeField] private GameObject radarPresentation;
    [SerializeField] private RadarPanelAnimator radarPanel;
    [SerializeField] private TMP_Text radarHint;
    [SerializeField] private SpriteRenderer[] blackoutRenderers;
    [SerializeField, Range(.1f, .5f)] private float hiddenEnvironmentVisibility = .25f;
    [SerializeField, Range(.2f, .8f)] private float exposedEnvironmentVisibility = .45f;

    private readonly List<SettlementDefenseCorruptedCore> cores = new List<SettlementDefenseCorruptedCore>(3);
    private readonly List<TraitDefinition> traits = new List<TraitDefinition>();
    private Sequence reveal;
    private Sequence centralPulse;
    private Tween purificationReturn;
    private Vector3 centralScale;
    private Color centralColor;
    private bool navigationOwned;
    private bool navigationWasActive;
    private bool managementWasActive;
    private int fusedCount;
    private bool introFinished;
    private GameStateManager observedState;
    private PlayerInteractor interactor;
    private bool active;
    private bool deckOpen;
    private bool completing;
    private bool originalOrthographic;
    private float originalCameraSize;
    private bool purpleStarted;
    private bool purpleCleared;
    private bool centralWasEnabled;
    private Color[] environmentColors;

    public bool IsActive => active;
    public int FusedCount => fusedCount;
    public IReadOnlyList<SettlementDefenseCorruptedCore> Cores => cores;
    public bool IsPurpleActive => active && purpleStarted && !purpleCleared;

    internal static string Text(string key, string fallback)
    {
        VoidScrapperLocalizationService service = VoidScrapperLocalizationService.Instance;
        return service != null && service.Catalog != null &&
            service.Catalog.TryGetText(key, service.CurrentLanguageCode, out string text, out _)
            ? text : fallback;
    }

    public void EnterDeck()
    {
        if (deckOpen || player == null || playerStats == null || deckRoot == null ||
            deckCanvas == null || managementCanvas == null || deckCamera == null || playerStart == null ||
            PermanentProgress.Instance == null || SettlementExpeditionLaunchGuard.IsDialogueActive ||
            GameplayPauseManager.IsPaused ||
            (SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsLoading) ||
            (RunManager.Instance != null && RunManager.Instance.HasActiveRun))
        {
            return;
        }

        deckOpen = true;
        originalOrthographic = deckCamera.orthographic;
        originalCameraSize = deckCamera.orthographicSize;
        deckCamera.orthographic = true;
        deckCamera.orthographicSize = 270f / (32f * 2f);
        managementWasActive = managementCanvas.activeSelf;
        managementCanvas.SetActive(false);
        deckRadar?.SetExternalInputLocked(this, true);
        deckRoot.SetActive(true);
        deckCanvas.SetActive(true);
        player.transform.position = playerStart.position;
        traits.Clear();
        traitCatalog?.AppendAllTo(traits);
        playerStats.Apply(null, PermanentProgress.Instance, ships, buildings, traits, true);
        player.ResetHealth();
        player.GetComponent<PlayerController2D>()?.AcquireTemporaryAimCamera(this, deckCamera);
        player.GetComponent<PlayerController2D>()?.AcquireTemporaryCameraViewportConstraint(
            this, deckCamera, deckCamera.transform, -7f, 7f, 0.4f, 0.7f, 0.7f);
        player.Died += HandlePlayerDied;
        interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null)
        {
            interactor.CurrentTargetChanged += HandleTargetChanged;
        }
        HandleTargetChanged(null);
        observedState = GameStateManager.Instance;
        if (observedState != null)
        {
            observedState.StateChanged += HandleStateChanged;
        }
        hud?.SetMessage("항로 코어에 접근하여 상태를 확인하십시오.");
    }

    public void LeaveDeck()
    {
        if (!deckOpen || active || completing || GameplayPauseManager.IsPaused ||
            SettlementExpeditionLaunchGuard.IsDialogueActive)
        {
            return;
        }
        CloseDeck(true);
    }

    // Bound as a persistent listener on RouteCore.defenseRequested. State changes
    // alone never start or complete this encounter.
    public void BeginEncounter()
    {
        if (active || completing || !deckOpen || routeCore == null || !routeCore.IsDefenseRequested)
        {
            return;
        }
        if (!HasValidConfiguration())
        {
            Debug.LogError("Settlement defense requires the authored three-core prefab, ordered parts, central visuals, Purple target, Deck Radar/HUD and blackout renderer bindings.", this);
            routeCore.CancelSettlementDefenseRequest();
            return;
        }
        if (Application.isPlaying && PoolManager.Instance == null)
        {
            Debug.LogError("SettlementDefenseEncounter requires the existing bootstrap PoolManager for pooled projectiles. Enter Settlement through Boot before starting defense.", this);
            routeCore.CancelSettlementDefenseRequest();
            return;
        }
        active = true;
        fusedCount = 0;
        purpleStarted = purpleCleared = false;
        introFinished = false;
        navigationWasActive = facilityNavigation.activeSelf;
        navigationOwned = true;
        facilityNavigation.SetActive(false);
        centralScale = centralVisual.transform.localScale;
        centralColor = centralVisual.color;
        try
        {
            foreach (ComponentCore config in componentCores)
            {
                var core = Instantiate(corePrefab, config.combatPoint.position, Quaternion.identity, deckRoot.transform);
                cores.Add(core);
                core.Configure(this, config.part, config.color, config.hp, player.transform, routeCore.transform);
            }
            PlayOpening();
        }
        catch (System.Exception exception)
        {
            FailEncounter();
            Debug.LogException(exception, this);
        }
    }

    private bool HasValidConfiguration()
    {
        if (corePrefab == null || !corePrefab.HasAuthoredBindings || centralVisual == null || corruptionWave == null ||
            facilityNavigation == null || componentCores == null || componentCores.Length != 3 ||
            purpleCore == null || !purpleCore.HasAuthoredBindings || deckRadar == null ||
            radarPresentation == null || radarPanel == null || radarHint == null ||
            blackoutRenderers == null || blackoutRenderers.Length == 0)
        {
            return false;
        }
        return componentCores[0].part == BossStoryPart.SectorStabilizer &&
            componentCores[1].part == BossStoryPart.PhaseNavigationLens &&
            componentCores[2].part == BossStoryPart.MatterCompressor &&
            componentCores[0].combatPoint != null && componentCores[1].combatPoint != null &&
            componentCores[2].combatPoint != null &&
            componentCores[0].combatPoint != componentCores[1].combatPoint &&
            componentCores[0].combatPoint != componentCores[2].combatPoint &&
            componentCores[1].combatPoint != componentCores[2].combatPoint;
    }

    private void PlayOpening()
    {
        hud?.SetMessage(Text("defense.core.intro", "정착지 방어 · 코어 오염 감지"));
        try
        {
            // No input/camera lock is needed: the inactive cores cannot damage the player.
            reveal = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            for (int i = 0; i < cores.Count; i++)
            {
                Vector3 nearCenter = routeCore.transform.position +
                    (componentCores[i].combatPoint.position - routeCore.transform.position).normalized * 0.5f;
                reveal.Insert(0f, cores[i].transform.DOMove(nearCenter, 0.85f).SetEase(Ease.InQuad));
                reveal.Insert(0.85f, cores[i].transform.DOMove(componentCores[i].combatPoint.position, 0.45f).SetEase(Ease.OutQuad));
            }
            reveal.Insert(0.8f, centralVisual.transform.DOPunchScale(centralScale * 0.45f, 0.5f, 5));
            corruptionWave.gameObject.SetActive(true);
            corruptionWave.transform.localScale = Vector3.one * 0.2f;
            corruptionWave.color = new Color(0.75f, 0.1f, 1f, 0f);
            reveal.Insert(0.8f, corruptionWave.DOFade(0.75f, 0.08f));
            reveal.Insert(0.8f, corruptionWave.transform.DOScale(Vector3.one * 12f, 0.5f));
            reveal.Insert(0.9f, corruptionWave.DOFade(0f, 0.45f));
            reveal.InsertCallback(0.85f, () =>
            {
                foreach (var core in cores)
                {
                    core.Corrupt();
                }
            });
            reveal.AppendInterval(0.65f);
            reveal.OnComplete(FinishOpening);
        }
        catch (System.Exception)
        {
            FinishOpening();
        }
    }

    private void FinishOpening()
    {
        if (!active || introFinished)
        {
            return;
        }
        reveal?.Kill();
        reveal = null;
        centralVisual.transform.localScale = centralScale;
        corruptionWave.gameObject.SetActive(false);
        for (int i = 0; i < cores.Count; i++)
        {
            cores[i].transform.position = componentCores[i].combatPoint.position;
            cores[i].Corrupt();
        }
        introFinished = true;
        ActivateNextCore();
    }

    private void ActivateNextCore()
    {
        if (!active || fusedCount >= cores.Count)
        {
            return;
        }
        cores[fusedCount].ActivateCombat();
        NamedPlaceholderUtility.TryFormat(
            Text("defense.core.combat", "오염 코어 격리 {phase} / 3"),
            new Dictionary<string, string> { { "phase", (fusedCount + 1).ToString() } },
            out string message, out _);
        hud?.SetMessage(message);
    }

    public void CoreDefeated(SettlementDefenseCorruptedCore core)
    {
        if (active && fusedCount < cores.Count && cores[fusedCount] == core)
        {
            hud?.SetMessage(core.InteractionText);
        }
    }

    public void CoreFused(SettlementDefenseCorruptedCore core)
    {
        if (!active || !introFinished || fusedCount >= cores.Count || cores[fusedCount] != core ||
            core.State != SettlementDefenseCorruptedCore.CoreState.Fused)
        {
            return;
        }
        fusedCount++;
        NamedPlaceholderUtility.TryFormat(
            Text("defense.core.fused", "코어 안정화 {count} / 3"),
            new Dictionary<string, string> { { "count", fusedCount.ToString() } },
            out string message, out _);
        hud?.SetMessage(message);
        PulseCenter(fusedCount == 3);
    }

    private void PulseCenter(bool final)
    {
        try
        {
            centralPulse?.Kill();
            centralVisual.transform.localScale = centralScale;
            centralPulse = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            centralPulse.Append(centralVisual.transform.DOPunchScale(centralScale * (final ? 0.4f : 0.15f), final ? 0.65f : 0.3f, 2));
            centralPulse.AppendInterval(0.2f);
            centralPulse.OnComplete(() =>
            {
                if (final)
                {
                    StartPurplePhase();
                }
                else
                {
                    ActivateNextCore();
                }
            });
        }
        catch (System.Exception)
        {
            if (final)
            {
                StartPurplePhase();
            }
            else
            {
                ActivateNextCore();
            }
        }
    }

    private void StartPurplePhase()
    {
        if (!active || fusedCount != 3 || purpleStarted) return;
        purpleStarted = true;
        centralWasEnabled = centralVisual.enabled;
        centralVisual.enabled = false;
        environmentColors = new Color[blackoutRenderers.Length];
        for (int i = 0; i < blackoutRenderers.Length; i++)
            if (blackoutRenderers[i] != null) environmentColors[i] = blackoutRenderers[i].color;
        ApplyBlackout(hiddenEnvironmentVisibility);
        radarPresentation.SetActive(true);
        radarPanel.CloseImmediate();
        deckRadar.CloseRadar();
        deckRadar.SetExternalInputLocked(this, false);
        NamedPlaceholderUtility.TryFormat(Text("defense.purple.hint", "[{radar}] 레이더\n[{scan}] 탐색"),
            new Dictionary<string, string> { { "radar", deckRadar.RadarBindingDisplay }, { "scan", deckRadar.QuickScanBindingDisplay } },
            out string hint, out _);
        radarHint.text = hint;
        SetPurpleMessage("defense.purple.start", "항로 코어 신호 소실 · 레이더 탐색 필요");
        if (!purpleCore.Begin(this, deckRadar))
        {
            Debug.LogError("RouteCoreDeck/PurpleCorruptionTarget could not begin; check its authored health, sibling colliders and Radar bindings.", this);
            FailEncounter();
        }
    }

    public void PurpleExposureChanged(SettlementDefensePurpleCore source, bool exposed)
    {
        if (!IsPurpleActive || source != purpleCore) return;
        ApplyBlackout(exposed ? exposedEnvironmentVisibility : hiddenEnvironmentVisibility);
        SetPurpleMessage(exposed ? "defense.purple.exposed" : "defense.purple.hidden",
            exposed ? "오염 코어 노출" : "신호 재은폐");
    }

    public void PurpleCleansed(SettlementDefensePurpleCore source)
    {
        if (!IsPurpleActive || source != purpleCore || source.State != SettlementDefensePurpleCore.Phase.Cleansed) return;
        purpleCleared = true;
        CompleteEncounter();
    }

    private void SetPurpleMessage(string key, string fallback)
    {
        string message = Text(key, fallback);
        hud?.SetMessage(message);
    }

    private void ApplyBlackout(float visibility)
    {
        if (environmentColors == null) return;
        for (int i = 0; i < blackoutRenderers.Length; i++)
        {
            if (blackoutRenderers[i] == null) continue;
            Color c = environmentColors[i];
            blackoutRenderers[i].color = new Color(c.r * visibility, c.g * visibility, c.b * visibility, c.a);
        }
    }

    private void CleanupPurple(bool preserveDeadPresentation = false)
    {
        if (purpleCore != null)
        {
            purpleCore.Cancel();
            if (!preserveDeadPresentation) purpleCore.gameObject.SetActive(false);
        }
        if (deckRadar != null) { deckRadar.SetExternalInputLocked(this, true); deckRadar.CloseRadar(); }
        if (radarPanel != null) radarPanel.CloseImmediate();
        if (radarPresentation != null) radarPresentation.SetActive(false);
        if (environmentColors != null)
        {
            for (int i = 0; i < blackoutRenderers.Length; i++)
                if (blackoutRenderers[i] != null) blackoutRenderers[i].color = environmentColors[i];
            environmentColors = null;
            if (centralVisual != null) centralVisual.enabled = centralWasEnabled;
        }
    }

    private void CompleteEncounter()
    {
        if (!active || completing || fusedCount != 3 || !purpleCleared)
        {
            return;
        }
        completing = true;
        active = false;
        try
        {
            // Keep the dead actor enabled only for presentation: re-enabling it would invoke
            // EnemyHealth.OnEnable and reset health/colliders during the visual tail.
            CleanupSpawnedObjects(true);
            routeCore.CompleteSettlementDefense();
            // Progress/save and all combat cleanup are synchronous. The camera stays briefly for
            // an optional visual tail; its independent timer also returns if the shader is disabled.
            if (!TryStartPurificationReturn()) CloseDeck(true);
            hud?.SetMessage(Text("defense.core.complete", "항로 코어 안정화 완료"));
        }
        finally
        {
            completing = false;
        }
    }

    private bool TryStartPurificationReturn()
    {
        try
        {
            if (purpleCore == null || !purpleCore.TryPlayPurification(out float duration)) return false;
            purificationReturn = DOVirtual.DelayedCall(duration, () => CloseDeck(true))
                .SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            return true;
        }
        catch (System.Exception exception)
        {
            // Optional presentation must never strand the player after the authoritative save.
            Debug.LogWarning("Route Core purification presentation failed; returning to facilities. " + exception.Message, this);
            return false;
        }
    }

    public void FailEncounter()
    {
        if (active)
        {
            HandlePlayerDied();
        }
    }

    public void CancelEncounter()
    {
        if (deckOpen || active)
        {
            CloseDeck(true);
        }
        else
        {
            StopEncounter();
        }
    }

    private void StopEncounter()
    {
        bool wasActive = active;
        active = false;
        CleanupSpawnedObjects();
        if (wasActive)
        {
            routeCore?.CancelSettlementDefenseRequest();
        }
    }

    private void HandlePlayerDied()
    {
        CloseDeck(true);
        hud?.SetMessage("방어 실패 · 항로 코어에서 다시 시도할 수 있습니다.");
    }

    private void HandleTargetChanged(IInteractable target)
    {
        if (interactionText == null)
        {
            return;
        }
        string binding = interactor != null
            ? InputBindingUtility.GetDisplayString(interactor.InputActions,
                interactor.ActionMapName, interactor.InteractActionName, "F") : "F";
        interactionText.text = target != null ? $"[{binding}] {target.InteractionText}" : string.Empty;
    }

    private void HandleStateChanged(GameState previous, GameState next)
    {
        if (!completing && ((active && next != GameState.SettlementDefense) ||
            (next != GameState.Settlement && next != GameState.SettlementDefense)))
        {
            CloseDeck(next == GameState.Settlement);
        }
    }

    private void CleanupSpawnedObjects(bool preserveDeadPresentation = false)
    {
        CleanupPurple(preserveDeadPresentation);
        reveal?.Kill();
        reveal = null;
        centralPulse?.Kill();
        centralPulse = null;
        if (navigationOwned)
        {
            navigationOwned = false;
            // Scene teardown may destroy presentation children before the encounter owner.
            if (facilityNavigation != null) facilityNavigation.SetActive(navigationWasActive);
            if (centralVisual != null)
            {
                centralVisual.transform.localScale = centralScale;
                centralVisual.color = centralColor;
            }
            if (corruptionWave != null) corruptionWave.gameObject.SetActive(false);
        }
        foreach (var core in cores)
        {
            if (core == null)
            {
                continue;
            }
            core.Cancel();
            core.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(core.gameObject);
            }
            else
            {
                DestroyImmediate(core.gameObject);
            }
        }
        cores.Clear();
        if (player != null)
        {
            Bullet.ReleaseAllActiveFromSource(player.transform);
        }
    }

    private void CloseDeck(bool showManagement)
    {
        purificationReturn?.Kill(); purificationReturn = null;
        if (observedState != null)
        {
            observedState.StateChanged -= HandleStateChanged;
        }
        observedState = null;
        if (player != null)
        {
            player.Died -= HandlePlayerDied;
            player.GetComponent<PlayerController2D>()?.ReleaseTemporaryCameraViewportConstraint(this);
            player.GetComponent<PlayerController2D>()?.ReleaseTemporaryAimCamera(this);
        }
        if (interactor != null)
        {
            interactor.CurrentTargetChanged -= HandleTargetChanged;
        }
        interactor = null;
        StopEncounter();
        if (deckOpen && deckCamera != null)
        {
            deckCamera.orthographic = originalOrthographic;
            deckCamera.orthographicSize = originalCameraSize;
        }
        deckOpen = false;
        if (deckRoot != null)
        {
            deckRoot.SetActive(false);
        }
        if (deckCanvas != null)
        {
            deckCanvas.SetActive(false);
        }
        if (showManagement && managementCanvas != null)
        {
            managementCanvas.SetActive(managementWasActive);
        }
    }

    private void OnDisable()
    {
        bool restoreManagement = deckOpen && (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState == GameState.Settlement ||
            GameStateManager.Instance.CurrentState == GameState.SettlementDefense);
        CloseDeck(restoreManagement);
    }
}
