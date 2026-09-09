using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer")]
    [Tooltip("Mixer exposing two float parameters named exactly \"MusicVolume\" and \"SFXVolume\" (in decibels).")]
    public AudioMixer audioMixer;
    [Tooltip("The mixer's Music child group - music audio sources are routed through it.")]
    public AudioMixerGroup musicMixerGroup;
    [Tooltip("The mixer's SFX child group - pooled SFX audio sources are routed through it.")]
    public AudioMixerGroup sfxMixerGroup;

    [Header("Music")]
    public float musicCrossfadeDuration = 1f;

    [Header("SFX Pool")]
    public int sfxPoolSize = 8;

    private const string MUSIC_MIXER_PARAM = "MusicVolume";
    private const string SFX_MIXER_PARAM = "SFXVolume";
    private const string MUSIC_PREF_KEY = "MusicVolume";
    private const string SFX_PREF_KEY = "SFXVolume";
    private const float DEFAULT_VOLUME = 0.5f;

    public float MusicVolume { get; private set; }
    public float SFXVolume { get; private set; }

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource activeMusicSource;
    private AudioClip currentMusicClip;
    private Coroutine crossfadeRoutine;

    private List<AudioSource> sfxPool = new List<AudioSource>();
    private int nextSfxIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSourceA = CreateSource(musicMixerGroup, loop: true);
        musicSourceB = CreateSource(musicMixerGroup, loop: true);
        activeMusicSource = musicSourceA;

        for (int i = 0; i < sfxPoolSize; i++)
            sfxPool.Add(CreateSource(sfxMixerGroup, loop: false));

        MusicVolume = PlayerPrefs.GetFloat(MUSIC_PREF_KEY, DEFAULT_VOLUME);
        SFXVolume = PlayerPrefs.GetFloat(SFX_PREF_KEY, DEFAULT_VOLUME);

        ApplyMusicVolume();
        ApplySFXVolume();
    }

    private AudioSource CreateSource(AudioMixerGroup group, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.outputAudioMixerGroup = group;
        source.loop = loop;
        source.playOnAwake = false;
        return source;
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

    // --- Music ---
    // Crossfades from whatever's currently playing into clip; no-ops if it's already the active track.
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || clip == currentMusicClip) return;

        currentMusicClip = clip;

        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(CrossfadeMusicRoutine(clip));
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip clip)
    {
        AudioSource incoming = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
        AudioSource outgoing = activeMusicSource;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        float elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = musicCrossfadeDuration > 0f ? elapsed / musicCrossfadeDuration : 1f;
            incoming.volume = t;
            outgoing.volume = 1f - t;
            yield return null;
        }

        incoming.volume = 1f;
        outgoing.Stop();
        outgoing.volume = 1f;

        activeMusicSource = incoming;
        crossfadeRoutine = null;
    }

    // --- SFX ---
    // Cycles through a pool so overlapping calls (e.g. two hits in the same frame) don't cut each other off.
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxPool.Count == 0) return;

        AudioSource source = sfxPool[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Count;

        source.PlayOneShot(clip);
    }
}