using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "MR Group 5/Pokedex Database", fileName = "PokedexDatabase")]
public class PokedexDatabase : ScriptableObject
{
    [SerializeField] private List<PokedexEntryData> entries = new List<PokedexEntryData>();
    [SerializeField] private bool sortEntriesByName = true;

    private readonly HashSet<string> discoveredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, PokedexEntryData> lookupCache;
    private bool lookupDirty = true;

    public event Action DatabaseChanged;

    public IReadOnlyList<PokedexEntryData> Entries => GetEntries();
    public IReadOnlyCollection<string> DiscoveredIds => discoveredIds;
    public int DiscoveredCount => discoveredIds.Count;
    public int TotalEntries => GetEntries().Count;

    public PokedexEntryData GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        EnsureLookup();
        lookupCache.TryGetValue(id.Trim(), out var entry);
        return entry;
    }

    public bool Contains(string id)
    {
        return GetById(id) != null;
    }

    public bool IsDiscovered(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return discoveredIds.Contains(id.Trim());
    }

    public bool MarkDiscovered(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        EnsureLookup();
        var key = id.Trim();
        if (!lookupCache.ContainsKey(key))
        {
            return false;
        }

        var added = discoveredIds.Add(key);
        if (added)
        {
            DatabaseChanged?.Invoke();
        }

        return added;
    }

    public void SetEntries(IEnumerable<PokedexEntryData> newEntries)
    {
        entries = newEntries?.Where(entry => entry != null).ToList() ?? new List<PokedexEntryData>();
        lookupDirty = true;
        DatabaseChanged?.Invoke();
    }

    public IReadOnlyList<PokedexEntryData> GetEntries()
    {
        var filteredEntries = entries.Where(entry => entry != null);
        return sortEntriesByName
            ? filteredEntries.OrderBy(entry => entry.CommonName, StringComparer.OrdinalIgnoreCase).ToList()
            : filteredEntries.ToList();
    }

    private void OnEnable()
    {
        lookupDirty = true;
    }

    private void OnValidate()
    {
        lookupDirty = true;
    }

    private void EnsureLookup()
    {
        if (!lookupDirty && lookupCache != null)
        {
            return;
        }

        lookupCache = new Dictionary<string, PokedexEntryData>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in GetEntries())
        {
            var key = entry.EntryId;
            if (!lookupCache.ContainsKey(key))
            {
                lookupCache.Add(key, entry);
            }
        }

        lookupDirty = false;
    }
}