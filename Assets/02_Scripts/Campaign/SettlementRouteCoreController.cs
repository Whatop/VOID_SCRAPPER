using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SettlementRouteCoreController : MonoBehaviour, IInteractable
{
    [Header("References")]
    [SerializeField] private SettlementHUD settlementHud;
    [SerializeField] private SettlementController settlementController;

    [Header("Visual Stages")]
    [SerializeField] private GameObject missingPartsRoot;
    [SerializeField] private GameObject readyToAssembleRoot;
    [SerializeField] private GameObject assembledRoot;
    [SerializeField] private GameObject activatedRoot;

    [Header("Prototype Defense Bridge")]
    [Tooltip("정착지 방어 콘텐츠가 아직 연결되지 않았을 때만 임시로 사용합니다.")]
    [SerializeField] private bool autoCompleteDefenseForPrototype;
    [SerializeField] private UnityEvent defenseRequested;
    [SerializeField] private UnityEvent defenseCompleted;
    [SerializeField] private UnityEvent finalExpeditionLaunched;

    [Header("Interaction Text")]
    [SerializeField] private string missingPartsText = "항로 코어 부품 확인";
    [SerializeField] private string assembleText = "완전 코어 조립";
    [SerializeField] private string activateText = "완전 코어 활성화";
    [SerializeField] private string defenseText = "정착지 방어 시작";
    [SerializeField] private string finalLaunchText = "중앙 물류망 출격";

    private bool defenseRequestInProgress;

    public UnityEvent DefenseRequested => defenseRequested;
    public UnityEvent DefenseCompleted => defenseCompleted;

    public string InteractionText
    {
        get
        {
            PermanentProgress progress = PermanentProgress.Instance;

            if (progress == null)
            {
                return "항로 코어 상태 확인";
            }

            return progress.CurrentRouteCoreState switch
            {
                RouteCoreState.MissingParts => missingPartsText,
                RouteCoreState.ReadyToAssemble => assembleText,
                RouteCoreState.Assembled => activateText,
                RouteCoreState.Activated when !progress.SettlementDefenseCleared => defenseText,
                RouteCoreState.Activated => finalLaunchText,
                _ => "항로 코어 상태 확인"
            };
        }
    }

    private void Reset()
    {
        Collider2D interactionCollider = GetComponent<Collider2D>();

        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        Collider2D interactionCollider = GetComponent<Collider2D>();

        if (interactionCollider != null)
        {
            interactionCollider.isTrigger = true;
        }

        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed += HandleProgressChanged;
        }

        RefreshVisuals();
    }

    private void OnDisable()
    {
        if (PermanentProgress.Instance != null)
        {
            PermanentProgress.Instance.Changed -= HandleProgressChanged;
        }
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null &&
               PermanentProgress.Instance != null &&
               !defenseRequestInProgress;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        PermanentProgress progress = PermanentProgress.Instance;

        switch (progress.CurrentRouteCoreState)
        {
            case RouteCoreState.MissingParts:
                ShowMessage(BuildMissingPartsMessage(progress));
                break;

            case RouteCoreState.ReadyToAssemble:
                TryAssembleRouteCore();
                break;

            case RouteCoreState.Assembled:
                TryActivateRouteCore();
                break;

            case RouteCoreState.Activated:
                if (progress.SettlementDefenseCleared)
                {
                    LaunchFinalExpedition();
                }
                else
                {
                    BeginSettlementDefense();
                }
                break;
        }
    }

    public bool TryAssembleRouteCore()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || !progress.TryAssembleRouteCore())
        {
            ShowMessage("구획 안정기, 물질 압축로, 위상 항법 렌즈가 모두 필요합니다.");
            return false;
        }

        SaveProgress();
        ShowMessage("완전 코어 조립 완료 · 정착지 주 동력 연결 준비");
        RefreshVisuals();
        return true;
    }

    public bool TryActivateRouteCore()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || !progress.TryActivateRouteCore())
        {
            ShowMessage("완전 코어를 먼저 조립해야 합니다.");
            return false;
        }

        SaveProgress();
        ShowMessage("완전 코어 활성화 · 중앙 통제체가 정착지 좌표를 역추적했습니다.");
        RefreshVisuals();
        BeginSettlementDefense();
        return true;
    }

    public bool BeginSettlementDefense()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || progress.CurrentRouteCoreState != RouteCoreState.Activated)
        {
            ShowMessage("완전 코어를 활성화해야 방어전을 시작할 수 있습니다.");
            return false;
        }

        if (progress.SettlementDefenseCleared)
        {
            ShowMessage("정착지 방어 완료 · 중앙 물류망 출격이 가능합니다.");
            return true;
        }

        defenseRequestInProgress = true;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.SettlementDefense);
        }

        ShowMessage("정착지 방어 시작 · 전송 앵커를 제거하십시오.");
        defenseRequested?.Invoke();

        if (autoCompleteDefenseForPrototype)
        {
            CompleteSettlementDefense();
        }

        return true;
    }

    public void CancelSettlementDefenseRequest()
    {
        defenseRequestInProgress = false;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Settlement);
        }

        ShowMessage("정착지 방어 준비를 취소했습니다.");
    }

    public void CompleteSettlementDefense()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null)
        {
            return;
        }

        if (progress.CurrentRouteCoreState != RouteCoreState.Activated)
        {
            ShowMessage("완전 코어가 활성화되지 않아 방어 완료를 기록할 수 없습니다.");
            return;
        }

        progress.MarkSettlementDefenseCleared();
        defenseRequestInProgress = false;
        SaveProgress();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ChangeState(GameState.Settlement);
        }

        defenseCompleted?.Invoke();
        ShowMessage("정착지 방어 완료 · 중앙 물류망 항로가 안정화되었습니다.");
        RefreshVisuals();
    }

    public bool LaunchFinalExpedition()
    {
        PermanentProgress progress = PermanentProgress.Instance;

        if (progress == null || !progress.CanLaunchFinalExpedition)
        {
            ShowMessage("완전 코어 활성화와 정착지 방어 완료가 필요합니다.");
            return false;
        }

        bool launched;

        if (settlementController != null)
        {
            launched = settlementController.LaunchFinalExpedition();
        }
        else
        {
            launched = RunManager.Instance != null &&
                       RunManager.Instance.StartFinalExpeditionAndLoad();
        }

        if (!launched)
        {
            ShowMessage("중앙 물류망 출격을 시작하지 못했습니다.");
            return false;
        }

        finalExpeditionLaunched?.Invoke();
        return true;
    }

    [ContextMenu("DEBUG/Complete Settlement Defense")]
    private void DebugCompleteSettlementDefense()
    {
        CompleteSettlementDefense();
    }

    [ContextMenu("DEBUG/Refresh Visuals")]
    private void DebugRefreshVisuals()
    {
        RefreshVisuals();
    }

    private void HandleProgressChanged()
    {
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        PermanentProgress progress = PermanentProgress.Instance;
        RouteCoreState state = progress != null
            ? progress.CurrentRouteCoreState
            : RouteCoreState.MissingParts;

        SetActive(missingPartsRoot, state == RouteCoreState.MissingParts);
        SetActive(readyToAssembleRoot, state == RouteCoreState.ReadyToAssemble);
        SetActive(assembledRoot, state == RouteCoreState.Assembled);
        SetActive(activatedRoot, state == RouteCoreState.Activated);
    }

    private string BuildMissingPartsMessage(PermanentProgress progress)
    {
        if (progress == null)
        {
            return "캠페인 진행 정보를 찾지 못했습니다.";
        }

        string missing = string.Empty;
        AppendMissingPart(ref missing, progress, BossStoryPart.SectorStabilizer);
        AppendMissingPart(ref missing, progress, BossStoryPart.MatterCompressor);
        AppendMissingPart(ref missing, progress, BossStoryPart.PhaseNavigationLens);

        return string.IsNullOrWhiteSpace(missing)
            ? "완전 코어를 조립할 수 있습니다."
            : $"미확보 부품: {missing}";
    }

    private void AppendMissingPart(
        ref string result,
        PermanentProgress progress,
        BossStoryPart part)
    {
        if (progress.HasBossStoryPart(part))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            result += ", ";
        }

        result += CampaignProgressionCatalog.GetStoryPartDisplayName(part);
    }

    private void ResolveReferences()
    {
        if (settlementHud == null)
        {
            settlementHud = FindFirstObjectByType<SettlementHUD>();
        }

        if (settlementController == null)
        {
            settlementController = FindFirstObjectByType<SettlementController>();
        }
    }

    private void ShowMessage(string message)
    {
        ResolveReferences();

        if (settlementHud != null)
        {
            settlementHud.SetMessage(message);
        }

        Debug.Log(message, this);
    }

    private void SaveProgress()
    {
        if (SaveManager.Instance != null && PermanentProgress.Instance != null)
        {
            SaveManager.Instance.Save(PermanentProgress.Instance);
        }
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
