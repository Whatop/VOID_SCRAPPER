using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRadarVFXController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform effectOrigin;

    [Header("Scan Pulse Effect")]
    [SerializeField] private GameObject scanPulseEffectPrefab;
    [SerializeField] private float scanPulseLifeTime = 0.3f;
    [SerializeField] private bool scalePulseByScanRadius = true;
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private float scanPulseScaleMultiplier = 2f;
    [SerializeField] private bool usePoolForPulse = true;
    [SerializeField] private Color scanPulseColor = new Color(0.15f, 0.82f, 1f, 0.15f);

    private Sequence activePulseSequence;
    private GameObject activePulseEffect;

    public bool IsPulseActive => activePulseEffect != null;
    public Color EffectiveScanPulseColor => ResolvePulseColor(scanPulseColor);
    public float EffectiveScanPulseDuration => ResolvePulseDuration(scanPulseLifeTime);

    private void Reset()
    {
        effectOrigin = transform;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        StopActivePulse();
    }

    private void OnDestroy()
    {
        StopActivePulse();
    }

    public void PlayPulse(float radius)
    {
        scanRadius = Mathf.Max(0.1f, radius);
        SpawnScanPulse();
    }

    public void StopPulse()
    {
        StopActivePulse();
    }

    public void SetScanRadius(float radius)
    {
        scanRadius = Mathf.Max(0.1f, radius);
    }

    private void CacheReferences()
    {
        if (effectOrigin == null)
        {
            effectOrigin = transform;
        }
    }

    private Transform GetOrigin()
    {
        return effectOrigin != null ? effectOrigin : transform;
    }

    private void SpawnScanPulse()
    {
        StopActivePulse();

        if (scanPulseEffectPrefab == null)
        {
            return;
        }

        Transform origin = GetOrigin();
        GameObject effect;

        if (PoolManager.Instance != null && usePoolForPulse)
        {
            effect = PoolManager.Instance.Get(scanPulseEffectPrefab, origin.position, origin.rotation);
        }
        else
        {
            effect = Instantiate(scanPulseEffectPrefab, origin.position, origin.rotation);
        }

        if (effect == null)
        {
            return;
        }

        activePulseEffect = effect;
        effect.transform.localScale = Vector3.one;

        Animator animator = effect.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = false;
        }

        DashShockwaveVFX legacyShockwave =
            effect.GetComponentInChildren<DashShockwaveVFX>(true);
        if (legacyShockwave != null)
        {
            legacyShockwave.enabled = false;
        }

        SpriteRenderer pulseRenderer = effect.GetComponentInChildren<SpriteRenderer>(true);
        if (pulseRenderer == null)
        {
            StopActivePulse();
            return;
        }

        float targetScale = ResolveTargetScale(pulseRenderer);
        Transform visual = pulseRenderer.transform;
        visual.localScale = Vector3.one * Mathf.Max(0.01f, targetScale * 0.15f);
        pulseRenderer.color = ResolvePulseColor(scanPulseColor);

        float duration = ResolvePulseDuration(scanPulseLifeTime);
        activePulseSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        activePulseSequence.Join(
            visual.DOScale(Vector3.one * targetScale, duration)
                .SetEase(Ease.OutCubic));
        activePulseSequence.Join(
            pulseRenderer.DOFade(0f, duration)
                .SetEase(Ease.OutQuad));
        activePulseSequence.OnComplete(CompleteActivePulse);
    }

    public static Color ResolvePulseColor(Color configuredColor)
    {
        configuredColor.r = Mathf.Clamp(configuredColor.r, 0f, 0.35f);
        configuredColor.g = Mathf.Clamp(configuredColor.g, 0.55f, 0.95f);
        configuredColor.b = Mathf.Clamp(configuredColor.b, 0.85f, 1f);
        configuredColor.a = Mathf.Clamp(configuredColor.a, 0.1f, 0.18f);
        return configuredColor;
    }

    public static float ResolvePulseDuration(float configuredDuration)
    {
        return Mathf.Clamp(configuredDuration, 0.2f, 0.35f);
    }

    private float ResolveTargetScale(SpriteRenderer pulseRenderer)
    {
        float diameter = scanRadius * (scalePulseByScanRadius
            ? Mathf.Clamp(scanPulseScaleMultiplier, 1f, 2f)
            : 1f);
        float spriteWidth = pulseRenderer.sprite != null
            ? pulseRenderer.sprite.bounds.size.x
            : 1f;
        return Mathf.Max(0.01f, diameter / Mathf.Max(0.01f, spriteWidth));
    }

    private void CompleteActivePulse()
    {
        activePulseSequence = null;
        ReleaseActivePulseEffect();
    }

    private void StopActivePulse()
    {
        activePulseSequence?.Kill();
        activePulseSequence = null;
        ReleaseActivePulseEffect();
    }

    private void ReleaseActivePulseEffect()
    {
        if (activePulseEffect == null)
        {
            return;
        }

        GameObject effect = activePulseEffect;
        activePulseEffect = null;

        if (PoolManager.Instance != null && usePoolForPulse)
        {
            PoolManager.Instance.Release(effect);
        }
        else
        {
            Destroy(effect);
        }
    }
}
