using System.Collections;
using DG.Tweening;
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
    private Sprite authoredCoreSprite;
    private bool authoredVisualResolved;
    private bool playing;
    private bool completed;
    private bool interrupted;
    private bool completeRequested;
    private Sequence recoveryTween;

    public bool IsPlaying => playing;
    public bool IsCompleted => completed;

    public void SetStoryPartSprite(Sprite sprite)
    {
        ResolveAuthoredVisual();
        if (!playing && coreRenderer != null)
        {
            coreRenderer.sprite = sprite != null ? sprite : authoredCoreSprite;
        }
    }

    private void Awake()
    {
        ResolveAuthoredVisual();
    }

    // Configuration is also used before Unity invokes Awake (inactive instances
    // and EditMode fixtures). Capture the authored visual once, before overrides.
    private void ResolveAuthoredVisual()
    {
        if (coreRenderer == null)
        {
            coreRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (coreRenderer != null && !authoredVisualResolved)
        {
            baseColor = coreRenderer.color;
            authoredCoreSprite = coreRenderer.sprite;
            authoredVisualResolved = true;
        }
    }

    private void OnEnable()
    {
        ResolveAuthoredVisual();
        playing = false;
        completed = false;
        interrupted = false;
        completeRequested = false;
        if (coreRenderer != null)
        {
            coreRenderer.sprite = authoredCoreSprite;
        }
        SubscribeRunEnd();
        SetVisible(false);
    }

    private void OnDisable()
    {
        CleanupPresentation();
        UnsubscribeRunEnd();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        CleanupPresentation();
        UnsubscribeRunEnd();
    }

    public IEnumerator PlayRoutine(Vector3 deathPosition)
    {
        return PlayRoutine(deathPosition, null);
    }

    // Shared first-defeat recovery. Visual only: rewards were saved by the boss
    // death authority before this routine starts. No input/camera/pause ownership.
    public IEnumerator PlayRoutine(Vector3 deathPosition, Transform recoveryTarget, bool resolveCurrentPlayer = false)
    {
        ResolveAuthoredVisual();
        if (playing || completed || interrupted || coreRenderer == null || coreRenderer.sprite == null)
        {
            yield break;
        }

        playing = true;
        bool flyToPlayer = resolveCurrentPlayer || recoveryTarget != null;
        float safeDuration = Mathf.Max(0.1f, duration);
        transform.position = deathPosition;
        transform.localScale = Vector3.one * startScale;
        SetVisible(true);
        Sequence tween = null;
        try
        {
            tween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable);
            recoveryTween = tween;
            Vector3 raised = deathPosition + Vector3.up * riseDistance;
            tween.Append(transform.DOMove(raised, safeDuration * 0.4f).SetEase(Ease.OutQuad));
            tween.Join(transform.DOScale(peakScale, safeDuration * 0.4f));
            tween.Join(coreRenderer.DOFade(baseColor.a, safeDuration * 0.2f));
            tween.AppendInterval(0.2f);
            if (flyToPlayer)
            {
                tween.AppendCallback(() =>
                {
                    // Resolve again at the flight handoff in case the player was
                    // replaced during the rise/hold. No per-frame target polling.
                    if (resolveCurrentPlayer || recoveryTarget == null || !recoveryTarget.gameObject.activeInHierarchy)
                    {
                        PlayerController2D currentPlayer = FindFirstObjectByType<PlayerController2D>();
                        recoveryTarget = currentPlayer != null ? currentPlayer.transform : null;
                        if (recoveryTarget == null)
                        {
                            CleanupPresentation();
                            return;
                        }
                    }
                    transform.SetParent(recoveryTarget, true);
                });
                tween.Append(transform.DOLocalMove(Vector3.zero, safeDuration * 0.6f).SetEase(Ease.InQuad));
            }
            else
            {
                tween.AppendInterval(safeDuration * 0.6f);
            }
            tween.Join(transform.DOScale(flyToPlayer ? 0.05f : 1f, safeDuration * 0.6f));
            if (flyToPlayer)
            {
                tween.Join(coreRenderer.DOColor(new Color(0.7f, 0.25f, 1f, baseColor.a), safeDuration * 0.35f));
            }
            tween.Insert(0.2f + safeDuration * 0.8f, coreRenderer.DOFade(0f, safeDuration * 0.2f));
            if (completeRequested)
            {
                tween.Complete(true);
            }
        }
        catch (System.Exception exception)
        {
            tween?.Kill();
            Debug.LogWarning("[Story Recovery] Presentation skipped: " + exception.Message, this);
        }

        if (tween != null && tween.IsActive())
        {
            // Uses CustomYieldInstruction rather than a second coroutine hosted
            // on DOTween.instance; the caller already owns this routine.
            yield return tween.WaitForCompletion(true);
        }
        recoveryTween = null;

        playing = false;
        completed = !interrupted && !IsRunEnding();
        if (completed)
        {
            AudioManager.PlayAt(SoundEventIds.PickupCore, transform.position, 0.82f);
        }
        SetVisible(false);
    }

    public void CompleteImmediately()
    {
        completeRequested = true;
        recoveryTween?.Complete(true);
    }

    public void CleanupPresentation()
    {
        interrupted = true;
        completeRequested = true;
        playing = false;
        Sequence tween = recoveryTween;
        recoveryTween = null;
        tween?.Kill();
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
