using UnityEngine;

[DisallowMultipleComponent]
public class InteractionPromptAnchor : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private Transform anchorTransform;

    [Tooltip("Anchor Transform 기준 추가 월드 오프셋")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    public Transform AnchorTransform => anchorTransform != null ? anchorTransform : transform;
    public Vector3 WorldOffset => worldOffset;

    private void Reset()
    {
        anchorTransform = transform;
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = AnchorTransform;
        if (target == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(target.position + worldOffset, 0.08f);
    }
}