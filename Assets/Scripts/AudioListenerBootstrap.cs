using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioListenerBootstrap : MonoBehaviour
{
    private static AudioListenerBootstrap instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBootstrapExists()
    {
        if (instance != null)
        {
            return;
        }

        var existing = FindObjectOfType<AudioListenerBootstrap>();
        if (existing != null)
        {
            instance = existing;
            return;
        }

        var bootstrapObject = new GameObject(nameof(AudioListenerBootstrap));
        instance = bootstrapObject.AddComponent<AudioListenerBootstrap>();
        DontDestroyOnLoad(bootstrapObject);
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureListenerPresent();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureListenerPresent();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureListenerPresent();
    }

    private static void EnsureListenerPresent()
    {
        var listeners = FindObjectsOfType<AudioListener>(true);
        if (listeners == null || listeners.Length == 0)
        {
            var listenerObject = new GameObject("AudioListener");
            listenerObject.transform.SetParent(instance != null ? instance.transform : null, false);
            listenerObject.AddComponent<AudioListener>();
            DontDestroyOnLoad(listenerObject);
            return;
        }

        var firstEnabledListenerFound = false;
        foreach (var listener in listeners)
        {
            if (listener == null)
            {
                continue;
            }

            if (!firstEnabledListenerFound && listener.enabled)
            {
                firstEnabledListenerFound = true;
                continue;
            }

            if (!firstEnabledListenerFound && !listener.enabled)
            {
                listener.enabled = true;
                firstEnabledListenerFound = true;
                continue;
            }

            listener.enabled = false;
        }
    }
}
