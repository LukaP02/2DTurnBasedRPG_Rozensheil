using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Full-screen black overlay used for scene/panel transitions - singleton like AudioManager,
// meant to sit as the very last sibling in the Canvas so it draws above everything else.
// ScreenFader.Transition(action) is the main entry point: fades to black, runs action while
// fully black (swap panels/screens here), then fades back in. Safe to call even if no
// ScreenFader exists in the scene - it just runs the action immediately with no fade.
//
// Transitions are allowed to overlap (a betweenAction commonly triggers another transition
// itself, e.g. an event ending into a dialogue starting) - each running fade is tracked via
// activeTransitions rather than a single coroutine reference, so raycastTarget only clears once
// every overlapping fade has actually finished. Never StopCoroutine a fade here - killing one
// mid-flight is what left raycastTarget stuck true (blocking all clicks) in an earlier version.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    public Image fadeImage;
    public float defaultFadeOutSeconds = 0.3f;
    public float defaultFadeInSeconds = 0.3f;

    private int activeTransitions;

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

        SetAlpha(1f);
        StartCoroutine(FadeThenCallback(1f, 0f, seconds >= 0f ? seconds : defaultFadeInSeconds, onComplete));
    }

    public void FadeOutThenIn(Action betweenAction, float outSeconds = -1f, float inSeconds = -1f)
    {
        if (fadeImage == null)
        {
            betweenAction?.Invoke();
            return;
        }

        StartCoroutine(FadeOutThenInRoutine(
            betweenAction,
            outSeconds >= 0f ? outSeconds : defaultFadeOutSeconds,
            inSeconds >= 0f ? inSeconds : defaultFadeInSeconds));
    }

    private IEnumerator FadeOutThenInRoutine(Action betweenAction, float outSeconds, float inSeconds)
    {
        activeTransitions++;
        fadeImage.raycastTarget = true; // block clicks on whatever's underneath while fading

        yield return Fade(fadeImage.color.a, 1f, outSeconds);

        betweenAction?.Invoke();

        yield return Fade(1f, 0f, inSeconds);

        activeTransitions--;
        if (activeTransitions <= 0)
        {
            activeTransitions = 0;
            fadeImage.raycastTarget = false;
        }
    }

    private IEnumerator FadeThenCallback(float from, float to, float seconds, Action onComplete)
    {
        yield return Fade(from, to, seconds);
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