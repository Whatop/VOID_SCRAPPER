using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PlayerProjectileInterceptor2D : MonoBehaviour
{
    [Header("Interception")]
    [SerializeField] private Collider2D interceptionTrigger;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer feedbackRenderer;
    [SerializeField] private Color flashColor = new Color(0.65f, 1f, 1f, 1f);
    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.08f;

    private Tween feedbackTween;
    private Color restColor = Color.white;
    private bool hasRestColor;
    private bool interceptionEnabled;

    public bool InterceptionEnabled => interceptionEnabled;

    private void Reset()
    {
        interceptionTrigger = GetComponent<Collider2D>();
        feedbackRenderer = GetComponentInParent<SpriteRenderer>();

        if (interceptionTrigger != null)
        {
            interceptionTrigger.isTrigger = true;
            interceptionTrigger.enabled = false;
        }
    }

    private void Awake()
    {
        CacheReferences();
        CaptureRestColor();
        SetTriggerEnabled(false);
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureRestColor();
        SetTriggerEnabled(interceptionEnabled);
    }

    private void OnDisable()
    {
        interceptionEnabled = false;
        SetTriggerEnabled(false);
        ResetFeedback();
    }

    public void SetInterceptionEnabled(bool value)
    {
        CacheReferences();

        if (!value)
        {
            interceptionEnabled = false;
            SetTriggerEnabled(false);
            ResetFeedback();
            enabled = false;
            return;
        }

        CaptureRestColor();
        interceptionEnabled = true;
        enabled = true;
        SetTriggerEnabled(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!interceptionEnabled || other == null)
        {
            return;
        }

        Bullet bullet = other.GetComponentInParent<Bullet>();
        if (bullet == null ||
            bullet.Owner != ProjectileOwner.Enemy ||
            !bullet.gameObject.activeInHierarchy)
        {
            return;
        }

        bullet.ForceRelease(false);
        PlayInterceptionFeedback();
    }

    private void CacheReferences()
    {
        if (interceptionTrigger == null)
        {
            interceptionTrigger = GetComponent<Collider2D>();
        }

        if (feedbackRenderer == null)
        {
            feedbackRenderer = GetComponentInParent<SpriteRenderer>();
        }
    }

    private void CaptureRestColor()
    {
        if (feedbackRenderer == null)
        {
            return;
        }

        restColor = feedbackRenderer.color;
        hasRestColor = true;
    }

    private void SetTriggerEnabled(bool value)
    {
        if (interceptionTrigger == null)
        {
            return;
        }

        interceptionTrigger.isTrigger = true;
        interceptionTrigger.enabled = value;
    }

    private void PlayInterceptionFeedback()
    {
        if (feedbackRenderer == null)
        {
            return;
        }

        feedbackTween?.Kill(false);
        feedbackRenderer.color = flashColor;
        feedbackTween = feedbackRenderer
            .DOColor(hasRestColor ? restColor : Color.white, flashDuration)
            .SetEase(Ease.OutQuad);
    }

    private void ResetFeedback()
    {
        feedbackTween?.Kill(false);
        feedbackTween = null;

        if (feedbackRenderer != null && hasRestColor)
        {
            feedbackRenderer.color = restColor;
        }
    }
}
