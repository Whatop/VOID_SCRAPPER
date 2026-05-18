using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private GameObject lockRoot;
    [SerializeField] private GameObject unlockedRoot;
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Option")]
    [SerializeField] private bool allowClickWhenLocked;

    private ShipTraitTreePanel owner;
    private bool isAvailable;
    private bool isUnlocked;

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

    private void Reset()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();
        labelText = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
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

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);

        SetStaticView(displayLabel, icon);
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
            iconImage.preserveAspect = true;
        }
    }

    public void SetVisualState(bool available, bool unlocked, bool selected)
    {
        isAvailable = available;
        isUnlocked = unlocked;

        if (lockRoot != null)
        {
            lockRoot.SetActive(!available);
        }

        if (unlockedRoot != null)
        {
            unlockedRoot.SetActive(unlocked);
        }

        if (selectedRoot != null)
        {
            selectedRoot.SetActive(selected);
        }

        if (button != null)
        {
            button.interactable = available || allowClickWhenLocked;
        }

        if (canvasGroup != null)
        {
            if (!available)
            {
                canvasGroup.alpha = 0.35f;
            }
            else if (unlocked)
            {
                canvasGroup.alpha = 0.9f;
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
        }
    }

    private void HandleClick()
    {
        if (owner == null)
        {
            return;
        }

        if (!isAvailable && !allowClickWhenLocked)
        {
            return;
        }

        owner.SelectNode(branchKind, NodeId);
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