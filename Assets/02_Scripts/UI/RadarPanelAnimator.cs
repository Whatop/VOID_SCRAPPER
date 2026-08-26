using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RadarPanelAnimator : MonoBehaviour
{
    private enum PanelState
    {
        Closed,
        Opening,
        Open,
        Closing
    }

    [Header("Root")]
    [Tooltip("전체 HUD Canvas가 아니라 RadarPanelRoot만 넣어야 합니다.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("RadarPanelRoot에 붙은 CanvasGroup을 넣어야 합니다. 전체 Canvas의 CanvasGroup을 넣으면 HUD 전체가 꺼집니다.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Frame Animation")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Sprite[] openFrames;
    [SerializeField] private Sprite[] closeFrames;
    [SerializeField] private float frameRate = 12f;

    [Header("Functional Radar")]
    [Tooltip("실제 마커/레이더 이미지 루트. 열림 애니메이션이 끝난 뒤 켜집니다.")]
    [SerializeField] private GameObject functionalRadarRoot;

    [Header("Alpha")]
    [SerializeField] private float fadeDuration = 0.12f;

    [Header("Startup")]
    [Tooltip("켜두면 시작 시 레이더만 닫힌 상태로 초기화됩니다. HUD 전체는 건드리지 않습니다.")]
    [SerializeField] private bool closeOnStart = true;

    [Tooltip("레거시 옵션입니다. panelRoot가 별도 표시 자식일 때만 비활성화하며 애니메이션 소유자는 항상 활성 상태를 유지합니다.")]
    [SerializeField] private bool deactivatePanelRootWhenClosed = false;

    private Coroutine routine;
    private RectTransform panelRectTransform;
    private Vector2 authoredOpenPosition;
    private PanelState state = PanelState.Closed;
    private bool initialized;
    private bool isDestroying;
    private readonly HashSet<object> presentationSuppressors = new HashSet<object>();
    private float requestedAlpha;
    private bool requestedVisible;

    public bool IsOpen => state == PanelState.Opening || state == PanelState.Open;

    private void Reset()
    {
        panelRoot = gameObject;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        InitializePresentation();

        if (closeOnStart)
        {
            CloseImmediate();
        }
        else
        {
            OpenImmediate();
        }
    }

    public void Open()
    {
        InitializePresentation();

        if (state == PanelState.Open || state == PanelState.Opening || isDestroying)
        {
            return;
        }

        StopCurrentRoutine();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RestoreAuthoredPosition();

        if (!isActiveAndEnabled)
        {
            ApplyOpenVisuals();
            return;
        }

        AudioManager.Play(SoundEventIds.RadarOpen);
        state = PanelState.Opening;
        routine = StartCoroutine(OpenRoutine());
    }

    public void Close()
    {
        InitializePresentation();

        if (state == PanelState.Closed || state == PanelState.Closing || isDestroying)
        {
            return;
        }

        StopCurrentRoutine();

        if (!isActiveAndEnabled)
        {
            ApplyClosedVisuals(false);
            return;
        }

        AudioManager.Play(SoundEventIds.RadarClose);
        state = PanelState.Closing;
        routine = StartCoroutine(CloseRoutine());
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void OpenImmediate()
    {
        InitializePresentation();
        StopCurrentRoutine();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        ApplyOpenVisuals();
    }

    public void CloseImmediate()
    {
        InitializePresentation();
        StopCurrentRoutine();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        ApplyClosedVisuals(true);
    }

    public void SetPresentationSuppressed(object owner, bool suppressed)
    {
        if (owner == null || isDestroying)
        {
            return;
        }

        InitializePresentation();

        if (suppressed)
        {
            presentationSuppressors.Add(owner);
        }
        else
        {
            presentationSuppressors.Remove(owner);
        }

        ApplyRequestedCanvasGroup();
    }

    private IEnumerator OpenRoutine()
    {
        RestoreAuthoredPosition();

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
        yield return FadeCanvasGroup(startAlpha, 1f, fadeDuration);
        yield return PlayFrames(openFrames);

        ApplyOpenVisuals();
        routine = null;
    }

    private IEnumerator CloseRoutine()
    {
        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        yield return PlayFrames(closeFrames);

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        yield return FadeCanvasGroup(startAlpha, 0f, fadeDuration);

        ApplyClosedVisuals(true);
        routine = null;
    }

    private void InitializePresentation()
    {
        if (initialized || isDestroying)
        {
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        frameRate = Mathf.Max(1f, frameRate);

        // Cache the scene-authored position after the Canvas has performed its
        // initial layout. Each scene keeps its own authored open position.
        Canvas.ForceUpdateCanvases();
        panelRectTransform = panelRoot.transform as RectTransform;

        if (panelRectTransform != null)
        {
            authoredOpenPosition = panelRectTransform.anchoredPosition;
        }

        initialized = true;
    }

    private void RestoreAuthoredPosition()
    {
        if (panelRectTransform != null)
        {
            panelRectTransform.anchoredPosition = authoredOpenPosition;
        }
    }

    private void ApplyOpenVisuals()
    {
        RestoreAuthoredPosition();
        SetCanvasGroup(1f, true);

        if (frameImage != null && openFrames != null && openFrames.Length > 0)
        {
            frameImage.sprite = openFrames[openFrames.Length - 1];
            frameImage.enabled = true;
        }

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(true);
        }

        state = PanelState.Open;
    }

    private void ApplyClosedVisuals(bool allowPanelDeactivation)
    {
        RestoreAuthoredPosition();

        if (functionalRadarRoot != null)
        {
            functionalRadarRoot.SetActive(false);
        }

        if (frameImage != null)
        {
            if (closeFrames != null && closeFrames.Length > 0)
            {
                frameImage.sprite = closeFrames[closeFrames.Length - 1];
                frameImage.enabled = true;
            }
            else if (openFrames != null && openFrames.Length > 0)
            {
                frameImage.sprite = openFrames[0];
                frameImage.enabled = true;
            }
        }

        SetCanvasGroup(0f, false);
        state = PanelState.Closed;

        // Never deactivate the GameObject that owns this animator. The legacy
        // option remains usable only when panelRoot is a separate visible child.
        if (allowPanelDeactivation &&
            deactivatePanelRootWhenClosed &&
            panelRoot != null &&
            panelRoot != gameObject)
        {
            panelRoot.SetActive(false);
        }
    }

    private IEnumerator PlayFrames(Sprite[] frames)
    {
        if (frameImage == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        frameImage.enabled = true;

        float delay = 1f / Mathf.Max(1f, frameRate);

        for (int i = 0; i < frames.Length; i++)
        {
            frameImage.sprite = frames[i];
            yield return new WaitForSecondsRealtime(delay);
        }
    }

    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        duration = Mathf.Max(0.001f, duration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float alpha = Mathf.Lerp(from, to, t);

            SetCanvasGroup(alpha, alpha > 0.01f);
            yield return null;
        }

        SetCanvasGroup(to, to > 0.01f);
    }

    private void SetCanvasGroup(float alpha, bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        requestedAlpha = Mathf.Clamp01(alpha);
        requestedVisible = visible;
        ApplyRequestedCanvasGroup();
    }

    private void ApplyRequestedCanvasGroup()
    {
        if (canvasGroup == null)
        {
            return;
        }

        bool suppressed = presentationSuppressors.Count > 0;
        canvasGroup.alpha = suppressed ? 0f : requestedAlpha;
        canvasGroup.interactable = !suppressed && requestedVisible;
        canvasGroup.blocksRaycasts = !suppressed && requestedVisible;
    }

    private void StopCurrentRoutine()
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private void OnDisable()
    {
        StopCurrentRoutine();

        if (!isDestroying && initialized)
        {
            ApplyClosedVisuals(false);
        }
    }

    private void OnDestroy()
    {
        isDestroying = true;
        presentationSuppressors.Clear();
        routine = null;
    }
}
