using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ShipTraitBranchTabButton : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private ShipTraitBranchKind branchKind = ShipTraitBranchKind.Shared;
    [SerializeField] private string fallbackLabel;

    [Header("Prefab Parts")]
    [Tooltip("루트 버튼. 비워두면 이 오브젝트의 Button을 사용합니다.")]
    [SerializeField] private Button button;

    [Tooltip("탭 이름 텍스트입니다. Label/LabelText/Text 이름의 자식을 우선 자동 사용합니다.")]
    [SerializeField] private TextMeshProUGUI labelText;

    [Tooltip("잠긴 탭에 표시할 자물쇠/잠금 오버레이입니다.")]
    [SerializeField] private GameObject lockRoot;

    [Tooltip("선택된 탭에 표시할 하이라이트/테두리입니다.")]
    [SerializeField] private GameObject selectedRoot;

    [Tooltip("탭 밑줄 또는 선택 라인 이미지입니다. Line/Underline/BottomLine 이름의 자식을 자동 연결합니다.")]
    [SerializeField] private Image lineImage;

    [Tooltip("선택 시 위로 움직일 대상입니다. VisualRoot/ContentRoot/MotionRoot 이름의 자식을 우선 사용합니다. 비우면 이 오브젝트가 움직입니다.")]
    [SerializeField] private RectTransform animatedRoot;

    [SerializeField] private CanvasGroup canvasGroup;

    [Header("State Image Optional")]
    [SerializeField] private Image stateImage;
    [SerializeField] private Sprite lockedStateSprite;
    [SerializeField] private Sprite availableStateSprite;
    [SerializeField] private Sprite selectedStateSprite;
    [SerializeField] private bool hideStateImageWhenAvailable = true;

    [Header("Selection Motion - DOTween")]
    [SerializeField] private bool useSelectionTween = true;
    [SerializeField] private bool liftOnlyWhenAvailable = true;
    [SerializeField] private float selectedYOffset = 10f;
    [SerializeField] private float transitionDuration = 0.15f;
    [SerializeField] private Ease transitionEase = Ease.OutQuad;

    [Header("Selection Colors")]
    [SerializeField] private Color availableLineColor = new Color(0.32f, 0.48f, 0.55f, 1f);
    [SerializeField] private Color selectedLineColor = new Color(0.48f, 0.92f, 1f, 1f);
    [SerializeField] private Color lockedLineColor = new Color(0.25f, 0.30f, 0.36f, 0.75f);
    [SerializeField] private Color availableTextColor = new Color(0.82f, 0.90f, 0.95f, 1f);
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color lockedTextColor = new Color(0.55f, 0.60f, 0.68f, 1f);

    [Header("Sound")]
    [SerializeField] private bool playClickSound = true;
    [SerializeField] private string clickSoundEventId = SoundEventIds.UiClick;
    [SerializeField] private string lockedClickSoundEventId = SoundEventIds.UiDisabled;

    [Header("Option")]
    [SerializeField] private bool allowClickWhenLocked = true;
    [Range(0f, 1f)]
    [SerializeField] private float lockedAlpha = 0.45f;
    [Range(0f, 1f)]
    [SerializeField] private float availableAlpha = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float selectedAlpha = 1f;

    private ShipTraitTreePanel owner;
    private bool isAvailable;
    private bool isSelected;
    private bool hasCapturedBasePosition;
    private bool hasAppliedMotionState;
    private Vector2 baseAnchoredPosition;
    private Tween moveTween;
    private Tween lineColorTween;
    private Tween labelColorTween;
    private Tween alphaTween;

    public ShipTraitBranchKind BranchKind => branchKind;
    public bool IsAvailable => isAvailable;
    public bool IsSelected => isSelected;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        CaptureBasePosition();
    }

    private void OnDisable()
    {
        KillTweens(false);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }

        KillTweens(false);
    }

    public void Bind(
        ShipTraitTreePanel panel,
        ShipTraitBranchKind targetBranchKind,
        string displayLabel)
    {
        owner = panel;
        branchKind = targetBranchKind;

        CacheReferences();
        CaptureBasePosition();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        SetLabel(displayLabel);
    }

    public void SetAllowClickWhenLocked(bool allow)
    {
        allowClickWhenLocked = allow;
        RefreshButtonInteractable();
    }

    public void SetLabel(string displayLabel)
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
    }

    public void SetVisualState(bool available, bool selected, bool visible)
    {
        isAvailable = available;
        isSelected = selected;

        if (!visible)
        {
            KillTweens(false);
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (lockRoot != null)
        {
            lockRoot.SetActive(!available);
        }

        if (selectedRoot != null)
        {
            selectedRoot.SetActive(selected);
        }

        SetStateImage(available, selected);
        RefreshButtonInteractable();
        RefreshCanvasGroup(available, selected);
        RefreshSelectionMotion(available, selected);
    }

    private void CacheReferences()
    {
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

        if (lineImage == null)
        {
            lineImage = FindChildComponent<Image>("Line", "Underline", "SelectedLine", "BottomLine", "SelectionLine");
        }

        if (animatedRoot == null)
        {
            animatedRoot = FindChildComponent<RectTransform>("VisualRoot", "ContentRoot", "MotionRoot", "Visual", "Content");
        }

        if (animatedRoot == null)
        {
            animatedRoot = transform as RectTransform;
        }

        if (stateImage == null)
        {
            stateImage = FindChildComponent<Image>("State", "StateImage", "StatusImage");
        }

        if (lockRoot == null)
        {
            lockRoot = FindChildObject("Lock", "LockRoot", "Locked", "LockImage");
        }

        if (selectedRoot == null)
        {
            selectedRoot = FindChildObject("Selected", "SelectedRoot", "Select", "Selection");
        }
    }

    private void CaptureBasePosition()
    {
        if (hasCapturedBasePosition || animatedRoot == null)
        {
            return;
        }

        baseAnchoredPosition = animatedRoot.anchoredPosition;
        hasCapturedBasePosition = true;
    }

    private void RefreshButtonInteractable()
    {
        if (button != null)
        {
            button.interactable = isAvailable || allowClickWhenLocked;
        }
    }

    private void RefreshCanvasGroup(bool available, bool selected)
    {
        if (canvasGroup == null)
        {
            return;
        }

        float targetAlpha;

        if (!available)
        {
            targetAlpha = lockedAlpha;
        }
        else if (selected)
        {
            targetAlpha = selectedAlpha;
        }
        else
        {
            targetAlpha = availableAlpha;
        }

        bool shouldTween = ShouldTween();

        if (!shouldTween)
        {
            KillTween(ref alphaTween, false);
            canvasGroup.alpha = targetAlpha;
            return;
        }

        KillTween(ref alphaTween, false);
        alphaTween = DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, targetAlpha, transitionDuration)
            .SetEase(transitionEase)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void RefreshSelectionMotion(bool available, bool selected)
    {
        CacheReferences();
        CaptureBasePosition();

        bool lifted = selected && (!liftOnlyWhenAvailable || available);
        Vector2 targetPosition = hasCapturedBasePosition
            ? baseAnchoredPosition + Vector2.up * (lifted ? selectedYOffset : 0f)
            : Vector2.zero;

        Color targetLineColor = GetTargetLineColor(available, selected);
        Color targetTextColor = GetTargetTextColor(available, selected);

        bool shouldTween = ShouldTween();
        hasAppliedMotionState = true;

        if (!shouldTween)
        {
            KillTween(ref moveTween, false);
            KillTween(ref lineColorTween, false);
            KillTween(ref labelColorTween, false);

            if (animatedRoot != null && hasCapturedBasePosition)
            {
                animatedRoot.anchoredPosition = targetPosition;
            }

            if (lineImage != null)
            {
                lineImage.color = targetLineColor;
            }

            if (labelText != null)
            {
                labelText.color = targetTextColor;
            }

            return;
        }

        if (animatedRoot != null && hasCapturedBasePosition)
        {
            KillTween(ref moveTween, false);
            moveTween = DOTween.To(() => animatedRoot.anchoredPosition, value => animatedRoot.anchoredPosition = value, targetPosition, transitionDuration)
                .SetEase(transitionEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (lineImage != null)
        {
            KillTween(ref lineColorTween, false);
            lineColorTween = DOTween.To(() => lineImage.color, value => lineImage.color = value, targetLineColor, transitionDuration)
                .SetEase(transitionEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (labelText != null)
        {
            KillTween(ref labelColorTween, false);
            labelColorTween = DOTween.To(() => labelText.color, value => labelText.color = value, targetTextColor, transitionDuration)
                .SetEase(transitionEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }
    }

    private bool ShouldTween()
    {
        return Application.isPlaying &&
               useSelectionTween &&
               hasAppliedMotionState &&
               gameObject.activeInHierarchy &&
               transitionDuration > 0f;
    }

    private Color GetTargetLineColor(bool available, bool selected)
    {
        if (!available)
        {
            return lockedLineColor;
        }

        return selected ? selectedLineColor : availableLineColor;
    }

    private Color GetTargetTextColor(bool available, bool selected)
    {
        if (!available)
        {
            return lockedTextColor;
        }

        return selected ? selectedTextColor : availableTextColor;
    }

    private void SetStateImage(bool available, bool selected)
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
        else if (selected)
        {
            targetSprite = selectedStateSprite;
        }
        else
        {
            targetSprite = availableStateSprite;
        }

        if (targetSprite != null)
        {
            stateImage.sprite = targetSprite;
        }

        bool shouldShow = !available || selected || !hideStateImageWhenAvailable;
        bool hasSprite = stateImage.sprite != null;

        stateImage.enabled = shouldShow && hasSprite;
        stateImage.gameObject.SetActive(shouldShow && hasSprite);
        stateImage.preserveAspect = true;
    }

    private void HandleClick()
    {
        if (owner == null)
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

        owner.SelectBranchTab(branchKind);
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

    private void KillTweens(bool complete)
    {
        KillTween(ref moveTween, complete);
        KillTween(ref lineColorTween, complete);
        KillTween(ref labelColorTween, complete);
        KillTween(ref alphaTween, complete);
    }

    private void KillTween(ref Tween tween, bool complete)
    {
        if (tween == null)
        {
            return;
        }

        tween.Kill(complete);
        tween = null;
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
            ShipTraitBranchKind.Shared => "공유",
            ShipTraitBranchKind.MachineGun => "기관총",
            ShipTraitBranchKind.Sniper => "스나",
            ShipTraitBranchKind.Shotgun => "샷건",
            _ => "특성"
        };
    }
}
