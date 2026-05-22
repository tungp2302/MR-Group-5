using UnityEngine;
using System.Collections;

public class Sound : MonoBehaviour
{
    public AudioClip sound;
    public float minInterval = 100f;
    public float maxInterval = 200f;

    private AudioSource audioSource;
    private Coroutine soundCoroutine;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (soundCoroutine != null)
            StopCoroutine(soundCoroutine);

        soundCoroutine = StartCoroutine(SoundRoutine());
    }

    private IEnumerator SoundRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);
            MakeSound();
        }
    }

    void MakeSound()
    {
        audioSource.PlayOneShot(sound, 1);
    }

}
