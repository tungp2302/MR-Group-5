using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to the same GameObject as lb_BirdController.
// Waits one frame after Start so lb_BirdController has finished instantiating all birds,
// then adds XR selection components to each bird automatically.
//
// Each bird gets a child "PokedexDetection" object with:
//   - Kinematic Rigidbody  → isolates the collider from the bird's compound physics body
//   - SphereCollider       → non-trigger, detectable by XR ray without extra settings
//   - XRSimpleInteractable → wired to reveal the matching Pokedex entry on select
[RequireComponent(typeof(lb_BirdController))]
public class LbBirdPokedexInjector : MonoBehaviour
{
    [Serializable]
    public class BirdSpeciesEntry
    {
        [Tooltip("Substring of the bird prefab name (case-insensitive). E.g. 'blueJay'")]
        public string prefabNameContains;
        [Tooltip("Must exactly match the EntryId field inside your PokedexEntryData ScriptableObject")]
        public string pokedexEntryId;
    }

    [Header("XR References")]
    [SerializeField] private XRInteractionManager interactionManager;
    [SerializeField] private PokedexXRCloneUIController uiController;

    [Header("Species to Pokedex ID Mapping")]
    [SerializeField] private BirdSpeciesEntry[] speciesMappings =
    {
        new BirdSpeciesEntry { prefabNameContains = "blueJay",   pokedexEntryId = "PokedexBlueJay" },
        new BirdSpeciesEntry { prefabNameContains = "cardinal",  pokedexEntryId = "PokedexCardinal" },
        new BirdSpeciesEntry { prefabNameContains = "chickadee", pokedexEntryId = "PokedexChickadee" },
        new BirdSpeciesEntry { prefabNameContains = "sparrow",   pokedexEntryId = "PokedexSparrow" },
        new BirdSpeciesEntry { prefabNameContains = "goldFinch", pokedexEntryId = "PokedexGoldfinch" },
        new BirdSpeciesEntry { prefabNameContains = "crow",      pokedexEntryId = "PokedexCrow" },
        new BirdSpeciesEntry { prefabNameContains = "robin",     pokedexEntryId = "PokedexBird" },
    };

    [Header("Detection")]
    [Tooltip("Radius of the sphere collider used for ray detection. Tune to roughly match the bird model size.")]
    [SerializeField] private float detectionRadius = 0.25f;

    private IEnumerator Start()
    {
        yield return null; // one frame delay so lb_BirdController.Start() finishes spawning all birds

        foreach (Transform child in transform)
        {
            if (child.GetComponent<lb_Bird>() == null) continue;

            string entryId = ResolveEntryId(child.name);
            if (string.IsNullOrEmpty(entryId)) continue;

            AttachPokedexDetection(child.gameObject, entryId);
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

    private void AttachPokedexDetection(GameObject birdGo, string entryId)
    {
        var detectionGo = new GameObject("PokedexDetection");
        detectionGo.transform.SetParent(birdGo.transform, false);

        // Own Rigidbody keeps this child's collider out of the bird's compound physics body,
        // so it cannot fire lb_Bird's OnTriggerEnter/Exit callbacks or affect flight forces.
        var rb = detectionGo.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var col = detectionGo.AddComponent<SphereCollider>();
        col.isTrigger = false;
        col.radius = detectionRadius;

        var interactable = detectionGo.AddComponent<XRSimpleInteractable>();
        if (interactionManager != null)
            interactable.interactionManager = interactionManager;

        string capturedId = entryId;
        interactable.selectEntered.AddListener(_ =>
        {
            if (uiController != null)
                uiController.ShowEntryById(capturedId, true);
        });
    }
}
