using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [Tooltip("Two sources so one track can fade out while the next fades in, instead of a hard cut. Both must be on this GameObject, Play On Awake off, Loop on.")]
    public AudioSource musicSourceA;
    public AudioSource musicSourceB;
    [Range(0f, 1f)] public float musicVolume = 0.6f;

    [Header("SFX")]
    [Tooltip("Pool size for simultaneous one-shot sound effects - built automatically at runtime, no manual setup needed.")]
    public int sfxPoolSize = 8;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    private const string VOLUME_PREF_KEY = "MasterVolume";
    private const float DEFAULT_VOLUME = 1f;

    public float MasterVolume { get; private set; }

    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;
    private AudioClip currentMusicClip;
    private Coroutine musicFadeCoroutine;

    private AudioSource[] sfxPool;
    private int nextSfxIndex;

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

        activeMusicSource = musicSourceA;
        inactiveMusicSource = musicSourceB;
        activeMusicSource.loop = true;
        inactiveMusicSource.loop = true;
        activeMusicSource.volume = musicVolume;
        inactiveMusicSource.volume = 0f;

        BuildSfxPool();
    }

    private void BuildSfxPool()
    {
        sfxPool = new AudioSource[Mathf.Max(1, sfxPoolSize)];
        for (int i = 0; i < sfxPool.Length; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            sfxPool[i] = src;
        }
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

    // Plays a one-shot SFX from the pool - round-robins across sfxPoolSize AudioSources so
    // several overlapping hits (e.g. an AoE) don't cut each other off the way a single shared
    // AudioSource.PlayOneShot can on retrigger.
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxPool == null || sfxPool.Length == 0) return;

        AudioSource src = sfxPool[nextSfxIndex];
        nextSfxIndex = (nextSfxIndex + 1) % sfxPool.Length;

        src.pitch = 1f;
        src.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    // Crossfades to newTrack over fadeSeconds. No-ops if newTrack is already playing, so callers
    // can call this on every scene/state change without worrying about restarting a track that's
    // already going (e.g. re-entering the overworld doesn't restart the overworld music).
    public void PlayMusic(AudioClip newTrack, float fadeSeconds = 1f)
    {
        if (newTrack == null || newTrack == currentMusicClip) return;

        currentMusicClip = newTrack;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(newTrack, fadeSeconds));
    }

    public void StopMusic(float fadeSeconds = 1f)
    {
        currentMusicClip = null;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(FadeOutMusicRoutine(fadeSeconds));
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip newTrack, float fadeSeconds)
    {
        inactiveMusicSource.clip = newTrack;
        inactiveMusicSource.volume = 0f;
        inactiveMusicSource.Play();

        float startActiveVolume = activeMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            float t = fadeSeconds > 0f ? Mathf.Clamp01(elapsed / fadeSeconds) : 1f;

            activeMusicSource.volume = Mathf.Lerp(startActiveVolume, 0f, t);
            inactiveMusicSource.volume = Mathf.Lerp(0f, musicVolume, t);
            yield return null;
        }

        activeMusicSource.volume = 0f;
        activeMusicSource.Stop();
        inactiveMusicSource.volume = musicVolume;

        AudioSource swap = activeMusicSource;
        activeMusicSource = inactiveMusicSource;
        inactiveMusicSource = swap;

        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutMusicRoutine(float fadeSeconds)
    {
        float startVolume = activeMusicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.deltaTime;
            float t = fadeSeconds > 0f ? Mathf.Clamp01(elapsed / fadeSeconds) : 1f;
            activeMusicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        activeMusicSource.volume = 0f;
        activeMusicSource.Stop();
        musicFadeCoroutine = null;
    }
}