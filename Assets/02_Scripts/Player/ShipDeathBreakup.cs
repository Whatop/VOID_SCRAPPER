using UnityEngine;

[DisallowMultipleComponent]
public class ShipDeathBreakup : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("평상시 보이는 기체 이미지 루트. 사망 시 비활성화됩니다.")]
    [SerializeField] private GameObject normalVisualRoot;

    [Header("Breakup Parts - 4 Pieces")]
    [Tooltip("4등분 파츠 Rigidbody2D. 보통 비활성화 상태의 자식 오브젝트로 둡니다.")]
    [SerializeField] private Rigidbody2D[] parts = new Rigidbody2D[4];

    [Header("Break Direction")]
    [SerializeField]
    private Vector2[] localDirections =
    {
        new Vector2(-1f, 1f),
        new Vector2(1f, 1f),
        new Vector2(-1f, -1f),
        new Vector2(1f, -1f)
    };

    [Header("Physics")]
    [SerializeField] private float minSpeed = 2.5f;
    [SerializeField] private float maxSpeed = 5.0f;
    [SerializeField] private float minAngularSpeed = 180f;
    [SerializeField] private float maxAngularSpeed = 540f;
    [SerializeField] private float linearDamping = 1.5f;
    [SerializeField] private float angularDamping = 1.5f;

    [Header("Lifetime")]
    [SerializeField] private bool destroyPartsAfterLifetime = true;
    [SerializeField] private float partLifetime = 2.5f;

    private bool broken;

    public void Break()
    {
        if (broken)
        {
            return;
        }

        broken = true;

        if (normalVisualRoot != null)
        {
            normalVisualRoot.SetActive(false);
        }

        for (int i = 0; i < parts.Length; i++)
        {
            Rigidbody2D part = parts[i];

            if (part == null)
            {
                continue;
            }

            Vector2 direction = GetBreakDirection(i);
            ActivatePart(part, direction);
        }
    }

    private Vector2 GetBreakDirection(int index)
    {
        Vector2 localDirection = Vector2.up;

        if (localDirections != null && index >= 0 && index < localDirections.Length)
        {
            localDirection = localDirections[index];
        }

        if (localDirection.sqrMagnitude <= 0.001f)
        {
            localDirection = Random.insideUnitCircle;
        }

        Vector2 worldDirection = transform.TransformDirection(localDirection.normalized);

        if (worldDirection.sqrMagnitude <= 0.001f)
        {
            worldDirection = Random.insideUnitCircle.normalized;
        }

        return worldDirection.normalized;
    }

    private void ActivatePart(Rigidbody2D part, Vector2 direction)
    {
        part.transform.SetParent(null, true);
        part.gameObject.SetActive(true);

        part.simulated = true;
        part.bodyType = RigidbodyType2D.Dynamic;
        part.gravityScale = 0f;
        part.linearDamping = linearDamping;
        part.angularDamping = angularDamping;

        float speed = Random.Range(minSpeed, maxSpeed);
        float angularSpeed = Random.Range(minAngularSpeed, maxAngularSpeed);

        if (Random.value < 0.5f)
        {
            angularSpeed *= -1f;
        }

        part.linearVelocity = direction * speed;
        part.angularVelocity = angularSpeed;

        if (destroyPartsAfterLifetime)
        {
            Destroy(part.gameObject, partLifetime);
        }
    }

    public void ResetBreakup()
    {
        broken = false;

        if (normalVisualRoot != null)
        {
            normalVisualRoot.SetActive(true);
        }

        if (parts == null)
        {
            return;
        }

        foreach (Rigidbody2D part in parts)
        {
            if (part == null)
            {
                continue;
            }

            part.linearVelocity = Vector2.zero;
            part.angularVelocity = 0f;
            part.simulated = false;
            part.gameObject.SetActive(false);
        }
    }
}