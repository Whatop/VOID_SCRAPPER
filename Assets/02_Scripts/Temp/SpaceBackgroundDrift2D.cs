using UnityEngine;

[DisallowMultipleComponent]
public class SpaceBackgroundDrift2D : MonoBehaviour
{
    [SerializeField] private Vector2 direction = Vector2.right;
    [SerializeField] private float distance = 0.1f;
    [SerializeField] private float speed = 0.08f;
    [SerializeField] private float phase;

    private Vector3 startLocalPosition;
    private bool initialized;

    public void Setup(Vector2 moveDirection, float moveDistance, float moveSpeed, float movePhase)
    {
        direction = moveDirection.sqrMagnitude <= 0.001f
            ? Vector2.right
            : moveDirection.normalized;

        distance = Mathf.Max(0f, moveDistance);
        speed = Mathf.Max(0f, moveSpeed);
        phase = movePhase;

        startLocalPosition = transform.localPosition;
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
        {
            startLocalPosition = transform.localPosition;
            initialized = true;
        }
    }

    private void Update()
    {
        float time = Application.isPlaying
            ? Time.time
            : Time.realtimeSinceStartup;

        float value = Mathf.Sin((time * speed) + phase);

        Vector3 offset = new Vector3(direction.x, direction.y, 0f) * distance * value;
        transform.localPosition = startLocalPosition + offset;
    }
}