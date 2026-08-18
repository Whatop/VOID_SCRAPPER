using UnityEngine;

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class SmoothCameraTarget2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private Vector3 worldOffset;

    [Header("Response")]
    [Min(0.001f)]
    [SerializeField] private float movingSmoothTime = 0.065f;
    [Min(0.001f)]
    [SerializeField] private float stoppingSmoothTime = 0.14f;
    [Min(0f)]
    [SerializeField] private float movingVelocityThreshold = 0.08f;
    [Min(0.1f)]
    [SerializeField] private float maxFollowSpeed = 100f;

    [Header("Teleport / Scene Transition")]
    [SerializeField] private bool snapOnEnable = true;
    [Min(0f)]
    [SerializeField] private float teleportSnapDistance = 5f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool preserveCurrentZ = true;

    private Vector3 smoothVelocity;

    public Transform Target => target;

    private void Reset()
    {
        ResolveTarget();
    }

    private void Awake()
    {
        ResolveTarget();

        if (snapOnEnable)
        {
            SnapToTarget();
        }
    }

    private void OnEnable()
    {
        ResolveTarget();

        if (snapOnEnable)
        {
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        ResolveTarget();

        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + worldOffset;

        if (preserveCurrentZ)
        {
            desired.z = transform.position.z;
        }

        float snapDistance = Mathf.Max(0f, teleportSnapDistance);
        if (snapDistance > 0f && (desired - transform.position).sqrMagnitude >= snapDistance * snapDistance)
        {
            transform.position = desired;
            smoothVelocity = Vector3.zero;
            return;
        }

        bool moving = targetRigidbody != null &&
                      targetRigidbody.linearVelocity.sqrMagnitude > movingVelocityThreshold * movingVelocityThreshold;
        float smoothTime = moving
            ? Mathf.Max(0.001f, movingSmoothTime)
            : Mathf.Max(0.001f, stoppingSmoothTime);
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref smoothVelocity,
            smoothTime,
            Mathf.Max(0.1f, maxFollowSpeed),
            deltaTime
        );
    }

    public void SetTarget(Transform newTarget, bool snapImmediately = true)
    {
        target = newTarget;
        targetRigidbody = target != null ? target.GetComponent<Rigidbody2D>() : null;

        if (snapImmediately)
        {
            SnapToTarget();
        }
    }

    [ContextMenu("Snap To Target")]
    public void SnapToTarget()
    {
        ResolveTarget();

        if (target == null)
        {
            return;
        }

        Vector3 desired = target.position + worldOffset;

        if (preserveCurrentZ)
        {
            desired.z = transform.position.z;
        }

        transform.position = desired;
        smoothVelocity = Vector3.zero;
    }

    private void ResolveTarget()
    {
        if (target == null && autoFindPlayer)
        {
            PlayerController2D playerController = FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (playerController != null)
            {
                target = playerController.transform;
            }
        }

        if (target != null && targetRigidbody == null)
        {
            targetRigidbody = target.GetComponent<Rigidbody2D>();
        }
    }
}
