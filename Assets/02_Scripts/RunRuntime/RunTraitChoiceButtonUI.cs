using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunTraitChoiceButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private GameObject rootObject;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI effectText;

    private TraitDefinition trait;
    private Action<TraitDefinition> selectedCallback;

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
        selectedCallback = onSelected;

        if (trait == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        if (titleText != null)
        {
            titleText.text = trait.DisplayName;
        }

        if (categoryText != null)
        {
            categoryText.text = trait.GetCategoryText();
        }

        if (descriptionText != null)
        {
            descriptionText.text = trait.Description;
        }

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

    public void Clear()
    {
        trait = null;
        selectedCallback = null;
        SetVisible(false);
    }

    private void HandleClicked()
    {
        if (trait == null)
        {
            return;
        }

        selectedCallback?.Invoke(trait);
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