using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DashShockwaveVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Default Play")]
    [SerializeField] private float defaultRadius = 2.2f;
    [SerializeField] private float defaultDuration = 0.18f;
    [SerializeField] private float startScaleMultiplier = 0.15f;
    [SerializeField] private float endScaleMultiplier = 2f;
    [SerializeField] private bool fadeOut = true;

    private Coroutine playRoutine;
    private Color initialColor = Color.white;

    private void Reset()
    {
        visualRoot = transform;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer != null)
        {
            initialColor = spriteRenderer.color;
        }
    }

    private void OnEnable()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = initialColor;
        }
    }

    private void OnDisable()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }

    public void Play()
    {
        Play(defaultRadius, defaultDuration);
    }

    public void Play(float radius, float duration)
    {
        radius = Mathf.Max(0.01f, radius);
        duration = Mathf.Max(0.01f, duration);

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine(radius, duration));
    }

    private IEnumerator PlayRoutine(float radius, float duration)
    {
        float timer = 0f;
        float startScale = radius * Mathf.Max(0.01f, startScaleMultiplier);
        float endScale = radius * Mathf.Max(startScaleMultiplier, endScaleMultiplier);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = initialColor;
        }

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.Lerp(startScale, endScale, eased);

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * scale;
            }

            if (fadeOut && spriteRenderer != null)
            {
                Color color = initialColor;
                color.a = Mathf.Lerp(initialColor.a, 0f, t);
                spriteRenderer.color = color;
            }

            yield return null;
        }

        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one * endScale;
        }

        if (fadeOut && spriteRenderer != null)
        {
            Color color = initialColor;
            color.a = 0f;
            spriteRenderer.color = color;
        }

        playRoutine = null;
    }
}