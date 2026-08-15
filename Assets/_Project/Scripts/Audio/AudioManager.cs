using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string VOLUME_PREF_KEY = "MasterVolume";
    private const float DEFAULT_VOLUME = 1f;

    public float MasterVolume { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat(VOLUME_PREF_KEY, DEFAULT_VOLUME);
        ApplyVolume();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyVolume();

        PlayerPrefs.SetFloat(VOLUME_PREF_KEY, MasterVolume);
        PlayerPrefs.Save();
    }

    private void ApplyVolume()
    {
        AudioListener.volume = MasterVolume;
    }
}