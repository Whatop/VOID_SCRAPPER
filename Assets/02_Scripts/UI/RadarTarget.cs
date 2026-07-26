using UnityEngine;

[DisallowMultipleComponent]
public class RadarTarget : MonoBehaviour, IRadarScannable
{
    [Header("Radar")]
    [SerializeField] private RadarMarkerType markerType = RadarMarkerType.RewardObject;
    [SerializeField] private Transform markerTransform;
    [SerializeField] private bool visible = true;

    [Header("Marker Visual")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Color markerColor = Color.white;
    [SerializeField] private float markerScale = 1f;

    [Header("Enemy Reaction")]
    [SerializeField] private EnemyBaseAI enemyAI;
    [SerializeField] private bool allowShotgunTaunt = true;
    [SerializeField] private bool alertOnMachineGunScan;
    [SerializeField] private bool alertOnSniperScan;

    [Header("Runtime Scan State")]
    [SerializeField] private float lastScannedTime = -999f;

    public RadarMarkerType MarkerType => markerType;
    public Transform RadarTransform => markerTransform != null ? markerTransform : transform;
    public bool IsRadarVisible => visible && isActiveAndEnabled && gameObject.activeInHierarchy;

    public Vector3 WorldPosition => RadarTransform.position;
    public Sprite MarkerSprite => markerSprite;
    public Color MarkerColor => markerColor;
    public float MarkerScale => Mathf.Max(0.1f, markerScale);

    public bool AllowShotgunTaunt => allowShotgunTaunt;
    public bool AlertOnMachineGunScan => alertOnMachineGunScan;
    public bool AlertOnSniperScan => alertOnSniperScan;
    public float LastScannedTime => lastScannedTime;

    private void Reset()
    {
        markerTransform = transform;
        markerScale = 1f;
        markerColor = Color.white;
        enemyAI = GetComponentInParent<EnemyBaseAI>();
    }

    private void OnEnable()
    {
        lastScannedTime = -999f;
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

    public void SetVisible(bool value)
    {
        visible = value;
    }

    public void SetMarkerType(RadarMarkerType type)
    {
        markerType = type;
    }

    public void SetMarkerVisual(Sprite sprite, Color color, float scale = 1f)
    {
        markerSprite = sprite;
        markerColor = color;
        markerScale = Mathf.Max(0.1f, scale);
    }

    public RadarScanResult OnRadarScanned(RadarScanContext context)
    {
        if (!IsRadarVisible)
        {
            return RadarScanResult.Ignored;
        }

        lastScannedTime = Time.time;
        HandleEnemyScanReaction(context);
        return RadarScanResult.Detected;
    }

    public bool WasScannedRecently(float duration)
    {
        return duration > 0f && Time.time - lastScannedTime <= duration;
    }

    private void HandleEnemyScanReaction(RadarScanContext context)
    {
        if (enemyAI == null)
        {
            return;
        }

        if (!IsEnemyLikeMarker())
        {
            return;
        }

        if (enemyAI.CurrentState == EnemyState.Dead)
        {
            return;
        }

        switch (context.weaponTreeType)
        {
            case WeaponTreeType.Shotgun:
                if (allowShotgunTaunt && enemyAI.IsRadarTauntable)
                {
                    enemyAI.ApplyRadarTaunt(context.scanOrigin);
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
        return markerType == RadarMarkerType.Enemy ||
               markerType == RadarMarkerType.Boss;
    }
}