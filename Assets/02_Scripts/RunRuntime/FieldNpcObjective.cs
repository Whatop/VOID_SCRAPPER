using System;
using System.Collections.Generic;
using UnityEngine;

public enum FieldNpcServiceType
{
    Technician,
    Converter,
    BlackMarket,
    RescueContact
}

public enum FieldNpcState
{
    WaitingForRescue = 0,
    RescueCombat = 1,
    Available = 2,
    Exhausted = 3,
    RewardPending = 4
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FieldNpcObjective : MonoBehaviour, IInteractable
{
    [Header("NPC")]
    [SerializeField] private FieldNpcServiceType serviceType = FieldNpcServiceType.Technician;
    [SerializeField] private FieldNpcState state = FieldNpcState.WaitingForRescue;
    [SerializeField] private bool requiresRescue = true;
    [SerializeField] private bool oneUseService;

    [Header("Base Rescue Flow")]
    [SerializeField] private bool useBaseRescueFlow;
    [SerializeField] private string baseRescueBlockedText = "보안시설을 먼저 비활성화해라.";
    [SerializeField] private string baseReleasedWarning = "NPC를 구조했다. 대화하거나 기지 제어기를 사용해 포탈을 열 수 있다.";

    [Header("Interaction Text")]
    [SerializeField] private string rescueText = "구조 신호 확인";
    [SerializeField] private string busyText = "구조 작전 진행 중";
    [SerializeField] private string exhaustedText = "서비스 종료";

    [Header("Rescue Objective")]
    [SerializeField] private int rescueBasicEnemyCount = 2;
    [SerializeField] private int rescueShotgunEnemyCount = 1;
    [SerializeField] private EnemyDefinition basicEnemyDefinition;
    [SerializeField] private EnemyDefinition shotgunEnemyDefinition;
    [SerializeField] private GameObject basicEnemyPrefabFallback;
    [SerializeField] private GameObject shotgunEnemyPrefabFallback;
    [SerializeField] private float rescueSpawnRadiusMin = 4.5f;
    [SerializeField] private float rescueSpawnRadiusMax = 7f;
    [SerializeField] private int rescueTuningChipReward = 1;
    [SerializeField] private string objectiveId;

    [Header("Rescue Reward Capsule")]
    [SerializeField] private GameObject rescueRewardCapsulePrefab;
    [SerializeField] private Transform rewardCapsuleSpawnPoint;
    [SerializeField] private Vector2 rewardCapsuleSpawnOffset = new Vector2(0f, -1.25f);
    [SerializeField] private bool grantImmediatelyWhenCapsuleMissing = true;

    [Header("Technician")]
    [SerializeField] private int technicianTuningChipCost = 1;

    [Header("Converter")]
    [SerializeField] private int converterCreditCost = 25;

    [Header("Black Market")]
    [SerializeField] private int blackMarketCreditCost = 45;
    [SerializeField] private int blackMarketChoiceCount = 3;

    [Header("Rescue Contact Bonus")]
    [SerializeField] private bool registerAdditionalSignalOnContactService;

    [Header("Catalog / UI")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private RunLevelTraitSelectionUI rewardChoiceUI;

    [Header("Radar")]
    [SerializeField] private RadarTarget radarTarget;

    private readonly List<EnemyHealth> trackedEnemies = new List<EnemyHealth>();
    private bool serviceUsed;
    private GameObject activeRewardCapsule;

    public FieldNpcServiceType ServiceType => serviceType;
    public FieldNpcState State => state;
    public bool ShouldHoldMovement =>
        IsCaptiveInBase ||
        state == FieldNpcState.RescueCombat ||
        state == FieldNpcState.RewardPending;

    public bool UseBaseRescueFlow => useBaseRescueFlow;
    public bool IsCaptiveInBase => useBaseRescueFlow && state == FieldNpcState.WaitingForRescue;

    public event Action<FieldNpcObjective> CaptivityReleased;
    public event Action<FieldNpcObjective> ServiceUnlocked;

    public void Configure(
        FieldNpcServiceType configuredServiceType,
        bool configuredRequiresRescue,
        EnemyDefinition configuredBasicEnemy,
        EnemyDefinition configuredShotgunEnemy,
        TraitCatalog configuredTraitCatalog,
        ReinforcementCatalog configuredReinforcementCatalog,
        RunLevelTraitSelectionUI configuredRewardChoiceUI,
        string configuredObjectiveId = null,
        GameObject configuredRewardCapsulePrefab = null)
    {
        serviceType = configuredServiceType;
        requiresRescue = configuredRequiresRescue;
        basicEnemyDefinition = configuredBasicEnemy;
        shotgunEnemyDefinition = configuredShotgunEnemy;
        traitCatalog = configuredTraitCatalog;
        reinforcementCatalog = configuredReinforcementCatalog;
        rewardChoiceUI = configuredRewardChoiceUI;
        objectiveId = configuredObjectiveId;

        if (configuredRewardCapsulePrefab != null)
        {
            rescueRewardCapsulePrefab = configuredRewardCapsulePrefab;
        }

        serviceUsed = false;
        activeRewardCapsule = null;
        state = requiresRescue ? FieldNpcState.WaitingForRescue : FieldNpcState.Available;
        ResolveReferences();

        if (radarTarget != null)
        {
            radarTarget.SetMarkerType(RadarMarkerType.Event);
            radarTarget.SetVisible(true);
        }
    }

    public string InteractionText
    {
        get
        {
            if (state == FieldNpcState.RescueCombat)
            {
                return busyText;
            }

            if (state == FieldNpcState.Exhausted)
            {
                return exhaustedText;
            }

            if (state == FieldNpcState.WaitingForRescue)
            {
                return useBaseRescueFlow ? baseRescueBlockedText : rescueText;
            }

            if (state == FieldNpcState.RewardPending)
            {
                return "보상 캡슐 회수 대기";
            }

            return serviceType switch
            {
                FieldNpcServiceType.Technician => $"현장 튜닝 · 칩 {Mathf.Max(1, technicianTuningChipCost)}",
                FieldNpcServiceType.Converter => $"장비 변환 · {Mathf.Max(0, converterCreditCost)} 크레딧",
                FieldNpcServiceType.BlackMarket => $"미확인 장비 상자 · {Mathf.Max(0, blackMarketCreditCost)} 크레딧",
                FieldNpcServiceType.RescueContact => "추가 항법 정보 수신",
                _ => "NPC와 대화"
            };
        }
    }

    private void Reset()
    {
        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.isTrigger = true;
        }

        radarTarget = GetComponent<RadarTarget>();
    }

    private void Awake()
    {
        Collider2D collider = GetComponent<Collider2D>();

        if (collider != null)
        {
            collider.isTrigger = true;
        }

        if (radarTarget == null)
        {
            radarTarget = GetComponent<RadarTarget>();
        }

        ResolveReferences();

        if (!requiresRescue)
        {
            state = FieldNpcState.Available;
        }
    }

    private void OnDisable()
    {
        UntrackEnemies();
    }

    public bool CanInteract(GameObject interactor)
    {
        return interactor != null &&
               state != FieldNpcState.RescueCombat &&
               state != FieldNpcState.Exhausted &&
               state != FieldNpcState.RewardPending;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        ResolveReferences();

        if (state == FieldNpcState.WaitingForRescue)
        {
            if (useBaseRescueFlow)
            {
                ShowWarning(baseRescueBlockedText);
                AudioManager.Play(SoundEventIds.ActionDenied);
                return;
            }

            BeginRescue(interactor);
            return;
        }

        if (state == FieldNpcState.RewardPending)
        {
            ShowWarning("투하된 보상 캡슐을 먼저 회수해라.");
            AudioManager.Play(SoundEventIds.ActionDenied);
            return;
        }

        ExecuteService(interactor);
    }

    private void BeginRescue(GameObject interactor)
    {
        state = FieldNpcState.RescueCombat;
        ShowWarning("구조 신호 확인. 접근하는 적을 제거해라.");
        AudioManager.PlayAt(SoundEventIds.EventStart, transform.position);

        SpawnRescueBatch(basicEnemyDefinition, basicEnemyPrefabFallback, rescueBasicEnemyCount, interactor);
        SpawnRescueBatch(shotgunEnemyDefinition, shotgunEnemyPrefabFallback, rescueShotgunEnemyCount, interactor);

        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0)
        {
            CompleteRescue();
        }
    }

