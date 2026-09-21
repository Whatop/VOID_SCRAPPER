using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class RunLevelTraitSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExpeditionHUD expeditionHUD;

    [Header("Trait Source")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeInspectorTraitDefinitions = true;
    [SerializeField] private bool includeHiddenTraits;
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Reinforcement Source")]
    [SerializeField] private ReinforcementCatalog reinforcementCatalog;
    [SerializeField] private bool includeInspectorReinforcementDefinitions = true;
    [SerializeField] private List<ReinforcementDefinition> reinforcementDefinitions = new List<ReinforcementDefinition>();

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private RunTraitChoiceButtonUI[] choiceButtons;

    [Header("Choice")]
    [SerializeField] private int choiceCount = 3;
    [SerializeField] private bool pauseGameplayWhileSelecting = true;

    private readonly List<TraitDefinition> resolvedTraits = new List<TraitDefinition>();
    private readonly List<ReinforcementDefinition> resolvedReinforcements = new List<ReinforcementDefinition>();
    private readonly List<RunRewardOption> currentOptions = new List<RunRewardOption>();
    private bool showing;
    private Vector2 currentSourcePosition;
    private Action<RunRewardChoiceResult> completionCallback;
    private int tuningChipCostOnSelection;
    private bool selectionLocked;

    public bool IsShowing => showing;
    public TraitCatalog TraitCatalog => traitCatalog;
    public ReinforcementCatalog ReinforcementCatalog => reinforcementCatalog;

    private void Awake()
    {
        ResolveReferences();
        ConfigureTypography();
        ResolveDefinitions();
        HideImmediate();
    }

    private void ConfigureTypography()
    {
        ConfigureText(titleText, 11f, 8f, false);
        ConfigureText(bodyText, 7f, 5.5f, true);
    }

    private static void ConfigureText(TextMeshProUGUI text, float maximumSize, float minimumSize, bool wrap)
    {
        if (text == null)
        {
            return;
        }

        text.fontSize = maximumSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void Start()
    {
        ResolveReferences();
        ResolveDefinitions();
    }

    private void OnDisable()
    {
        if (showing && pauseGameplayWhileSelecting)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }

        showing = false;
        selectionLocked = false;
    }

    public void ConfigureCatalogs(TraitCatalog traits, ReinforcementCatalog reinforcements)
    {
        if (traits != null)
        {
            traitCatalog = traits;
        }

        if (reinforcements != null)
        {
            reinforcementCatalog = reinforcements;
        }

        ResolveDefinitions();
    }

    public bool TryBuildRewardOptions(
        SpecialRewardMode mode,
        int requestedChoiceCount,
        RunRewardRarity minimumRarity,
        bool forceAtLeastOneRareOrBetter,
        out List<RunRewardOption> options)
    {
        ResolveReferences();
        ResolveDefinitions();

        PlayerReinforcementController reinforcementController = FindFirstObjectByType<PlayerReinforcementController>();
        string equippedId = reinforcementController != null
            ? reinforcementController.EquippedReinforcementId
            : string.Empty;

        options = RunRewardChoiceGenerator.BuildOptionsFromDefinitions(
            mode,
            Mathf.Max(1, requestedChoiceCount),
            minimumRarity,
            resolvedTraits,
            resolvedReinforcements,
            ResolveSelectedWeaponTree(),
            forceAtLeastOneRareOrBetter,
            equippedId
        );

        return options != null && options.Count > 0;
    }

    public bool ShowSpecialContainerChoices(
        SpecialRewardMode mode,
        int requestedChoiceCount,
        bool forceAtLeastOneRareOrBetter,
        Vector2 sourcePosition,
        Action<RunRewardChoiceResult> onCompleted,
        out bool offeredRareOrBetter)
    {
        offeredRareOrBetter = false;
        int finalCount = ResolveChoiceCount(requestedChoiceCount);

        if (!TryBuildRewardOptions(
                mode,
                finalCount,
                RunRewardRarity.Common,
                forceAtLeastOneRareOrBetter,
                out List<RunRewardOption> options))
        {
            return false;
        }

        offeredRareOrBetter = RunRewardChoiceGenerator.ContainsRareOrBetter(options);

        string title = mode switch
        {
            SpecialRewardMode.TraitOnly => "특수 패시브 화물",
            SpecialRewardMode.ReinforcementOnly => "특수 액티브 화물",
            _ => "특수 장비 화물"
        };

        return ShowOptions(
            title,
            "탐사 중 사용할 보상 하나를 선택해라. 중복 특성은 자동으로 강화된다.",
            options,
            sourcePosition,
            onCompleted,
            0
        );
    }

    public bool ShowBossRewardChoices(
        int requestedChoiceCount,
        bool rareGuaranteed,
        Vector2 sourcePosition,
        Action<RunRewardChoiceResult> onCompleted)
    {
        int finalCount = ResolveChoiceCount(requestedChoiceCount);
        RunRewardRarity minimumRarity = rareGuaranteed
            ? RunRewardRarity.Rare
            : RunRewardRarity.Common;

        if (!TryBuildRewardOptions(
                SpecialRewardMode.Mixed,
                finalCount,
                minimumRarity,
                rareGuaranteed,
                out List<RunRewardOption> options))
        {
            return false;
        }

        return ShowOptions(
            "보스 회수품",
            "희귀 이상 장비를 선택해 현재 탐사 빌드를 완성해라.",
            options,
            sourcePosition,
            onCompleted,
            0
        );
    }

    public bool ShowBlackMarketChoices(
        int requestedChoiceCount,
        Vector2 sourcePosition,
        Action<RunRewardChoiceResult> onCompleted)
    {
        int finalCount = ResolveChoiceCount(requestedChoiceCount);

        if (!TryBuildRewardOptions(
                SpecialRewardMode.Mixed,
                finalCount,
                RunRewardRarity.Common,
                false,
                out List<RunRewardOption> options))
        {
            return false;
        }

        return ShowOptions(
            "암시장 미확인 화물",
            "구매할 장비 하나를 선택해라.",
            options,
            sourcePosition,
            onCompleted,
            0
        );
    }

    public bool ShowTraitTuningChoices(
        int tuningChipCost,
        Vector2 sourcePosition,
        Action<RunRewardChoiceResult> onCompleted)
    {
        ResolveDefinitions();
        int finalCount = ResolveChoiceCount(choiceCount);
        List<RunRewardOption> options = BuildOwnedTraitUpgradeOptions(finalCount);

        if (options.Count <= 0)
        {
            return false;
        }

        return ShowOptions(
            "현장 튜닝",
            $"튜닝 칩 {Mathf.Max(1, tuningChipCost)}개를 사용해 보유 특성 하나를 강화한다.",
            options,
            sourcePosition,
            onCompleted,
            Mathf.Max(1, tuningChipCost)
        );
    }

    private void ResolveReferences()
    {
        if (expeditionHUD == null)
        {
            expeditionHUD = FindFirstObjectByType<ExpeditionHUD>();
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot != null
                ? panelRoot.GetComponent<CanvasGroup>()
                : null;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // 선택 UI 매니저와 패널 루트가 같은 오브젝트여도
        // 매니저 자체가 비활성화되어 검색/호출 불가능해지지 않게 유지한다.
        if (panelRoot == gameObject && canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
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
    }

    private bool ShowOptions(
        string title,
        string body,
        IReadOnlyList<RunRewardOption> options,
        Vector2 sourcePosition,
        Action<RunRewardChoiceResult> onCompleted,
        int tuningChipCost)
    {
        ResolveReferences();

        if (showing || options == null || options.Count <= 0 || choiceButtons == null || choiceButtons.Length <= 0)
        {
            return false;
        }

        currentOptions.Clear();

        int visibleCount = Mathf.Min(options.Count, ResolveChoiceCount(options.Count));

        for (int i = 0; i < visibleCount; i++)
        {
            if (options[i] != null)
            {
                currentOptions.Add(options[i]);
            }
        }

        if (currentOptions.Count <= 0)
        {
            return false;
        }

        showing = true;
        selectionLocked = false;
        currentSourcePosition = sourcePosition;
        completionCallback = onCompleted;
        tuningChipCostOnSelection = Mathf.Max(0, tuningChipCost);

        if (pauseGameplayWhileSelecting)
        {
            GameplayPauseManager.Instance.PushPause(this, "RunRewardChoice");
        }

        SetVisible(true);
        AudioManager.Play(SoundEventIds.UiPanelOpen);

        if (titleText != null)
        {
            titleText.text = title;
        }

        if (bodyText != null)
        {
            bodyText.text = body;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            RunTraitChoiceButtonUI button = choiceButtons[i];

            if (button == null)
            {
                continue;
            }

            if (i >= currentOptions.Count)
            {
                button.Clear();
                continue;
            }

            button.SetupReward(currentOptions[i], HandleRewardSelected);
        }

        return true;
    }

    private void HandleRewardSelected(RunRewardOption option)
    {
        if (!showing || selectionLocked || option == null)
        {
            return;
        }

        bool tuningSpent = false;

        if (tuningChipCostOnSelection > 0)
        {
            RunWallet wallet = ResolveWallet();

            if (wallet == null || !wallet.TrySpend(CurrencyType.TuningChips, tuningChipCostOnSelection))
            {
                AudioManager.Play(SoundEventIds.ActionDenied);
                expeditionHUD?.ShowWarning("튜닝 칩이 부족합니다.");
                return;
            }

            tuningSpent = true;
        }

        selectionLocked = true;
        SetChoiceButtonsInteractable(false);

        RunRewardChoiceResult result = RunRewardChoiceApplier.Apply(option, currentSourcePosition);

        if (!result.Success)
        {
            if (tuningSpent)
            {
                ResolveWallet()?.Add(CurrencyType.TuningChips, tuningChipCostOnSelection);
            }

            AudioManager.Play(SoundEventIds.ActionDenied);
            expeditionHUD?.ShowWarning("보상 적용에 실패했습니다.");
            selectionLocked = false;
            SetChoiceButtonsInteractable(true);
            return;
        }

        Action<RunRewardChoiceResult> callback = completionCallback;
        CloseChoice();
        callback?.Invoke(result);
    }

    private List<RunRewardOption> BuildOwnedTraitUpgradeOptions(int requestedCount)
    {
        List<RunRewardOption> result = new List<RunRewardOption>();
        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition trait = resolvedTraits[i];

            if (trait == null ||
                !trait.CanAppearAsLevelUpTrait ||
                !RunTraitAcquisitionService.IsOrdinaryCandidate(trait))
            {
                continue;
            }

            int currentLevel = store.GetLevel(trait.TraitId);

            if (currentLevel <= 0 || currentLevel >= trait.MaxLevel)
            {
                continue;
            }

            result.Add(RunRewardOption.FromTrait(trait));
        }

        Shuffle(result);

        if (result.Count > requestedCount)
        {
            result.RemoveRange(requestedCount, result.Count - requestedCount);
        }

        return result;
    }

    private void ResolveDefinitions()
    {
        ResolveReferences();
        resolvedTraits.Clear();
        resolvedReinforcements.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraits);
        }

        if (includeInspectorTraitDefinitions && traitDefinitions != null)
        {
            for (int i = 0; i < traitDefinitions.Count; i++)
            {
                AppendUniqueTrait(traitDefinitions[i]);
            }
        }

        if (!includeHiddenTraits)
        {
            for (int i = resolvedTraits.Count - 1; i >= 0; i--)
            {
                if (resolvedTraits[i] == null || resolvedTraits[i].IsHidden)
                {
                    resolvedTraits.RemoveAt(i);
                }
            }
        }

        if (reinforcementCatalog != null)
        {
            reinforcementCatalog.AppendAllTo(resolvedReinforcements);
        }

        if (includeInspectorReinforcementDefinitions && reinforcementDefinitions != null)
        {
            for (int i = 0; i < reinforcementDefinitions.Count; i++)
            {
                AppendUniqueReinforcement(reinforcementDefinitions[i]);
            }
        }
    }

    private void AppendUniqueTrait(TraitDefinition trait)
    {
        if (trait == null)
        {
            return;
        }

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            if (resolvedTraits[i] != null && resolvedTraits[i].TraitId == trait.TraitId)
            {
                return;
            }
        }

        resolvedTraits.Add(trait);
    }

    private void AppendUniqueReinforcement(ReinforcementDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        for (int i = 0; i < resolvedReinforcements.Count; i++)
        {
            if (resolvedReinforcements[i] != null &&
                resolvedReinforcements[i].EquipmentId == definition.EquipmentId)
            {
                return;
            }
        }

        resolvedReinforcements.Add(definition);
    }

    private RunWallet ResolveWallet()
    {
        return RunManager.Instance != null && RunManager.Instance.HasActiveRun
            ? RunManager.Instance.CurrentRun.Wallet
            : null;
    }

    private WeaponTreeType ResolveSelectedWeaponTree()
    {
        if (RunManager.Instance != null && RunManager.Instance.HasActiveRun)
        {
            return RunManager.Instance.CurrentRun.SelectedWeaponTree;
        }

        if (PermanentProgress.Instance != null)
        {
            return PermanentProgress.Instance.LastSelectedWeaponTree;
        }

        return WeaponTreeType.MachineGun;
    }

    private int ResolveChoiceCount(int requested)
    {
        int capacity = choiceButtons != null ? choiceButtons.Length : 0;
        return capacity > 0 ? Mathf.Clamp(requested, 1, capacity) : 0;
    }

    private void CloseChoice()
    {
        if (pauseGameplayWhileSelecting && showing)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }

        showing = false;
        selectionLocked = false;
        tuningChipCostOnSelection = 0;
        completionCallback = null;
        currentOptions.Clear();
        SetVisible(false);
        AudioManager.Play(SoundEventIds.UiPanelClose);
    }

    private void HideImmediate()
    {
        showing = false;
        selectionLocked = false;
        SetVisible(false);
    }

    private void SetChoiceButtonsInteractable(bool interactable)
    {
        if (choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            choiceButtons[i]?.SetInteractable(interactable);
        }
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int index = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[index];
            list[index] = temp;
        }
    }
}

