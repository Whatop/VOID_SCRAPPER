using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainMenuSpaceBackground : MonoBehaviour
{
    private sealed class Drifter
    {
        public RectTransform rect;
        public Vector2 position;
        public Vector2 velocity;
        public float radius;
        public float mass;
        public float rotation;
        public float angularVelocity;
    }

    private const float HalfWidth = 240f;
    private const float HalfHeight = 135f;
    public const int RequiredAuthoredStarCount = 30;
    public const int RequiredAuthoredAsteroidCount = 7;
    private const int AsteroidCount = RequiredAuthoredAsteroidCount;
    private const float AsteroidMinX = -HalfWidth - 24f;
    private const float AsteroidMaxX = HalfWidth + 24f;
    private const float AsteroidMinY = -HalfHeight - 20f;
    private const float AsteroidMaxY = HalfHeight + 20f;
    private const float AsteroidMinSpeed = 10f;
    private const float AsteroidMaxSpeed = 24f;
    private const float AsteroidMinScale = 10f;
    private const float AsteroidMaxScale = 28f;
    private const float MinAngularSpeed = -34f;
    private const float MaxAngularSpeed = 34f;
    private const float CollisionRestitution = 0.2f;
    private const float CollisionVelocityFloor = 4f;
    private const float SpawnMargin = 8f;
    private const float SpawnDirectionSpreadDegrees = 50f;
    private readonly List<Drifter> asteroidDrifters = new List<Drifter>(AsteroidCount);
    private readonly System.Random random = new System.Random();
    private const float MaxSafeSpeed = 36f;

    [SerializeField] private RectTransform[] authoredStarRects = new RectTransform[0];
    [SerializeField] private RectTransform[] authoredAsteroidRects = new RectTransform[0];
    [SerializeField] private RectTransform cursedRect;
    [SerializeField] private Image cursedImage;
    [SerializeField] private bool authoredVisuals;

    private Vector2 cursedPosition;
    private Vector2 cursedVelocity;
    private float cursedTimer;
    private bool cursedActive;
    private bool configured;
    private bool missingAuthoredVisualsLogged;

    public bool HasAuthoredVisuals =>
        authoredVisuals &&
        HasCompleteDirectChildReferences(authoredStarRects, RequiredAuthoredStarCount) &&
        HasCompleteDirectChildReferences(authoredAsteroidRects, AsteroidCount) &&
        cursedRect != null &&
        cursedRect.parent == transform &&
        cursedImage != null &&
        cursedImage.gameObject == cursedRect.gameObject;


    private bool HasCompleteDirectChildReferences(RectTransform[] references, int requiredCount)
    {
        if (references == null || references.Length != requiredCount)
        {
            return false;
        }

        for (int i = 0; i < references.Length; i++)
        {
            RectTransform current = references[i];
            if (current == null || current.parent != transform)
            {
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (references[j] == current)
                {
                    return false;
                }
            }
        }

        return true;
    }


    public bool InitializeAuthoredVisuals()
    {
        if (configured)
        {
            return true;
        }

        if (!HasAuthoredVisuals)
        {
            if (!missingAuthoredVisualsLogged)
            {
                missingAuthoredVisualsLogged = true;
                Debug.LogError(
                    $"[{nameof(MainMenuSpaceBackground)}] '{name}' is missing serialized " +
                    "background visuals. Restore MainMenuSpaceBackground's authored visual references in the Boot Inspector.",
                    this);
            }

            enabled = false;
            return false;
        }

        asteroidDrifters.Clear();
        for (int i = 0; i < authoredAsteroidRects.Length; i++)
        {
            RectTransform rect = authoredAsteroidRects[i];
            if (rect == null)
            {
                return false;
            }

            float radius = Mathf.Max(1f, rect.sizeDelta.x * 0.34f);
            Drifter drifter = new Drifter
            {
                rect = rect,
                radius = radius,
                mass = Mathf.Max(1f, radius * radius),
                position = rect.anchoredPosition,
                rotation = rect.localEulerAngles.z,
                angularVelocity = NextFloat(MinAngularSpeed, MaxAngularSpeed)
            };
            RandomizeAsteroidVelocity(
                drifter,
                new Vector2(NextFloat(-1f, 1f), NextFloat(-1f, 1f)).normalized);
            asteroidDrifters.Add(drifter);
        }

        configured = true;
        cursedRect.gameObject.SetActive(false);
        cursedActive = false;
        ScheduleCursedPasser();
        return true;
    }


    private void LateUpdate()
    {
        if (!configured)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        UpdateAsteroids(deltaTime);

        UpdateCursedPasser(deltaTime);
    }

    private void UpdateAsteroids(float deltaTime)
    {
        for (int i = 0; i < asteroidDrifters.Count; i++)
        {
            Drifter drifter = asteroidDrifters[i];
            drifter.position += drifter.velocity * deltaTime;
            drifter.rotation += drifter.angularVelocity * deltaTime;
        }

        ResolveAsteroidCollisions();

        for (int i = 0; i < asteroidDrifters.Count; i++)
        {
            Drifter drifter = asteroidDrifters[i];
            if (IsDrifterOutsideBounds(drifter))
            {
                SpawnFromEdge(drifter);
            }

            drifter.rect.anchoredPosition = drifter.position;
            drifter.rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                drifter.rotation
            );
        }
    }

    private void ResolveAsteroidCollisions()
    {
        for (int i = 0; i < asteroidDrifters.Count - 1; i++)
        {
            Drifter first = asteroidDrifters[i];
            for (int j = i + 1; j < asteroidDrifters.Count; j++)
            {
                Drifter second = asteroidDrifters[j];
                Vector2 separation = second.position - first.position;
                float minimumDistance = first.radius + second.radius;
                float distanceSquared = separation.sqrMagnitude;
                if (distanceSquared >= minimumDistance * minimumDistance)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(Mathf.Max(distanceSquared, 0.0001f));
                Vector2 normal = distanceSquared > 0.0001f
                    ? separation / distance
                    : Vector2.right;
                float inverseFirstMass = 1f / Mathf.Max(0.01f, first.mass);
                float inverseSecondMass = 1f / Mathf.Max(0.01f, second.mass);
                float inverseMassTotal = inverseFirstMass + inverseSecondMass;
                float penetration = minimumDistance - distance;

                first.position -= normal * (penetration * inverseFirstMass / inverseMassTotal);
                second.position += normal * (penetration * inverseSecondMass / inverseMassTotal);

                Vector2 relativeVelocity = second.velocity - first.velocity;
                float normalSpeed = Vector2.Dot(relativeVelocity, normal);
                if (normalSpeed < 0f)
                {
                    float impulseMagnitude = -(1f + CollisionRestitution) * normalSpeed /
                                             inverseMassTotal;
                    Vector2 impulse = normal * impulseMagnitude;
                    first.velocity -= impulse * inverseFirstMass;
                    second.velocity += impulse * inverseSecondMass;

                    float tangentSpeed = Vector2.Dot(relativeVelocity, new Vector2(-normal.y, normal.x));
                    first.angularVelocity = Mathf.Clamp(
                        first.angularVelocity - tangentSpeed * 1.1f,
                        -18f,
                        18f
                    );
                    second.angularVelocity = Mathf.Clamp(
                        second.angularVelocity + tangentSpeed * 1.1f,
                        -18f,
                        18f
                    );
                }

                first.velocity = ClampAsteroidVelocity(first.velocity);
                second.velocity = ClampAsteroidVelocity(second.velocity);
            }
        }
    }

    private bool IsDrifterOutsideBounds(Drifter drifter)
    {
        return drifter.position.x < AsteroidMinX - SpawnMargin
            || drifter.position.x > AsteroidMaxX + SpawnMargin
            || drifter.position.y < AsteroidMinY - SpawnMargin
            || drifter.position.y > AsteroidMaxY + SpawnMargin;
    }


    private void SpawnFromEdge(Drifter drifter)
    {
        int edge = random.Next(0, 4);
        float radius = drifter.radius;

        for (int attempt = 0; attempt < 16; attempt++)
        {
            Vector2 position = Vector2.zero;
            Vector2 direction = Vector2.zero;
            float angleDegrees;

            switch (edge)
            {
                case 0: // Left
                    position = new Vector2(
                        AsteroidMinX - radius,
                        NextFloat(-HalfHeight + 8f, HalfHeight - 8f)
                    );
                    angleDegrees = NextFloat(-SpawnDirectionSpreadDegrees, SpawnDirectionSpreadDegrees);
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                case 1: // Right
                    position = new Vector2(
                        AsteroidMaxX + radius,
                        NextFloat(-HalfHeight + 8f, HalfHeight - 8f)
                    );
                    angleDegrees = 180f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                case 2: // Bottom
                    position = new Vector2(
                        NextFloat(-HalfWidth + 8f, HalfWidth - 8f),
                        AsteroidMinY - radius
                    );
                    angleDegrees = 90f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
                default: // Top
                    position = new Vector2(
                        NextFloat(-HalfWidth + 8f, HalfWidth - 8f),
                        AsteroidMaxY + radius
                    );
                    angleDegrees = -90f + NextFloat(
                        -SpawnDirectionSpreadDegrees,
                        SpawnDirectionSpreadDegrees
                    );
                    direction = new Vector2(
                        Mathf.Cos(angleDegrees * Mathf.Deg2Rad),
                        Mathf.Sin(angleDegrees * Mathf.Deg2Rad)
                    );
                    break;
            }

            float edgeBias = random.Next(0, 2) == 0 ? 0.8f : 1f;
            RandomizeAsteroidVelocity(drifter, direction.normalized);

            bool overlaps = false;
            for (int i = 0; i < asteroidDrifters.Count; i++)
            {
                if (asteroidDrifters[i] == drifter)
                {
                    continue;
                }

                Drifter other = asteroidDrifters[i];
                float minimumDistance = radius + other.radius + 2f;
                if ((position - other.position).sqrMagnitude < minimumDistance * minimumDistance)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                drifter.position = position;
                drifter.velocity *= edgeBias;
                drifter.velocity = ClampAsteroidVelocity(drifter.velocity);
                return;
            }

            edge = random.Next(0, 4);
        }

        drifter.position = new Vector2(
            NextFloat(AsteroidMinX, AsteroidMaxX),
            NextFloat(AsteroidMinY, AsteroidMaxY)
        );
        RandomizeAsteroidVelocity(
            drifter,
            new Vector2(NextFloat(-1f, 1f), NextFloat(-1f, 1f))
        );
        drifter.velocity = ClampAsteroidVelocity(drifter.velocity);
    }

    private void RandomizeAsteroidVelocity(Drifter drifter, Vector2 direction)
    {
        float sizeRatio = 0f;
        if (drifter.radius > 0f)
        {
            sizeRatio = Mathf.Clamp01(
                (drifter.radius * 2f - AsteroidMinScale) / (AsteroidMaxScale - AsteroidMinScale)
            );
        }

        float speed = NextFloat(AsteroidMinSpeed, AsteroidMaxSpeed);
        speed = Mathf.Lerp(speed, AsteroidMinSpeed + 2f, sizeRatio * 0.5f);
        Vector2 tangent = new Vector2(-direction.y, direction.x);
        float drift = NextFloat(-0.08f, 0.08f);
        drifter.velocity = direction.normalized * speed + tangent * drift;
        drifter.angularVelocity = NextFloat(MinAngularSpeed, MaxAngularSpeed);
    }

    private static Vector2 ClampAsteroidVelocity(Vector2 velocity)
    {
        float speed = velocity.magnitude;
        if (speed > MaxSafeSpeed)
        {
            return velocity * (MaxSafeSpeed / speed);
        }

        if (speed < AsteroidMinSpeed && speed > 0.001f)
        {
            return velocity * (CollisionVelocityFloor / speed);
        }

        return velocity;
    }


    private void UpdateCursedPasser(float deltaTime)
    {
        if (cursedRect == null)
        {
            return;
        }

        if (!cursedActive)
        {
            cursedTimer -= deltaTime;

            if (cursedTimer <= 0f)
            {
                cursedActive = true;
                bool enterFromLeft = random.Next(0, 2) == 0;
                cursedPosition = new Vector2(
                    enterFromLeft ? -HalfWidth - 14f : HalfWidth + 14f,
                    NextFloat(-HalfHeight + 22f, HalfHeight - 22f)
                );
                float horizontalSpeed = NextFloat(42f, 55f) * (enterFromLeft ? 1f : -1f);
                cursedVelocity = new Vector2(horizontalSpeed, NextFloat(-1.5f, 1.5f));
                cursedRect.gameObject.SetActive(true);
            }

            return;
        }

        cursedPosition += cursedVelocity * deltaTime;
        cursedRect.anchoredPosition = RoundToLogicalPixel(cursedPosition);

        if (cursedImage != null)
        {
            Color color = cursedImage.color;
            color.a = 0.34f + Mathf.PingPong(Time.unscaledTime * 2.4f, 0.14f);
            cursedImage.color = color;
        }

        if ((cursedVelocity.x > 0f && cursedPosition.x > HalfWidth + 16f) ||
            (cursedVelocity.x < 0f && cursedPosition.x < -HalfWidth - 16f))
        {
            cursedActive = false;
            cursedRect.gameObject.SetActive(false);
            ScheduleCursedPasser();
        }
    }

    private void ScheduleCursedPasser()
    {
        cursedTimer = NextFloat(12f, 22f);
    }

    private float NextFloat(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }

    private static Vector2 RoundToLogicalPixel(Vector2 position)
    {
        return new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
    }

}
