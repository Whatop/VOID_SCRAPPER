using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Region2BossCoreRewardPresentation : MonoBehaviour
{
    [SerializeField] private SpriteRenderer coreRenderer;
    [SerializeField, Min(0.1f)] private float duration = 0.8f;
    [SerializeField, Min(0f)] private float riseDistance = 1.15f;
    [SerializeField, Min(0.01f)] private float startScale = 0.2f;
    [SerializeField, Min(0.01f)] private float peakScale = 1.15f;

    private RunManager observedRunManager;
    private Color baseColor = Color.white;
    private bool playing;
    private bool completed;
    private bool interrupted;
    private bool completeRequested;

    public bool IsPlaying => playing;
    public bool IsCompleted => completed;

    private void Awake()
    {
        if (coreRenderer == null)
        {
            coreRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (coreRenderer != null)
        {
            baseColor = coreRenderer.color;
        }
    }

    private void OnEnable()
    {
        playing = false;
        completed = false;
        interrupted = false;
        completeRequested = false;
        SubscribeRunEnd();
        SetVisible(false);
    }

    private void OnDisable()
    {
        interrupted = true;
        playing = false;
        UnsubscribeRunEnd();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        UnsubscribeRunEnd();
    }

    public IEnumerator PlayRoutine(Vector3 deathPosition)
    {
        if (playing || completed || interrupted || coreRenderer == null)
        {
            yield break;
        }

        playing = true;
        Vector3 startPosition = deathPosition;
        float safeDuration = Mathf.Max(0.1f, duration);
        float elapsed = 0f;
        SetVisible(true);
        AudioManager.PlayAt(SoundEventIds.PickupCore, deathPosition, 0.82f);

        while (elapsed < safeDuration &&
               !completeRequested &&
               !interrupted &&
               !IsRunEnding())
        {
            elapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = startPosition + Vector3.up * (riseDistance * eased);

            float scaleProgress = Mathf.Clamp01(progress / 0.58f);
            float scale = Mathf.Lerp(startScale, peakScale, scaleProgress);
            if (progress > 0.58f)
            {
                scale = Mathf.Lerp(peakScale, 1f, (progress - 0.58f) / 0.42f);
            }

            transform.localScale = Vector3.one * scale;
            float fadeIn = Mathf.Clamp01(progress / 0.2f);
            float fadeOut = Mathf.Clamp01((1f - progress) / 0.2f);
            Color color = baseColor;
            color.a *= Mathf.Min(fadeIn, fadeOut);
            coreRenderer.color = color;
            yield return null;
        }

        playing = false;
        completed = !interrupted && !IsRunEnding();
        SetVisible(false);
    }

    public void CompleteImmediately()
    {
        completeRequested = true;
    }

    public void CleanupPresentation()
    {
        interrupted = true;
        completeRequested = true;
        playing = false;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (coreRenderer == null)
        {
            return;
        }

        coreRenderer.enabled = visible;
        coreRenderer.color = visible
            ? new Color(baseColor.r, baseColor.g, baseColor.b, 0f)
            : baseColor;
    }

    private void SubscribeRunEnd()
    {
        observedRunManager = RunManager.Instance;
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded += HandleRunEnded;
        }
    }

    private void UnsubscribeRunEnd()
    {
        if (observedRunManager != null)
        {
            observedRunManager.RunEnded -= HandleRunEnded;
            observedRunManager = null;
        }
    }

    private void HandleRunEnded(RunResultData _)
    {
        CleanupPresentation();
    }

    private static bool IsRunEnding()
    {
        return RunManager.Instance != null && RunManager.Instance.IsCompletingRun;
    }
}
