using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MR Group 5/Pokedex Entry", fileName = "PokedexEntry")]
public class PokedexEntryData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string entryId = "animal_id";
    [SerializeField] private string commonName = "Animal Name";
    [SerializeField] private string scientificName = "Species name";
    [SerializeField] private Texture2D icon;

    [Header("Classification")]
    [SerializeField] private string category = "Animal";
    [SerializeField] private string habitat = "Forest";
    [SerializeField] private string height = "";
    [SerializeField] private string weight = "";
    [SerializeField] private string diet = "Unknown";
    [SerializeField] private string rarity = "Common";

    [Header("Descriptions")]
    [TextArea(3, 6)]
    [SerializeField] private string shortDescription = "Short description goes here.";

    [TextArea(2, 5)]
    [SerializeField] private string behaviorNotes = "Behaviour or movement notes.";

    [TextArea(2, 5)]
    [SerializeField] private string observationTips = "How to spot this animal in the world.";

    [TextArea(2, 5)]
    [SerializeField] private string funFact = "";

    [SerializeField] private List<string> facts = new List<string>();

    public string EntryId => string.IsNullOrWhiteSpace(entryId) ? name : entryId.Trim();
    public string CommonName => string.IsNullOrWhiteSpace(commonName) ? name : commonName.Trim();
    public string ScientificName => scientificName?.Trim() ?? string.Empty;
    public Texture2D Icon => icon;
    public string Category => category?.Trim() ?? string.Empty;
    public string Habitat => habitat?.Trim() ?? string.Empty;
    public string Height => height?.Trim() ?? string.Empty;
    public string Weight => weight?.Trim() ?? string.Empty;
    public string Diet => diet?.Trim() ?? string.Empty;
    public string Rarity => rarity?.Trim() ?? string.Empty;
    public string ShortDescription => shortDescription?.Trim() ?? string.Empty;
    public string BehaviorNotes => behaviorNotes?.Trim() ?? string.Empty;
    public string ObservationTips => observationTips?.Trim() ?? string.Empty;
    public string FunFact => funFact?.Trim() ?? string.Empty;
    public IReadOnlyList<string> Facts => facts;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            entryId = name;
        }
    }
}