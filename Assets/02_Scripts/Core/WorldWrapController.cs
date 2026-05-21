using System;
using UnityEngine;

public class WorldWrapController : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private MapGenerationConfig config;
    [SerializeField] private Vector2 fallbackMapSize = new Vector2(80f, 80f);

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetTag = "Player";

    [Header("Wrap Axis")]
    [SerializeField] private bool wrapX = true;
    [SerializeField] private bool wrapY = true;

    [Header("Optional")]
    [SerializeField] private bool useRigidbodyMovePosition = true;

    private Rigidbody2D targetRb;

    public event Action<Vector2> Wrapped;

    private void Start()
    {
        ResolveTarget();
    }

    private void LateUpdate()
    {
        ResolveTarget();

        if (target == null)
        {
            return;
        }

        Vector2 mapSize = ResolveMapSize();
        Vector3 position = target.position;
        Vector3 newPosition = position;
        Vector2 delta = Vector2.zero;

        float halfWidth = mapSize.x * 0.5f;
        float halfHeight = mapSize.y * 0.5f;

        if (wrapX)
        {
            if (newPosition.x > halfWidth)
            {
                newPosition.x -= mapSize.x;
                delta.x -= mapSize.x;
            }
            else if (newPosition.x < -halfWidth)
            {
                newPosition.x += mapSize.x;
                delta.x += mapSize.x;
            }
        }

        if (wrapY)
        {
            if (newPosition.y > halfHeight)
            {
                newPosition.y -= mapSize.y;
                delta.y -= mapSize.y;
            }
            else if (newPosition.y < -halfHeight)
            {
                newPosition.y += mapSize.y;
                delta.y += mapSize.y;
            }
        }

        if (delta.sqrMagnitude <= 0.001f)
        {
            return;
        }

        if (targetRb != null && useRigidbodyMovePosition)
        {
            targetRb.position = newPosition;
        }
        else
        {
            target.position = newPosition;
        }

        Wrapped?.Invoke(delta);
    }

    private void ResolveTarget()
    {
        if (target == null && !string.IsNullOrWhiteSpace(targetTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(targetTag);

            if (found != null)
            {
                target = found.transform;
            }
        }

        if (target != null && targetRb == null)
        {
            targetRb = target.GetComponent<Rigidbody2D>();
        }
    }

    private Vector2 ResolveMapSize()
    {
        if (config != null)
        {
            return config.MapSize;
        }

        return fallbackMapSize;
    }
}