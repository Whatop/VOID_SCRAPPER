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

    [Header("Background Rebase")]
    [Tooltip("월드랩으로 플레이어가 순간이동할 때 배경 파라allax가 튀지 않도록 보정합니다.")]
    [SerializeField] private bool rebaseBackgroundOnWrap = true;

    [SerializeField] private SpaceBackgroundGenerator2D[] backgroundGenerators;

    [SerializeField] private bool autoFindBackgroundGenerator = true;

    private Rigidbody2D targetRb;

    public event Action<Vector2> Wrapped;

    private void Start()
    {
        ResolveTarget();
        ResolveBackgroundGenerators();
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

        RebaseBackgrounds(delta);
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

    private void ResolveBackgroundGenerators()
    {
        if (!autoFindBackgroundGenerator)
        {
            return;
        }

        if (backgroundGenerators != null && backgroundGenerators.Length > 0)
        {
            bool hasValid = false;

            for (int i = 0; i < backgroundGenerators.Length; i++)
            {
                if (backgroundGenerators[i] != null)
                {
                    hasValid = true;
                    break;
                }
            }

            if (hasValid)
            {
                return;
            }
        }

        SpaceBackgroundGenerator2D found = FindFirstObjectByType<SpaceBackgroundGenerator2D>();

        if (found != null)
        {
            backgroundGenerators = new[] { found };
        }
    }

    private void RebaseBackgrounds(Vector2 delta)
    {
        if (!rebaseBackgroundOnWrap)
        {
            return;
        }

        ResolveBackgroundGenerators();

        if (backgroundGenerators == null)
        {
            return;
        }

        for (int i = 0; i < backgroundGenerators.Length; i++)
        {
            SpaceBackgroundGenerator2D generator = backgroundGenerators[i];

            if (generator != null)
            {
                generator.RebaseAfterWorldWrap(delta);
            }
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