public static class TraitEffectTextUtility
{
    public static string BuildEffectText(TraitDefinition trait, int level)
    {
        if (trait == null || trait.LevelEffects == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < trait.LevelEffects.Count; i++)
        {
            TraitLevelEffect effect = trait.LevelEffects[i];

            if (effect == null || effect.Level != level)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(FormatEffect(effect.EffectType, effect.Value));
        }

        return builder.ToString();
    }

    public static string FormatEffect(TraitEffectType effectType, float value)
    {
        return effectType switch
        {
            TraitEffectType.DamagePercent => $"공격력 +{value:0.#}%",
            TraitEffectType.ProjectileSpeedPercent => $"탄속 +{value:0.#}%",
            TraitEffectType.RangePercent => $"사거리 +{value:0.#}%",
            TraitEffectType.MoveSpeedPercent => $"이동속도 +{value:0.#}%",
            TraitEffectType.DashCooldownReduction => $"대쉬 쿨다운 -{Mathf.Abs(value):0.##}초",
            TraitEffectType.DashDistanceBonus => $"대쉬 거리 +{value:0.##}",
            TraitEffectType.MaxHpBonus => $"최대 체력 +{value:0.#}",
            TraitEffectType.HealEfficiencyPercent => $"회복 자원 효과 +{value:0.#}%",
            TraitEffectType.PickupRangeBonus => $"아이템 흡수 범위 +{value:0.##}",
            TraitEffectType.SpreadReductionPercent => $"탄 퍼짐 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.ProjectileCountBonus => $"탄 수 +{Mathf.RoundToInt(value)}",
            TraitEffectType.PierceCountBonus => $"관통 횟수 +{Mathf.RoundToInt(value)}",
            TraitEffectType.ChargeTimeReductionPercent => $"차징 시간 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.ChargeDamagePercent => $"차징 피해 +{value:0.#}%",
            TraitEffectType.HomingAngleBonus => $"유도 각도 +{value:0.#}도",
            TraitEffectType.HomingRangeBonus => $"유도 거리 +{value:0.##}",
            TraitEffectType.FireRatePercent => $"연사력 +{value:0.#}%",
            TraitEffectType.CloseRangeDamageReductionPercent => $"근거리 피해 감소 +{value:0.#}%",
            TraitEffectType.DashDamageReductionPercent => $"대쉬 후 피해 감소 +{value:0.#}%",
            TraitEffectType.CloseRangeSuppressionPercent => $"근거리 제압 효과 +{value:0.#}%",
            TraitEffectType.ChargeSightBonusPercent => $"차징 중 시야 +{value:0.#}%",
            TraitEffectType.ChargedProjectileSizePercent => $"차징탄 크기 +{value:0.#}%",
            TraitEffectType.RemovePierceDamageFalloff => "관통 후 피해 감쇠 제거",
            TraitEffectType.CargoCapacityBonus => $"적재 한도 +{value:0.#}",
            TraitEffectType.HarvestYieldPercent => $"수확량 +{value:0.#}%",
            TraitEffectType.HarvestObjectDamagePercent => $"수확 오브젝트 피해 +{value:0.#}%",
            TraitEffectType.EmergencyReturnCapacityRatioBonus => $"긴급복귀 보존 한도 +{value:0.#}%p",
            TraitEffectType.RadarScanRadiusBonus => $"레이더 반경 +{value:0.#}",
            TraitEffectType.ActiveCooldownReductionPercent => $"액티브 쿨다운 -{Mathf.Abs(value):0.#}%",
            TraitEffectType.RadarTauntDurationBonus => $"도발 지속시간 +{value:0.#}초",
            TraitEffectType.RadarStealthDurationBonus => $"은밀 표식 유지 +{value:0.#}초",
            TraitEffectType.SniperSemiAutoMode => "짧은 클릭으로 세미오토 레이저 발사",
            TraitEffectType.ShotgunCloseRangeDamagePercent => $"샷건 초근거리 피해 최대 +{value:0.#}%",
            TraitEffectType.MachineGunTerminalGuidance => "기관총 종말 유도 활성화",
            TraitEffectType.PeriodicReflectiveShield => $"반사 방벽 재충전 {value:0.#}초",
            TraitEffectType.MachineGunDashMissileSalvo => "대쉬 시 추격 미사일 3발 사출",
            TraitEffectType.SniperDashEchoShot => "대쉬 위치에서 다음 저격 사격을 40% 위력으로 복제",
            _ => $"{effectType} {value:0.##}"
        };
    }
}
