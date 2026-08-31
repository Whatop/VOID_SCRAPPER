using System;
using UnityEngine;

[Serializable]
public sealed class EnemyArrivalSpawnSettings
{
    public bool Enabled = true;

    [Header("Marker / Impact Optional")]
    public GameObject MarkerPrefab;
    public GameObject ImpactPrefab;
    public bool CreateFallbackMarker = true;
    public bool CreateFallbackLandingBurst = true;

    [Header("Timing")]
    [Min(0f)] public float WarningTime = 0.75f;
    [Min(0.01f)] public float TravelTime = 0.35f;
    [Min(0f)] public float ReadyDelay = 0.25f;

    [Header("Entry")]
    [Min(0.1f)] public float OffscreenEntryDistance = 10.5f;
    public bool PreferOffscreenEntry = true;
    [Range(0f, 0.5f)] public float CameraViewportPadding = 0.08f;

    [Header("Marker")]
    [Min(0.05f)] public float MarkerRadius = 0.85f;
    public Color MarkerColor = new Color(1f, 0.05f, 0.03f, 0.9f);

    [Header("Afterimage")]
    public bool UseAfterimages = true;
    [Min(0.01f)] public float AfterimageInterval = 0.055f;
    [Min(0.01f)] public float AfterimageLifetime = 0.22f;
    public Color AfterimageColor = new Color(1f, 0.18f, 0.12f, 0.35f);
    public int AfterimageSortingOrderOffset = -1;

    [Header("Collision")]
    public bool DisableCollidersDuringArrival = true;

    public static EnemyArrivalSpawnSettings CreateDefault()
    {
        return new EnemyArrivalSpawnSettings();
    }
}

/// <summary>
/// 이벤트 증원에서 사용하던 원 + 진입 화살표 + 화면 밖 돌진 연출을
/// 모든 일반 적 생성 경로에서 공유하기 위한 공통 유틸리티입니다.
/// 보스 전용 컷신 오브젝트는 이 유틸리티 대신 기존 보스 연출을 사용합니다.
/// </summary>
public static class EnemyArrivalSpawnUtility
{
    private static readonly EnemyArrivalSpawnSettings DefaultSettings =
        EnemyArrivalSpawnSettings.CreateDefault();

    public static bool BeginArrival(
        GameObject enemyObject,
        Vector2 arrivalPosition,
        Transform target,
        bool alertWhenReady,
        Vector2 fallbackCenter,
        EnemyArrivalSpawnSettings settings = null)
    {
        if (enemyObject == null)
        {
            return false;
        }

        EnemyBaseAI enemyAI = enemyObject.GetComponentInChildren<EnemyBaseAI>(true);

        if (enemyAI != null && enemyAI.IsBossEncounterIsolated)
        {
            return false;
        }

        EnemyArrivalSpawnSettings activeSettings = settings ?? DefaultSettings;

        if (!activeSettings.Enabled)
        {
            MoveImmediately(enemyObject, arrivalPosition);
            return false;
        }

        Vector2 entryPosition = ResolveEntryPosition(
            arrivalPosition,
            fallbackCenter,
            activeSettings
        );

        ShowArrivalMarker(arrivalPosition, entryPosition, activeSettings);

        EventEnemyArrivalMover mover = enemyObject.GetComponent<EventEnemyArrivalMover>();

        if (mover == null)
        {
            mover = enemyObject.AddComponent<EventEnemyArrivalMover>();
        }

        mover.Begin(
            entryPosition,
            arrivalPosition,
            target,
            alertWhenReady,
            activeSettings.WarningTime,
            activeSettings.TravelTime,
            activeSettings.ReadyDelay,
            activeSettings.ImpactPrefab,
            activeSettings.CreateFallbackLandingBurst,
            activeSettings.MarkerColor,
            Mathf.Max(0.1f, activeSettings.MarkerRadius * 0.75f),
            activeSettings.UseAfterimages,
            activeSettings.AfterimageInterval,
            activeSettings.AfterimageLifetime,
            activeSettings.AfterimageColor,
            activeSettings.AfterimageSortingOrderOffset,
            activeSettings.DisableCollidersDuringArrival
        );

        return true;
    }

