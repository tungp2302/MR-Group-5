using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach to an animal whose interaction "Sphere" proxy is offset from the visible model
// (e.g. the Toad). On start it shifts the Sphere's parent (HoverSystem) so the Sphere —
// and its hover particle — sit on the model's actual renderer bounds center. Optionally
// resizes the Sphere collider/mesh to roughly enclose the model.
public class PokedexInteractionSphereAligner : MonoBehaviour
{
    [Tooltip("The animal's visible renderer. Leave empty to auto-pick the largest non-sphere renderer.")]
    [SerializeField] private Renderer targetRenderer;

    [Tooltip("Re-center every frame instead of once (use if the model wanders far from the sphere).")]
    [SerializeField] private bool continuous = false;

    [Tooltip("Scale the Sphere so its world radius is this fraction of the model's largest bounds extent.")]
    [SerializeField] private bool matchSize = true;
    [SerializeField, Range(0.1f, 1.5f)] private float sizeFraction = 0.6f;

    private Transform _sphere;        // the XRSimpleInteractable proxy
    private Transform _hoverSystem;   // parent that carries sphere + particle
    private SphereCollider _sphereCollider;

    private IEnumerator Start()
    {
        yield return null; // let the model/animator settle for one frame

        var interactable = FindFirstChildInteractable(gameObject);
        if (interactable == null)
        {
            Debug.LogWarning($"[SphereAligner] No child XRSimpleInteractable on '{name}'.");
            yield break;
        }

        _sphere = interactable.transform;
        _hoverSystem = _sphere.parent != null ? _sphere.parent : _sphere;
        _sphereCollider = interactable.GetComponent<SphereCollider>();

        if (targetRenderer == null)
            targetRenderer = FindModelRenderer();

        if (targetRenderer == null)
        {
            Debug.LogWarning($"[SphereAligner] No model renderer found on '{name}'.");
            yield break;
        }

        Align();
        if (matchSize) ApplySize();

        if (continuous)
            while (true) { Align(); yield return null; }
    }

    private void Align()
    {
        var center = targetRenderer.bounds.center;
        // Move the HoverSystem so the Sphere lands on the model center (keeps particle in sync).
        _hoverSystem.position += center - _sphere.position;
    }

    private void ApplySize()
    {
        if (_sphereCollider == null) return;
        var extents = targetRenderer.bounds.extents;
        float worldRadius = Mathf.Max(extents.x, extents.y, extents.z) * sizeFraction;
        // Convert desired world radius into the collider's local radius via its lossy scale.
        var ls = _sphere.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
        if (maxScale > 0.0001f)
            _sphereCollider.radius = worldRadius / maxScale;
    }

    private Renderer FindModelRenderer()
    {
        Renderer best = null;
        float bestSize = -1f;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            // Skip the Sphere proxy's own mesh and any particle renderers.
            if (r is ParticleSystemRenderer) continue;
            if (r.GetComponentInParent<XRSimpleInteractable>() != null) continue;

            float size = r.bounds.size.sqrMagnitude;
            if (size > bestSize) { bestSize = size; best = r; }
        }
        return best;
    }

    private static XRSimpleInteractable FindFirstChildInteractable(GameObject root)
    {
        foreach (var i in root.GetComponentsInChildren<XRSimpleInteractable>(true))
            if (i.gameObject != root) return i;
        return null;
    }
}
