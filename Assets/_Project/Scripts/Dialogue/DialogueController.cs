using System;
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

    private DialogueSequence currentSequence;
    private int currentLineIndex;
    private bool isActive;

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
        dialogueText.text = line.text;

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
    }

    private void EndDialogue()
    {
        isActive = false;
        currentSequence = null;
        dialoguePanel.SetActive(false);

        OnDialogueEnded?.Invoke();
    }
}