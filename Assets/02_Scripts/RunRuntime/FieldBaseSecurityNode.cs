using System;
using UnityEngine;

[DisallowMultipleComponent]
public class FieldBaseSecurityNode : MonoBehaviour, IDamageable, IHoldInteractable
{
    [Header("Identity")]
    [SerializeField] private string nodeName = "보안 장치";
    [SerializeField] private bool startsEnabled = true;

    [Header("Interaction")]
    [SerializeField] private bool allowPlayerInteractDisable = true;
    [SerializeField] private bool requireHoldInteraction = true;
    [Min(0f)]
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private bool cancelHoldOnDamage = true;
    [SerializeField] private string interactText = "전력망 차단";

    [Header("Durability Optional")]
    [Tooltip("켜면 총알로도 장치를 파괴할 수 있습니다. 전력 장치는 보통 꺼두는 것을 권장합니다.")]
    [SerializeField] private bool allowDamageDisable = true;
    [SerializeField] private float maxHp = 8f;

    [Header("Visual")]
    [SerializeField] private GameObject[] activeVisuals;
    [SerializeField] private GameObject[] disabledVisuals;
    [SerializeField] private GameObject[] holdVisuals;
    [SerializeField] private Collider2D[] collidersToDisable;

    [Header("Messages")]
    [SerializeField] private string holdStartedWarning = "전력망 차단을 시작합니다. 피격되거나 상호작용 입력을 놓으면 취소됩니다.";
    [SerializeField] private string disabledWarning = "기지 전력망을 차단했다.";

    private float currentHp;
    private bool disabled;
    private bool holding;

    public bool IsDead => disabled;
    public bool IsDisabled => disabled;
    public string NodeName => string.IsNullOrWhiteSpace(nodeName) ? name : nodeName;
    public float HoldDuration => requireHoldInteraction ? Mathf.Max(0f, holdDuration) : 0f;
    public bool CancelHoldOnDamage => cancelHoldOnDamage;

    public string InteractionText
    {
        get
        {
            if (disabled)
            {
                return $"{NodeName} 꺼짐";
            }

            if (!requireHoldInteraction || holdDuration <= 0f)
            {
                return interactText;
            }

            return $"{interactText} ({holdDuration:0.#}초 유지)";
        }
    }

    public event Action<FieldBaseSecurityNode> Disabled;

    private void Awake()
    {
        currentHp = Mathf.Max(1f, maxHp);
        disabled = !startsEnabled;
        holding = false;
        RefreshVisualState();
    }

    public bool CanInteract(GameObject interactor)
    {
        return allowPlayerInteractDisable && !disabled && interactor != null;
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        DisableNode(true);
    }

    public void OnHoldStarted(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        holding = true;
        SetObjectsActive(holdVisuals, true);

        if (!string.IsNullOrWhiteSpace(holdStartedWarning))
        {
            ShowWarning(holdStartedWarning);
        }

        AudioManager.PlayAt(SoundEventIds.EventStart, transform.position, 0.7f);
    }

    public void OnHoldCanceled(GameObject interactor)
    {
        holding = false;
        SetObjectsActive(holdVisuals, false);
    }

    public void TakeDamage(float damage)
    {
        if (disabled || !allowDamageDisable)
        {
            return;
        }

        currentHp -= Mathf.Max(0f, damage);

        if (currentHp <= 0f)
        {
            DisableNode(true);
        }
    }

    public void ForceDisable(bool showWarning)
    {
        DisableNode(showWarning);
    }

    private void DisableNode(bool showWarning)
    {
        if (disabled)
        {
            return;
        }

        disabled = true;
        holding = false;
        currentHp = 0f;
        RefreshVisualState();

        if (showWarning)
        {
            ShowWarning(string.IsNullOrWhiteSpace(disabledWarning)
                ? $"{NodeName} 비활성화"
                : disabledWarning);
        }

        AudioManager.PlayAt(SoundEventIds.EventComplete, transform.position, 0.8f);
        Disabled?.Invoke(this);
    }

    private void RefreshVisualState()
    {
        SetObjectsActive(activeVisuals, !disabled);
        SetObjectsActive(disabledVisuals, disabled);
        SetObjectsActive(holdVisuals, holding && !disabled);

        if (collidersToDisable == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            if (collidersToDisable[i] != null)
            {
                collidersToDisable[i].enabled = !disabled;
            }
        }
    }

    private static void SetObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private static void ShowWarning(string message)
    {
        ExpeditionHUD hud = FindFirstObjectByType<ExpeditionHUD>();
        if (hud != null && !string.IsNullOrWhiteSpace(message))
        {
            hud.ShowWarning(message);
        }
    }
}
