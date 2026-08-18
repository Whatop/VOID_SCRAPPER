using System;
using UnityEngine;

public enum HighValueObjectiveSource
{
    SpecialEquipmentContainer,
    ExpeditionEvent,
    NpcRescue,
    EliteCargo,
    RivalHarvester,
    Debug
}

[DefaultExecutionOrder(-7000)]
[DisallowMultipleComponent]
public class ExpeditionObjectiveDirector : MonoBehaviour
{
    private static ExpeditionObjectiveDirector instance;

    [Header("Core Tracking")]
    [SerializeField] private int signalsRequiredToRevealCore = 2;
    [SerializeField] private int signalsRequiredForRareBossReward = 3;
    [SerializeField] private int signalsRequiredForExtraBossChoice = 4;

    [Header("Debug")]
    [SerializeField] private bool logProgress = true;
    [Tooltip("Legacy run-objective feedback. Expedition Core Tracking owns current Core progress presentation.")]
    [SerializeField] private bool showLegacyCoreTrackingMessages;

    public static ExpeditionObjectiveDirector Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ExpeditionObjectiveDirector>();

                if (instance == null && Application.isPlaying)
                {
                    GameObject obj = new GameObject(nameof(ExpeditionObjectiveDirector));
                    instance = obj.AddComponent<ExpeditionObjectiveDirector>();
                }
            }

            return instance;
        }
    }

    public int SignalCount => ResolveRunContext() != null
        ? ResolveRunContext().ObjectiveSignalCount
        : 0;

    public int SignalsRequiredToRevealCore => Mathf.Max(1, signalsRequiredToRevealCore);
    public int SignalsRequiredForRareBossReward => Mathf.Max(SignalsRequiredToRevealCore, signalsRequiredForRareBossReward);
    public int SignalsRequiredForExtraBossChoice => Mathf.Max(SignalsRequiredForRareBossReward, signalsRequiredForExtraBossChoice);

    public bool CoreRevealed => SignalCount >= SignalsRequiredToRevealCore;
    public bool BossRareGuaranteed => SignalCount >= SignalsRequiredForRareBossReward;
    public bool BossExtraChoice => SignalCount >= SignalsRequiredForExtraBossChoice;

    public event Action<int, int> ProgressChanged;
    public event Action CoreRevealedEvent;
    public event Action<int> ObjectiveCompleted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SyncRunContextFlags();
    }

    private void Start()
    {
        RaiseProgressChanged();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public bool RegisterObjective(
        string objectiveId,
        HighValueObjectiveSource source,
        int signalAmount = 1)
    {
        RunContext run = ResolveRunContext();

        if (run == null || !run.IsActive)
        {
            return false;
        }

        bool wasRevealed = CoreRevealed;
        bool added = run.RegisterObjectiveSignal(objectiveId, Mathf.Max(1, signalAmount));

        if (!added)
        {
            return false;
        }

        SyncRunContextFlags();
        RaiseProgressChanged();
        ObjectiveCompleted?.Invoke(run.ObjectiveSignalCount);

        if (!wasRevealed && CoreRevealed)
        {
            CoreRevealedEvent?.Invoke();
            if (showLegacyCoreTrackingMessages)
            {
                AudioManager.Play(SoundEventIds.UiUnlock);
                ShowWarning("코어 추적 완료. 코어 위치가 공개되었습니다.");
            }
        }
        else if (showLegacyCoreTrackingMessages)
        {
            ShowWarning($"코어 추적 신호 {SignalCount}/{SignalsRequiredToRevealCore}");
        }

        if (logProgress)
        {
            Debug.Log(
                $"High-value objective completed. Source: {source}, Signal: {SignalCount}, " +
                $"Core: {CoreRevealed}, RareBossReward: {BossRareGuaranteed}, ExtraBossChoice: {BossExtraChoice}",
                this
            );
        }

        return true;
    }

    public void RaiseProgressChanged()
    {
        ProgressChanged?.Invoke(SignalCount, SignalsRequiredToRevealCore);
    }

    public string BuildProgressText()
    {
        return CoreRevealed
            ? $"CORE SIGNAL {SignalCount}/{SignalsRequiredToRevealCore}  READY"
            : $"CORE SIGNAL {SignalCount}/{SignalsRequiredToRevealCore}";
    }

    public int ResolveBossChoiceCount(int baseChoiceCount)
    {
        int result = Mathf.Max(1, baseChoiceCount);
        return BossExtraChoice ? result + 1 : result;
    }

    private void SyncRunContextFlags()
    {
        RunContext run = ResolveRunContext();

        if (run == null)
        {
            return;
        }

        run.SetCoreSignalRevealed(CoreRevealed);
    }

    private RunContext ResolveRunContext()
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasActiveRun)
        {
            return null;
        }

        return RunManager.Instance.CurrentRun;
    }

    private void ShowWarning(string message)
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();

        if (hud != null)
        {
            hud.ShowWarning(message);
        }
    }
}
