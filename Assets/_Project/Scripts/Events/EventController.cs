using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventController : MonoBehaviour
{
    [Header("Prompt UI")]
    public GameObject eventPanel;
    public Image backgroundImage;
    public Image eventImage;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;

    [Header("Outcome UI")]
    public GameObject outcomePanel;
    public TMP_Text outcomeText;
    public Button outcomeContinueButton;

    [Header("Typewriter Effect")]
    [Tooltip("Title, then Description, type out on the prompt panel (choices stay hidden until both finish); Outcome Text does the same on the outcome panel (Continue button stays hidden until it finishes).")]
    public float typewriterSecondsPerChar = 0.02f;
    [Tooltip("Blank pause before each panel starts typing - one beat before the prompt panel's title, and one beat before the outcome panel's text.")]
    public float delayBeforeTypewriter = 1f;
    [Tooltip("Played once per revealed character while text types out (skipped for whitespace). Leave empty for a silent typewriter.")]
    public AudioClip typewriterBlipSound;
    [Tooltip("Base pitch the blip plays at - turn this down (e.g. 0.7) if your clip sounds too high-pitched.")]
    [Range(0.1f, 2f)] public float typewriterBlipBasePitch = 1f;
    [Tooltip("Each blip's pitch is randomized by +/- this much around the base pitch (0.15 = +/-15%) so a fast repeated sound doesn't sound mechanical.")]
    [Range(0f, 0.5f)] public float typewriterBlipPitchVariance = 0.15f;

    [NonSerialized] public List<CharacterInstance> currentParty;

    private EventData currentEvent;
    private Coroutine promptTypewriterCoroutine;
    private Coroutine outcomeTypewriterCoroutine;

    public event Action OnEventClosed;

    private bool isTyping;
    private bool skipTypewriter;

    // Dedicated AudioSource rather than routing through AudioManager's shared SFX pool - lets each
    // new blip cut off the previous one (Stop() in PlayTypewriterBlip) instead of letting several
    // overlapping copies ring out and stack into a wall of sound that outlasts the visible typing.
    private AudioSource typewriterBlipSource;

    private void Awake()
    {
        if (eventPanel != null) eventPanel.SetActive(false);
        if (outcomePanel != null) outcomePanel.SetActive(false);

        typewriterBlipSource = gameObject.AddComponent<AudioSource>();
        typewriterBlipSource.playOnAwake = false;
        if (AudioManager.Instance != null)
            typewriterBlipSource.outputAudioMixerGroup = AudioManager.Instance.sfxMixerGroup;
    }

    public void StartEvent(EventData eventData)
    {
        if (eventData == null)
        {
            OnEventClosed?.Invoke();
            return;
        }

        currentEvent = eventData;

        if (eventImage != null)
            eventImage.sprite = eventData.image;

        if (backgroundImage != null && eventData.backgroundImage != null)
            backgroundImage.sprite = eventData.backgroundImage;

        // Choices are built now (so IsChoiceAvailable reads current state right away) but stay
        // hidden until the title/description typewriter below finishes.
        PopulateChoices();
        if (choiceButtonContainer != null)
            choiceButtonContainer.gameObject.SetActive(false);

        eventPanel.SetActive(true);
        outcomePanel.SetActive(false);

        // Blank both fields immediately (synchronously, before the coroutine's pause even starts)
        // so whatever the previous event left in them can't be visible during delayBeforeTypewriter -
        // clearing this only once the coroutine reaches TypewriterReveal was too late, since that
        // happened after the pause instead of before it.
        ClearText(titleText);
        ClearText(descriptionText);

        if (promptTypewriterCoroutine != null)
            StopCoroutine(promptTypewriterCoroutine);

        promptTypewriterCoroutine = StartCoroutine(PlayPromptTypewriter(eventData.title, eventData.description));
    }

    // Types the title, then the description, one after the other, then reveals the choices -
    // matches the "read the setup before you're asked to decide" pacing of the outcome panel below.
    // The blank pause only happens once, before the title - both fields are already cleared by the
    // time this starts (see StartEvent), so there's no leftover-text moment left to cover before
    // the description and a second pause there would just double the wait for no benefit.
    private IEnumerator PlayPromptTypewriter(string title, string description)
    {
        yield return new WaitForSeconds(delayBeforeTypewriter);

        yield return TypewriterReveal(titleText, title);
        yield return TypewriterReveal(descriptionText, description);

        if (choiceButtonContainer != null)
            choiceButtonContainer.gameObject.SetActive(true);

        promptTypewriterCoroutine = null;
    }

    // Blanks a field and forces the mesh to rebuild immediately - without ForceMeshUpdate, TMP
    // defers the actual rebuild to the next Canvas pass, so simply assigning .text = "" can still
    // leave the previous string rendered on screen for a moment.
    private void ClearText(TMP_Text target)
    {
        if (target == null) return;

        target.text = string.Empty;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate();
    }

    // Setting the full string up front lets TMP compute word-wrap once, so the layout never
    // shifts mid-reveal; only maxVisibleCharacters changes as the line types out. Caller is
    // expected to have already blanked target (see ClearText) before any pause happens.
    private IEnumerator TypewriterReveal(TMP_Text target, string fullText)
    {
        target.text = fullText;
        target.maxVisibleCharacters = 0; // re-assert - assigning .text can reset this on its own
        target.ForceMeshUpdate();

        int totalChars = target.textInfo.characterCount;
        isTyping = true;
        skipTypewriter = false;

        for (int i = 0; i <= totalChars; i++)
        {
            if (skipTypewriter)
            {
                target.maxVisibleCharacters = totalChars;
                break;
            }

            target.maxVisibleCharacters = i;

            // i - 1 is the character that just became visible this step (i == 0 reveals nothing
            // yet). Read it from textInfo rather than fullText directly since TMP rich text tags
            // (e.g. <b>) are stripped from textInfo's indexing but not from the raw string.
            if (i > 0 && !char.IsWhiteSpace(target.textInfo.characterInfo[i - 1].character))
                PlayTypewriterBlip();

            yield return new WaitForSeconds(typewriterSecondsPerChar);
        }

        isTyping = false;
        skipTypewriter = false;

        // Cut the blip off the instant typing stops (whether it finished or was skipped) - without
        // this, whatever the clip's own natural length is keeps ringing out after the text is done.
        typewriterBlipSource?.Stop();
    }

    private void PlayTypewriterBlip()
    {
        if (typewriterBlipSound == null || typewriterBlipSource == null) return;

        // Stop the previous blip before starting the next one - back-to-back characters at a fast
        // typewriterSecondsPerChar would otherwise overlap several copies of the clip on top of
        // each other, which is what made it sound like it dragged on past the last character.
        typewriterBlipSource.Stop();
        typewriterBlipSource.pitch = typewriterBlipBasePitch + UnityEngine.Random.Range(-typewriterBlipPitchVariance, typewriterBlipPitchVariance);
        typewriterBlipSource.clip = typewriterBlipSound;
        typewriterBlipSource.Play();
    }

    // Click-to-fast-forward, same idea as DialogueController.AdvanceDialogue - completes whichever
    // text is currently typing instead of stopping the coroutine outright, so for the prompt panel
    // (title then description, sequenced through one outer coroutine) completing the title doesn't
    // kill the description that's supposed to type out next.
    public void AdvanceTypewriter()
    {
        if (isTyping)
            skipTypewriter = true;
    }

    private void PopulateChoices()
    {
        foreach (Transform child in choiceButtonContainer)
            Destroy(child.gameObject);

        foreach (var choice in currentEvent.choices)
        {
            if (!IsChoiceAvailable(choice)) continue;

            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.choiceText;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => SelectChoice(choice));
        }
    }

    // Hides a choice entirely (rather than showing it disabled) when picking it wouldn't make
    // sense right now: it grants something already owned (e.g. re-offering an item bought on an
    // earlier visit to the same vendor), or it costs more gold than the player currently has. A
    // choice with no granted items and no gold cost (e.g. a "Leave" option) never gets hidden by
    // either check, so as long as an event has one, it can never end up with zero choices shown.
    private bool IsChoiceAvailable(EventChoice choice)
    {
        if (PartyManager.Instance == null) return true;

        if (choice.grantItems != null)
        {
            foreach (var item in choice.grantItems)
            {
                if (item != null && PartyManager.Instance.OwnsItem(item))
                    return false;
            }
        }

        if (choice.goldChange < 0 && PartyManager.Instance.Gold < -choice.goldChange)
            return false;

        return true;
    }

    private void SelectChoice(EventChoice choice)
    {
        ApplyEffects(choice);

        eventPanel.SetActive(false);

        // Continue stays hidden until the outcome text finishes typing, same idea as the choices above.
        if (outcomeContinueButton != null)
            outcomeContinueButton.gameObject.SetActive(false);

        outcomePanel.SetActive(true);

        // Same reasoning as StartEvent - blank immediately, before the pause, not after it.
        ClearText(outcomeText);

        if (outcomeTypewriterCoroutine != null)
            StopCoroutine(outcomeTypewriterCoroutine);

        outcomeTypewriterCoroutine = StartCoroutine(PlayOutcomeTypewriter(choice.outcomeText));
    }

    private IEnumerator PlayOutcomeTypewriter(string text)
    {
        yield return new WaitForSeconds(delayBeforeTypewriter);

        yield return TypewriterReveal(outcomeText, text);

        if (outcomeContinueButton != null)
            outcomeContinueButton.gameObject.SetActive(true);

        outcomeTypewriterCoroutine = null;
    }

    private void ApplyEffects(EventChoice choice)
    {
        if (choice.goldChange != 0 && PartyManager.Instance != null)
        {
            if (choice.goldChange > 0)
                PartyManager.Instance.AddGold(choice.goldChange);
            else
                PartyManager.Instance.SpendGold(-choice.goldChange);
        }

        if (choice.hpChangePercent != 0 && currentParty != null)
        {
            foreach (var character in currentParty)
            {
                int amount = Mathf.RoundToInt(character.maxHP * (choice.hpChangePercent / 100f));

                if (amount > 0) character.Heal(amount);
                else if (amount < 0) character.TakeDamage(-amount);
            }

            // Costing HP only matters if the very next node turns out to be combat - see
            // GameFlowManager.ProceedAfterIntro, which resolves this one way or the other for
            // every node the player enters.
            if (choice.hpChangePercent < 0 && PartyManager.Instance != null)
                PartyManager.Instance.MarkPendingEventPenalty();
        }
        if (choice.recruitCharacter != null && PartyManager.Instance != null)
        {
            PartyManager.Instance.RecruitCharacter(choice.recruitCharacter);
        }
        if (PartyManager.Instance != null)
        {
            // Cost first, then grant - matters if the same item somehow appears in both lists
            // (a straight swap), though that's not the expected use case.
            if (choice.costItems != null)
            {
                foreach (var item in choice.costItems)
                {
                    if (item != null) PartyManager.Instance.RemoveItem(item);
                }
            }

            if (choice.grantItems != null)
            {
                foreach (var item in choice.grantItems)
                {
                    if (item != null) PartyManager.Instance.GrantItem(item);
                }
            }
        }
    }


    public void CloseOutcome()
    {
        outcomePanel.SetActive(false);
        currentEvent = null;

        OnEventClosed?.Invoke();
    }
}