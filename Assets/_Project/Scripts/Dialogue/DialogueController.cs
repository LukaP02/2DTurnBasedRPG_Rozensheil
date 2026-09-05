using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dialoguePanel;
    public Image backgroundImage;
    public Image portraitImage;
    public TMP_Text speakerNameText;
    public TMP_Text dialogueText;
    [Tooltip("Optional. AspectRatioFitter on the same object as Portrait Image, set to Fit In Parent - keeps each portrait's original proportions (no stretching/squishing) instead of it filling the frame exactly. Its Aspect Ratio value is set from each sprite automatically; leave empty to skip.")]
    public AspectRatioFitter portraitAspectFitter;

    [Header("Darken Overlay")]
    [Tooltip("A full-screen black Image, first child of dialoguePanel (behind the box art), that dims whatever's behind the dialogue - e.g. the combat arena during mid-battle/phase-transition dialogue. Also blocks clicks to it while dialogue is up. Leave empty to skip.")]
    public Image darkenOverlay;
    [Range(0f, 1f)] public float darkenOverlayAlpha = 0.6f;

    [Header("Screen Flash")]
    [Tooltip("A full-screen Image, above everything else (dialogue box, darken overlay), used for a story-beat flash driven by a line's Flash Color. Leave empty to skip.")]
    public Image screenFlash;
    public float flashFadeInSeconds = 0.08f;
    public float flashHoldSeconds = 0.1f;
    public float flashFadeOutSeconds = 0.4f;

    [Header("Fade-In")]
    // Drag your box art pieces here (e.g. DialogueBoxArt, DialogueBoxOrnament, DialogueBoxOrnament (1), NameBoxArt).
    // They fade in once, together, when the dialogue box opens.
    public Image[] dialogueBoxArtImages;
    public float portraitFadeSeconds = 0.25f;
    public float boxFadeSeconds = 0.3f;
    [Tooltip("How far (UI units) the portrait and dialogue box art slide up from below while fading in - both on the initial dialogue open and on every speaker change.")]
    public float speakerSlideDistance = 30f;

    [Header("Typewriter Effect")]
    public float typewriterSecondsPerChar = 0.02f;

    [Header("Skip")]
    [Tooltip("Optional. Immediately ends the current dialogue sequence, skipping any remaining lines.")]
    public Button skipButton;

    private DialogueSequence currentSequence;
    private int currentLineIndex;
    private bool isActive;
    private bool isTyping;
    private Coroutine typewriterCoroutine;
    private Coroutine portraitFadeCoroutine;
    private Coroutine boxFadeCoroutine;
    private Coroutine darkenFadeCoroutine;
    private Coroutine flashCoroutine;
    private Sprite lastPortraitSprite;
    private bool suppressBackground;

    public event Action OnDialogueEnded;


    private void Awake()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // Must stay active at all times - visibility is controlled purely by alpha (faded by
        // StartDialogue/EndDialogue below), not by toggling the GameObject. If this object gets
        // switched off in the Editor (e.g. while reorganizing dialoguePanel's children), the alpha
        // fades still run but nothing renders, silently breaking the mid-battle/phase-transition darken.
        if (darkenOverlay != null)
        {
            darkenOverlay.gameObject.SetActive(true);
            Color c = darkenOverlay.color;
            c.a = 0f;
            darkenOverlay.color = c;
        }

        // Same reasoning as darkenOverlay above - stays active permanently, alpha 0 until a line
        // with a Flash Color plays.
        if (screenFlash != null)
        {
            screenFlash.gameObject.SetActive(true);
            Color c = screenFlash.color;
            c.a = 0f;
            screenFlash.color = c;
        }

        if (skipButton != null)
            skipButton.onClick.AddListener(SkipDialogue);
    }

    // suppressBackground: pass true for dialogue shown over a scene that should stay visible
    // underneath (mid-battle, phase-transition) - the dialogue's own backgroundImage art is
    // hidden and only the darken overlay dims things, instead of covering the arena entirely.
    public void SkipDialogue()
    {
        if (!isActive) return;

        EndDialogue();
    }

    public void StartDialogue(DialogueSequence sequence, bool suppressBackground = false)
    {
        if (sequence == null || sequence.lines.Length == 0)
        {
            OnDialogueEnded?.Invoke();
            return;
        }

        currentSequence = sequence;
        currentLineIndex = 0;
        isActive = true;
        lastPortraitSprite = null;
        this.suppressBackground = suppressBackground;

        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            Color portraitColor = portraitImage.color;
            portraitColor.a = 0f;
            portraitImage.color = portraitColor;
        }

        // Same reasoning as the portrait reset above: backgroundImage is shared too, so a
        // suppressed-background dialogue must explicitly hide it rather than trust "no line sets
        // one" to mean "stays blank" - it would otherwise keep showing whatever an earlier,
        // non-suppressed dialogue (e.g. this level's intro) last displayed.
        if (backgroundImage != null)
            backgroundImage.gameObject.SetActive(!suppressBackground);

        dialoguePanel.SetActive(true);
        dialoguePanel.transform.SetAsLastSibling();

        if (boxFadeCoroutine != null)
            StopCoroutine(boxFadeCoroutine);

        boxFadeCoroutine = StartCoroutine(FadeSlideImagesIn(dialogueBoxArtImages, boxFadeSeconds, speakerSlideDistance));

        // Only mid-battle/phase-transition dialogue (suppressBackground) darkens the scene behind
        // it - normal dialogue (intro, post-level) has its own background art and shouldn't dim.
        if (darkenOverlay != null && suppressBackground)
        {
            if (darkenFadeCoroutine != null)
                StopCoroutine(darkenFadeCoroutine);

            darkenFadeCoroutine = StartCoroutine(FadeImageAlpha(darkenOverlay, darkenOverlayAlpha, boxFadeSeconds));
        }

        ShowCurrentLine();
    }

    public void AdvanceDialogue()
    {
        if (!isActive) return;

        // First click completes the current line's reveal instead of skipping to the next line.
        if (isTyping)
        {
            CompleteCurrentLine();
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= currentSequence.lines.Length)
        {
            EndDialogue();
        }
        else
        {
            ShowCurrentLine();
        }
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = currentSequence.lines[currentLineIndex];

        speakerNameText.text = line.speakerName;

        if (portraitImage != null)
        {
            // Only replay the fade-slide when the portrait actually changes, so the same speaker
            // talking across consecutive lines doesn't flicker every line.
            if (line.speakerPortrait != lastPortraitSprite)
            {
                portraitImage.sprite = line.speakerPortrait;
                if (portraitAspectFitter != null && line.speakerPortrait != null)
                {
                    Rect r = line.speakerPortrait.rect;
                    portraitAspectFitter.aspectRatio = r.width / r.height;
                }

                if (portraitFadeCoroutine != null)
                    StopCoroutine(portraitFadeCoroutine);

                if (line.speakerPortrait != null)
                {
                    // New speaker appearing - Hades-style fade-slide-up, and the box art pops
                    // along with it on the same trigger.
                    portraitFadeCoroutine = StartCoroutine(FadeSlideImageIn(portraitImage, portraitFadeSeconds, speakerSlideDistance));

                    if (boxFadeCoroutine != null)
                        StopCoroutine(boxFadeCoroutine);

                    boxFadeCoroutine = StartCoroutine(FadeSlideImagesIn(dialogueBoxArtImages, boxFadeSeconds, speakerSlideDistance));
                }
                else
                {
                    // No portrait for this line - just fade out, nothing to slide.
                    portraitFadeCoroutine = StartCoroutine(FadeImageAlpha(portraitImage, 0f, portraitFadeSeconds));
                }

                lastPortraitSprite = line.speakerPortrait;
            }
        
    }

        // Leaving a line's backgroundImage empty keeps whatever background is already showing,
        // so a sequence doesn't need to repeat the same sprite on every line.
        if (backgroundImage != null && line.backgroundImage != null)
            backgroundImage.sprite = line.backgroundImage;

        if (typewriterCoroutine != null)
            StopCoroutine(typewriterCoroutine);

        if (!suppressBackground && backgroundImage != null && line.backgroundImage != null)
            backgroundImage.sprite = line.backgroundImage;

        if (screenFlash != null && line.flashColor.a > 0f)
            PlayFlash(line.flashColor, line.flashFadeInSeconds, line.flashHoldSeconds, line.flashFadeOutSeconds);

        typewriterCoroutine = StartCoroutine(TypewriterReveal(line.text));
    }

    // Setting the full string up front lets TMP compute word-wrap once, so the layout never
    // shifts mid-reveal; only maxVisibleCharacters changes as the line types out.
    private IEnumerator TypewriterReveal(string fullText)
    {
        isTyping = true;

        dialogueText.text = fullText;
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();

        int totalChars = dialogueText.textInfo.characterCount;

        for (int i = 0; i <= totalChars; i++)
        {
            dialogueText.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typewriterSecondsPerChar);
        }

        isTyping = false;
        typewriterCoroutine = null;
    }

    // Snaps the flash image to the line's color at alpha 0, fades in, holds briefly, then fades
    // back out - always ends transparent so it never lingers over the next line. Any override
    // left at -1 falls back to this DialogueController's default timing.
    private void PlayFlash(Color color, float fadeInOverride, float holdOverride, float fadeOutOverride)
    {
        screenFlash.transform.SetAsLastSibling(); // guarantee it renders above the dialogue box/darken overlay

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        float fadeIn = fadeInOverride >= 0f ? fadeInOverride : flashFadeInSeconds;
        float hold = holdOverride >= 0f ? holdOverride : flashHoldSeconds;
        float fadeOut = fadeOutOverride >= 0f ? fadeOutOverride : flashFadeOutSeconds;

        flashCoroutine = StartCoroutine(FlashRoutine(color, fadeIn, hold, fadeOut));
    }

    private IEnumerator FlashRoutine(Color color, float fadeIn, float hold, float fadeOut)
    {
        Color transparent = color;
        transparent.a = 0f;
        screenFlash.color = transparent;

        yield return FadeImageAlpha(screenFlash, color.a, fadeIn);
        yield return new WaitForSeconds(hold);
        yield return FadeImageAlpha(screenFlash, 0f, fadeOut);

        flashCoroutine = null;
    }
    private IEnumerator FadeImageAlpha(Image image, float targetAlpha, float duration)
    {
        Color color = image.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        // duration of 0 (or less) just snaps straight to the target alpha.
        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            image.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        image.color = color;
    }

    // Fades a single image in from alpha 0 to 1 while it slides up into its current (home)
    // position from slideDistance below - the Hades-style "pop" used for the speaker portrait.
    // Snaps to alpha 0 and the start offset first, so there's always something to animate
    // from regardless of the image's alpha/position when this is called.
    private IEnumerator FadeSlideImageIn(Image image, float duration, float slideDistance)
    {
        RectTransform rt = image.rectTransform;
        Vector2 home = rt.anchoredPosition;
        Vector2 start = home + new Vector2(0f, -slideDistance);

        Color color = image.color;
        color.a = 0f;
        image.color = color;
        rt.anchoredPosition = start;

        float elapsed = 0f;
        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = t * t * (3f - 2f * t);

            rt.anchoredPosition = Vector2.Lerp(start, home, eased);
            color.a = eased;
            image.color = color;
            yield return null;
        }

        rt.anchoredPosition = home;
        color.a = 1f;
        image.color = color;
    }

    // Same idea as FadeSlideImageIn, for a whole set of images at once (e.g. the dialogue box's
    // separate art pieces) - each keeps its own home position, so pieces placed differently on
    // screen all slide up by the same distance rather than converging on one spot.
    private IEnumerator FadeSlideImagesIn(Image[] images, float duration, float slideDistance)
    {
        if (images == null || images.Length == 0) yield break;

        var rects = new RectTransform[images.Length];
        var homes = new Vector2[images.Length];
        var starts = new Vector2[images.Length];

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;

            rects[i] = images[i].rectTransform;
            homes[i] = rects[i].anchoredPosition;
            starts[i] = homes[i] + new Vector2(0f, -slideDistance);

            Color c = images[i].color;
            c.a = 0f;
            images[i].color = c;
            rects[i].anchoredPosition = starts[i];
        }

        float elapsed = 0f;
        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = t * t * (3f - 2f * t);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null) continue;

                rects[i].anchoredPosition = Vector2.Lerp(starts[i], homes[i], eased);
                Color c = images[i].color;
                c.a = eased;
                images[i].color = c;
            }

            yield return null;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;

            rects[i].anchoredPosition = homes[i];
            Color c = images[i].color;
            c.a = 1f;
            images[i].color = c;
        }
    }

   
    

    private void CompleteCurrentLine()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
        isTyping = false;
    }

    private void EndDialogue()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (portraitFadeCoroutine != null)
        {
            StopCoroutine(portraitFadeCoroutine);
            portraitFadeCoroutine = null;
        }

        if (boxFadeCoroutine != null)
        {
            StopCoroutine(boxFadeCoroutine);
            boxFadeCoroutine = null;
        }

        if (darkenOverlay != null)
        {
            if (darkenFadeCoroutine != null)
                StopCoroutine(darkenFadeCoroutine);

            darkenFadeCoroutine = StartCoroutine(FadeImageAlpha(darkenOverlay, 0f, boxFadeSeconds));
        }

        isActive = false;
        isTyping = false;
        currentSequence = null;
        dialoguePanel.SetActive(false);

        OnDialogueEnded?.Invoke();
    }
}