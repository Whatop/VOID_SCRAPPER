using UnityEngine;

public enum CombatFeedbackKind
{
    Player,
    Enemy,
    Boss,
    Harvest,
    Meteor,
    Shield,
    Structure
}

[DefaultExecutionOrder(-250)]
[DisallowMultipleComponent]
public sealed class CombatFeedbackManager : MonoBehaviour
{
    public static CombatFeedbackManager Instance { get; private set; }

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Procedural Hit Sparks")]
    [SerializeField] private bool enableProceduralHitSparks = true;
    [SerializeField] private int minSparkCount = 4;
    [SerializeField] private int maxSparkCount = 7;
    [SerializeField] private float minSparkSpeed = 1.8f;
    [SerializeField] private float maxSparkSpeed = 4.2f;
    [SerializeField] private float sparkSpreadAngle = 75f;
    [SerializeField] private float minSparkLifetime = 0.07f;
    [SerializeField] private float maxSparkLifetime = 0.16f;
    [SerializeField] private float minSparkSize = 0.025f;
    [SerializeField] private float maxSparkSize = 0.065f;
    [SerializeField] private float flashLifetime = 0.055f;
    [SerializeField] private float flashSize = 0.16f;

    [Header("Break Burst")]
    [SerializeField] private int breakSparkCount = 14;
    [SerializeField] private float breakMinSpeed = 2.2f;
    [SerializeField] private float breakMaxSpeed = 5.8f;
    [SerializeField] private float breakFlashSize = 0.3f;

