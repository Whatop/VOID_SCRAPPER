using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AudioEventDefinition
{
    [SerializeField] private string eventId;
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 2f)] private float volume = 1f;
    [SerializeField] private float pitchMin = 1f;
    [SerializeField] private float pitchMax = 1f;
    [SerializeField, Range(0f, 1f)] private float spatialBlend;
    [SerializeField] private bool loop;
    [SerializeField, Range(0, 256)] private int priority = 128;

    public string EventId => eventId;
    public IReadOnlyList<AudioClip> Clips => clips;
    public float Volume => volume;
    public float PitchMin => pitchMin;
    public float PitchMax => pitchMax;
    public float SpatialBlend => spatialBlend;
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
