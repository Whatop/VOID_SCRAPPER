using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RadarTarget : MonoBehaviour, IRadarScannable
{
    private static readonly HashSet<RadarTarget> activeTargets = new HashSet<RadarTarget>();
    private readonly Dictionary<UnityEngine.Object, float> temporaryRevealExpirations =
        new Dictionary<UnityEngine.Object, float>(2);

    [Header("Radar")]
    [SerializeField] private RadarMarkerType markerType = RadarMarkerType.RewardObject;
    [SerializeField] private Transform markerTransform;
    [SerializeField] private bool visible = true;

    [Header("Marker Visual")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Color markerColor = Color.white;
    [SerializeField] private float markerScale = 1f;

    [Header("Full Map")]
    [SerializeField] private bool showOnMap = true;
    [Tooltip("적처럼 일시적으로만 표시할 대상의 최소 지도 유지 시간입니다.")]
    [SerializeField, Min(0f)] private float mapMarkerLifetime = 6f;
    [SerializeField] private bool mapDiscovered;

    [Header("Enemy Reaction")]
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private bool allowShotgunTaunt = true;
    [SerializeField] private bool alertOnMachineGunScan;
    [SerializeField] private bool alertOnSniperScan;

    [Header("Runtime Scan State")]
    [SerializeField] private float lastScannedTime = -999f;
    private Vector2 recordedMapPosition;
    private bool hasRecordedMapPosition;

    public static IReadOnlyCollection<RadarTarget> ActiveTargets => activeTargets;
    public static event Action RegistryChanged;
    public static event Action<RadarTarget> PresentationChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeTargets.Clear();
        RegistryChanged = null;
        PresentationChanged = null;
    }

    public RadarMarkerType MarkerType => markerType;
    public Transform RadarTransform => markerTransform != null ? markerTransform : transform;
    public bool IsRadarVisible => visible && isActiveAndEnabled && gameObject.activeInHierarchy;
    public bool IsTemporarilyRevealed
    {
        get
        {
            if (!IsRadarVisible)
            {
                return false;
            }

            float now = Time.time;

            foreach (KeyValuePair<UnityEngine.Object, float> reveal in temporaryRevealExpirations)
            {
                if (reveal.Key != null && reveal.Value > now)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public Vector3 WorldPosition => RadarTransform.position;
    public Sprite MarkerSprite => markerSprite;
    public Color MarkerColor => markerColor;
    public float MarkerScale => Mathf.Max(0.1f, markerScale);

    public bool ShowOnMap => showOnMap;
    public float MapMarkerLifetime => Mathf.Max(0f, mapMarkerLifetime);
    public bool IsMapDiscovered => mapDiscovered;

    public bool AllowShotgunTaunt => allowShotgunTaunt;
    public bool AlertOnMachineGunScan => alertOnMachineGunScan;
    public bool AlertOnSniperScan => alertOnSniperScan;
    public float LastScannedTime => lastScannedTime;
    public Vector2 RecordedMapPosition => hasRecordedMapPosition
        ? recordedMapPosition
        : (Vector2)WorldPosition;

    private void Reset()
    {
        markerTransform = transform;
        markerScale = 1f;
        markerColor = Color.white;
        enemyAI = GetComponentInParent<EnemyBaseAI>();
    }

    private void Awake()
    {
        if (markerTransform == null)
        {
            markerTransform = transform;
        }

        if (enemyAI == null)
        {
            enemyAI = GetComponentInParent<EnemyBaseAI>();
        }
    }

    private void OnEnable()
    {
        lastScannedTime = -999f;
        if (mapDiscovered && !hasRecordedMapPosition)
        {
            RecordCurrentMapPosition();
        }

        temporaryRevealExpirations.Clear();
        activeTargets.Add(this);
        RegistryChanged?.Invoke();
    }

    private void OnDisable()
    {
        temporaryRevealExpirations.Clear();

        if (activeTargets.Remove(this))
        {
            RegistryChanged?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (activeTargets.Remove(this))
        {
            RegistryChanged?.Invoke();
        }
    }

    public void SetVisible(bool value)
    {
        visible = value;
        NotifyPresentationChanged();
    }

    public void SetMarkerType(RadarMarkerType type)
    {
        markerType = type;
        NotifyPresentationChanged();
    }

    public void SetMarkerVisual(Sprite sprite, Color color, float scale = 1f)
    {
        markerSprite = sprite;
        markerColor = color;
        markerScale = Mathf.Max(0.1f, scale);
        NotifyPresentationChanged();
    }

    public void SetMapDiscovered(bool value)
    {
        if (value)
        {
            RecordCurrentMapPosition();
        }
        else
        {
            hasRecordedMapPosition = false;
        }

        if (mapDiscovered == value)
        {
            return;
        }

        mapDiscovered = value;
        RegistryChanged?.Invoke();
    }

    public void SetShowOnMap(bool value)
    {
        showOnMap = value;
        NotifyPresentationChanged();
    }

    private void NotifyPresentationChanged()
    {
        RegistryChanged?.Invoke();
        PresentationChanged?.Invoke(this);
    }

    public bool SetTemporaryReveal(UnityEngine.Object source, float duration)
    {
        if (source == null || duration <= 0f || !IsRadarVisible)
        {
            return false;
        }

        float requestedExpiration = Time.time + duration;

        if (temporaryRevealExpirations.TryGetValue(source, out float currentExpiration))
        {
            temporaryRevealExpirations[source] = Mathf.Max(currentExpiration, requestedExpiration);
        }
        else
        {
            temporaryRevealExpirations.Add(source, requestedExpiration);
        }

        RegistryChanged?.Invoke();
        return true;
    }

    public bool ClearTemporaryReveal(UnityEngine.Object source)
    {
        if (source == null || !temporaryRevealExpirations.Remove(source))
        {
            return false;
        }

        RegistryChanged?.Invoke();
        return true;
    }

    public RadarScanResult OnRadarScanned(RadarScanContext context)
    {
        if (!IsRadarVisible)
        {
            return RadarScanResult.Ignored;
        }

        lastScannedTime = Time.time;
        RecordCurrentMapPosition();
        mapDiscovered = true;
        HandleEnemyScanReaction(context);
        RegistryChanged?.Invoke();
        return RadarScanResult.Detected;
    }

    public bool WasScannedRecently(float duration)
    {
        return duration > 0f && Time.time - lastScannedTime <= duration;
    }

    private void RecordCurrentMapPosition()
    {
        recordedMapPosition = WorldPosition;
        hasRecordedMapPosition = true;
    }

    private void HandleEnemyScanReaction(RadarScanContext context)
    {
        if (enemyAI == null || !IsEnemyLikeMarker() || enemyAI.CurrentState == EnemyState.Dead)
        {
            return;
        }

        switch (context.weaponTreeType)
        {
            case WeaponTreeType.Shotgun:
                if (allowShotgunTaunt && enemyAI.IsRadarTauntable)
                {
                    PlayerRuntimeBonusState bonusState = context.scannerObject != null
                        ? context.scannerObject.GetComponent<PlayerRuntimeBonusState>()
                        : null;
                    float durationBonus = bonusState != null
                        ? bonusState.RadarTauntDurationBonus
                        : 0f;
                    enemyAI.ApplyRadarTaunt(
                        context.scannerObject != null ? context.scannerObject : this,
                        context.scanOrigin,
                        durationBonus
                    );
                }
                break;

            case WeaponTreeType.Sniper:
                if (alertOnSniperScan)
                {
                    enemyAI.ApplyRadarAlert(context.scanOrigin);
                }
                break;

            case WeaponTreeType.MachineGun:
                if (alertOnMachineGunScan)
                {
                    enemyAI.ApplyRadarAlert(context.scanOrigin);
                }
                break;
        }
    }

    private bool IsEnemyLikeMarker()
    {
        return markerType == RadarMarkerType.Enemy || markerType == RadarMarkerType.Boss;
    }
}
