using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRadarVFXController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform effectOrigin;
    [SerializeField] private PlayerChargeGaugeUI chargeGauge;

    [Header("Charge Gauge")]
    [SerializeField] private bool controlChargeGauge = true;

    [Tooltip("켜면 레이더 충전 게이지를 쓰지 않고 이펙트/사운드 중심으로 표현합니다. 슬라이더 과밀 방지용 기본값입니다.")]
    [SerializeField] private bool useEffectOnlyFeedback = true;

    [Header("Charge Loop Effect")]
    [SerializeField] private GameObject chargeLoopEffectPrefab;
    [SerializeField] private bool parentChargeLoopToOrigin = true;
    [SerializeField] private float chargeMinScale = 0.75f;
    [SerializeField] private float chargeMaxScale = 1.25f;
    [SerializeField] private float chargeLoopDestroyDelay = 0.15f;

    [Header("Scan Pulse Effect")]
    [SerializeField] private GameObject scanPulseEffectPrefab;
    [SerializeField] private float scanPulseLifeTime = 0.45f;
    [SerializeField] private bool scalePulseByScanRadius = true;
    [SerializeField] private float scanRadius = 15f;
    [SerializeField] private float scanPulseScaleMultiplier = 2f;
    [SerializeField] private bool usePoolForPulse = true;

    private GameObject activeChargeEffect;
    private float currentChargeRatio;

    public bool IsCharging { get; private set; }

    private void Reset()
    {
        effectOrigin = transform;
        chargeGauge = FindFirstObjectByType<PlayerChargeGaugeUI>(FindObjectsInactive.Include);
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        CancelCharge();
    }

    public void BeginCharge()
    {
        IsCharging = true;
        currentChargeRatio = 0f;

        if (ShouldControlGauge())
        {
            chargeGauge.ShowRatio(0f);
        }

        SpawnChargeLoop();
        ApplyChargeLoopScale(0f);
    }

    public void SetChargeRatio(float ratio)
    {
        currentChargeRatio = Mathf.Clamp01(ratio);

        if (ShouldControlGauge())
        {
            chargeGauge.ShowRatio(currentChargeRatio);
        }

        if (activeChargeEffect != null)
        {
            Transform origin = GetOrigin();

            if (!parentChargeLoopToOrigin)
            {
                activeChargeEffect.transform.position = origin.position;
                activeChargeEffect.transform.rotation = origin.rotation;
            }

            ApplyChargeLoopScale(currentChargeRatio);
        }
    }

    public void CancelCharge()
    {
        IsCharging = false;
        currentChargeRatio = 0f;

        if (ShouldControlGauge())
        {
            chargeGauge.Hide();
        }

        StopChargeLoop();
    }

    public void CompleteScan()
    {
        IsCharging = false;
        currentChargeRatio = 1f;

        if (ShouldControlGauge())
        {
            chargeGauge.ShowRatio(1f);
            chargeGauge.Hide();
        }

        StopChargeLoop();
        SpawnScanPulse();
    }

    public void PlayPulse(float radius)
    {
        scanRadius = Mathf.Max(0.1f, radius);
        SpawnScanPulse();
    }

    public void SetScanRadius(float radius)
    {
        scanRadius = Mathf.Max(0.1f, radius);
    }

    private bool ShouldControlGauge()
    {
        return controlChargeGauge && !useEffectOnlyFeedback && chargeGauge != null;
    }

    private void CacheReferences()
    {
        if (effectOrigin == null)
        {
            effectOrigin = transform;
        }

        if (chargeGauge == null)
        {
            chargeGauge = FindFirstObjectByType<PlayerChargeGaugeUI>(FindObjectsInactive.Include);
        }
    }

    private Transform GetOrigin()
    {
        return effectOrigin != null ? effectOrigin : transform;
    }

    private void SpawnChargeLoop()
    {
        if (chargeLoopEffectPrefab == null)
        {
            return;
        }

        if (activeChargeEffect != null)
        {
            return;
        }

        Transform origin = GetOrigin();

        if (parentChargeLoopToOrigin)
        {
            activeChargeEffect = Instantiate(
                chargeLoopEffectPrefab,
                origin.position,
                origin.rotation,
                origin
            );

            activeChargeEffect.transform.localPosition = Vector3.zero;
            activeChargeEffect.transform.localRotation = Quaternion.identity;
        }
        else
        {
            activeChargeEffect = Instantiate(
                chargeLoopEffectPrefab,
                origin.position,
                origin.rotation
            );
        }

        ApplyChargeLoopScale(currentChargeRatio);
    }

    private void StopChargeLoop()
    {
        if (activeChargeEffect == null)
        {
            return;
        }

        ParticleSystem[] particles = activeChargeEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            if (particle == null)
            {
                continue;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(activeChargeEffect, Mathf.Max(0f, chargeLoopDestroyDelay));
        activeChargeEffect = null;
    }

    private void ApplyChargeLoopScale(float ratio)
    {
        if (activeChargeEffect == null)
        {
            return;
        }

        float scale = Mathf.Lerp(chargeMinScale, chargeMaxScale, Mathf.Clamp01(ratio));
        activeChargeEffect.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    private void SpawnScanPulse()
    {
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

        if (scalePulseByScanRadius)
        {
            float scale = scanRadius * scanPulseScaleMultiplier;
            effect.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        }

        if (PoolManager.Instance != null && usePoolForPulse)
        {
            PoolManager.Instance.ReleaseAfter(effect, scanPulseLifeTime);
        }
        else
        {
            Destroy(effect, scanPulseLifeTime);
        }
    }
}
