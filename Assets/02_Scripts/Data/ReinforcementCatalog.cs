using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VOID SCRAPPER/Reinforcements/Reinforcement Catalog")]
public class ReinforcementCatalog : ScriptableObject
{
    [Header("Manual List")]
    [SerializeField] private List<ReinforcementDefinition> reinforcementDefinitions = new List<ReinforcementDefinition>();

    [Header("Optional Resources Load")]
    [SerializeField] private bool includeResourcesReinforcements;
    [SerializeField] private string resourcesPath = "ReinforcementDefinition";

    public IReadOnlyList<ReinforcementDefinition> ReinforcementDefinitions => reinforcementDefinitions;

    public void AppendAllTo(List<ReinforcementDefinition> target)
    {
        if (target == null)
        {
            return;
        }

        AppendManual(target);

        if (includeResourcesReinforcements)
        {
            AppendResources(target);
        }
    }

    public ReinforcementDefinition FindById(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            return null;
        }

        List<ReinforcementDefinition> buffer = new List<ReinforcementDefinition>();
        AppendAllTo(buffer);

        for (int i = 0; i < buffer.Count; i++)
        {
            ReinforcementDefinition definition = buffer[i];

            if (definition != null && definition.EquipmentId == equipmentId)
            {
                return definition;
            }
        }

        return null;
    }

    private void AppendManual(List<ReinforcementDefinition> target)
    {
        if (reinforcementDefinitions == null)
        {
            return;
        }

        for (int i = 0; i < reinforcementDefinitions.Count; i++)
        {
            AppendUnique(target, reinforcementDefinitions[i]);
        }
    }

    private void AppendResources(List<ReinforcementDefinition> target)
    {
        string path = NormalizeResourcesPath(resourcesPath);
        ReinforcementDefinition[] loaded = Resources.LoadAll<ReinforcementDefinition>(path);

        if (loaded == null)
        {
            return;
        }

        for (int i = 0; i < loaded.Length; i++)
        {
            AppendUnique(target, loaded[i]);
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

    private void AppendUnique(List<ReinforcementDefinition> target, ReinforcementDefinition definition)
    {
        if (target == null || definition == null)
        {
            return;
        }

        string id = definition.EquipmentId;

        for (int i = 0; i < target.Count; i++)
        {
            ReinforcementDefinition existing = target[i];

            if (existing != null && existing.EquipmentId == id)
            {
                return;
            }
        }

        target.Add(definition);
    }
}