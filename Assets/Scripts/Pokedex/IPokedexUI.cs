using UnityEngine;

public interface IPokedexUI
{
    PokedexDatabase Database { get; }
    void ShowEntry(PokedexEntryData entry, bool markDiscovered = true);
    bool ShowEntryById(string entryId, bool markDiscovered = true);
}
