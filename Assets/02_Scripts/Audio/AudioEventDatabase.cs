using System;
using System.Collections.Generic;
using UnityEngine;

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
        return lookup.TryGetValue(eventId, out definition);
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

            lookup[entry.EventId] = entry;
        }
    }
}
