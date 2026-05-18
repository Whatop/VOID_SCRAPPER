using UnityEngine;

public enum RadarScanResult
{
    None,
    Detected,
    Ignored
}

public readonly struct RadarScanContext
{
    public readonly GameObject scannerObject;
    public readonly Transform scannerTransform;
    public readonly Vector2 scanOrigin;
    public readonly WeaponTreeType weaponTreeType;
    public readonly float scanRadius;

    public RadarScanContext(
        GameObject scannerObject,
        Transform scannerTransform,
        Vector2 scanOrigin,
        WeaponTreeType weaponTreeType,
        float scanRadius)
    {
        this.scannerObject = scannerObject;
        this.scannerTransform = scannerTransform;
        this.scanOrigin = scanOrigin;
        this.weaponTreeType = weaponTreeType;
        this.scanRadius = scanRadius;
    }
}

public interface IRadarScannable
{
    RadarMarkerType MarkerType { get; }
    Transform RadarTransform { get; }
    bool IsRadarVisible { get; }

    RadarScanResult OnRadarScanned(RadarScanContext context);
}