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

    private readonly List<SettlementDefenseCorruptedCore> cores = new List<SettlementDefenseCorruptedCore>(3);
    private readonly List<TraitDefinition> traits = new List<TraitDefinition>();
    private Sequence reveal;
    private Sequence centralPulse;
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

    public bool IsActive => active;
    public int FusedCount => fusedCount;
    public IReadOnlyList<SettlementDefenseCorruptedCore> Cores => cores;

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
        deckRoot.SetActive(true);
        deckCanvas.SetActive(true);
        player.transform.position = playerStart.position;
        traits.Clear();
        traitCatalog?.AppendAllTo(traits);
        playerStats.Apply(null, PermanentProgress.Instance, ships, buildings, traits, true);
        player.ResetHealth();
        player.GetComponent<PlayerController2D>()?.AcquireTemporaryCameraViewportConstraint(
            this, deckCamera, deckCamera.transform, -7f, 7f, 0.4f, 0.7f, 0.7f);
        player.Died += HandlePlayerDied;
        player.Changed += HandleVitalsChanged;
        interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null)
        {
            interactor.CurrentTargetChanged += HandleTargetChanged;
        }
        HandleTargetChanged(null);
        HandleVitalsChanged(player.CurrentHp, player.MaxHp);
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
            Debug.LogError("Settlement defense requires the authored three-core prefab, ordered parts, central visuals and navigation binding.", this);
            routeCore.CancelSettlementDefenseRequest();
            return;
        }
        active = true;
        fusedCount = 0;
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
            facilityNavigation == null || componentCores == null || componentCores.Length != 3)
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
                    CompleteEncounter();
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
                CompleteEncounter();
            }
            else
            {
                ActivateNextCore();
            }
        }
    }

    private void CompleteEncounter()
    {
        if (!active || completing || fusedCount != 3)
        {
            return;
        }
        completing = true;
        active = false;
        try
        {
            CleanupSpawnedObjects();
            routeCore.CompleteSettlementDefense();
            CloseDeck(true);
            hud?.SetMessage(Text("defense.core.complete", "항로 코어 안정화 완료"));
        }
        finally
        {
            completing = false;
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

    private void HandleVitalsChanged(float current, float maximum)
    {
        if (vitalsText != null)
        {
            vitalsText.text = $"HP {current:0} / {maximum:0}";
        }
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

    private void CleanupSpawnedObjects()
    {
        reveal?.Kill();
        reveal = null;
        centralPulse?.Kill();
        centralPulse = null;
        if (navigationOwned)
        {
            facilityNavigation.SetActive(navigationWasActive);
            centralVisual.transform.localScale = centralScale;
            centralVisual.color = centralColor;
            corruptionWave.gameObject.SetActive(false);
            navigationOwned = false;
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
        if (observedState != null)
        {
            observedState.StateChanged -= HandleStateChanged;
        }
        observedState = null;
        if (player != null)
        {
            player.Died -= HandlePlayerDied;
            player.Changed -= HandleVitalsChanged;
            player.GetComponent<PlayerController2D>()?.ReleaseTemporaryCameraViewportConstraint(this);
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
