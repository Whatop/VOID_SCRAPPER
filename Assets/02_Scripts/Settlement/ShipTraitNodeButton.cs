using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections.Generic;

public enum ShipTraitBranchKind
{
    Shared,
    MachineGun,
    Sniper,
    Shotgun
}

[RequireComponent(typeof(Button))]
public class ShipTraitNodeButton : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private ShipTraitBranchKind branchKind;
    [SerializeField] private string nodeId;
    [SerializeField] private string fallbackLabel;

    [Header("Prefab Parts")]
    [Tooltip("루트 버튼. 비워두면 이 오브젝트의 Button을 사용합니다.")]
    [SerializeField] private Button button;

    [Tooltip("노드 아이콘 Image. 자식 이름 Icon/IconImage를 쓰면 Reset에서 자동 연결됩니다.")]
    [SerializeField] private Image iconImage;

    [Tooltip("선택사항. 아이콘 아래 번호/이름을 표시할 때만 연결합니다.")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Tooltip("선택사항. 노드의 현재 레벨을 별도로 표시할 때 연결합니다. 예: 1/3")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("잠김 오버레이 또는 자물쇠 루트 오브젝트입니다.")]
    [SerializeField] private GameObject lockRoot;

    [Tooltip("해금되어 탐사 시작 시 적용되는 상태 표시입니다.")]
    [FormerlySerializedAs("unlockedRoot")]
    [SerializeField] private GameObject activeRoot;

    [Tooltip("해금은 됐지만 비활성화된 상태 표시입니다.")]
    [SerializeField] private GameObject inactiveRoot;

    [Tooltip("선택 테두리/하이라이트입니다.")]
    [SerializeField] private GameObject selectedRoot;

    [Tooltip("Optional alpha feedback on this object or an explicitly assigned child visual wrapper.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("State Image Optional")]
    [SerializeField] private Image stateImage;
    [SerializeField] private Sprite lockedStateSprite;
    [SerializeField] private Sprite availableStateSprite;
    [FormerlySerializedAs("unlockedStateSprite")]
    [SerializeField] private Sprite activeStateSprite;
    [SerializeField] private Sprite inactiveStateSprite;
    [SerializeField] private bool hideStateImageWhenAvailable = true;

    [Header("Sound")]
    [SerializeField] private bool playClickSound = true;
    [SerializeField] private string clickSoundEventId = SoundEventIds.UiClick;
    [SerializeField] private string lockedClickSoundEventId = SoundEventIds.UiDisabled;

    [Header("Option")]
    [SerializeField] private bool allowClickWhenLocked = true;
    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float inactiveAlpha = 0.55f;
    [Range(0f, 1f)]
    [SerializeField] private float activeAlpha = 1f;

    private ShipTraitTreePanel owner;
    private bool isAvailable;
    private bool isUnlocked;
    private bool isActive;
    private Button subscribedButton;
    private bool alphaCaptured;
    private float baseAlpha = 1f;

    public ShipTraitBranchKind BranchKind => branchKind;

    public string NodeId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                return nodeId;
            }

            return name;
        }
    }

    public bool IsAvailable => isAvailable;
    public bool IsUnlocked => isUnlocked;
    public bool IsActive => isActive;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDestroy()
    {
        UnbindClick();
    }

    private void OnDisable() => UnbindClick();
    private void OnEnable() { if (owner != null) BindClick(); }
    private void UnbindClick()
    {
        if (subscribedButton != null) subscribedButton.onClick.RemoveListener(HandleClick);
        subscribedButton = null;
    }
    private void BindClick()
    {
        UnbindClick();
        if (button == null) return;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == nameof(HandleClick) &&
                button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off) return;
        subscribedButton = button;
        button.onClick.AddListener(HandleClick);
    }

    public void CollectPresentationErrors(List<string> errors)
    {
        var seen = new HashSet<Component>();
        void Role(string field, Component value, bool self = false)
        {
            string reason = value == null ? "Missing binding" : value.gameObject.scene != gameObject.scene ? "Cross-scene ownership" :
                (!self && value.transform == transform) || !value.transform.IsChildOf(transform) ? "Wrong ancestry" :
                !seen.Add(value) ? "Duplicate role" : value is TMP_Text text && text.font == null ? "Missing font" : null;
            if (reason != null) errors.Add(SettlementSectorTechnologyPanelUI.BindingDiagnostic("ShipTraitNodeButton." + field, value, transform, gameObject.scene, reason));
        }
        Role(nameof(button), button, true);
        Role(nameof(iconImage), iconImage);
        Role(nameof(labelText), labelText);
        Role(nameof(levelText), levelText);
        Role(nameof(selectedRoot), selectedRoot != null ? selectedRoot.transform : null);
        if (canvasGroup != null) Role(nameof(canvasGroup), canvasGroup, true);
        if (stateImage != null) Role(nameof(stateImage), stateImage);
        if (lockRoot != null) Role(nameof(lockRoot), lockRoot.transform);
        if (activeRoot != null) Role(nameof(activeRoot), activeRoot.transform);
        if (inactiveRoot != null) Role(nameof(inactiveRoot), inactiveRoot.transform);
        if (button != null)
        {
            int callbacks = 0;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off)
                {
                    callbacks++;
                    if (button.onClick.GetPersistentMethodName(i) != nameof(HandleClick)) errors.Add("ShipTraitNodeButton.button: incorrect owned action mapping.");
                }
            if (callbacks > 1) errors.Add("ShipTraitNodeButton.button: duplicate persistent owned actions.");
        }
    }

    public void Bind(
        ShipTraitTreePanel panel,
        ShipTraitBranchKind targetBranchKind,
        string targetNodeId,
        string displayLabel,
        Sprite icon)
    {
        owner = panel;
        branchKind = targetBranchKind;
        nodeId = string.IsNullOrWhiteSpace(targetNodeId) ? name : targetNodeId;

        CacheReferences();

        BindClick();

        SetStaticView(displayLabel, icon);
    }

    public void SetAllowClickWhenLocked(bool allow)
    {
        allowClickWhenLocked = allow;
        RefreshButtonInteractable();
    }

    public void SetStaticView(string displayLabel, Sprite icon)
    {
        string finalLabel = !string.IsNullOrWhiteSpace(displayLabel)
            ? displayLabel
            : fallbackLabel;

        if (string.IsNullOrWhiteSpace(finalLabel))
        {
            finalLabel = BuildDefaultLabel(branchKind);
        }

        if (labelText != null)
        {
            labelText.text = finalLabel;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            if (owner == null || !owner.UsesAuthoredPresentation) iconImage.preserveAspect = true;
        }
    }

    public void SetLevelView(string levelLabel)
    {
        if (levelText == null)
        {
            return;
        }

        bool hasLevel = !string.IsNullOrWhiteSpace(levelLabel);
        levelText.text = hasLevel ? levelLabel : string.Empty;
        levelText.gameObject.SetActive(hasLevel);
    }

    public void SetVisualState(bool available, bool unlocked, bool selected)
    {
        SetVisualState(available, unlocked, unlocked, selected);
    }

    public void SetVisualState(bool available, bool unlocked, bool active, bool selected)
    {
        isAvailable = available;
        isUnlocked = unlocked;
        isActive = unlocked && active;

        if (lockRoot != null)
        {
            lockRoot.SetActive(!available);
        }

        if (activeRoot != null)
        {
            activeRoot.SetActive(unlocked && active);
        }

        if (inactiveRoot != null)
        {
            inactiveRoot.SetActive(unlocked && !active);
        }

        if (selectedRoot != null)
        {
            selectedRoot.SetActive(selected);
        }

        SetStateImage(available, unlocked, active);
        RefreshButtonInteractable();
        RefreshCanvasGroup(available, unlocked, active);
    }

    private void CacheReferences()
    {
        if (owner != null && owner.UsesAuthoredPresentation) return;
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (labelText == null)
        {
            labelText = FindChildComponent<TextMeshProUGUI>("Label", "LabelText", "Text", "Name", "NameText");
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (levelText == null)
        {
            levelText = FindChildComponent<TextMeshProUGUI>("Level", "LevelText", "LevelLabel", "NodeLevel");
        }

        if (iconImage == null)
        {
            iconImage = FindChildComponent<Image>("Icon", "IconImage", "NodeIcon");
        }

        if (stateImage == null)
        {
            stateImage = FindChildComponent<Image>("State", "StateImage", "StatusImage");
        }

        if (lockRoot == null)
        {
            lockRoot = FindChildObject("Lock", "LockRoot", "Locked", "LockImage");
        }

        if (activeRoot == null)
        {
            activeRoot = FindChildObject("Active", "ActiveRoot", "Unlocked", "UnlockedRoot");
        }

        if (inactiveRoot == null)
        {
            inactiveRoot = FindChildObject("Inactive", "InactiveRoot", "Disabled", "DisabledRoot");
        }

        if (selectedRoot == null)
        {
            selectedRoot = FindChildObject("Selected", "SelectedRoot", "Select", "Selection");
        }
    }

    private void RefreshButtonInteractable()
    {
        if (button != null)
        {
            button.interactable = isAvailable || allowClickWhenLocked;
        }
    }

    private void RefreshCanvasGroup(bool available, bool unlocked, bool active)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (!alphaCaptured) { baseAlpha = canvasGroup.alpha; alphaCaptured = true; }
        float authoredAlpha = owner != null && owner.UsesAuthoredPresentation ? baseAlpha : 1f;

        if (!available)
        {
            canvasGroup.alpha = authoredAlpha * lockedAlpha;
        }
        else if (unlocked && !active)
        {
            canvasGroup.alpha = authoredAlpha * inactiveAlpha;
        }
        else
        {
            canvasGroup.alpha = authoredAlpha * activeAlpha;
        }
    }

    private void SetStateImage(bool available, bool unlocked, bool active)
    {
        if (stateImage == null)
        {
            return;
        }

        Sprite targetSprite = null;

        if (!available)
        {
            targetSprite = lockedStateSprite;
        }
        else if (unlocked && !active)
        {
            targetSprite = inactiveStateSprite;
        }
        else if (unlocked)
        {
            targetSprite = activeStateSprite;
        }
        else
        {
            targetSprite = availableStateSprite;
        }

        if (targetSprite != null)
        {
            stateImage.sprite = targetSprite;
        }

        bool shouldShow = !available || unlocked || !hideStateImageWhenAvailable;
        bool hasSprite = stateImage.sprite != null;

        stateImage.enabled = shouldShow && hasSprite;
        stateImage.gameObject.SetActive(shouldShow && hasSprite);
        if (owner == null || !owner.UsesAuthoredPresentation) stateImage.preserveAspect = true;
    }

    private void HandleClick()
    {
        if (owner == null || !owner.CanUseTraitInput)
        {
            return;
        }

        if (!isAvailable)
        {
            PlayClickSound(lockedClickSoundEventId);

            if (!allowClickWhenLocked)
            {
                return;
            }
        }
        else
        {
            PlayClickSound(clickSoundEventId);
        }

        owner.SelectNode(branchKind, NodeId);
    }

    private void PlayClickSound(string eventId)
    {
        if (!playClickSound)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        AudioManager.Play(eventId);
    }

    private T FindChildComponent<T>(params string[] candidateNames) where T : Component
    {
        GameObject child = FindChildObject(candidateNames);
        return child != null ? child.GetComponent<T>() : null;
    }

    private GameObject FindChildObject(params string[] candidateNames)
    {
        if (candidateNames == null || candidateNames.Length == 0)
        {
            return null;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            for (int j = 0; j < candidateNames.Length; j++)
            {
                string candidate = candidateNames[j];

                if (!string.IsNullOrWhiteSpace(candidate) &&
                    string.Equals(child.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
        }

        return null;
    }

    private string BuildDefaultLabel(ShipTraitBranchKind kind)
    {
        return kind switch
        {
            ShipTraitBranchKind.Shared => "공유 특성",
            ShipTraitBranchKind.MachineGun => "기관총 특성",
            ShipTraitBranchKind.Sniper => "스나 특성",
            ShipTraitBranchKind.Shotgun => "샷건 특성",
            _ => "특성"
        };
    }
}
