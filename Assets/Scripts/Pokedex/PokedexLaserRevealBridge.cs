using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Attach to the same GameObject as XRRayInteractor.
//
// DEX REVEAL (runtime, this interactor):
//   On selectEntered, walk up parents from the selected interactable and call
//   PokedexAnimalTarget.Reveal(). Works for the wolf and every mammal with a
//   PokedexAnimalTarget. Birds are revealed separately by LbBirdPokedexInjector.
//
// SPHERE VISUAL FIX (Start, scene-wide):
//   Each mammal has a redundant XRSimpleInteractable on its ROOT (added in the
//   Inspector) with empty colliders. At Awake the root interactable auto-grabs ALL
//   child colliders — including the "Sphere" proxy's collider — and can win the
//   collider->interactable mapping, so selecting the animal selects the ROOT and the
//   Sphere's pink material-swap never fires. We disable that root interactable and
//   re-register the Sphere so the Sphere is the selected target (pink visual fires,
//   and Reveal still works because GetComponentInParent walks up to the root target).
[RequireComponent(typeof(XRRayInteractor))]
public class PokedexLaserRevealBridge : MonoBehaviour
{
    [SerializeField] private XRInteractionManager interactionManager;
    [SerializeField] private PokedexXRCloneUIController uiController;

    private XRRayInteractor _interactor;

    private void Awake()
    {
        _interactor = GetComponent<XRRayInteractor>();
    }

    private void OnEnable()
    {
        _interactor?.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        _interactor?.selectEntered.RemoveListener(OnSelectEntered);
    }

    private IEnumerator Start()
    {
        yield return null; // wait for all interactables to finish OnEnable registration

        // Idempotent: safe to run from multiple ray-interactor bridges and after scene reload.
        foreach (var target in FindObjectsOfType<PokedexAnimalTarget>())
            PreferSphereInteractable(target);
    }

    // Make the animal's Sphere proxy the selection target instead of the root interactable.
    private static void PreferSphereInteractable(PokedexAnimalTarget target)
    {
        var sphere = FindFirstChildInteractable(target.gameObject);
        if (sphere == null) return; // no proxy: leave the root interactable as-is

        var rootInteractable = target.GetComponent<XRSimpleInteractable>();
        if (rootInteractable != null && rootInteractable.enabled)
            rootInteractable.enabled = false; // OnDisable unregisters it, freeing the collider

        // Re-register the Sphere so it (re)claims its collider in the now-free mapping.
        if (sphere.enabled)
        {
            sphere.enabled = false;
            sphere.enabled = true;
        }
    }

    private static XRSimpleInteractable FindFirstChildInteractable(GameObject root)
    {
        var all = root.GetComponentsInChildren<XRSimpleInteractable>(true);
        foreach (var i in all)
            if (i.gameObject != root) return i;
        return null;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (uiController == null) return;
        args.interactableObject.transform.GetComponentInParent<PokedexAnimalTarget>()?.Reveal(uiController);
    }
}
