using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartySetupUI : MonoBehaviour
{
    [Header("All Playable Characters")]
    [Tooltip("Every character that can ever be in the party, in display order - including ones not yet recruited (they'll show up locked/grayed out until PartyManager.RecruitCharacter is called for them).")]
    public CharacterCardData[] allPlayableCharacters;

    [Header("Roster")]
    public Transform rosterContainer;
    public GameObject rosterEntryPrefab; // uses RosterCardUI

    [Header("Info")]
    public TMP_Text selectedCountText;

    private void OnEnable()
    {
        RefreshRoster();
    }

    private void RefreshRoster()
    {
        foreach (Transform child in rosterContainer)
            Destroy(child.gameObject);

        List<CharacterCardData> activeParty = PartyManager.Instance.GetActiveParty();
        List<CharacterCardData> fullRoster = PartyManager.Instance.GetFullRoster();

        foreach (var character in allPlayableCharacters)
        {
            if (character == null) continue;

            GameObject cardObj = Instantiate(rosterEntryPrefab, rosterContainer);
            RosterCardUI cardUI = cardObj.GetComponent<RosterCardUI>();

            bool isRecruited = fullRoster.Contains(character);
            bool isSelected = activeParty.Contains(character);

            cardUI.Bind(character, isSelected, isRecruited, () => ToggleCharacter(character));
        }

        UpdateSelectedCountText();
    }

    private void ToggleCharacter(CharacterCardData character)
    {
        List<CharacterCardData> current = new List<CharacterCardData>(PartyManager.Instance.GetActiveParty());

        if (current.Contains(character))
        {
            if (current.Count <= 1)
            {
                Debug.LogWarning("PartySetupUI: at least one character must remain in the active party.");
                return;
            }

            current.Remove(character);
        }
        else
        {
            if (current.Count >= PartyManager.MaxActivePartySize)
            {
                Debug.LogWarning($"PartySetupUI: active party is full ({PartyManager.MaxActivePartySize} max). Deselect someone first.");
                return;
            }

            current.Add(character);
        }

        PartyManager.Instance.SetActiveParty(current);
        RefreshRoster();
    }

    private void UpdateSelectedCountText()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"{PartyManager.Instance.GetActiveParty().Count} / {PartyManager.MaxActivePartySize} Selected";
    }
}