using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Full-screen black overlay used for scene/panel transitions - singleton like AudioManager,
// meant to sit as the very last sibling in the Canvas so it draws above everything else.
// ScreenFader.Transition(action) is the main entry point: fades to black, runs action while
// fully black (swap panels/screens here), then fades back in. Safe to call even if no
// ScreenFader exists in the scene - it just runs the action immediately with no fade.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    public Image fadeImage;
    public float defaultFadeOutSeconds = 0.3f;
    public float defaultFadeInSeconds = 0.3f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (fadeImage != null)
        {
            SetAlpha(0f);
            fadeImage.raycastTarget = false;
        }
    }

    // Fades to black, runs betweenAction, then fades back in. Static convenience so call sites
    // don't need a null-check for Instance - falls back to just running the action with no fade.
    public static void Transition(Action betweenAction, float outSeconds = -1f, float inSeconds = -1f)
    {
        if (Instance != null)
            Instance.FadeOutThenIn(betweenAction, outSeconds, inSeconds);
        else
            betweenAction?.Invoke();
    }

    // Fades in from black (alpha 1 -> 0) - call once at game start, after whatever's behind it
    // is already set up and ready to show.
    public void FadeInFromBlack(float seconds = -1f, Action onComplete = null)
    {
        if (fadeImage == null) { onComplete?.Invoke(); return; }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        SetAlpha(1f);
        fadeRoutine = StartCoroutine(FadeThenCallback(1f, 0f, seconds >= 0f ? seconds : defaultFadeInSeconds, onComplete));
    }

    public void FadeOutThenIn(Action betweenAction, float outSeconds = -1f, float inSeconds = -1f)
    {
        if (fadeImage == null)
        {
            betweenAction?.Invoke();
            return;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        // Already fully black (e.g. a transition triggered from inside another one's
        // betweenAction) - skip the redundant fade-out so nested calls don't sit on black
        // longer than necessary.
        float actualOutSeconds = fadeImage.color.a >= 1f ? 0f : (outSeconds >= 0f ? outSeconds : defaultFadeOutSeconds);
        float actualInSeconds = inSeconds >= 0f ? inSeconds : defaultFadeInSeconds;

        fadeRoutine = StartCoroutine(FadeOutThenInRoutine(betweenAction, actualOutSeconds, actualInSeconds));
    }

    private IEnumerator FadeOutThenInRoutine(Action betweenAction, float outSeconds, float inSeconds)
    {
        fadeImage.raycastTarget = true; // block clicks on whatever's underneath while fading

        yield return Fade(fadeImage.color.a, 1f, outSeconds);

        betweenAction?.Invoke();

        yield return Fade(1f, 0f, inSeconds);
        fadeImage.raycastTarget = false;

        fadeRoutine = null;
    }

    private IEnumerator FadeThenCallback(float from, float to, float seconds, Action onComplete)
    {
        yield return Fade(from, to, seconds);
        fadeRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float elapsed = 0f;

        while (seconds > 0f && elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, to, elapsed / seconds));
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        Color c = fadeImage.color;
        c.a = alpha;
        fadeImage.color = c;
    }
}