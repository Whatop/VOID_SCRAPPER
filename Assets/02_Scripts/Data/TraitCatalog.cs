using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Traits/Trait Catalog")]
public class TraitCatalog : ScriptableObject
{
    [Header("Manual List")]
    [SerializeField] private List<TraitDefinition> traitDefinitions = new List<TraitDefinition>();

    [Header("Optional Resources Load")]
    [Tooltip("켜면 Resources 폴더 안의 TraitDefinition도 같이 읽는다.")]
    [SerializeField] private bool includeResourcesTraits;

    [Tooltip("예: TraitDefinition. 비워두면 Resources 전체에서 TraitDefinition을 찾는다.")]
    [SerializeField] private string resourcesPath = "TraitDefinition";

    public IReadOnlyList<TraitDefinition> TraitDefinitions => traitDefinitions;

    public void AppendAllTo(List<TraitDefinition> target)
    {
        if (target == null)
        {
            return;
        }

        AppendManualTraits(target);

        if (includeResourcesTraits)
        {
            AppendResourcesTraits(target);
        }
    }

    public TraitDefinition FindById(string traitId)
    {
        if (string.IsNullOrWhiteSpace(traitId))
        {
            return null;
        }

        List<TraitDefinition> buffer = new List<TraitDefinition>();
        AppendAllTo(buffer);

        for (int i = 0; i < buffer.Count; i++)
        {
            TraitDefinition trait = buffer[i];

            if (trait != null && trait.TraitId == traitId)
            {
                return trait;
            }
        }

        return null;
    }

    private void AppendManualTraits(List<TraitDefinition> target)
    {
        if (traitDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < traitDefinitions.Count; i++)
        {
            AppendUnique(target, traitDefinitions[i]);
        }
    }

    private void AppendResourcesTraits(List<TraitDefinition> target)
    {
        string path = NormalizeResourcesPath(resourcesPath);
        TraitDefinition[] loadedTraits = Resources.LoadAll<TraitDefinition>(path);

        if (loadedTraits == null)
        {
            return;
        }

        for (int i = 0; i < loadedTraits.Length; i++)
        {
            AppendUnique(target, loadedTraits[i]);
        }
    }

    private string NormalizeResourcesPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        path = path.Replace("\\", "/").Trim();

        const string resourcesToken = "/Resources/";
        int resourcesIndex = path.IndexOf(resourcesToken, StringComparison.OrdinalIgnoreCase);

        if (resourcesIndex >= 0)
        {
            path = path.Substring(resourcesIndex + resourcesToken.Length);
        }

        if (path.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring("Resources/".Length);
        }

        return path.Trim('/');
    }

    private void AppendUnique(List<TraitDefinition> target, TraitDefinition trait)
    {
        if (target == null || trait == null)
        {
            return;
        }

        string traitId = trait.TraitId;

        for (int i = 0; i < target.Count; i++)
        {
            TraitDefinition existing = target[i];

            if (existing == null)
            {
                continue;
            }

            if (existing.TraitId == traitId)
            {
                return;
            }
        }

        target.Add(trait);
    }
}