    private void SpawnRescueBatch(
        EnemyDefinition definition,
        GameObject fallbackPrefab,
        int count,
        GameObject interactor)
    {
        GameObject prefab = definition != null && definition.EnemyPrefab != null
            ? definition.EnemyPrefab
            : fallbackPrefab;

        if (prefab == null)
        {
            return;
        }

        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            float minRadius = Mathf.Max(0.5f, Mathf.Min(rescueSpawnRadiusMin, rescueSpawnRadiusMax));
            float maxRadius = Mathf.Max(minRadius, Mathf.Max(rescueSpawnRadiusMin, rescueSpawnRadiusMax));
            Vector2 position = (Vector2)transform.position + direction * UnityEngine.Random.Range(minRadius, maxRadius);

            GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
            EnemyBaseAI ai = spawned.GetComponentInChildren<EnemyBaseAI>(true);
            EnemyHealth health = spawned.GetComponentInChildren<EnemyHealth>(true);

            if (ai != null)
            {
                if (definition != null)
                {
                    ai.ApplyDefinition(definition);
                }

                if (interactor != null)
                {
                    ai.SetTarget(interactor.transform);
                }
            }
            else if (health != null && definition != null)
            {
                health.ApplyDefinition(definition);
            }

            if (health != null)
            {
                trackedEnemies.Add(health);
                health.Died += HandleRescueEnemyDied;
            }

            Transform target = interactor != null ? interactor.transform : null;
            bool arrivalStarted = EnemyArrivalSpawnUtility.BeginArrival(
                spawned,
                position,
                target,
                true,
                transform.position
            );

            if (!arrivalStarted && ai != null && target != null)
            {
                ai.AlertTo(target.position);
            }
        }
    }

    private void HandleRescueEnemyDied(EnemyHealth health)
    {
        if (health != null)
        {
            health.Died -= HandleRescueEnemyDied;
        }

        trackedEnemies.Remove(health);
        CleanupTrackedEnemies();

        if (trackedEnemies.Count <= 0 && state == FieldNpcState.RescueCombat)
        {
            CompleteRescue();
        }
    }

    private void CompleteRescue()
    {
        UntrackEnemies();
        AudioManager.PlayAt(SoundEventIds.EventComplete, transform.position);

        if (TrySpawnRescueRewardCapsule())
        {
            state = FieldNpcState.RewardPending;
            ShowWarning("NPC 구조 완료 · 보상 캡슐이 투하되었습니다.");
            return;
        }

        state = FieldNpcState.Available;

        if (grantImmediatelyWhenCapsuleMissing)
        {
            GrantRescueRewardDirectly();
            return;
        }

        ShowWarning("NPC 구조 완료 · 보상 캡슐 프리팹이 연결되지 않았습니다.");
    }

    private bool TrySpawnRescueRewardCapsule()
    {
        if (rescueRewardCapsulePrefab == null)
        {
            return false;
        }

        Vector3 spawnPosition = rewardCapsuleSpawnPoint != null
            ? rewardCapsuleSpawnPoint.position
            : transform.position + (Vector3)rewardCapsuleSpawnOffset;

        activeRewardCapsule = Instantiate(
            rescueRewardCapsulePrefab,
            spawnPosition,
            Quaternion.identity
        );

        if (activeRewardCapsule == null)
        {
            return false;
        }

        RewardCapsule capsule = activeRewardCapsule.GetComponent<RewardCapsule>();

        if (capsule == null)
        {
            capsule = activeRewardCapsule.AddComponent<RewardCapsule>();
        }

        bool configured = capsule.ConfigureNpcRescueReward(
            rescueTuningChipReward,
            ResolveObjectiveId(),
            HandleRescueCapsuleClaimed
        );

        if (configured)
        {
            return true;
        }

        Destroy(activeRewardCapsule);
        activeRewardCapsule = null;
        return false;
    }

    private void HandleRescueCapsuleClaimed()
    {
        activeRewardCapsule = null;
        state = FieldNpcState.Available;
        ShowWarning("보상 회수 완료 · NPC 서비스를 이용할 수 있습니다.");
        ServiceUnlocked?.Invoke(this);
    }

    private void GrantRescueRewardDirectly()
    {
        if (rescueTuningChipReward > 0 &&
            RunManager.Instance != null &&
            RunManager.Instance.HasActiveRun)
        {
            RunManager.Instance.AddCurrency(CurrencyType.TuningChips, rescueTuningChipReward);
        }

        ExpeditionObjectiveDirector.Instance?.RegisterObjective(
            ResolveObjectiveId(),
            HighValueObjectiveSource.NpcRescue
        );

        ShowWarning($"NPC 구조 완료 · 튜닝 칩 +{Mathf.Max(0, rescueTuningChipReward)}");
        ServiceUnlocked?.Invoke(this);
    }

    public void SetBaseCaptiveState(bool captive)
    {
        useBaseRescueFlow = captive;

        if (captive)
        {
            requiresRescue = true;
            state = FieldNpcState.WaitingForRescue;
            return;
        }

        if (state == FieldNpcState.WaitingForRescue ||
            state == FieldNpcState.RescueCombat ||
            state == FieldNpcState.RewardPending)
        {
            state = FieldNpcState.Available;
        }
    }

    public bool ReleaseFromCaptivity(bool showMessage = true)
    {
        if (!useBaseRescueFlow)
        {
            return false;
        }

        if (state != FieldNpcState.WaitingForRescue)
        {
            return false;
        }

        requiresRescue = false;
        state = FieldNpcState.Available;

        if (showMessage)
        {
            ShowWarning(baseReleasedWarning);
        }

        ExpeditionObjectiveDirector.Instance?.RegisterObjective(
            ResolveObjectiveId(),
            HighValueObjectiveSource.NpcRescue
        );

        CaptivityReleased?.Invoke(this);
        ServiceUnlocked?.Invoke(this);
        return true;
    }

    private void ExecuteService(GameObject interactor)
    {
        if (oneUseService && serviceUsed)
        {
            state = FieldNpcState.Exhausted;
            return;
        }

        bool success = serviceType switch
        {
            FieldNpcServiceType.Technician => ExecuteTechnician(),
            FieldNpcServiceType.Converter => ExecuteConverter(interactor),
            FieldNpcServiceType.BlackMarket => ExecuteBlackMarket(),
            FieldNpcServiceType.RescueContact => ExecuteRescueContact(),
            _ => false
        };

        if (!success)
        {
            return;
        }

        serviceUsed = true;

        if (oneUseService)
        {
            state = FieldNpcState.Exhausted;
        }
    }

    private bool ExecuteTechnician()
    {
        ResolveReferences();

        if (rewardChoiceUI == null)
        {
            ShowWarning("현장 튜닝 UI를 찾지 못했습니다.");
            return false;
        }

        if (!HasCurrency(CurrencyType.TuningChips, technicianTuningChipCost))
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            ShowWarning("튜닝 칩이 부족합니다.");
            return false;
        }

        bool opened = rewardChoiceUI.ShowTraitTuningChoices(
            Mathf.Max(1, technicianTuningChipCost),
            transform.position,
            _ => { }
        );

        if (!opened)
        {
            ShowWarning("강화 가능한 보유 특성이 없습니다.");
        }

        return opened;
    }

    private bool ExecuteConverter(GameObject interactor)
    {
        ResolveReferences();

        PlayerReinforcementController controller = interactor != null
            ? interactor.GetComponentInParent<PlayerReinforcementController>()
            : null;

        if (controller == null)
        {
            controller = FindFirstObjectByType<PlayerReinforcementController>();
        }

        if (controller == null || controller.EquippedDefinition == null)
        {
            ShowWarning("변환할 Reinforcement가 없습니다.");
            return false;
        }

        ReinforcementDefinition replacement = RunRewardChoiceGenerator.PickSameRarityReplacement(
            reinforcementCatalog,
            controller.EquippedDefinition,
            ResolveSelectedWeaponTree()
        );

        if (replacement == null)
        {
            ShowWarning("같은 등급의 변환 후보가 없습니다.");
            return false;
        }

        if (!TrySpendCurrency(CurrencyType.Credits, converterCreditCost))
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            ShowWarning("크레딧이 부족합니다.");
            return false;
        }

        bool equipped = controller.EquipWithoutDropping(replacement);

        if (!equipped)
        {
            RunManager.Instance?.CurrentRun?.Wallet.Add(CurrencyType.Credits, converterCreditCost);
            return false;
        }

        ShowWarning($"장비 변환 완료: {replacement.DisplayName}");
        return true;
    }

    private bool ExecuteBlackMarket()
    {
        ResolveReferences();

        if (rewardChoiceUI == null)
        {
            ShowWarning("암시장 보상 UI를 찾지 못했습니다.");
            return false;
        }

        if (!HasCurrency(CurrencyType.Credits, blackMarketCreditCost))
        {
            AudioManager.Play(SoundEventIds.ActionDenied);
            ShowWarning("크레딧이 부족합니다.");
            return false;
        }

        bool opened = rewardChoiceUI.ShowBlackMarketChoices(
            Mathf.Max(1, blackMarketChoiceCount),
            transform.position,
            result =>
            {
                if (result.Success)
                {
                    TrySpendCurrency(CurrencyType.Credits, blackMarketCreditCost);
                }
            }
        );

        return opened;
    }

    private bool ExecuteRescueContact()
    {
        ShowWarning("특수 화물 신호와 코어 추적 데이터를 갱신했습니다.");

        if (registerAdditionalSignalOnContactService)
        {
            ExpeditionObjectiveDirector.Instance?.RegisterObjective(
                $"{ResolveObjectiveId()}_contact_bonus",
                HighValueObjectiveSource.NpcRescue
            );
        }

        return true;
    }

    private bool HasCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               RunManager.Instance.CurrentRun.Wallet.CanSpend(type, amount);
    }

    private bool TrySpendCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return RunManager.Instance != null &&
               RunManager.Instance.HasActiveRun &&
               RunManager.Instance.CurrentRun.Wallet.TrySpend(type, amount);
    }

    private WeaponTreeType ResolveSelectedWeaponTree()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        return WeaponTreeType.MachineGun;
    }

    private void ResolveReferences()
    {
        if (rewardChoiceUI == null)
        {
            rewardChoiceUI = FindFirstObjectByType<RunLevelTraitSelectionUI>();
        }

        ShopStockController shopStock = FindFirstObjectByType<ShopStockController>();

        if (traitCatalog == null && shopStock != null)
        {
            traitCatalog = shopStock.TraitCatalog;
        }

        if (reinforcementCatalog == null && shopStock != null)
        {
            reinforcementCatalog = shopStock.ReinforcementCatalog;
        }

        if (rewardChoiceUI != null)
        {
            rewardChoiceUI.ConfigureCatalogs(traitCatalog, reinforcementCatalog);
        }
    }

    private void CleanupTrackedEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            EnemyHealth health = trackedEnemies[i];

            if (health == null || health.IsDead)
            {
                if (health != null)
                {
                    health.Died -= HandleRescueEnemyDied;
                }

                trackedEnemies.RemoveAt(i);
            }
        }
    }

    private void UntrackEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            if (trackedEnemies[i] != null)
            {
                trackedEnemies[i].Died -= HandleRescueEnemyDied;
            }
        }

        trackedEnemies.Clear();
    }

    private string ResolveObjectiveId()
    {
        return string.IsNullOrWhiteSpace(objectiveId)
            ? $"field_npc_{serviceType}_{GetInstanceID()}"
            : objectiveId;
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
