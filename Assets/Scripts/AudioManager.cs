using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private AudioSource audioSource;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();

        float volume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        audioSource.volume = volume;
    }

    public void SetVolume(float volume)
    {
        audioSource.volume = volume;

        PlayerPrefs.SetFloat("MusicVolume", volume);
        PlayerPrefs.Save();
    }
}