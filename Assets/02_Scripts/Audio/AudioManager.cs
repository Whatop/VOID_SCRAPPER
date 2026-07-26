using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private sealed class PooledVoice
    {
        public AudioSource source;
        public AudioLowPassFilter lowPass;
        public string eventId;
        public int priority = 256;
        public float startedAt;
        public bool isWorld;
        public Vector2 worldPosition;
        public float effectiveVolume;

        public bool IsPlaying => source != null && source.isPlaying;

        public void ClearRuntime()
        {
            eventId = null;
            priority = 256;
            startedAt = 0f;
            isWorld = false;
            worldPosition = Vector2.zero;
            effectiveVolume = 0f;
        }
    }

    private struct PlaybackProfile
    {
        public bool isWorld;
        public float spatialBlend;
        public float minDistance;
        public float maxDistance;
        public AudioRolloffMode rolloffMode;
        public float dopplerLevel;
        public bool hardCull;
        public bool useOcclusion;
        public float oneOccluderVolume;
        public float multipleOccluderVolume;
        public float oneOccluderCutoff;
        public float multipleOccluderCutoff;
        public int maxVoices;
        public int priority;
        public float retriggerInterval;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private AudioEventDatabase database;
    [SerializeField] private string resourcesDatabasePath = "Audio/SoundEventLibrary";

    [Header("Sources")]
    [SerializeField] private int pooledSourceCount = 24;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float loopVolume = 1f;

    [Header("World Audio Defaults")]
    [SerializeField] private float defaultWorldMinDistance = 1.5f;
    [SerializeField] private float defaultWorldMaxDistance = 14f;
    [SerializeField] private bool hardCullWorldAudio = true;
    [SerializeField] private bool useWorldOcclusion = true;
    [SerializeField] private LayerMask audioOccluderMask;
    [SerializeField] private bool autoResolveAudioOccluderLayer = true;
    [SerializeField, Range(0f, 1f)] private float oneOccluderVolumeMultiplier = 0.6f;
    [SerializeField, Range(0f, 1f)] private float multipleOccluderVolumeMultiplier = 0.3f;
    [SerializeField, Range(10f, 22000f)] private float oneOccluderLowPassCutoff = 3500f;
    [SerializeField, Range(10f, 22000f)] private float multipleOccluderLowPassCutoff = 1500f;

    [Header("Duplicate Guard")]
    [SerializeField] private float sameEventMinimumInterval = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool logMissingEvents;
    [SerializeField] private bool logDistanceCulling;

    private readonly List<PooledVoice> voices = new List<PooledVoice>();
    private readonly Dictionary<string, AudioSource> loopSources = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, AudioLowPassFilter> loopFilters = new Dictionary<string, AudioLowPassFilter>();
    private readonly Dictionary<string, string> loopEventIds = new Dictionary<string, string>();
    private readonly Dictionary<string, float> loopBaseVolumes = new Dictionary<string, float>();
    private readonly Dictionary<string, float> loopBasePitches = new Dictionary<string, float>();
    private readonly Dictionary<string, float> lastOneShotTimes = new Dictionary<string, float>();
    private readonly RaycastHit2D[] occlusionHits = new RaycastHit2D[12];
    private readonly HashSet<int> occlusionColliderIds = new HashSet<int>();

    private AudioListener cachedListener;
    private Transform cachedListenerTransform;
    private float nextListenerSearchTime;
    private int nextVoiceIndex;

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

    public static bool PlayLoop(string eventId, string channelName, float volumeScale = 1f)
    {
        return EnsureExists().PlayLoopEvent(eventId, channelName, volumeScale);
    }

    public static void StopLoop(string channelName)
    {
        EnsureExists().StopLoopEvent(channelName);
    }

    public static bool SetLoopModulation(
        string channelName,
        float pitchMultiplier = 1f,
        float volumeMultiplier = 1f)
    {
        return EnsureExists().SetLoopModulationEvent(
            channelName,
            pitchMultiplier,
            volumeMultiplier
        );
    }

    public static void StopAllLoops()
    {
        EnsureExists().StopEveryLoop();
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
        ResolveListener(true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedListener = null;
        cachedListenerTransform = null;
        nextListenerSearchTime = 0f;
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
        if (voices.Count > 0)
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
            source.dopplerLevel = 0f;

            AudioLowPassFilter lowPass = sourceObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            lowPass.enabled = false;

            voices.Add(new PooledVoice
            {
                source = source,
                lowPass = lowPass
            });
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

        PlaybackProfile profile = ResolvePlaybackProfile(eventId, definition, position.HasValue);
        Vector2 worldPosition = position.HasValue ? (Vector2)position.Value : Vector2.zero;
        Transform listener = ResolveListener(false);
        float distanceToListener = 0f;

        if (profile.isWorld && listener != null)
        {
            distanceToListener = Vector2.Distance(worldPosition, listener.position);

            if (profile.hardCull && distanceToListener > profile.maxDistance)
            {
                if (logDistanceCulling)
                {
                    Debug.Log($"Audio culled by distance: {eventId}, {distanceToListener:0.0}/{profile.maxDistance:0.0}", this);
                }

                return false;
            }
        }

        if (IsBlockedByDuplicateGuard(eventId, profile.retriggerInterval))
        {
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

        PooledVoice voice = GetAvailableVoice(eventId, profile, worldPosition, listener);

        if (voice == null || voice.source == null)
        {
            return false;
        }

        int occluderCount = 0;

        if (profile.isWorld && profile.useOcclusion && listener != null)
        {
            occluderCount = CountOccluders(worldPosition, listener.position);
        }

        float occlusionVolume = ResolveOcclusionVolume(profile, occluderCount);
        float finalVolume = definition.Volume * masterVolume * sfxVolume * Mathf.Max(0f, volumeScale) * occlusionVolume;

        PrepareVoiceForReuse(voice);
        SetSourcePosition(voice.source, profile.isWorld, position, listener);
        ApplyDefinitionToSource(voice.source, voice.lowPass, definition, clip, finalVolume, profile, occluderCount);

        voice.eventId = eventId;
        voice.priority = profile.priority;
        voice.startedAt = Time.unscaledTime;
        voice.isWorld = profile.isWorld;
        voice.worldPosition = worldPosition;
        voice.effectiveVolume = Mathf.Clamp01(finalVolume);

        voice.source.loop = false;
        voice.source.Play();
        return true;
    }

    private bool PlayLoopEvent(string eventId, string channelName, float volumeScale)
    {
        if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(channelName))
        {
            return false;
        }

        LoadDatabaseIfNeeded();

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

        AudioSource source = GetLoopSource(channelName);

        if (source == null)
        {
            return false;
        }

        if (loopEventIds.TryGetValue(channelName, out string currentEventId) &&
            currentEventId == eventId &&
            source.isPlaying)
        {
            return true;
        }

        source.Stop();
        AudioLowPassFilter lowPass = loopFilters.TryGetValue(channelName, out AudioLowPassFilter filter)
            ? filter
            : null;

        PlaybackProfile profile = ResolvePlaybackProfile(eventId, definition, false);
        profile.isWorld = false;
        profile.spatialBlend = 0f;

        ApplyDefinitionToSource(
            source,
            lowPass,
            definition,
            clip,
            definition.Volume * masterVolume * loopVolume * Mathf.Max(0f, volumeScale),
            profile,
            0
        );

        source.transform.localPosition = Vector3.zero;
        source.loop = true;
        source.Play();
        loopEventIds[channelName] = eventId;
        loopBaseVolumes[channelName] = source.volume;
        loopBasePitches[channelName] = source.pitch;
        return true;
    }

    private bool SetLoopModulationEvent(
        string channelName,
        float pitchMultiplier,
        float volumeMultiplier)
    {
        if (string.IsNullOrWhiteSpace(channelName) ||
            !loopSources.TryGetValue(channelName, out AudioSource source) ||
            source == null ||
            !source.isPlaying)
        {
            return false;
        }

        float basePitch = loopBasePitches.TryGetValue(channelName, out float storedPitch)
            ? storedPitch
            : source.pitch;
        float baseVolume = loopBaseVolumes.TryGetValue(channelName, out float storedVolume)
            ? storedVolume
            : source.volume;

        source.pitch = Mathf.Clamp(basePitch * Mathf.Max(0.01f, pitchMultiplier), -3f, 3f);
        source.volume = Mathf.Clamp01(baseVolume * Mathf.Max(0f, volumeMultiplier));
        return true;
    }

    private void StopLoopEvent(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            return;
        }

        if (loopSources.TryGetValue(channelName, out AudioSource source) && source != null)
        {
            source.Stop();
            source.clip = null;
        }

        loopEventIds.Remove(channelName);
        loopBaseVolumes.Remove(channelName);
        loopBasePitches.Remove(channelName);
    }

    private void StopEveryLoop()
    {
        foreach (KeyValuePair<string, AudioSource> pair in loopSources)
        {
            if (pair.Value != null)
            {
                pair.Value.Stop();
                pair.Value.clip = null;
            }
        }

        loopEventIds.Clear();
        loopBaseVolumes.Clear();
        loopBasePitches.Clear();
    }

    private void ApplyDefinitionToSource(
        AudioSource source,
        AudioLowPassFilter lowPass,
        AudioEventDefinition definition,
        AudioClip clip,
        float volume,
        PlaybackProfile profile,
        int occluderCount)
    {
        float pitchMin = Mathf.Min(definition.PitchMin, definition.PitchMax);
        float pitchMax = Mathf.Max(definition.PitchMin, definition.PitchMax);

        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Approximately(pitchMin, pitchMax)
            ? pitchMin
            : UnityEngine.Random.Range(pitchMin, pitchMax);
        source.spatialBlend = profile.isWorld ? profile.spatialBlend : 0f;
        source.priority = profile.priority;
        source.rolloffMode = profile.rolloffMode;
        source.minDistance = Mathf.Max(0.01f, profile.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.01f, profile.maxDistance);
        source.dopplerLevel = profile.isWorld ? profile.dopplerLevel : 0f;
        source.panStereo = 0f;
        source.spread = 0f;

        ApplyLowPass(lowPass, profile, occluderCount);
    }

    private void ApplyLowPass(AudioLowPassFilter lowPass, PlaybackProfile profile, int occluderCount)
    {
        if (lowPass == null)
        {
            return;
        }

        if (!profile.isWorld || !profile.useOcclusion || occluderCount <= 0)
        {
            lowPass.cutoffFrequency = 22000f;
            lowPass.enabled = false;
            return;
        }

        lowPass.enabled = true;
        lowPass.cutoffFrequency = occluderCount >= 2
            ? profile.multipleOccluderCutoff
            : profile.oneOccluderCutoff;
    }

    private PlaybackProfile ResolvePlaybackProfile(
        string eventId,
        AudioEventDefinition definition,
        bool hasWorldPosition)
    {
        bool isWorld = ResolveIsWorld(eventId, definition, hasWorldPosition);
        bool autoMode = definition.SpatialMode == AudioSpatialMode.Auto;

        float minDistance = autoMode
            ? ResolveAutomaticMinDistance(eventId)
            : definition.MinDistance;

        float maxDistance = autoMode
            ? ResolveAutomaticMaxDistance(eventId)
            : definition.MaxDistance;

        if (minDistance <= 0f)
        {
            minDistance = defaultWorldMinDistance;
        }

        if (maxDistance <= minDistance)
        {
            maxDistance = Mathf.Max(minDistance + 0.1f, defaultWorldMaxDistance);
        }

        float oneVolume = autoMode || definition.OneOccluderVolumeMultiplier <= 0f
            ? oneOccluderVolumeMultiplier
            : definition.OneOccluderVolumeMultiplier;

        float multipleVolume = autoMode || definition.MultipleOccluderVolumeMultiplier <= 0f
            ? multipleOccluderVolumeMultiplier
            : definition.MultipleOccluderVolumeMultiplier;

        float oneCutoff = autoMode || definition.OneOccluderLowPassCutoff <= 20f
            ? oneOccluderLowPassCutoff
            : definition.OneOccluderLowPassCutoff;

        float multipleCutoff = autoMode || definition.MultipleOccluderLowPassCutoff <= 20f
            ? multipleOccluderLowPassCutoff
            : definition.MultipleOccluderLowPassCutoff;

        return new PlaybackProfile
        {
            isWorld = isWorld,
            spatialBlend = isWorld
                ? (definition.SpatialBlend > 0.01f ? definition.SpatialBlend : 1f)
                : 0f,
            minDistance = minDistance,
            maxDistance = maxDistance,
            rolloffMode = autoMode ? AudioRolloffMode.Linear : definition.RolloffMode,
            dopplerLevel = autoMode ? 0f : Mathf.Max(0f, definition.DopplerLevel),
            hardCull = autoMode ? hardCullWorldAudio : definition.HardCullOutsideMaxDistance,
            useOcclusion = isWorld && (autoMode ? useWorldOcclusion : definition.UseOcclusion),
            oneOccluderVolume = Mathf.Clamp01(oneVolume),
            multipleOccluderVolume = Mathf.Clamp01(multipleVolume),
            oneOccluderCutoff = Mathf.Clamp(oneCutoff, 10f, 22000f),
            multipleOccluderCutoff = Mathf.Clamp(multipleCutoff, 10f, 22000f),
            maxVoices = definition.MaxSimultaneousVoices > 0
                ? definition.MaxSimultaneousVoices
                : ResolveAutomaticVoiceLimit(eventId),
            priority = ResolvePriority(eventId, definition.Priority),
            retriggerInterval = definition.MinimumRetriggerInterval > 0f
                ? definition.MinimumRetriggerInterval
                : ResolveAutomaticRetriggerInterval(eventId)
        };
    }

    private bool ResolveIsWorld(string eventId, AudioEventDefinition definition, bool hasWorldPosition)
    {
        if (!hasWorldPosition)
        {
            return false;
        }

        if (definition.SpatialMode == AudioSpatialMode.Force2D)
        {
            return false;
        }

        if (definition.SpatialMode == AudioSpatialMode.World2D)
        {
            return true;
        }

        return !IsAutomaticTwoDEvent(eventId);
    }

    private bool IsBlockedByDuplicateGuard(string eventId, float minimumInterval)
    {
        float interval = minimumInterval > 0f ? minimumInterval : sameEventMinimumInterval;

        if (interval <= 0f)
        {
            return false;
        }

        float now = Time.unscaledTime;

        if (lastOneShotTimes.TryGetValue(eventId, out float previousTime) && now - previousTime < interval)
        {
            return true;
        }

        lastOneShotTimes[eventId] = now;
        return false;
    }

    private PooledVoice GetAvailableVoice(
        string eventId,
        PlaybackProfile profile,
        Vector2 worldPosition,
        Transform listener)
    {
        RefreshVoiceRuntime();

        PooledVoice limitedCandidate = null;
        int sameEventCount = 0;
        float newDistanceSq = profile.isWorld && listener != null
            ? ((Vector2)listener.position - worldPosition).sqrMagnitude
            : 0f;

        for (int i = 0; i < voices.Count; i++)
        {
            PooledVoice voice = voices[i];

            if (!voice.IsPlaying || !string.Equals(voice.eventId, eventId, StringComparison.Ordinal))
            {
                continue;
            }

            sameEventCount++;

            if (limitedCandidate == null || IsWorseVoice(voice, limitedCandidate, listener))
            {
                limitedCandidate = voice;
            }
        }

        if (profile.maxVoices > 0 && sameEventCount >= profile.maxVoices)
        {
            if (limitedCandidate == null)
            {
                return null;
            }

            float candidateDistanceSq = limitedCandidate.isWorld && listener != null
                ? ((Vector2)listener.position - limitedCandidate.worldPosition).sqrMagnitude
                : 0f;

            bool newVoiceIsBetter = profile.priority < limitedCandidate.priority ||
                                    (profile.isWorld && newDistanceSq < candidateDistanceSq) ||
                                    (!profile.isWorld && profile.priority <= limitedCandidate.priority);

            if (!newVoiceIsBetter)
            {
                return null;
            }

            return limitedCandidate;
        }

        for (int i = 0; i < voices.Count; i++)
        {
            int index = (nextVoiceIndex + i) % voices.Count;
            PooledVoice voice = voices[index];

            if (!voice.IsPlaying)
            {
                nextVoiceIndex = (index + 1) % voices.Count;
                return voice;
            }
        }

        PooledVoice stealCandidate = null;

        for (int i = 0; i < voices.Count; i++)
        {
            PooledVoice voice = voices[i];

            if (stealCandidate == null || IsWorseVoice(voice, stealCandidate, listener))
            {
                stealCandidate = voice;
            }
        }

        if (stealCandidate == null || profile.priority > stealCandidate.priority)
        {
            return null;
        }

        return stealCandidate;
    }

    private bool IsWorseVoice(PooledVoice candidate, PooledVoice currentWorst, Transform listener)
    {
        if (candidate.priority != currentWorst.priority)
        {
            return candidate.priority > currentWorst.priority;
        }

        if (listener != null && candidate.isWorld != currentWorst.isWorld)
        {
            return candidate.isWorld;
        }

        if (listener != null && candidate.isWorld && currentWorst.isWorld)
        {
            float candidateDistance = ((Vector2)listener.position - candidate.worldPosition).sqrMagnitude;
            float currentDistance = ((Vector2)listener.position - currentWorst.worldPosition).sqrMagnitude;

            if (!Mathf.Approximately(candidateDistance, currentDistance))
            {
                return candidateDistance > currentDistance;
            }
        }

        if (!Mathf.Approximately(candidate.effectiveVolume, currentWorst.effectiveVolume))
        {
            return candidate.effectiveVolume < currentWorst.effectiveVolume;
        }

        return candidate.startedAt < currentWorst.startedAt;
    }

    private void RefreshVoiceRuntime()
    {
        for (int i = 0; i < voices.Count; i++)
        {
            PooledVoice voice = voices[i];

            if (!voice.IsPlaying && !string.IsNullOrEmpty(voice.eventId))
            {
                voice.ClearRuntime();
            }
        }
    }

    private void PrepareVoiceForReuse(PooledVoice voice)
    {
        if (voice == null || voice.source == null)
        {
            return;
        }

        voice.source.Stop();
        voice.source.clip = null;
        voice.source.loop = false;

        if (voice.lowPass != null)
        {
            voice.lowPass.cutoffFrequency = 22000f;
            voice.lowPass.enabled = false;
        }

        voice.ClearRuntime();
    }

    private void SetSourcePosition(
        AudioSource source,
        bool isWorld,
        Vector3? requestedPosition,
        Transform listener)
    {
        if (source == null)
        {
            return;
        }

        if (!isWorld || !requestedPosition.HasValue)
        {
            source.transform.localPosition = Vector3.zero;
            return;
        }

        Vector3 finalPosition = requestedPosition.Value;

        if (listener != null)
        {
            finalPosition.z = listener.position.z;
        }

        source.transform.position = finalPosition;
    }

    private Transform ResolveListener(bool forceSearch)
    {
        if (!forceSearch &&
            cachedListener != null &&
            cachedListener.enabled &&
            cachedListener.gameObject.activeInHierarchy)
        {
            return cachedListenerTransform;
        }

        if (!forceSearch && Time.unscaledTime < nextListenerSearchTime)
        {
            return cachedListenerTransform;
        }

        nextListenerSearchTime = Time.unscaledTime + 0.5f;
        cachedListener = null;
        cachedListenerTransform = null;

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener candidate = listeners[i];

            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            cachedListener = candidate;
            cachedListenerTransform = candidate.transform;
            break;
        }

        return cachedListenerTransform;
    }

    private int CountOccluders(Vector2 sourcePosition, Vector2 listenerPosition)
    {
        int mask = ResolveOccluderMask();

        if (mask == 0)
        {
            return 0;
        }

        Vector2 delta = listenerPosition - sourcePosition;
        float distance = delta.magnitude;

        if (distance <= 0.05f)
        {
            return 0;
        }

        int hitCount = Physics2D.RaycastNonAlloc(
            sourcePosition,
            delta / distance,
            occlusionHits,
            distance,
            mask
        );

        occlusionColliderIds.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = occlusionHits[i];
            Collider2D collider = hit.collider;

            if (collider == null || collider.isTrigger || hit.fraction <= 0.01f || hit.fraction >= 0.99f)
            {
                continue;
            }

            occlusionColliderIds.Add(collider.GetInstanceID());

            if (occlusionColliderIds.Count >= 2)
            {
                return 2;
            }
        }

        return occlusionColliderIds.Count;
    }

    private int ResolveOccluderMask()
    {
        if (audioOccluderMask.value != 0)
        {
            return audioOccluderMask.value;
        }

        if (!autoResolveAudioOccluderLayer)
        {
            return 0;
        }

        int layer = LayerMask.NameToLayer("AudioOccluder");
        return layer >= 0 ? 1 << layer : 0;
    }

    private float ResolveOcclusionVolume(PlaybackProfile profile, int occluderCount)
    {
        if (occluderCount <= 0)
        {
            return 1f;
        }

        return occluderCount >= 2
            ? profile.multipleOccluderVolume
            : profile.oneOccluderVolume;
    }

    private AudioSource GetLoopSource(string channelName)
    {
        if (loopSources.TryGetValue(channelName, out AudioSource source) && source != null)
        {
            return source;
        }

        GameObject sourceObject = new GameObject($"LoopSource_{channelName}");
        sourceObject.transform.SetParent(transform, false);

        source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.priority = 200;
        source.dopplerLevel = 0f;

        AudioLowPassFilter lowPass = sourceObject.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = 22000f;
        lowPass.enabled = false;

        loopSources[channelName] = source;
        loopFilters[channelName] = lowPass;
        return source;
    }

    private bool IsAutomaticTwoDEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return true;
        }

        if (eventId.StartsWith("ui_", StringComparison.Ordinal) ||
            eventId.StartsWith("pickup_", StringComparison.Ordinal) ||
            eventId.StartsWith("radar_", StringComparison.Ordinal) ||
            eventId.StartsWith("trait_", StringComparison.Ordinal) ||
            eventId.StartsWith("reinforcement_", StringComparison.Ordinal) ||
            eventId.StartsWith("tab_status_", StringComparison.Ordinal) ||
            eventId.StartsWith("result_", StringComparison.Ordinal) ||
            eventId.StartsWith("music_", StringComparison.Ordinal) ||
            eventId.StartsWith("amb_", StringComparison.Ordinal))
        {
            return true;
        }

        return eventId == SoundEventIds.WarningMessage ||
               eventId == SoundEventIds.ActionDenied ||
               eventId == SoundEventIds.LevelUp ||
               eventId == SoundEventIds.ReturnChoiceOpen ||
               eventId == SoundEventIds.SafeReturn ||
               eventId == SoundEventIds.ShipDashStart ||
               eventId == SoundEventIds.ShipDashEnd ||
               eventId == SoundEventIds.ShipHit ||
               eventId == SoundEventIds.ShipDeathBreakup ||
               eventId == SoundEventIds.MachineGunFire ||
               eventId == SoundEventIds.ShotgunFire ||
               eventId == SoundEventIds.SniperChargeStart ||
               eventId == SoundEventIds.SniperChargeCancel ||
               eventId == SoundEventIds.SniperFire ||
               eventId == SoundEventIds.ShopOpen ||
               eventId == SoundEventIds.ShopBuySuccess ||
               eventId == SoundEventIds.ShopBuyFail ||
               eventId == SoundEventIds.ShopItemSold ||
               eventId == SoundEventIds.ShopTransactionComplete;
    }

    private float ResolveAutomaticMinDistance(string eventId)
    {
        if (eventId.StartsWith("boss_", StringComparison.Ordinal))
        {
            return 2f;
        }

        return Mathf.Max(0.1f, defaultWorldMinDistance);
    }

    private float ResolveAutomaticMaxDistance(string eventId)
    {
        if (eventId.StartsWith("boss_", StringComparison.Ordinal))
        {
            return 24f;
        }

        if (eventId == SoundEventIds.EnemyChargerAimLoop || eventId == SoundEventIds.EnemyChargerFire)
        {
            return 18f;
        }

        if (eventId == SoundEventIds.ObjectContainerHit ||
            eventId == SoundEventIds.ObjectDebrisHit ||
            eventId == SoundEventIds.ObjectMeteorHit)
        {
            return 10f;
        }

        if (eventId == SoundEventIds.ObjectShipwreckBreak)
        {
            return 16f;
        }

        if (eventId.StartsWith("object_", StringComparison.Ordinal) ||
            eventId.StartsWith("enemy_", StringComparison.Ordinal))
        {
            return 14f;
        }

        if (eventId.StartsWith("core_", StringComparison.Ordinal) ||
            eventId.StartsWith("shop_", StringComparison.Ordinal) ||
            eventId.StartsWith("event_", StringComparison.Ordinal) ||
            eventId == SoundEventIds.ReturnBeaconSpawn ||
            eventId == SoundEventIds.WormholeEnter ||
            eventId == SoundEventIds.SecurityDroneSpawn)
        {
            return 18f;
        }

        return Mathf.Max(1f, defaultWorldMaxDistance);
    }

    private int ResolveAutomaticVoiceLimit(string eventId)
    {
        if (eventId == SoundEventIds.EnemyBasicFire)
        {
            return 3;
        }

        if (eventId == SoundEventIds.EnemyShotgunFire ||
            eventId == SoundEventIds.EnemyChargerAimLoop ||
            eventId == SoundEventIds.ObjectContainerHit ||
            eventId == SoundEventIds.ObjectDebrisHit ||
            eventId == SoundEventIds.ObjectMeteorHit ||
            eventId == SoundEventIds.ObjectContainerBreak ||
            eventId == SoundEventIds.ObjectDebrisBreak ||
            eventId == SoundEventIds.ObjectMeteorBreak)
        {
            return 2;
        }

        if (eventId == SoundEventIds.EnemyHit || eventId == SoundEventIds.EnemyDeath)
        {
            return 3;
        }

        if (eventId.StartsWith("boss_", StringComparison.Ordinal))
        {
            return 4;
        }

        return 4;
    }

    private float ResolveAutomaticRetriggerInterval(string eventId)
    {
        if (eventId == SoundEventIds.MachineGunFire)
        {
            return 0.015f;
        }

        if (eventId == SoundEventIds.EnemyHit || eventId.StartsWith("object_", StringComparison.Ordinal))
        {
            return 0.035f;
        }

        return Mathf.Max(0f, sameEventMinimumInterval);
    }

    private int ResolvePriority(string eventId, int configuredPriority)
    {
        int priority = Mathf.Clamp(configuredPriority, 0, 256);

        if (eventId == SoundEventIds.ShipHit ||
            eventId == SoundEventIds.ShipDeathBreakup ||
            eventId == SoundEventIds.WarningMessage ||
            eventId == SoundEventIds.ActionDenied)
        {
            return Mathf.Min(priority, 32);
        }

        if (eventId.StartsWith("boss_", StringComparison.Ordinal) ||
            eventId == SoundEventIds.EnemyChargerAimLoop)
        {
            return Mathf.Min(priority, 64);
        }

        return priority;
    }
}
