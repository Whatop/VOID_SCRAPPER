using UnityEngine;

/// <summary>
/// 기지 밖에서 자원 보관 지점까지 이어지는 수동 경로입니다.
/// Waypoint는 반드시 바깥쪽 -> 입구 -> 안쪽 순서로 배치합니다.
/// 적은 화물 보관 후 같은 경로를 역순으로 빠져나갑니다.
/// </summary>
[DisallowMultipleComponent]
public class FieldBaseCargoRoute2D : MonoBehaviour
{
    [SerializeField] private string routeId = "CargoRoute";
    [SerializeField] private bool routeEnabled = true;
    [SerializeField] private Transform[] waypoints;
    [Min(0.05f)]
    [SerializeField] private float waypointArrivalDistance = 0.55f;

    public string RouteId => string.IsNullOrWhiteSpace(routeId) ? name : routeId;
    public bool IsValid => routeEnabled && isActiveAndEnabled && CountValidWaypoints() > 0;
    public int WaypointCount => waypoints != null ? waypoints.Length : 0;
    public float WaypointArrivalDistance => Mathf.Max(0.05f, waypointArrivalDistance);

    public Transform GetWaypoint(int index)
    {
        if (waypoints == null || index < 0 || index >= waypoints.Length)
        {
            return null;
        }

        return waypoints[index];
    }

    public Vector2 GetEntryPosition()
    {
        if (waypoints == null)
        {
            return transform.position;
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                return waypoints[i].position;
            }
        }

        return transform.position;
    }

    public float GetSqrDistanceToEntry(Vector2 position)
    {
        return (GetEntryPosition() - position).sqrMagnitude;
    }

    private int CountValidWaypoints()
    {
        if (waypoints == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null)
            {
                count++;
            }
        }

        return count;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            return;
        }

        Gizmos.color = routeEnabled ? new Color(1f, 0.65f, 0.1f, 1f) : Color.gray;

        Transform previous = null;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform current = waypoints[i];

            if (current == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(current.position, waypointArrivalDistance);

            if (previous != null)
            {
                Gizmos.DrawLine(previous.position, current.position);
            }

            previous = current;
        }
    }
#endif
}
