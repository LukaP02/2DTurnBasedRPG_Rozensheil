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
    private Sprite lastPortraitSprite;

    public event Action OnDialogueEnded;

    private void Awake()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Length == 0)
        {
            OnDialogueEnded?.Invoke(); // nothing to show, immediately signal done
            return;
        }

        currentSequence = sequence;
        currentLineIndex = 0;
        isActive = true;
        lastPortraitSprite = null;

        dialoguePanel.SetActive(true);

        if (boxFadeCoroutine != null)
            StopCoroutine(boxFadeCoroutine);

        boxFadeCoroutine = StartCoroutine(FadeImagesAlpha(dialogueBoxArtImages, 1f, boxFadeSeconds));

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

        isActive = false;
        isTyping = false;
        currentSequence = null;
        dialoguePanel.SetActive(false);

        OnDialogueEnded?.Invoke();
    }
}