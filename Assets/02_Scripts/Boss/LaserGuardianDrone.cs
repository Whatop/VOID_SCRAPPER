using System.Collections;
using UnityEngine;

public enum LaserGuardianAnchor
{
    Up,
    Right
}

[DisallowMultipleComponent]
public class LaserGuardianDrone : MonoBehaviour
{
    [Header("Laser Anchor Points")]
    [SerializeField] private Transform upLaserAnchor;
    [SerializeField] private Transform rightLaserAnchor;

    [Header("Fallback Anchor")]
    [SerializeField] private bool createMissingAnchors = true;
    [SerializeField] private float autoAnchorDistance = 0.6f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("경고 펄스 때 크기만 바꿀 비주얼 루트입니다. 비우면 SpriteRenderer Transform을 사용합니다.")]
    [SerializeField] private Transform warningPulseScaleTarget;

    [Header("Runtime")]
    [SerializeField] private int runtimeIndex;

    private Coroutine warningPulseRoutine;
    private Vector3 basePulseScale;
    private Color runtimeBaseTint = Color.white;

    public int RuntimeIndex => runtimeIndex;
    public Transform UpLaserAnchor => upLaserAnchor != null ? upLaserAnchor : transform;
    public Transform RightLaserAnchor => rightLaserAnchor != null ? rightLaserAnchor : transform;
    public Vector3 UpLaserAnchorPosition => UpLaserAnchor.position;
    public Vector3 RightLaserAnchorPosition => RightLaserAnchor.position;

    private void Reset()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        TryFindAnchorsByName();
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (upLaserAnchor == null || rightLaserAnchor == null)
        {
            TryFindAnchorsByName();
        }

        if (createMissingAnchors)
        {
            CreateMissingAnchors();
        }

        if (warningPulseScaleTarget == null)
        {
            warningPulseScaleTarget = spriteRenderer != null
                ? spriteRenderer.transform
                : transform;
        }

        basePulseScale = warningPulseScaleTarget.localScale;
        runtimeBaseTint = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    private void OnDisable()
    {
        if (warningPulseRoutine != null)
        {
            StopCoroutine(warningPulseRoutine);
            warningPulseRoutine = null;
        }

        if (warningPulseScaleTarget != null)
        {
            warningPulseScaleTarget.localScale = basePulseScale == Vector3.zero
                ? Vector3.one
                : basePulseScale;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = runtimeBaseTint;
        }
    }

    public void SetRuntimeIndex(int index)
    {
        runtimeIndex = Mathf.Max(0, index);
    }

    public Vector3 GetWorldAnchorPosition(LaserGuardianAnchor anchor)
    {
        return anchor == LaserGuardianAnchor.Up
            ? UpLaserAnchorPosition
            : RightLaserAnchorPosition;
    }

    public void ApplySlotRotation(float zRotation)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    public void SetTint(Color color)
    {
        runtimeBaseTint = color;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    public void PlayWarningPulse(Color warningColor, float duration, float scaleMultiplier = 1.12f)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (warningPulseRoutine != null)
        {
            StopCoroutine(warningPulseRoutine);
        }

        warningPulseRoutine = StartCoroutine(WarningPulseRoutine(
            warningColor,
            Mathf.Max(0.05f, duration),
            Mathf.Max(1f, scaleMultiplier)
        ));
    }

    private IEnumerator WarningPulseRoutine(Color warningColor, float duration, float scaleMultiplier)
    {
        Transform scaleTarget = warningPulseScaleTarget != null
            ? warningPulseScaleTarget
            : transform;
        Vector3 startScale = basePulseScale == Vector3.zero
            ? scaleTarget.localScale
            : basePulseScale;
        Color startColor = runtimeBaseTint;
        float halfDuration = duration * 0.5f;
        float timer = 0f;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, halfDuration));
            float eased = Mathf.SmoothStep(0f, 1f, t);
            scaleTarget.localScale = Vector3.Lerp(startScale, startScale * scaleMultiplier, eased);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(startColor, warningColor, eased);
            }

            yield return null;
        }

        timer = 0f;

        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, halfDuration));
            float eased = Mathf.SmoothStep(0f, 1f, t);
            scaleTarget.localScale = Vector3.Lerp(startScale * scaleMultiplier, startScale, eased);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(warningColor, startColor, eased);
            }

            yield return null;
        }

        scaleTarget.localScale = startScale;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = startColor;
        }

        warningPulseRoutine = null;
    }

    public void SetVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    private void TryFindAnchorsByName()
    {
        if (upLaserAnchor == null)
        {
            upLaserAnchor =
                FindChildRecursive(transform, "LaserPoint_Up") ??
                FindChildRecursive(transform, "UpLaserAnchor") ??
                FindChildRecursive(transform, "Point_Up");
        }

        if (rightLaserAnchor == null)
        {
            rightLaserAnchor =
                FindChildRecursive(transform, "LaserPoint_Right") ??
                FindChildRecursive(transform, "RightLaserAnchor") ??
                FindChildRecursive(transform, "Point_Right");
        }
    }

    private void CreateMissingAnchors()
    {
        float distance = Mathf.Max(0.01f, autoAnchorDistance);

        if (upLaserAnchor == null)
        {
            GameObject anchorObject = new GameObject("LaserPoint_Up");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localPosition = Vector3.up * distance;
            upLaserAnchor = anchorObject.transform;
        }

        if (rightLaserAnchor == null)
        {
            GameObject anchorObject = new GameObject("LaserPoint_Right");
            anchorObject.transform.SetParent(transform, false);
            anchorObject.transform.localPosition = Vector3.right * distance;
            rightLaserAnchor = anchorObject.transform;
        }
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == targetName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, targetName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(UpLaserAnchorPosition, 0.12f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(RightLaserAnchorPosition, 0.12f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, UpLaserAnchorPosition);
        Gizmos.DrawLine(transform.position, RightLaserAnchorPosition);
    }
}