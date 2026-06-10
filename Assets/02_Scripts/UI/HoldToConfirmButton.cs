using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Button targetButton;
    [SerializeField] private Image fillImage;

    [Header("Hold")]
    [SerializeField] private float holdDuration = 1f;
    [SerializeField] private bool requireReleaseAfterFull = true;
    [SerializeField] private bool cancelOnPointerExit = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onHoldCompleted;

    private float holdTimer;
    private bool isHolding;
    private bool isReadyToComplete;
    private bool completedThisHold;

    public event Action HoldCompleted;

    public float HoldDuration => holdDuration;
    public float HoldRatio => holdDuration <= 0f ? 1f : Mathf.Clamp01(holdTimer / holdDuration);
    public bool IsHolding => isHolding;
    public bool IsReadyToComplete => isReadyToComplete;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        ResetVisual();
    }

    private void OnDisable()
    {
        CancelHold();
    }

    private void Update()
    {
        if (!isHolding)
        {
            return;
        }

        if (!CanHold())
        {
            CancelHold();
            return;
        }

        holdTimer += Time.unscaledDeltaTime;

        float ratio = HoldRatio;
        SetFill(ratio);

        if (ratio >= 1f)
        {
            isReadyToComplete = true;

            if (!requireReleaseAfterFull && !completedThisHold)
            {
                CompleteHold();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!CanHold())
        {
            return;
        }

        StartHold();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHolding)
        {
            return;
        }

        if (isReadyToComplete || HoldRatio >= 1f)
        {
            CompleteHold();
        }
        else
        {
            CancelHold();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!cancelOnPointerExit)
        {
            return;
        }

        if (isHolding)
        {
            CancelHold();
        }
    }

    public void StartHold()
    {
        isHolding = true;
        isReadyToComplete = false;
        completedThisHold = false;
        holdTimer = 0f;

        SetFill(0f);
    }

    public void CancelHold()
    {
        isHolding = false;
        isReadyToComplete = false;
        completedThisHold = false;
        holdTimer = 0f;

        SetFill(0f);
    }

    private void CompleteHold()
    {
        if (completedThisHold)
        {
            return;
        }

        completedThisHold = true;
        isHolding = false;
        isReadyToComplete = false;
        holdTimer = 0f;

        SetFill(0f);

        HoldCompleted?.Invoke();

        if (onHoldCompleted != null)
        {
            onHoldCompleted.Invoke();
        }
    }

    private bool CanHold()
    {
        if (targetButton == null)
        {
            return true;
        }

        return targetButton.IsActive() && targetButton.IsInteractable();
    }

    private void ResetVisual()
    {
        SetFill(0f);
    }

    private void SetFill(float value)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.fillAmount = Mathf.Clamp01(value);
    }
}