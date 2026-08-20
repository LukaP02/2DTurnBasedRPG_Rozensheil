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

    [Header("Typewriter Effect")]
    public float typewriterSecondsPerChar = 0.02f;

    private DialogueSequence currentSequence;
    private int currentLineIndex;
    private bool isActive;
    private bool isTyping;
    private Coroutine typewriterCoroutine;

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

        dialoguePanel.SetActive(true);
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
            portraitImage.sprite = line.speakerPortrait;

            // With no sprite assigned, an Image still renders its flat fill color as a solid box -
            // hide it entirely instead by zeroing its alpha (restored to opaque once a portrait is set).
            Color portraitColor = portraitImage.color;
            portraitColor.a = line.speakerPortrait != null ? 1f : 0f;
            portraitImage.color = portraitColor;
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

        isActive = false;
        isTyping = false;
        currentSequence = null;
        dialoguePanel.SetActive(false);

        OnDialogueEnded?.Invoke();
    }
}