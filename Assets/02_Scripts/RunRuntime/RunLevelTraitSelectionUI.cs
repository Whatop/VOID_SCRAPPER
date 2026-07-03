using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class RunLevelTraitSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RunLevelSystem runLevelSystem;
    [SerializeField] private RunTraitEffectApplier traitEffectApplier;
    [SerializeField] private ExpeditionHUD expeditionHUD;

    [Header("Trait Source")]
    [SerializeField] private TraitCatalog traitCatalog;
    [SerializeField] private bool includeInspectorTraitDefinitions = true;
    [SerializeField] private bool includeHiddenTraits;
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

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
    private readonly List<TraitDefinition> candidateBuffer = new List<TraitDefinition>();
    private readonly List<TraitDefinition> choiceBuffer = new List<TraitDefinition>();
    private readonly Queue<int> pendingLevelQueue = new Queue<int>();

    private bool showing;

    private void Awake()
    {
        ResolveReferences();
        HideImmediate();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (showing && pauseGameplayWhileSelecting)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }
    }

    private void ResolveReferences()
    {
        if (runLevelSystem == null)
        {
            runLevelSystem = FindFirstObjectByType<RunLevelSystem>();
        }

        if (traitEffectApplier == null)
        {
            traitEffectApplier = FindFirstObjectByType<RunTraitEffectApplier>();
        }

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
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Subscribe()
    {
        if (runLevelSystem != null)
        {
            runLevelSystem.LeveledUp += HandleLeveledUp;
        }
    }

    private void Unsubscribe()
    {
        if (runLevelSystem != null)
        {
            runLevelSystem.LeveledUp -= HandleLeveledUp;
        }
    }

    private void HandleLeveledUp(int newLevel)
    {
        pendingLevelQueue.Enqueue(newLevel);

        if (!showing)
        {
            ShowNextPendingLevel();
        }
    }

    private void ShowNextPendingLevel()
    {
        if (pendingLevelQueue.Count <= 0)
        {
            Hide();
            return;
        }

        int level = pendingLevelQueue.Dequeue();
        ShowForLevel(level);
    }

    private void ShowForLevel(int level)
    {
        ResolveReferences();
        ResolveTraitDefinitions();
        BuildCandidateList();
        BuildChoiceList();

        if (choiceBuffer.Count <= 0)
        {
            expeditionHUD?.ShowWarning("선택 가능한 특성이 없습니다.");
            ShowNextPendingLevel();
            return;
        }

        showing = true;

        if (pauseGameplayWhileSelecting)
        {
            GameplayPauseManager.Instance.PushPause(this, "LevelUpTraitSelection");
        }

        SetVisible(true);

        if (titleText != null)
        {
            titleText.text = $"레벨 {level} 달성";
        }

        if (bodyText != null)
        {
            bodyText.text = "이번 탐사 동안 적용할 특성을 선택해라.";
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            RunTraitChoiceButtonUI button = choiceButtons[i];

            if (button == null)
            {
                continue;
            }

            if (i >= choiceBuffer.Count)
            {
                button.Clear();
                continue;
            }

            TraitDefinition trait = choiceBuffer[i];
            int currentLevel = store != null ? store.GetLevel(trait.TraitId) : 0;
            int nextLevel = Mathf.Clamp(currentLevel + 1, 1, trait.MaxLevel);
            string effectText = TraitEffectTextUtility.BuildEffectText(trait, nextLevel);

            button.Setup(trait, currentLevel, nextLevel, effectText, HandleTraitSelected);
        }
    }

    private void HandleTraitSelected(TraitDefinition trait)
    {
        if (trait == null)
        {
            return;
        }

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        int newLevel = store.AddOrUpgrade(trait);

        if (newLevel <= 0)
        {
            expeditionHUD?.ShowWarning("특성 적용에 실패했습니다.");
            return;
        }

        if (traitEffectApplier == null)
        {
            traitEffectApplier = FindFirstObjectByType<RunTraitEffectApplier>();
        }

        if (traitEffectApplier != null)
        {
            traitEffectApplier.ApplyTraitLevel(trait, newLevel);
        }

        expeditionHUD?.ShowWarning($"{trait.DisplayName} Lv{newLevel} 적용");

        if (pauseGameplayWhileSelecting)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }

        showing = false;
        SetVisible(false);
        ShowNextPendingLevel();
    }

    private void ResolveTraitDefinitions()
    {
        resolvedTraits.Clear();

        if (traitCatalog != null)
        {
            traitCatalog.AppendAllTo(resolvedTraits);
        }

        if (!includeInspectorTraitDefinitions || traitDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            AppendUniqueTrait(traitDefinitions[i]);
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
            TraitDefinition existing = resolvedTraits[i];

            if (existing != null && existing.TraitId == trait.TraitId)
            {
                return;
            }
        }

        resolvedTraits.Add(trait);
    }

    private void BuildCandidateList()
    {
        candidateBuffer.Clear();

        RunRuntimeTraitStore store = RunRuntimeTraitStore.Instance;
        WeaponTreeType selectedWeaponTree = ResolveSelectedWeaponTree();

        for (int i = 0; i < resolvedTraits.Count; i++)
        {
            TraitDefinition trait = resolvedTraits[i];

            if (trait == null)
            {
                continue;
            }

            if (!trait.IsAvailableFor(selectedWeaponTree))
            {
                continue;
            }

            if (!IsLevelUpTraitEligible(trait))
            {
                continue;
            }

            if (store != null && !store.CanUpgrade(trait))
            {
                continue;
            }

            candidateBuffer.Add(trait);
        }
    }

    private bool IsLevelUpTraitEligible(TraitDefinition trait)
    {
        if (trait == null)
        {
            return false;
        }

        if (trait.CanAppearAsLevelUpTrait)
        {
            return true;
        }

        if (includeHiddenTraits && trait.IsHidden)
        {
            return true;
        }

        return false;
    }

    private void BuildChoiceList()
    {
        choiceBuffer.Clear();

        int targetCount = Mathf.Clamp(choiceCount, 1, Mathf.Max(1, choiceButtons != null ? choiceButtons.Length : 1));

        while (candidateBuffer.Count > 0 && choiceBuffer.Count < targetCount)
        {
            int index = Random.Range(0, candidateBuffer.Count);
            TraitDefinition picked = candidateBuffer[index];
            candidateBuffer.RemoveAt(index);

            if (picked != null)
            {
                choiceBuffer.Add(picked);
            }
        }
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

    private void Hide()
    {
        if (pauseGameplayWhileSelecting && showing)
        {
            GameplayPauseManager.Instance.PopPause(this);
        }

        showing = false;
        SetVisible(false);
    }

    private void HideImmediate()
    {
        showing = false;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
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
        switch (effectType)
        {
            case TraitEffectType.DamagePercent:
                return $"공격력 +{value:0.#}%";

            case TraitEffectType.ProjectileSpeedPercent:
                return $"탄속 +{value:0.#}%";

            case TraitEffectType.RangePercent:
                return $"사거리 +{value:0.#}%";

            case TraitEffectType.MoveSpeedPercent:
                return $"이동속도 +{value:0.#}%";

            case TraitEffectType.DashCooldownReduction:
                return $"대쉬 쿨다운 -{Mathf.Abs(value):0.##}초";

            case TraitEffectType.DashDistanceBonus:
                return $"대쉬 거리 +{value:0.##}";

            case TraitEffectType.MaxHpBonus:
                return $"최대 체력 +{value:0.#}";

            case TraitEffectType.HealEfficiencyPercent:
                return $"회복 자원 효과 +{value:0.#}%";

            case TraitEffectType.PickupRangeBonus:
                return $"아이템 흡수 범위 +{value:0.##}";

            case TraitEffectType.SpreadReductionPercent:
                return $"탄 퍼짐 -{Mathf.Abs(value):0.#}%";

            case TraitEffectType.ProjectileCountBonus:
                return $"탄 수 +{Mathf.RoundToInt(value)}";

            case TraitEffectType.PierceCountBonus:
                return $"관통 횟수 +{Mathf.RoundToInt(value)}";

            case TraitEffectType.ChargeTimeReductionPercent:
                return $"차징 시간 -{Mathf.Abs(value):0.#}%";

            case TraitEffectType.ChargeDamagePercent:
                return $"차징 피해 +{value:0.#}%";

            case TraitEffectType.HomingAngleBonus:
                return $"유도 각도 +{value:0.#}도";

            case TraitEffectType.HomingRangeBonus:
                return $"유도 거리 +{value:0.##}";

            case TraitEffectType.FireRatePercent:
                return $"연사력 +{value:0.#}%";

            case TraitEffectType.CloseRangeDamageReductionPercent:
                return $"근거리 피해 감소 +{value:0.#}%";

            case TraitEffectType.DashDamageReductionPercent:
                return $"대쉬 후 피해 감소 +{value:0.#}%";

            case TraitEffectType.CloseRangeSuppressionPercent:
                return $"근거리 제압 효과 +{value:0.#}%";

            case TraitEffectType.ChargeSightBonusPercent:
                return $"차징 중 시야 +{value:0.#}%";

            case TraitEffectType.ChargedProjectileSizePercent:
                return $"차징탄 크기 +{value:0.#}%";

            case TraitEffectType.RemovePierceDamageFalloff:
                return "관통 후 피해 감쇠 제거";

            default:
                return $"{effectType} {value:0.##}";
        }
    }
}