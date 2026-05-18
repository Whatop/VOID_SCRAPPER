using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class SpaceBackgroundTwinkle2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private float minAlpha = 0.1f;
    [SerializeField] private float maxAlpha = 0.5f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float scalePulse = 0.08f;
    [SerializeField] private float phase;

    private Vector3 baseScale;
    private bool initialized;

    public void Setup(
        SpriteRenderer renderer,
        float targetMinAlpha,
        float targetMaxAlpha,
        float targetSpeed,
        float targetScalePulse,
        float targetPhase)
    {
        targetRenderer = renderer;

        minAlpha = Mathf.Clamp01(Mathf.Min(targetMinAlpha, targetMaxAlpha));
        maxAlpha = Mathf.Clamp01(Mathf.Max(targetMinAlpha, targetMaxAlpha));
        speed = Mathf.Max(0f, targetSpeed);
        scalePulse = Mathf.Max(0f, targetScalePulse);
        phase = targetPhase;

        baseScale = transform.localScale;
        initialized = true;
    }

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (!initialized)
        {
            baseScale = transform.localScale;
            initialized = true;
        }
    }

    private void Update()
    {
        if (targetRenderer == null)
        {
            return;
        }

        float time = Application.isPlaying
            ? Time.time
            : Time.realtimeSinceStartup;

        float wave = 0.5f + Mathf.Sin((time * speed) + phase) * 0.5f;

        Color color = targetRenderer.color;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
        targetRenderer.color = color;

        float scale = 1f + wave * scalePulse;
        transform.localScale = baseScale * scale;
    }
}