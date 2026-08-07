using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EventController : MonoBehaviour
{
    [Header("Prompt UI")]
    public GameObject eventPanel;
    public Image eventImage;
    public TMP_Text titleText;
    public TMP_Text descriptionText;
    public Transform choiceButtonContainer;
    public GameObject choiceButtonPrefab;

    [Header("Outcome UI")]
    public GameObject outcomePanel;
    public TMP_Text outcomeText;
    public Button outcomeContinueButton;

    [Header("Party reference (for HP effects)")]
    public List<CharacterInstance> currentParty;

    private EventData currentEvent;

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
            OnEventClosed?.Invoke(); // nothing to show, immediately signal done
            return;
        }

        currentEvent = eventData;

        titleText.text = eventData.title;
        descriptionText.text = eventData.description;

        if (eventImage != null)
            eventImage.sprite = eventData.image;

        PopulateChoices();

        eventPanel.SetActive(true);
        outcomePanel.SetActive(false);
    }

    private void PopulateChoices()
    {
        foreach (Transform child in choiceButtonContainer)
            Destroy(child.gameObject);

        foreach (var choice in currentEvent.choices)
        {
            GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.choiceText;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => SelectChoice(choice));
        }
    }

    private void SelectChoice(EventChoice choice)
    {
        ApplyEffects(choice);

        eventPanel.SetActive(false);
        outcomeText.text = choice.outcomeText;
        outcomePanel.SetActive(true);
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
        }
    }

    public void CloseOutcome()
    {
        outcomePanel.SetActive(false);
        currentEvent = null;

        OnEventClosed?.Invoke();
    }
}