using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum AudioSpatialMode
{
    Auto = 0,
    Force2D = 1,
    World2D = 2
}

[Serializable]
public class AudioEventDefinition
{
    [Header("Event")]
    [SerializeField] private string eventId;
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 2f)] private float volume = 1f;
    [SerializeField] private float pitchMin = 1f;
    [SerializeField] private float pitchMax = 1f;
    [SerializeField] private bool loop;
    [SerializeField, Range(0, 256)] private int priority = 128;

    [Header("Spatial")]
    [Tooltip("Auto는 PlayAt 호출과 이벤트 ID를 기준으로 2D/월드 사운드를 자동 분류합니다.")]
    [SerializeField] private AudioSpatialMode spatialMode = AudioSpatialMode.Auto;
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 14f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
    [SerializeField, Range(0f, 5f)] private float dopplerLevel;
    [SerializeField] private bool hardCullOutsideMaxDistance = true;

    [Header("Occlusion")]
    [SerializeField] private bool useOcclusion = true;
    [SerializeField, Range(0f, 1f)] private float oneOccluderVolumeMultiplier = 0.6f;
    [SerializeField, Range(0f, 1f)] private float multipleOccluderVolumeMultiplier = 0.3f;
    [SerializeField, Range(10f, 22000f)] private float oneOccluderLowPassCutoff = 3500f;
    [SerializeField, Range(10f, 22000f)] private float multipleOccluderLowPassCutoff = 1500f;

    [Header("Voice Limit")]
    [Tooltip("0이면 AudioManager의 이벤트별 자동 제한을 사용합니다.")]
    [SerializeField] private int maxSimultaneousVoices;
    [Tooltip("0 이하이면 AudioManager의 기본 중복 방지 시간을 사용합니다.")]
    [SerializeField] private float minimumRetriggerInterval = -1f;

    public string EventId => eventId;
    public IReadOnlyList<AudioClip> Clips => clips;
    public float Volume => volume;
    public float PitchMin => pitchMin;
    public float PitchMax => pitchMax;
    public AudioSpatialMode SpatialMode => spatialMode;
    public float SpatialBlend => spatialBlend;
    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public AudioRolloffMode RolloffMode => rolloffMode;
    public float DopplerLevel => dopplerLevel;
    public bool HardCullOutsideMaxDistance => hardCullOutsideMaxDistance;
    public bool UseOcclusion => useOcclusion;
    public float OneOccluderVolumeMultiplier => oneOccluderVolumeMultiplier;
    public float MultipleOccluderVolumeMultiplier => multipleOccluderVolumeMultiplier;
    public float OneOccluderLowPassCutoff => oneOccluderLowPassCutoff;
    public float MultipleOccluderLowPassCutoff => multipleOccluderLowPassCutoff;
    public int MaxSimultaneousVoices => maxSimultaneousVoices;
    public float MinimumRetriggerInterval => minimumRetriggerInterval;
    public bool Loop => loop;
    public int Priority => priority;

#if UNITY_EDITOR
    internal void SetEventIdForEditor(string value)
    {
        eventId = value;
    }

    internal void ConfigureMusicForEditor(AudioClip clip, float configuredVolume, int configuredPriority)
    {
        if (clip != null)
        {
            clips = new[] { clip };
        }

        volume = Mathf.Clamp(configuredVolume, 0f, 2f);
        pitchMin = 1f;
        pitchMax = 1f;
        loop = true;
        priority = Mathf.Clamp(configuredPriority, 0, 256);
        spatialMode = AudioSpatialMode.Force2D;
        spatialBlend = 0f;
        dopplerLevel = 0f;
        useOcclusion = false;
        maxSimultaneousVoices = 1;
        minimumRetriggerInterval = -1f;
    }
#endif

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        if (clips.Length == 1)
        {
            return clips[0];
        }

        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }
}

[CreateAssetMenu(menuName = "VOID SCRAPPER/Audio/Audio Event Database", fileName = "AudioEventDatabase")]
public class AudioEventDatabase : ScriptableObject
{
    [SerializeField] private AudioEventDefinition[] entries;

    private Dictionary<string, AudioEventDefinition> lookup;

