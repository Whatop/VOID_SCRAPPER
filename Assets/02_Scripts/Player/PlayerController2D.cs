using Action = System.Action;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    private readonly struct CameraViewportConstraint
    {
        public Camera ViewportCamera { get; }
        public Transform ViewportCenter { get; }
        public float LeftWorldLimit { get; }
        public float RightWorldLimit { get; }
        public float HorizontalInset { get; }
        public float TopInset { get; }
        public float BottomInset { get; }

        public CameraViewportConstraint(
            Camera viewportCamera,
            Transform viewportCenter,
            float leftWorldLimit,
            float rightWorldLimit,
            float horizontalInset,
            float topInset,
            float bottomInset)
        {
            ViewportCamera = viewportCamera;
            ViewportCenter = viewportCenter;
            LeftWorldLimit = leftWorldLimit;
            RightWorldLimit = rightWorldLimit;
            HorizontalInset = horizontalInset;
            TopInset = topInset;
            BottomInset = bottomInset;
        }
    }

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Action Names")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Rotation Settings")]
    [Tooltip("Visual-only aim pivot. Keep the Rigidbody2D root unrotated so physics interpolation remains the sole position presentation path.")]
    [SerializeField] private Transform aimVisualRoot;
    [SerializeField] private float rotationOffset = -90f;
    [Min(0f)]
    [SerializeField] private float aimDeadZoneDistance = 0.28f;
    [Min(0f)]
    [SerializeField] private float rotationSmoothSpeed = 28f;
    [SerializeField] private bool useUnscaledRotationTime = true;
    [Tooltip("Optional visual-only aim quantization. Zero keeps smooth rotation and does not affect AimDirection or projectile direction.")]
    [Min(0f)]
    [SerializeField] private float aimRotationStepDegrees;

    [Header("External Push")]
    [Min(0.02f)]
    [SerializeField] private float defaultExternalPushDuration = 0.22f;

    [Header("Safe Reposition")]
    [Tooltip("Layers that make a tactical reposition destination invalid. When empty, current project blocking layers are resolved by layer name.")]
    [SerializeField] private LayerMask repositionBlockingLayers;
    [Min(0f)]
    [SerializeField] private float repositionClearancePadding = 0.04f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Camera mainCamera;
    private InputActionMap playerActionMap;
    private InputAction moveAction;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.up;
    private float currentRotationZ;

    private bool controlEnabled = true;
    private bool movementLocked;
    private bool movementInputActive;
    private readonly HashSet<object> externalControlLocks = new HashSet<object>();

    private Vector2 externalPushVelocity;
    private float externalPushTimer;
    private float externalPushDuration;
    private readonly Dictionary<object, Vector2> ownedPushVelocities = new Dictionary<object, Vector2>();

    private bool movementVelocityOverrideActive;
    private Vector2 movementVelocityOverride;
    private readonly Collider2D[] repositionOverlapBuffer = new Collider2D[24];
    private readonly Dictionary<object, float> externalMoveSpeedMultipliers =
        new Dictionary<object, float>();
    private readonly Dictionary<object, CameraViewportConstraint> temporaryCameraViewportConstraints =
        new Dictionary<object, CameraViewportConstraint>();
    private readonly Dictionary<object, Bounds> temporaryWorldBoundsConstraints =
        new Dictionary<object, Bounds>();
    private float externalMoveSpeedMultiplier = 1f;

    public InputActionAsset InputActions => inputActions;
    public string ActionMapName => actionMapName;
    public Vector2 MoveInput => moveInput;
    public Vector2 AimDirection => aimDirection;
    public float MoveSpeed => moveSpeed;
    public float EffectiveMoveSpeed => moveSpeed * externalMoveSpeedMultiplier;
    public Transform AimVisualRoot => aimVisualRoot;
    public bool IsMoving => moveInput.sqrMagnitude > 0.001f;
    public bool ControlEnabled => controlEnabled && externalControlLocks.Count == 0;
    public bool MovementLocked => movementLocked;
    public bool MovementVelocityOverrideActive => movementVelocityOverrideActive;

    public event Action MovementStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        mainCamera = Camera.main;
        ResolveAimVisualRoot();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;

        currentRotationZ = aimVisualRoot != null
            ? aimVisualRoot.localEulerAngles.z
            : 0f;
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void OnDisable()
    {
        playerActionMap?.Disable();

        externalPushVelocity = Vector2.zero;
        externalPushTimer = 0f;
        ownedPushVelocities.Clear();
        movementVelocityOverrideActive = false;
        movementVelocityOverride = Vector2.zero;
        movementInputActive = false;
        temporaryCameraViewportConstraints.Clear();
        temporaryWorldBoundsConstraints.Clear();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void Update()
    {
        ReadInput();
        RotateToMouse();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void BindInput()
    {
        inputActions = InputBindingUtility.ResolvePlayerInputActions(inputActions, this);

        if (inputActions == null)
        {
            Debug.LogError("InputActionAsset이 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        playerActionMap = inputActions.FindActionMap(actionMapName, false);
        if (playerActionMap == null)
        {
            Debug.LogError($"Action Map을 찾을 수 없습니다: {actionMapName}", this);
            enabled = false;
            return;
        }

        InputBindingPersistence.LoadOnce(inputActions);
        moveAction = playerActionMap.FindAction(moveActionName, false);
        if (moveAction == null)
        {
            Debug.LogError($"Move Action을 찾을 수 없습니다: {moveActionName}", this);
            enabled = false;
            return;
        }

        playerActionMap.Enable();
    }

    private void ReadInput()
    {
        if (GameplayPauseManager.IsPaused)
        {
            SetMoveInput(Vector2.zero);
            return;
        }

        if (!ControlEnabled || moveAction == null)
        {
            SetMoveInput(Vector2.zero);
            return;
        }

        SetMoveInput(moveAction.ReadValue<Vector2>().normalized);
    }

    private void SetMoveInput(Vector2 value)
    {
        moveInput = value;

        bool isActive = moveInput.sqrMagnitude > 0.001f;
        if (isActive && !movementInputActive)
        {
            MovementStarted?.Invoke();
        }

        movementInputActive = isActive;
    }

    private void MovePlayer()
    {
        if (rb == null)
        {
            return;
        }

        Vector2 inputVelocity;

        if (movementVelocityOverrideActive)
        {
            inputVelocity = movementVelocityOverride;
        }
        else
        {
            inputVelocity = (!ControlEnabled || movementLocked)
                ? Vector2.zero
                : moveInput * EffectiveMoveSpeed;
        }

        Vector2 pushVelocity = Vector2.zero;

        if (externalPushTimer > 0f)
        {
            externalPushTimer = Mathf.Max(0f, externalPushTimer - Time.fixedDeltaTime);
            float ratio = externalPushDuration > 0f
                ? Mathf.Clamp01(externalPushTimer / externalPushDuration)
                : 0f;
            pushVelocity = externalPushVelocity * ratio;

            if (externalPushTimer <= 0f)
            {
                externalPushVelocity = Vector2.zero;
            }
        }

        foreach (Vector2 velocity in ownedPushVelocities.Values)
        {
            pushVelocity += velocity;
        }
        Vector2 resolvedVelocity = inputVelocity + pushVelocity;
        ApplyTemporaryMovementConstraints(ref resolvedVelocity);
        rb.linearVelocity = resolvedVelocity;
    }

    private void RotateToMouse()
    {
        if (GameplayPauseManager.IsPaused || !ControlEnabled)
        {
            return;
        }

        if (!TryGetAimWorldPosition(out Vector2 mouseWorldPosition))
        {
            return;
        }

        Vector2 direction = mouseWorldPosition - (Vector2)transform.position;
        float deadZone = Mathf.Max(0.001f, aimDeadZoneDistance);

        if (direction.sqrMagnitude <= deadZone * deadZone)
        {
            return;
        }

        aimDirection = direction.normalized;

        float targetRotationZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + rotationOffset;
        float rotationStep = Mathf.Max(0f, aimRotationStepDegrees);

        if (rotationStep > 0f)
        {
            targetRotationZ = Mathf.Round(targetRotationZ / rotationStep) * rotationStep;
        }

        float deltaTime = useUnscaledRotationTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (rotationSmoothSpeed <= 0f || deltaTime <= 0f)
        {
            currentRotationZ = targetRotationZ;
        }
        else
        {
            float t = 1f - Mathf.Exp(-rotationSmoothSpeed * deltaTime);
            currentRotationZ = Mathf.LerpAngle(currentRotationZ, targetRotationZ, t);
        }

        if (aimVisualRoot != null)
        {
            aimVisualRoot.localRotation = Quaternion.Euler(0f, 0f, currentRotationZ);
        }
    }

    public bool TryGetAimWorldPosition(out Vector2 worldPosition)
    {
        worldPosition = transform.position;

        if (Mouse.current == null)
        {
            return false;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        worldPosition = new Vector2(mouseWorldPosition.x, mouseWorldPosition.y);
        return true;
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;

        if (!controlEnabled)
        {
            SetMoveInput(Vector2.zero);
            ClearMovementVelocityOverride();

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void SetExternalControlLocked(object source, bool locked)
    {
        if (source == null)
        {
            return;
        }

        if (locked)
        {
            externalControlLocks.Add(source);
        }
        else
        {
            externalControlLocks.Remove(source);
        }

        if (!ControlEnabled)
        {
            SetMoveInput(Vector2.zero);

            if (rb != null && !movementVelocityOverrideActive)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (locked && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void SetMovementVelocityOverride(Vector2 velocity)
    {
        movementVelocityOverride = velocity;
        movementVelocityOverrideActive = true;
    }

    public void ClearMovementVelocityOverride()
    {
        movementVelocityOverrideActive = false;
        movementVelocityOverride = Vector2.zero;
    }

    public bool AcquireTemporaryCameraViewportConstraint(
        object owner,
        Camera viewportCamera,
        Transform viewportCenter,
        float leftWorldLimit,
        float rightWorldLimit,
        float horizontalInset,
        float topInset,
        float bottomInset)
    {
        if (owner == null || viewportCamera == null || viewportCenter == null ||
            !IsFinite(leftWorldLimit) || !IsFinite(rightWorldLimit) ||
            leftWorldLimit >= rightWorldLimit)
        {
            return false;
        }

        CameraViewportConstraint constraint = new CameraViewportConstraint(
            viewportCamera,
            viewportCenter,
            leftWorldLimit,
            rightWorldLimit,
            Mathf.Max(0f, horizontalInset),
            Mathf.Max(0f, topInset),
            Mathf.Max(0f, bottomInset)
        );

        if (!TryResolveConstraintBounds(constraint, out _))
        {
            return false;
        }

        temporaryCameraViewportConstraints[owner] = constraint;
        return true;
    }

    public void ReleaseTemporaryCameraViewportConstraint(object owner)
    {
        if (owner != null)
        {
            temporaryCameraViewportConstraints.Remove(owner);
        }
    }

    public bool HasTemporaryCameraViewportConstraint(object owner)
    {
        return owner != null && temporaryCameraViewportConstraints.ContainsKey(owner);
    }

    public bool TryGetTemporaryCameraViewportConstraintBounds(object owner, out Bounds bounds)
    {
        bounds = default;

        return owner != null &&
               temporaryCameraViewportConstraints.TryGetValue(owner, out CameraViewportConstraint constraint) &&
               TryResolveConstraintBounds(constraint, out bounds);
    }

    public bool AcquireTemporaryWorldBoundsConstraint(object owner, Bounds worldBounds)
    {
        if (owner == null ||
            !IsFinite(worldBounds.min) ||
            !IsFinite(worldBounds.max) ||
            worldBounds.min.x >= worldBounds.max.x ||
            worldBounds.min.y >= worldBounds.max.y)
        {
            return false;
        }

        temporaryWorldBoundsConstraints[owner] = worldBounds;

        if (rb != null)
        {
            Vector2 velocity = rb.linearVelocity;
            ApplyTemporaryMovementConstraints(ref velocity);
            rb.linearVelocity = velocity;
        }

        return true;
    }

    public void ReleaseTemporaryWorldBoundsConstraint(object owner)
    {
        if (owner != null)
        {
            temporaryWorldBoundsConstraints.Remove(owner);
        }
    }

    public bool HasTemporaryWorldBoundsConstraint(object owner)
    {
        return owner != null && temporaryWorldBoundsConstraints.ContainsKey(owner);
    }

    public bool TryGetTemporaryWorldBoundsConstraint(object owner, out Bounds bounds)
    {
        bounds = default;
        return owner != null && temporaryWorldBoundsConstraints.TryGetValue(owner, out bounds);
    }

    public void ApplyExternalPush(
        Vector2 direction,
        float distance,
        float duration = -1f)
    {
        if (rb == null || distance <= 0f)
        {
            return;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Random.insideUnitCircle;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.up;
        }

        float finalDuration = duration > 0f
            ? duration
            : Mathf.Max(0.02f, defaultExternalPushDuration);

        externalPushDuration = Mathf.Max(0.02f, finalDuration);
        externalPushTimer = externalPushDuration;
        externalPushVelocity = direction.normalized * (distance / externalPushDuration);
    }

    // Sustained forces compose with movement/dash and the existing decaying impulse.
    // The source must clear its own contribution on cancellation or completion.
    public void SetExternalPushVelocity(object source, Vector2 velocity)
    {
        if (source != null && IsFinite(velocity)) ownedPushVelocities[source] = velocity;
    }

    public void ClearExternalPush(object source)
    {
        if (source != null) ownedPushVelocities.Remove(source);
    }

    public bool IsRepositionDestinationValid(Vector2 destination)
    {
        if (!IsFinite(destination))
        {
            return false;
        }

        int blockingMask = ResolveRepositionBlockingMask();
        if (blockingMask == 0)
        {
            return false;
        }

        Physics2D.SyncTransforms();

        Vector2 centerOffset = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center - (Vector2)transform.position
            : Vector2.zero;
        float clearanceRadius = bodyCollider != null
            ? Mathf.Max(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y)
            : 0.18f;
        clearanceRadius = Mathf.Max(0.08f, clearanceRadius + repositionClearancePadding);

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(blockingMask);
        // Boss walls and some generated blockers are trigger-based, so they must
        // participate in destination validation as well as solid colliders.
        filter.useTriggers = true;

        int hitCount = Physics2D.OverlapCircle(
            destination + centerOffset,
            clearanceRadius,
            filter,
            repositionOverlapBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = repositionOverlapBuffer[i];
            repositionOverlapBuffer[i] = null;

            if (hit != null && hit.transform.root != transform.root)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryRepositionTo(Vector2 destination)
    {
        if (rb == null || !IsRepositionDestinationValid(destination))
        {
            return false;
        }

        SetMoveInput(Vector2.zero);
        externalPushVelocity = Vector2.zero;
        externalPushTimer = 0f;
        ClearMovementVelocityOverride();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = destination;

        Physics2D.SyncTransforms();
        GungeonStyleCamera2D.Instance?.NotifyTargetWarped();
        return true;
    }

    private void ResolveAimVisualRoot()
    {
        if (aimVisualRoot != null)
        {
            return;
        }

        aimVisualRoot = transform.Find("AimVisualRoot");

        if (aimVisualRoot == null)
        {
            // Legacy scene safety: keep aim rotation visual-only even before an
            // authored AimVisualRoot is assigned. Production scenes serialize
            // the dedicated pivot above the recoil VisualRoot.
            aimVisualRoot = transform.Find("VisualRoot");
        }

        if (aimVisualRoot == null)
        {
            Debug.LogError("Player aim visual root is not assigned.", this);
        }
    }

    private int ResolveRepositionBlockingMask()
    {
        if (repositionBlockingLayers.value != 0)
        {
            return repositionBlockingLayers.value;
        }

        return LayerMask.GetMask("Meteor", "Shop", "WorldSolid", "BaseLaser");
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void ApplyTemporaryMovementConstraints(ref Vector2 velocity)
    {
        if ((temporaryCameraViewportConstraints.Count == 0 &&
             temporaryWorldBoundsConstraints.Count == 0) ||
            !TryResolveCombinedTemporaryBounds(out Bounds allowedBounds))
        {
            return;
        }

        Vector2 bodyPosition = rb.position;
        Vector2 colliderExtents = bodyCollider != null
            ? bodyCollider.bounds.extents
            : Vector2.zero;
        Vector2 colliderCenterOffset = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center - bodyPosition
            : Vector2.zero;

        float minimumX = allowedBounds.min.x + colliderExtents.x - colliderCenterOffset.x;
        float maximumX = allowedBounds.max.x - colliderExtents.x - colliderCenterOffset.x;
        float minimumY = allowedBounds.min.y + colliderExtents.y - colliderCenterOffset.y;
        float maximumY = allowedBounds.max.y - colliderExtents.y - colliderCenterOffset.y;

        CollapseInvalidRange(ref minimumX, ref maximumX);
        CollapseInvalidRange(ref minimumY, ref maximumY);

        Vector2 constrainedPosition = new Vector2(
            Mathf.Clamp(bodyPosition.x, minimumX, maximumX),
            Mathf.Clamp(bodyPosition.y, minimumY, maximumY)
        );

        if ((constrainedPosition - bodyPosition).sqrMagnitude > 0.0000001f)
        {
            rb.position = constrainedPosition;
            bodyPosition = constrainedPosition;
        }

        float fixedDeltaTime = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        velocity.x = Mathf.Clamp(
            velocity.x,
            (minimumX - bodyPosition.x) / fixedDeltaTime,
            (maximumX - bodyPosition.x) / fixedDeltaTime
        );
        velocity.y = Mathf.Clamp(
            velocity.y,
            (minimumY - bodyPosition.y) / fixedDeltaTime,
            (maximumY - bodyPosition.y) / fixedDeltaTime
        );
    }

    private bool TryResolveCombinedTemporaryBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        Vector3 combinedMinimum = Vector3.zero;
        Vector3 combinedMaximum = Vector3.zero;

        foreach (KeyValuePair<object, CameraViewportConstraint> entry in temporaryCameraViewportConstraints)
        {
            if (!TryResolveConstraintBounds(entry.Value, out Bounds constraintBounds))
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedMinimum = constraintBounds.min;
                combinedMaximum = constraintBounds.max;
                hasBounds = true;
                continue;
            }

            combinedMinimum.x = Mathf.Max(combinedMinimum.x, constraintBounds.min.x);
            combinedMinimum.y = Mathf.Max(combinedMinimum.y, constraintBounds.min.y);
            combinedMaximum.x = Mathf.Min(combinedMaximum.x, constraintBounds.max.x);
            combinedMaximum.y = Mathf.Min(combinedMaximum.y, constraintBounds.max.y);
        }

        foreach (KeyValuePair<object, Bounds> entry in temporaryWorldBoundsConstraints)
        {
            Bounds constraintBounds = entry.Value;

            if (!hasBounds)
            {
                combinedMinimum = constraintBounds.min;
                combinedMaximum = constraintBounds.max;
                hasBounds = true;
                continue;
            }

            combinedMinimum.x = Mathf.Max(combinedMinimum.x, constraintBounds.min.x);
            combinedMinimum.y = Mathf.Max(combinedMinimum.y, constraintBounds.min.y);
            combinedMaximum.x = Mathf.Min(combinedMaximum.x, constraintBounds.max.x);
            combinedMaximum.y = Mathf.Min(combinedMaximum.y, constraintBounds.max.y);
        }

        if (!hasBounds || combinedMinimum.x > combinedMaximum.x || combinedMinimum.y > combinedMaximum.y)
        {
            return false;
        }

        combinedBounds.SetMinMax(combinedMinimum, combinedMaximum);
        return true;
    }

    private static bool TryResolveConstraintBounds(
        CameraViewportConstraint constraint,
        out Bounds bounds)
    {
        bounds = default;
        Camera viewportCamera = constraint.ViewportCamera;
        Transform viewportCenter = constraint.ViewportCenter;
        if (viewportCamera == null || viewportCenter == null || !viewportCamera.orthographic)
        {
            return false;
        }

        Vector3 center = viewportCenter.position;
        float halfHeight = Mathf.Max(0.01f, viewportCamera.orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.01f, viewportCamera.aspect);
        float minimumX = Mathf.Max(
            center.x - halfWidth + constraint.HorizontalInset,
            constraint.LeftWorldLimit
        );
        float maximumX = Mathf.Min(
            center.x + halfWidth - constraint.HorizontalInset,
            constraint.RightWorldLimit
        );
        float minimumY = center.y - halfHeight + constraint.BottomInset;
        float maximumY = center.y + halfHeight - constraint.TopInset;

        if (minimumX > maximumX || minimumY > maximumY)
        {
            return false;
        }

        bounds.SetMinMax(
            new Vector3(minimumX, minimumY, 0f),
            new Vector3(maximumX, maximumY, 0f)
        );
        return true;
    }

    private static void CollapseInvalidRange(ref float minimum, ref float maximum)
    {
        if (minimum <= maximum)
        {
            return;
        }

        float center = (minimum + maximum) * 0.5f;
        minimum = center;
        maximum = center;
    }

    public void SetMoveSpeed(float newMoveSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newMoveSpeed);
    }

    public void AddMoveSpeed(float amount)
    {
        moveSpeed = Mathf.Max(0.1f, moveSpeed + amount);
    }

    public void SetExternalMoveSpeedMultiplier(object source, float multiplier)
    {
        if (source == null)
        {
            return;
        }

        externalMoveSpeedMultipliers[source] = Mathf.Max(0.05f, multiplier);
        RecalculateExternalMoveSpeedMultiplier();
    }

    public void ClearExternalMoveSpeedMultiplier(object source)
    {
        if (source == null || !externalMoveSpeedMultipliers.Remove(source))
        {
            return;
        }

        RecalculateExternalMoveSpeedMultiplier();
    }

    private void RecalculateExternalMoveSpeedMultiplier()
    {
        float multiplier = 1f;

        foreach (KeyValuePair<object, float> entry in externalMoveSpeedMultipliers)
        {
            multiplier *= Mathf.Max(0.05f, entry.Value);
        }

        externalMoveSpeedMultiplier = Mathf.Max(0.05f, multiplier);
    }
}
