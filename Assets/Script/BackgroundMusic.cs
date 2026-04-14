using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        AudioSource source = GetComponent<AudioSource>();
        if (!source.isPlaying)
        {
            source.Play();
        }
    }
}
