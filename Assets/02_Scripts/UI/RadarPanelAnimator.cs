using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RadarPanelAnimator : MonoBehaviour
{
    [Header("Authored Radar Presentation")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject functionalRadarRoot;
    [SerializeField] private RadarScopeGraphic scopeGraphic;
    [SerializeField] private Image scanSweep;

    [Header("Unscaled Transitions")]
    [SerializeField, Min(0f)] private float openDuration = 0.21f;
    [SerializeField, Min(0f)] private float closeDuration = 0.18f;
    [SerializeField, Range(0.8f, 1f)] private float closedScale = 0.92f;
    [SerializeField] private Vector2 closedOffset = new Vector2(0f, -5f);
    [SerializeField, Min(0.1f)] private float idleDuration = 1.5f;
    [SerializeField, Min(0.05f)] private float scanDuration = 0.32f;

    [Header("Startup")]
    [SerializeField] private bool closeOnStart = true;
    [SerializeField] private bool deactivatePanelRootWhenClosed;

    private readonly HashSet<object> presentationSuppressors = new HashSet<object>();
    private RectTransform panelRectTransform;
    private Vector2 authoredOpenPosition;
    private Vector3 authoredOpenScale;
    private Color authoredSweepColor;
    private Sequence transitionSequence;
    private Tween idleTween;
    private Sequence scanSequence;
    private bool initialized;
    private bool missingPresentationReported;
    private bool isDestroying;
    private bool requestedOpen;

    // Requested presentation state mirrors the scanner; suppression never changes it.
    public bool IsOpen => requestedOpen;

    private void Awake()
    {
        if (closeOnStart) CloseImmediate();
        else OpenImmediate();
    }

    private void OnEnable()
    {
        if (initialized) ApplyRequestedStateImmediate();
    }

    public bool TryValidateAuthoredPresentation(out string error)
    {
        bool valid = panelRoot != null && panelRoot.transform is RectTransform &&
                     canvasGroup != null && canvasGroup.gameObject == panelRoot &&
                     (panelRoot == gameObject || panelRoot.transform.IsChildOf(transform)) &&
                     functionalRadarRoot != null && functionalRadarRoot != panelRoot &&
                     functionalRadarRoot.transform.IsChildOf(panelRoot.transform) &&
                     scopeGraphic != null && scopeGraphic.enabled &&
                     scopeGraphic.GetComponent<CanvasRenderer>() != null &&
                     scopeGraphic.transform.IsChildOf(functionalRadarRoot.transform) &&
                     scopeGraphic.rectTransform.rect.width >= 32f &&
                     scopeGraphic.rectTransform.rect.height >= 32f &&
                     scopeGraphic.rectTransform.localScale.x > 0f &&
                     scopeGraphic.rectTransform.localScale.y > 0f &&
                     scanSweep != null && scanSweep.transform.IsChildOf(scopeGraphic.transform) &&
                     scanSweep.sprite == null && !scanSweep.raycastTarget;
        error = valid ? string.Empty :
            "RadarPanelAnimator requires authored panelRoot/CanvasGroup, functionalRadarRoot, an enabled scopeGraphic with CanvasRenderer and non-trivial bounds, and a sprite-free scanSweep.";
        return valid;
    }

    private bool InitializePresentation()
    {
        if (isDestroying) return false;
        if (initialized) return true;
        if (!TryValidateAuthoredPresentation(out string error))
        {
            if (!missingPresentationReported) Debug.LogError(error, this);
            missingPresentationReported = true;
            return false;
        }
        panelRectTransform = (RectTransform)panelRoot.transform;
        authoredOpenPosition = panelRectTransform.anchoredPosition;
        authoredOpenScale = panelRectTransform.localScale;
        authoredSweepColor = scanSweep.color;
        initialized = true;
        return true;
    }

    public void Open()
    {
        if (!InitializePresentation() || requestedOpen) return;
        requestedOpen = true;
        AudioManager.Play(SoundEventIds.RadarOpen);
        BeginTransition(true);
    }

    public void Close()
    {
        if (!InitializePresentation() || !requestedOpen) return;
        requestedOpen = false;
        AudioManager.Play(SoundEventIds.RadarClose);
        BeginTransition(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void OpenImmediate()
    {
        if (!InitializePresentation()) return;
        requestedOpen = true;
        ApplyRequestedStateImmediate();
    }

    public void CloseImmediate()
    {
        if (!InitializePresentation()) return;
        requestedOpen = false;
        ApplyRequestedStateImmediate();
    }

    public void SetPresentationSuppressed(object owner, bool suppressed)
    {
        if (owner == null || isDestroying) return;
        bool wasSuppressed = presentationSuppressors.Count > 0;
        if (suppressed) presentationSuppressors.Add(owner);
        else presentationSuppressors.Remove(owner);
        if (wasSuppressed == (presentationSuppressors.Count > 0)) return;
        if (InitializePresentation()) ApplyRequestedStateImmediate();
    }

    private void BeginTransition(bool opening)
    {
        StopOwnedTweens();
        if (presentationSuppressors.Count > 0 || !isActiveAndEnabled)
        {
            ApplyRequestedStateImmediate();
            return;
        }
        panelRoot.SetActive(true);
        // Contacts share the scope reveal; presentation never delays scan gameplay.
        functionalRadarRoot.SetActive(true);
        canvasGroup.interactable = opening;
        canvasGroup.blocksRaycasts = opening;
        if (opening && canvasGroup.alpha <= 0.001f)
        {
            panelRectTransform.localScale = authoredOpenScale * closedScale;
            panelRectTransform.anchoredPosition = authoredOpenPosition + closedOffset;
        }
        scopeGraphic.SetBorderIntensity(opening ? 1.35f : 1f);
        float duration = Mathf.Max(0.001f, opening ? openDuration : closeDuration);
        transitionSequence = DOTween.Sequence().SetUpdate(true);
        transitionSequence.Join(canvasGroup.DOFade(opening ? 1f : 0f, duration).SetEase(Ease.OutQuad));
        transitionSequence.Join(panelRectTransform.DOScale(
            opening ? authoredOpenScale : authoredOpenScale * closedScale, duration).SetEase(Ease.OutCubic));
        transitionSequence.Join(panelRectTransform.DOAnchorPos(
            opening ? authoredOpenPosition : authoredOpenPosition + closedOffset, duration).SetEase(Ease.OutCubic));
        transitionSequence.Join(DOTween.To(() => scopeGraphic.BorderIntensity,
            scopeGraphic.SetBorderIntensity, 1f, duration));
        transitionSequence.OnComplete(() =>
        {
            transitionSequence = null;
            ApplyRequestedStateImmediate();
        });
    }

    private void ApplyRequestedStateImmediate()
    {
        StopOwnedTweens();
        RestorePanelTransform();
        bool visible = requestedOpen && presentationSuppressors.Count == 0 && isActiveAndEnabled;
        if (visible) panelRoot.SetActive(true);
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        functionalRadarRoot.SetActive(visible);
        if (visible) StartIdleTween();
        else if (deactivatePanelRootWhenClosed && panelRoot != gameObject) panelRoot.SetActive(false);
    }

    private void StartIdleTween()
    {
        if (idleTween != null || !requestedOpen || presentationSuppressors.Count > 0 || !isActiveAndEnabled) return;
        scopeGraphic.SetBorderIntensity(1f);
        idleTween = DOTween.To(() => scopeGraphic.BorderIntensity, scopeGraphic.SetBorderIntensity,
                1.1f, Mathf.Max(0.1f, idleDuration))
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    // Only a successful scanner action calls this; it owns no discovery/input state.
    public void PlayScanPulse()
    {
        if (!InitializePresentation() || !requestedOpen || presentationSuppressors.Count > 0 || !isActiveAndEnabled) return;
        // Finish a partial reveal before the scan owns the border highlight.
        transitionSequence?.Complete(true);
        idleTween?.Kill(false);
        idleTween = null;
        scanSequence?.Kill(false);
        float duration = Mathf.Max(0.05f, scanDuration);
        RectTransform sweep = scanSweep.rectTransform;
        float radius = Mathf.Min(scopeGraphic.rectTransform.rect.width, scopeGraphic.rectTransform.rect.height) * 0.5f - 2f;
        sweep.anchoredPosition = new Vector2(0f, -radius);
        scanSweep.color = authoredSweepColor;
        scanSweep.gameObject.SetActive(true);
        scopeGraphic.SetBorderIntensity(1.55f);
        scanSequence = DOTween.Sequence().SetUpdate(true);
        scanSequence.Join(sweep.DOAnchorPosY(radius, duration).SetEase(Ease.Linear).OnUpdate(UpdateSweepWidth));
        scanSequence.Join(scanSweep.DOFade(0f, duration).SetEase(Ease.InQuad));
        scanSequence.Join(DOTween.To(() => scopeGraphic.BorderIntensity, scopeGraphic.SetBorderIntensity, 1f, duration));
        scanSequence.OnComplete(() =>
        {
            scanSequence = null;
            HideSweep();
            StartIdleTween();
        });
    }

    private void StopOwnedTweens()
    {
        transitionSequence?.Kill(false);
        transitionSequence = null;
        idleTween?.Kill(false);
        idleTween = null;
        scanSequence?.Kill(false);
        scanSequence = null;
        if (scopeGraphic != null) scopeGraphic.SetBorderIntensity(1f);
        HideSweep();
    }

    private void UpdateSweepWidth()
    {
        // Keep a rectangular scan line inside the circular procedural scope.
        float radius = Mathf.Max(0f, Mathf.Min(scopeGraphic.rectTransform.rect.width,
            scopeGraphic.rectTransform.rect.height) * 0.5f - 2f);
        float y = scanSweep.rectTransform.anchoredPosition.y;
        Vector2 size = scanSweep.rectTransform.sizeDelta;
        size.x = 2f * Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y));
        scanSweep.rectTransform.sizeDelta = size;
    }

    private void HideSweep()
    {
        if (scanSweep == null) return;
        scanSweep.gameObject.SetActive(false);
        scanSweep.color = authoredSweepColor;
        scanSweep.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void RestorePanelTransform()
    {
        if (panelRectTransform == null) return;
        panelRectTransform.anchoredPosition = authoredOpenPosition;
        panelRectTransform.localScale = authoredOpenScale;
    }

    private void OnDisable()
    {
        StopOwnedTweens();
        RestorePanelTransform();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        isDestroying = true;
        StopOwnedTweens();
        presentationSuppressors.Clear();
    }
}
