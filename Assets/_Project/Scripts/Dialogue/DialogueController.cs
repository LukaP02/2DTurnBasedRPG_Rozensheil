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

    [Header("Typewriter Effect")]
    public float typewriterSecondsPerChar = 0.02f;

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
    }

    // suppressBackground: pass true for dialogue shown over a scene that should stay visible
    // underneath (mid-battle, phase-transition) - the dialogue's own backgroundImage art is
    // hidden and only the darken overlay dims things, instead of covering the arena entirely.
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

        boxFadeCoroutine = StartCoroutine(FadeImagesAlpha(dialogueBoxArtImages, 1f, boxFadeSeconds));

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
            // Only replay the fade when the portrait actually changes, so the same speaker
            // talking across consecutive lines doesn't flicker every line.
            if (line.speakerPortrait != lastPortraitSprite)
            {
                portraitImage.sprite = line.speakerPortrait;

                float targetAlpha = line.speakerPortrait != null ? 1f : 0f;

                if (portraitFadeCoroutine != null)
                    StopCoroutine(portraitFadeCoroutine);

                // Snap to fully transparent first so there's actually something to fade *from* -
                // interpolating from whatever alpha it already happened to be at (usually 1) is invisible.
                Color startColor = portraitImage.color;
                startColor.a = 0f;
                portraitImage.color = startColor;

                portraitFadeCoroutine = StartCoroutine(FadeImageAlpha(portraitImage, targetAlpha, portraitFadeSeconds));

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

    private IEnumerator FadeImagesAlpha(Image[] images, float targetAlpha, float duration)
    {
        if (images == null || images.Length == 0) yield break;

        foreach (var image in images)
        {
            if (image == null) continue;
            Color c = image.color;
            c.a = 0f;
            image.color = c;
        }

        float elapsed = 0f;

        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, targetAlpha, elapsed / duration);

            foreach (var image in images)
            {
                if (image == null) continue;
                Color c = image.color;
                c.a = alpha;
                image.color = c;
            }

            yield return null;
        }

        foreach (var image in images)
        {
            if (image == null) continue;
            Color c = image.color;
            c.a = targetAlpha;
            image.color = c;
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