    public bool TryGet(string eventId, out AudioEventDefinition definition)
    {
        EnsureLookup();

        if (string.IsNullOrWhiteSpace(eventId))
        {
            definition = null;
            return false;
        }

        if (lookup.TryGetValue(eventId, out definition))
        {
            return true;
        }

        string numbered = SoundEventIds.ToNumbered(eventId);
        if (!string.Equals(numbered, eventId, StringComparison.Ordinal) &&
            lookup.TryGetValue(numbered, out definition))
        {
            return true;
        }

        string legacy = SoundEventIds.ToLegacy(eventId);
        return !string.Equals(legacy, eventId, StringComparison.Ordinal) &&
               lookup.TryGetValue(legacy, out definition);
    }

    private void OnEnable()
    {
        lookup = null;
    }

    private void OnValidate()
    {
        lookup = null;
    }

    private void EnsureLookup()
    {
        if (lookup != null)
        {
            return;
        }

        lookup = new Dictionary<string, AudioEventDefinition>(StringComparer.Ordinal);

        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            AudioEventDefinition entry = entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.EventId))
            {
                continue;
            }

            RegisterLookupAlias(entry.EventId, entry);
            RegisterLookupAlias(SoundEventIds.ToNumbered(entry.EventId), entry);
            RegisterLookupAlias(SoundEventIds.ToLegacy(entry.EventId), entry);
        }
    }

    private void RegisterLookupAlias(string eventId, AudioEventDefinition entry)
    {
        if (string.IsNullOrWhiteSpace(eventId) || entry == null)
        {
            return;
        }

        lookup[eventId] = entry;
    }

#if UNITY_EDITOR
    [ContextMenu("번호 적용 + 정렬 + 상점 음악 항목 추가")]
    public void EditorApplyNumberingAndEnsureShopMusic()
    {
        Undo.RecordObject(this, "Number Sound Event Library");
        EnsureShopMusicEntryForEditor();

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                AudioEventDefinition entry = entries[i];

                if (entry == null || string.IsNullOrWhiteSpace(entry.EventId))
                {
                    continue;
                }

                entry.SetEventIdForEditor(SoundEventIds.ToNumbered(entry.EventId));
            }

            Array.Sort(entries, CompareEntriesForEditor);
        }

        lookup = null;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    [ContextMenu("상점 음악 항목만 추가")]
    public void EditorEnsureShopMusicEntry()
    {
        Undo.RecordObject(this, "Add Shop Music Event");
        EnsureShopMusicEntryForEditor();
        lookup = null;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    private void EnsureShopMusicEntryForEditor()
    {
        AudioEventDefinition shopEntry = null;

        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                AudioEventDefinition entry = entries[i];

                if (entry == null)
                {
                    continue;
                }

                if (string.Equals(
                        SoundEventIds.ToNumbered(entry.EventId),
                        SoundEventIds.MusicShopLoop,
                        StringComparison.Ordinal))
                {
                    shopEntry = entry;
                    break;
                }
            }
        }

        if (shopEntry == null)
        {
            int oldLength = entries != null ? entries.Length : 0;
            Array.Resize(ref entries, oldLength + 1);
            shopEntry = new AudioEventDefinition();
            entries[oldLength] = shopEntry;
        }

        shopEntry.SetEventIdForEditor(SoundEventIds.MusicShopLoop);
        shopEntry.ConfigureMusicForEditor(FindShopMusicClipForEditor(), 0.14f, 210);
    }

    private static AudioClip FindShopMusicClipForEditor()
    {
        string[] clipGuids = AssetDatabase.FindAssets("shop t:AudioClip");
        AudioClip fallback = null;

        for (int i = 0; i < clipGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

            if (clip == null)
            {
                continue;
            }

            fallback ??= clip;

            string normalizedPath = assetPath.Replace('\\', '/');
            if (normalizedPath.EndsWith("/BGM/shop.wav", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.EndsWith("/BGM/shop.ogg", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.EndsWith("/BGM/shop.mp3", StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        return fallback;
    }

    private static int CompareEntriesForEditor(AudioEventDefinition left, AudioEventDefinition right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int leftOrder = SoundEventIds.GetOrder(left.EventId);
        int rightOrder = SoundEventIds.GetOrder(right.EventId);
        int orderCompare = leftOrder.CompareTo(rightOrder);

        if (orderCompare != 0)
        {
            return orderCompare;
        }

        return string.Compare(left.EventId, right.EventId, StringComparison.Ordinal);
    }
#endif
}
