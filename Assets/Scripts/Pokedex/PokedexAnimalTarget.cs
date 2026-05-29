using UnityEngine;

public class PokedexAnimalTarget : MonoBehaviour
{
    [SerializeField] private PokedexEntryData entry;
    [SerializeField] private string entryIdOverride;

    public PokedexEntryData Entry => entry;
    public string EntryId => !string.IsNullOrWhiteSpace(entryIdOverride)
        ? entryIdOverride.Trim()
        : entry != null
            ? entry.EntryId
            : string.Empty;

    public bool TryResolveEntry(PokedexDatabase database, out PokedexEntryData resolved)
    {
        resolved = entry;
        if (resolved != null)
        {
            return true;
        }

        if (database == null)
        {
            return false;
        }

        resolved = database.GetById(EntryId);
        return resolved != null;
    }

    public void Reveal(PokedexUIController uiController)
    {
        if (uiController == null)
        {
            return;
        }

        if (TryResolveEntry(uiController.Database, out var resolved))
        {
            uiController.ShowEntry(resolved, true);
        }
    }
}