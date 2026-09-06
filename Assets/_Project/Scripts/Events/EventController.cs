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

    [NonSerialized] public List<CharacterInstance> currentParty;

    private EventData currentEvent;
    private Coroutine promptTypewriterCoroutine;
    private Coroutine outcomeTypewriterCoroutine;

    public event Action OnEventClosed;

    private void Awake()
    {
        if (eventPanel != null) eventPanel.SetActive(false);
        if (outcomePanel != null) outcomePanel.SetActive(false);
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

        if (promptTypewriterCoroutine != null)
            StopCoroutine(promptTypewriterCoroutine);

        promptTypewriterCoroutine = StartCoroutine(PlayPromptTypewriter(eventData.title, eventData.description));
    }

    // Types the title, then the description, one after the other, then reveals the choices -
    // matches the "read the setup before you're asked to decide" pacing of the outcome panel below.
    private IEnumerator PlayPromptTypewriter(string title, string description)
    {
        yield return TypewriterReveal(titleText, title);
        yield return TypewriterReveal(descriptionText, description);

        if (choiceButtonContainer != null)
            choiceButtonContainer.gameObject.SetActive(true);

        promptTypewriterCoroutine = null;
    }

    // Setting the full string up front lets TMP compute word-wrap once, so the layout never
    // shifts mid-reveal; only maxVisibleCharacters changes as the line types out.
    private IEnumerator TypewriterReveal(TMP_Text target, string fullText)
    {
        target.text = fullText;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate();

        int totalChars = target.textInfo.characterCount;

        for (int i = 0; i <= totalChars; i++)
        {
            target.maxVisibleCharacters = i;
            yield return new WaitForSeconds(typewriterSecondsPerChar);
        }
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

        if (outcomeTypewriterCoroutine != null)
            StopCoroutine(outcomeTypewriterCoroutine);

        outcomeTypewriterCoroutine = StartCoroutine(PlayOutcomeTypewriter(choice.outcomeText));
    }

    private IEnumerator PlayOutcomeTypewriter(string text)
    {
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