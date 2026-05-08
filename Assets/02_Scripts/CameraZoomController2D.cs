using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class CameraZoomController2D : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Zoom")]
    [SerializeField] private float baseOrthographicSize = 4.2f;
    [SerializeField] private float zoomSmoothSpeed = 10f;

    private float targetZoomMultiplier = 1f;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    private void Awake()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }

        if (cinemachineCamera != null)
        {
            baseOrthographicSize = cinemachineCamera.Lens.OrthographicSize;
        }
    }

    private void LateUpdate()
    {
        if (cinemachineCamera == null)
        {
            return;
        }

        float targetSize = baseOrthographicSize * targetZoomMultiplier;

        cinemachineCamera.Lens.OrthographicSize = Mathf.Lerp(
            cinemachineCamera.Lens.OrthographicSize,
            targetSize,
            1f - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime)
        );
    }

    public void SetZoomMultiplier(float multiplier)
    {
        targetZoomMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetZoom()
    {
        targetZoomMultiplier = 1f;
    }

    public void SetBaseOrthographicSize(float size)
    {
        baseOrthographicSize = Mathf.Max(0.1f, size);
    }
}