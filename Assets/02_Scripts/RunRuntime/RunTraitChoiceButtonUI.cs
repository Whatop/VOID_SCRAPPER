using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunTraitChoiceButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private GameObject rootObject;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityFrameImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI effectText;

    private TraitDefinition trait;
    private RunRewardOption rewardOption;
    private Action<TraitDefinition> selectedCallback;
    private Action<RunRewardOption> rewardSelectedCallback;

    private void Reset()
    {
        button = GetComponent<Button>();
        rootObject = gameObject;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Setup(
        TraitDefinition targetTrait,
        int currentLevel,
        int nextLevel,
        string nextEffectText,
        Action<TraitDefinition> onSelected)
    {
        CacheReferences();

        trait = targetTrait;
        rewardOption = targetTrait != null ? RunRewardOption.FromTrait(targetTrait) : null;
        selectedCallback = onSelected;
        rewardSelectedCallback = null;

        if (targetTrait == null)
        {
            Clear();
            return;
        }

        SetVisible(true);
        ApplyCommonVisual(
            targetTrait.Icon,
            targetTrait.DisplayName,
            $"{targetTrait.GetRarityText()} · {targetTrait.GetCategoryText()}",
            targetTrait.Description,
            targetTrait.GetRarityColor()
        );

        if (levelText != null)
        {
            levelText.text = $"Lv {currentLevel} → {nextLevel}";
        }

        if (effectText != null)
        {
            effectText.text = string.IsNullOrWhiteSpace(nextEffectText)
                ? "효과 정보 없음"
                : nextEffectText;
        }

        if (button != null)
        {
            button.interactable = true;
        }
    }

    public void SetupReward(
        RunRewardOption option,
        Action<RunRewardOption> onSelected)
    {
        CacheReferences();

        trait = null;
        selectedCallback = null;
        rewardOption = option;
        rewardSelectedCallback = onSelected;

        if (option == null)
        {
            Clear();
            return;
        }

        SetVisible(true);
        ApplyCommonVisual(
            option.Icon,
            option.DisplayName,
            $"{option.RarityText} · {option.CategoryText}",
            option.Description,
            option.RarityColor
        );

        if (option.OptionType == RunRewardOptionType.Trait && option.Trait != null)
        {
            int currentLevel = RunRuntimeTraitStore.Instance.GetLevel(option.Trait.TraitId);
            int nextLevel = Mathf.Clamp(currentLevel + 1, 1, option.Trait.MaxLevel);

            if (levelText != null)
            {
                levelText.text = currentLevel > 0
                    ? $"Lv {currentLevel} → {nextLevel}"
                    : $"NEW · Lv {nextLevel}";
            }

            if (effectText != null)
            {
                string text = TraitEffectTextUtility.BuildEffectText(option.Trait, nextLevel);
                effectText.text = string.IsNullOrWhiteSpace(text) ? "효과 정보 없음" : text;
            }
        }
        else if (option.OptionType == RunRewardOptionType.Reinforcement && option.Reinforcement != null)
        {
            if (levelText != null)
            {
                levelText.text = $"{option.Reinforcement.GetAvailabilityText()}";
            }

            if (effectText != null)
            {
                effectText.text = option.Reinforcement.BuildEffectSummary();
            }
        }

        if (button != null)
        {
            button.interactable = true;
        }
    }

    public void Clear()
    {
        trait = null;
        rewardOption = null;
        selectedCallback = null;
        rewardSelectedCallback = null;
        SetVisible(false);
    }

    private void HandleClicked()
    {
        if (rewardOption != null && rewardSelectedCallback != null)
        {
            rewardSelectedCallback.Invoke(rewardOption);
            return;
        }

        if (trait != null)
        {
            selectedCallback?.Invoke(trait);
        }
    }

    private void ApplyCommonVisual(
        Sprite icon,
        string title,
        string category,
        string description,
        Color rarityColor)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (rarityFrameImage != null)
        {
            rarityFrameImage.color = rarityColor;
        }

        if (titleText != null)
        {
            titleText.text = title;
            titleText.color = rarityColor;
        }

        if (categoryText != null)
        {
            categoryText.text = category;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
    }

    private void SetVisible(bool visible)
    {
        if (rootObject != null)
        {
            rootObject.SetActive(visible);
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void CacheReferences()
    {
        if (rootObject == null)
        {
            rootObject = gameObject;
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }
}
