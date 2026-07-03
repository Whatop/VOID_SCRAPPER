using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class SpaceParallaxLayer2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("SpaceKit Style Parallax")]
    [Tooltip("1이면 카메라를 완전히 따라가서 화면상 거의 고정됩니다. 0이면 월드 원점에 고정되어 가장 크게 밀립니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float movementResistance = 0.95f;

    [SerializeField] private bool autoFindCamera = true;
    [SerializeField] private bool lockZ = true;

    [Header("Origin")]
    [SerializeField] private Vector3 originWorldPosition;
    [SerializeField] private bool useCurrentPositionAsOriginOnEnable = true;

    private bool initialized;

    public float MovementResistance => movementResistance;

    private void Reset()
    {
        targetCamera = Camera.main;
        originWorldPosition = transform.position;
    }

    private void OnEnable()
    {
        if (!initialized && useCurrentPositionAsOriginOnEnable)
        {
            originWorldPosition = transform.position;
            initialized = true;
        }

        ResolveCamera();
        ApplyParallax();
    }

    private void LateUpdate()
    {
        ResolveCamera();
        ApplyParallax();
    }

    public void Setup(Camera camera, float resistance, Vector3 originPosition)
    {
        targetCamera = camera;
        movementResistance = Mathf.Clamp01(resistance);
        originWorldPosition = originPosition;
        initialized = true;
        ApplyParallax();
    }

    public void SetMovementResistance(float resistance)
    {
        movementResistance = Mathf.Clamp01(resistance);
        ApplyParallax();
    }

    public void SetOriginWorldPosition(Vector3 originPosition)
    {
        originWorldPosition = originPosition;
        initialized = true;
        ApplyParallax();
    }

    private void ResolveCamera()
    {
        if (targetCamera != null || !autoFindCamera)
        {
            return;
        }

        targetCamera = Camera.main;
    }

    private void ApplyParallax()
    {
        if (targetCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 wantedPosition = originWorldPosition + new Vector3(
            cameraPosition.x * movementResistance,
            cameraPosition.y * movementResistance,
            0f
        );

        if (lockZ)
        {
            wantedPosition.z = originWorldPosition.z;
        }

        transform.position = wantedPosition;
    }
}
