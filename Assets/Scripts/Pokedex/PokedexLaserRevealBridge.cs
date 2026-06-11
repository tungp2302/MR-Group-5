using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Attach to the same GameObject as XRRayInteractor.
// Animals need a PokedexAnimalTarget component and any XRBaseInteractable (e.g. XRSimpleInteractable)
// so the ray interactor can select them.
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

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (uiController == null)
        {
            Debug.LogWarning("[PokedexLaserRevealBridge] uiController is not assigned.", this);
            return;
        }

        args.interactableObject.transform.GetComponentInParent<PokedexAnimalTarget>()?.Reveal(uiController);
    }
}
