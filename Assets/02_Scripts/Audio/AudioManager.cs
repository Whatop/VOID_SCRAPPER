using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private AudioEventDatabase database;
    [SerializeField] private string resourcesDatabasePath = "Audio/SoundEventLibrary";

    [Header("Sources")]
    [SerializeField] private int pooledSourceCount = 24;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool logMissingEvents;

    private readonly List<AudioSource> sources = new List<AudioSource>();
    private int nextSourceIndex;

    public static AudioManager EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        AudioManager existing = FindFirstObjectByType<AudioManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject root = new GameObject("AudioManager");
        Instance = root.AddComponent<AudioManager>();
        return Instance;
    }

    public static bool Play(string eventId, float volumeScale = 1f)
    {
        return EnsureExists().PlayEvent(eventId, null, volumeScale);
    }

    public static bool PlayAt(string eventId, Vector3 position, float volumeScale = 1f)
    {
        return EnsureExists().PlayEvent(eventId, position, volumeScale);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadDatabaseIfNeeded();
        BuildSourcePool();
    }

    private void LoadDatabaseIfNeeded()
    {
        if (database != null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(resourcesDatabasePath))
        {
            database = Resources.Load<AudioEventDatabase>(resourcesDatabasePath);
        }

        if (database != null)
        {
            return;
        }

        string[] fallbackPaths =
        {
            "Audio/SoundEventLibrary",
            "Audio/AudioEventDatabase",
            "SoundEventLibrary",
            "AudioEventDatabase"
        };

        for (int i = 0; i < fallbackPaths.Length; i++)
        {
            string path = fallbackPaths[i];

            if (string.IsNullOrWhiteSpace(path) || path == resourcesDatabasePath)
            {
                continue;
            }

            database = Resources.Load<AudioEventDatabase>(path);

            if (database != null)
            {
                resourcesDatabasePath = path;
                return;
            }
        }
    }

    private void BuildSourcePool()
    {
        if (sources.Count > 0)
        {
            return;
        }

        int count = Mathf.Max(1, pooledSourceCount);

        for (int i = 0; i < count; i++)
        {
            GameObject sourceObject = new GameObject($"AudioSource_{i:00}");
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.priority = 128;

            sources.Add(source);
        }
    }

    private bool PlayEvent(string eventId, Vector3? position, float volumeScale)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        LoadDatabaseIfNeeded();
        BuildSourcePool();

        if (database == null || !database.TryGet(eventId, out AudioEventDefinition definition) || definition == null)
        {
            if (logMissingEvents)
            {
                Debug.LogWarning($"AudioEvent not found: {eventId}", this);
            }

            return false;
        }

        AudioClip clip = definition.GetRandomClip();

        if (clip == null)
        {
            if (logMissingEvents)
            {
                Debug.LogWarning($"AudioEvent has no clip: {eventId}", this);
            }

            return false;
        }

        AudioSource source = GetAvailableSource();

        if (source == null)
        {
            return false;
        }

        if (position.HasValue)
        {
            source.transform.position = position.Value;
        }
        else
        {
            source.transform.localPosition = Vector3.zero;
        }

        float pitchMin = Mathf.Min(definition.PitchMin, definition.PitchMax);
        float pitchMax = Mathf.Max(definition.PitchMin, definition.PitchMax);

        source.clip = clip;
        source.volume = Mathf.Clamp01(definition.Volume * masterVolume * sfxVolume * Mathf.Max(0f, volumeScale));
        source.pitch = Mathf.Approximately(pitchMin, pitchMax)
            ? pitchMin
            : UnityEngine.Random.Range(pitchMin, pitchMax);
        source.spatialBlend = definition.SpatialBlend;
        source.loop = definition.Loop;
        source.priority = definition.Priority;
        source.Play();

        return true;
    }

    private AudioSource GetAvailableSource()
    {
        if (sources.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < sources.Count; i++)
        {
            int index = (nextSourceIndex + i) % sources.Count;
            AudioSource source = sources[index];

            if (source != null && !source.isPlaying)
            {
                nextSourceIndex = (index + 1) % sources.Count;
                return source;
            }
        }

        AudioSource fallback = sources[nextSourceIndex % sources.Count];
        nextSourceIndex = (nextSourceIndex + 1) % sources.Count;
        return fallback;
    }
}