    public static Vector2 ResolveEntryPosition(
        Vector2 arrivalPosition,
        Vector2 fallbackCenter,
        EnemyArrivalSpawnSettings settings = null)
    {
        EnemyArrivalSpawnSettings activeSettings = settings ?? DefaultSettings;
        float distance = Mathf.Max(0.1f, activeSettings.OffscreenEntryDistance);
        Camera camera = Camera.main;

        if (activeSettings.PreferOffscreenEntry && camera != null)
        {
            for (int i = 0; i < 24; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle;

                if (direction.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                direction.Normalize();
                Vector2 candidate = arrivalPosition + direction * distance;

                if (!IsInsideCameraView(
                        camera,
                        candidate,
                        Mathf.Max(0f, activeSettings.CameraViewportPadding)))
                {
                    return candidate;
                }
            }

            Vector2 fromCamera = arrivalPosition - (Vector2)camera.transform.position;

            if (fromCamera.sqrMagnitude > 0.001f)
            {
                return arrivalPosition + fromCamera.normalized * distance;
            }
        }

        Vector2 fallbackDirection = arrivalPosition - fallbackCenter;

        if (fallbackDirection.sqrMagnitude <= 0.001f)
        {
            fallbackDirection = UnityEngine.Random.insideUnitCircle;
        }

        if (fallbackDirection.sqrMagnitude <= 0.001f)
        {
            fallbackDirection = Vector2.up;
        }

        return arrivalPosition + fallbackDirection.normalized * distance;
    }

    private static bool IsInsideCameraView(
        Camera camera,
        Vector2 worldPosition,
        float viewportPadding)
    {
        if (camera == null)
        {
            return false;
        }

        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);

        if (viewport.z < 0f)
        {
            return false;
        }

        return viewport.x >= -viewportPadding &&
               viewport.x <= 1f + viewportPadding &&
               viewport.y >= -viewportPadding &&
               viewport.y <= 1f + viewportPadding;
    }

    private static void ShowArrivalMarker(
        Vector2 arrivalPosition,
        Vector2 entryPosition,
        EnemyArrivalSpawnSettings settings)
    {
        float markerDuration = Mathf.Max(
            0.05f,
            Mathf.Max(0f, settings.WarningTime) + Mathf.Max(0.01f, settings.TravelTime)
        );

        Vector2 approachDirection = arrivalPosition - entryPosition;

        if (approachDirection.sqrMagnitude > 0.001f)
        {
            approachDirection.Normalize();
        }

        if (settings.MarkerPrefab != null)
        {
            GameObject markerObject = UnityEngine.Object.Instantiate(
                settings.MarkerPrefab,
                arrivalPosition,
                Quaternion.identity
            );

            EventEnemyArrivalMarker marker =
                markerObject.GetComponent<EventEnemyArrivalMarker>();

            if (marker != null)
            {
                marker.Arm(
                    settings.MarkerColor,
                    settings.MarkerRadius,
                    markerDuration,
                    approachDirection,
                    false
                );
            }
            else
            {
                UnityEngine.Object.Destroy(markerObject, markerDuration + 0.15f);
            }

            return;
        }

        if (settings.CreateFallbackMarker)
        {
            EventEnemyArrivalMarker.Spawn(
                arrivalPosition,
                settings.MarkerColor,
                settings.MarkerRadius,
                markerDuration,
                approachDirection
            );
        }
    }

    private static void MoveImmediately(GameObject enemyObject, Vector2 position)
    {
        Rigidbody2D rb = enemyObject.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = position;
        }

        enemyObject.transform.position = position;
    }
}