    [Header("Colors")]
    [SerializeField] private Color playerColor = new Color(0.35f, 0.9f, 1f, 1f);
    [SerializeField] private Color enemyColor = new Color(1f, 0.52f, 0.16f, 1f);
    [SerializeField] private Color bossColor = new Color(1f, 0.18f, 0.5f, 1f);
    [SerializeField] private Color harvestColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] private Color meteorColor = new Color(0.75f, 0.82f, 0.9f, 1f);
    [SerializeField] private Color shieldColor = new Color(0.25f, 0.7f, 1f, 1f);
    [SerializeField] private Color structureColor = new Color(0.75f, 0.35f, 1f, 1f);

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 180;

    private ParticleSystem sparkSystem;
    private ParticleSystem flashSystem;
    private Material runtimeMaterial;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureParticleSystems();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    public static void PlayHit(
        Vector2 position,
        Vector2 incomingDirection,
        CombatFeedbackKind kind,
        float intensity = 1f,
        float shakeAmplitude = 0f,
        float shakeDuration = 0f,
        bool spawnProceduralVfx = true)
    {
        intensity = Mathf.Clamp(intensity, 0.15f, 3f);

        if (spawnProceduralVfx && Application.isPlaying)
        {
            CombatFeedbackManager manager = EnsureInstance();

            if (manager != null)
            {
                manager.EmitHit(position, incomingDirection, kind, intensity);
            }
        }

        if (shakeAmplitude > 0f && shakeDuration > 0f)
        {
            GungeonStyleCamera2D.RequestShake(shakeAmplitude, shakeDuration);
        }
    }

    public static void PlayBreak(
        Vector2 position,
        CombatFeedbackKind kind,
        float intensity = 1f,
        float shakeAmplitude = 0f,
        float shakeDuration = 0f,
        bool spawnProceduralVfx = true)
    {
        intensity = Mathf.Clamp(intensity, 0.2f, 3f);

        if (spawnProceduralVfx && Application.isPlaying)
        {
            CombatFeedbackManager manager = EnsureInstance();

            if (manager != null)
            {
                manager.EmitBreak(position, kind, intensity);
            }
        }

        if (shakeAmplitude > 0f && shakeDuration > 0f)
        {
            GungeonStyleCamera2D.RequestShake(shakeAmplitude, shakeDuration);
        }
    }

    private static CombatFeedbackManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindFirstObjectByType<CombatFeedbackManager>();

        if (Instance != null)
        {
            Instance.EnsureParticleSystems();
            return Instance;
        }

        if (!Application.isPlaying)
        {
            return null;
        }

        GameObject managerObject = new GameObject("[Combat Feedback Manager]");
        Instance = managerObject.AddComponent<CombatFeedbackManager>();
        return Instance;
    }

    private void EnsureParticleSystems()
    {
        if (!enableProceduralHitSparks)
        {
            return;
        }

        if (sparkSystem == null)
        {
            sparkSystem = CreateParticleSystem("Hit Sparks", true, sortingOrder);
        }

        if (flashSystem == null)
        {
            flashSystem = CreateParticleSystem("Hit Flash", false, sortingOrder + 1);
        }
    }

    private ParticleSystem CreateParticleSystem(string objectName, bool stretch, int targetSortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);

        ParticleSystem system = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 512;
        main.startLifetime = 0.1f;
        main.startSpeed = 0f;
        main.startSize = 0.05f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.85f),
                new Keyframe(1f, 0.1f)
            )
        );

        ParticleSystemRenderer targetRenderer = system.GetComponent<ParticleSystemRenderer>();
        targetRenderer.renderMode = stretch
            ? ParticleSystemRenderMode.Stretch
            : ParticleSystemRenderMode.Billboard;
        targetRenderer.velocityScale = stretch ? 0.18f : 0f;
        targetRenderer.lengthScale = stretch ? 1.8f : 1f;
        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = targetSortingOrder;

        Material material = GetRuntimeMaterial();
        if (material != null)
        {
            targetRenderer.material = material;
        }

        system.Play(false);
        return system;
    }

    private Material GetRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeMaterial = new Material(shader)
        {
            name = "Runtime Combat Feedback Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        return runtimeMaterial;
    }

    private void EmitHit(Vector2 position, Vector2 incomingDirection, CombatFeedbackKind kind, float intensity)
    {
        if (!enableProceduralHitSparks)
        {
            return;
        }

        EnsureParticleSystems();

        if (sparkSystem == null || flashSystem == null)
        {
            return;
        }

        Color baseColor = ResolveColor(kind);
        Vector2 reboundDirection = ResolveReboundDirection(incomingDirection);

        int baseCount = Random.Range(
            Mathf.Max(1, minSparkCount),
            Mathf.Max(minSparkCount + 1, maxSparkCount + 1)
        );
        int count = Mathf.Clamp(Mathf.RoundToInt(baseCount * Mathf.Sqrt(intensity)), 2, 20);

        if (!sparkSystem.isPlaying)
        {
            sparkSystem.Play(false);
        }

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-sparkSpreadAngle, sparkSpreadAngle);
            Vector2 direction = Rotate(reboundDirection, angle);
            float speed = Random.Range(minSparkSpeed, maxSparkSpeed) * Mathf.Sqrt(intensity);

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = direction * speed,
                startLifetime = Random.Range(minSparkLifetime, maxSparkLifetime),
                startSize = Random.Range(minSparkSize, maxSparkSize) * Mathf.Lerp(0.85f, 1.4f, intensity / 3f),
                startColor = Color.Lerp(baseColor, Color.white, Random.Range(0.05f, 0.55f))
            };

            sparkSystem.Emit(emit, 1);
        }

        EmitFlash(position, baseColor, flashSize * Mathf.Lerp(0.8f, 1.55f, intensity / 3f));
    }

    private void EmitBreak(Vector2 position, CombatFeedbackKind kind, float intensity)
    {
        if (!enableProceduralHitSparks)
        {
            return;
        }

        EnsureParticleSystems();

        if (sparkSystem == null || flashSystem == null)
        {
            return;
        }

        Color baseColor = ResolveColor(kind);
        int count = Mathf.Clamp(Mathf.RoundToInt(breakSparkCount * intensity), 6, 42);

        if (!sparkSystem.isPlaying)
        {
            sparkSystem.Play(false);
        }

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle;

            if (direction.sqrMagnitude <= 0.001f)
            {
                direction = Vector2.up;
            }

            direction.Normalize();

            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = direction * Random.Range(breakMinSpeed, breakMaxSpeed) * Mathf.Sqrt(intensity),
                startLifetime = Random.Range(minSparkLifetime * 1.25f, maxSparkLifetime * 1.8f),
                startSize = Random.Range(minSparkSize * 1.15f, maxSparkSize * 1.7f) * Mathf.Sqrt(intensity),
                startColor = Color.Lerp(baseColor, Color.white, Random.Range(0.05f, 0.65f))
            };

            sparkSystem.Emit(emit, 1);
        }

        EmitFlash(position, baseColor, breakFlashSize * Mathf.Sqrt(intensity));
    }

    private void EmitFlash(Vector2 position, Color color, float size)
    {
        if (flashSystem == null)
        {
            return;
        }

        if (!flashSystem.isPlaying)
        {
            flashSystem.Play(false);
        }

        ParticleSystem.EmitParams flash = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = Vector3.zero,
            startLifetime = Mathf.Max(0.01f, flashLifetime),
            startSize = Mathf.Max(0.01f, size),
            startColor = Color.Lerp(color, Color.white, 0.45f)
        };

        flashSystem.Emit(flash, 1);
    }

    private Color ResolveColor(CombatFeedbackKind kind)
    {
        switch (kind)
        {
            case CombatFeedbackKind.Player:
                return playerColor;

            case CombatFeedbackKind.Enemy:
                return enemyColor;

            case CombatFeedbackKind.Boss:
                return bossColor;

            case CombatFeedbackKind.Harvest:
                return harvestColor;

            case CombatFeedbackKind.Meteor:
                return meteorColor;

            case CombatFeedbackKind.Shield:
                return shieldColor;

            case CombatFeedbackKind.Structure:
                return structureColor;

            default:
                return Color.white;
        }
    }

    private static Vector2 ResolveReboundDirection(Vector2 incomingDirection)
    {
        if (incomingDirection.sqrMagnitude <= 0.001f)
        {
            Vector2 random = Random.insideUnitCircle;
            return random.sqrMagnitude > 0.001f ? random.normalized : Vector2.up;
        }

        return -incomingDirection.normalized;
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }
}
