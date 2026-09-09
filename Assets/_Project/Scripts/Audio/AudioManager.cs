using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Tooltip("Mixer exposing two float parameters named exactly \"MusicVolume\" and \"SFXVolume\" (in decibels) - see the Music/SFX child groups.")]
    public AudioMixer audioMixer;

    private const string MUSIC_MIXER_PARAM = "MusicVolume";
    private const string SFX_MIXER_PARAM = "SFXVolume";

    private const string MUSIC_PREF_KEY = "MusicVolume";
    private const string SFX_PREF_KEY = "SFXVolume";

    // Linear 0-1 slider value. Lower than the old 1f default so a fresh install isn't blasting.
    private const float DEFAULT_VOLUME = 0.5f;

    public float MusicVolume { get; private set; }
    public float SFXVolume { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        MusicVolume = PlayerPrefs.GetFloat(MUSIC_PREF_KEY, DEFAULT_VOLUME);
        SFXVolume = PlayerPrefs.GetFloat(SFX_PREF_KEY, DEFAULT_VOLUME);

        ApplyMusicVolume();
        ApplySFXVolume();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();

        PlayerPrefs.SetFloat(MUSIC_PREF_KEY, MusicVolume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        ApplySFXVolume();

        PlayerPrefs.SetFloat(SFX_PREF_KEY, SFXVolume);
        PlayerPrefs.Save();
    }

    private void ApplyMusicVolume() => SetMixerVolume(MUSIC_MIXER_PARAM, MusicVolume);
    private void ApplySFXVolume() => SetMixerVolume(SFX_MIXER_PARAM, SFXVolume);

    // A 0-1 slider isn't perceptually linear on a mixer fader, so convert to decibels; treat
    // near-zero as -80dB (silent) instead of letting log10(0) blow up to -infinity.
    private void SetMixerVolume(string parameterName, float linearValue)
    {
        if (audioMixer == null) return;

        float dB = linearValue > 0.0001f ? Mathf.Log10(linearValue) * 20f : -80f;
        audioMixer.SetFloat(parameterName, dB);
    }
}