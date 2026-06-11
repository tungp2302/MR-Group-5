using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to the same GameObject as lb_BirdController.
// lb_BirdController.Start() instantiates every bird synchronously (inactive children),
// so after one frame we can find them all. Each bird prefab already has a Sphere child
// with an XRSimpleInteractable (ray select + pink material swap + particles). We add a
// selectEntered listener to that Sphere that reveals the bird in the dex.
//
// IMPORTANT: pokedexEntryId must match the PokedexEntryData.entryId FIELD (e.g. "bird_bluejay"),
// NOT the asset file name (e.g. "PokedexBlueJay").
[RequireComponent(typeof(lb_BirdController))]
public class LbBirdPokedexInjector : MonoBehaviour
{
    [Serializable]
    public class BirdSpeciesEntry
    {
        [Tooltip("Substring of the bird prefab name (case-insensitive). E.g. 'blueJay'")]
        public string prefabNameContains;
        [Tooltip("Must match the entryId FIELD in PokedexEntryData (e.g. 'bird_bluejay')")]
        public string pokedexEntryId;
    }

    [Header("Species to Pokedex ID Mapping")]
    [SerializeField] private BirdSpeciesEntry[] speciesMappings =
    {
        new BirdSpeciesEntry { prefabNameContains = "blueJay",   pokedexEntryId = "bird_bluejay" },
        new BirdSpeciesEntry { prefabNameContains = "cardinal",  pokedexEntryId = "bird_cardinal" },
        new BirdSpeciesEntry { prefabNameContains = "chickadee", pokedexEntryId = "bird_chickadee" },
        new BirdSpeciesEntry { prefabNameContains = "goldFinch", pokedexEntryId = "bird_goldfinch" },
        new BirdSpeciesEntry { prefabNameContains = "crow",      pokedexEntryId = "bird_crow" },
        new BirdSpeciesEntry { prefabNameContains = "robin",     pokedexEntryId = "bird_robin" },
    };

    [Header("Pokedex UI")]
    [Tooltip("Leave empty to auto-detect at runtime.")]
    [SerializeField] private PokedexXRCloneUIController uiController;

    private IEnumerator Start()
    {
        yield return null; // let lb_BirdController.Start() finish spawning birds

        uiController ??= FindObjectOfType<PokedexXRCloneUIController>();
        if (uiController == null)
        {
            Debug.LogError("[LbBirdPokedexInjector] No PokedexXRCloneUIController found in scene.");
            yield break;
        }

        foreach (Transform child in transform)
        {
            if (child.GetComponent<lb_Bird>() == null) continue;
            string entryId = ResolveEntryId(child.name);
            if (string.IsNullOrEmpty(entryId)) continue;
            WireBirdDex(child.gameObject, entryId, uiController);
        }
    }

    private string ResolveEntryId(string goName)
    {
        foreach (var m in speciesMappings)
        {
            if (string.IsNullOrEmpty(m.prefabNameContains)) continue;
            if (goName.IndexOf(m.prefabNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return m.pokedexEntryId;
        }
        return null;
    }

    private static void WireBirdDex(GameObject birdGo, string entryId, PokedexXRCloneUIController ui)
    {
        // The bird prefab's Sphere child (under HoverSystem) holds the XRSimpleInteractable.
        var sphere = FindFirstChildInteractable(birdGo);
        if (sphere == null)
        {
            Debug.LogWarning($"[LbBirdPokedexInjector] No child XRSimpleInteractable on bird '{birdGo.name}'.");
            return;
        }

        var capturedId = entryId;
        var capturedUi = ui;
        sphere.selectEntered.AddListener(_ => capturedUi.ShowEntryById(capturedId, true));
    }

    private static XRSimpleInteractable FindFirstChildInteractable(GameObject root)
    {
        var all = root.GetComponentsInChildren<XRSimpleInteractable>(true);
        foreach (var i in all)
            if (i.gameObject != root) return i;
        return null;
    }
}
