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
    private const float DEFAULT_VOLUME = 0.8f;

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
    }

    // AudioMixer.SetFloat calls made from Awake can silently no-op - the mixer's audio graph
    // isn't guaranteed to be fully initialized that early, especially right after entering Play
    // mode. Applying the loaded volume here instead (a frame later, guaranteed after every
    // object's Awake) is what actually makes the restored value take effect on the mixer instead
    // of it staying at the mixer's baked-in Editor default until something else changes it.
    private void Start()
    {
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

    // Hearing perceives loudness roughly logarithmically, so the slider should move in equal
    // decibel steps, not equal amplitude steps - map it linearly across the dB range instead of
    // converting a linear amplitude value via log10 (that alternative crams almost the whole
    // useful range into the bottom ~10% of the slider, since log10(0.5)*20 is only -6dB).
    private const float MIN_VOLUME_DB = -80f;
    private const float MAX_VOLUME_DB = 0f;

    private void SetMixerVolume(string parameterName, float linearValue)
    {
        if (audioMixer == null) return;

        float dB = Mathf.Lerp(MIN_VOLUME_DB, MAX_VOLUME_DB, linearValue);
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
    // Fades whatever's currently playing out to silence instead of crossfading into a new track.
    public void StopMusic()
    {
        currentMusicClip = null;

        if (crossfadeRoutine != null) StopCoroutine(crossfadeRoutine);
        crossfadeRoutine = StartCoroutine(FadeOutMusicRoutine());
    }

    private IEnumerator FadeOutMusicRoutine()
    {
        AudioSource outgoing = activeMusicSource;
        float startVolume = outgoing.volume;
        float elapsed = 0f;

        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = musicCrossfadeDuration > 0f ? elapsed / musicCrossfadeDuration : 1f;
            outgoing.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        outgoing.Stop();
        outgoing.volume = 1f;
        crossfadeRoutine = null;
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
    // --- SFX ---
    // Cycles through a pool so overlapping calls (e.g. two hits in the same frame) don't cut each other off.
    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f);
    }

    // Pitch overload - used by the typewriter blip (EventController/DialogueController) to vary
    // each character's blip slightly so a fast, repeated sound doesn't sound like a machine gun.
    public void PlaySFX(AudioClip clip, float pitch)
    {
        if (clip == null || sfxPool.Count == 0) return;

        AudioSource source = sfxPool[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Count;

        source.pitch = pitch;
        source.PlayOneShot(clip);
    }
}
