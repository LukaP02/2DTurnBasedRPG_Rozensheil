using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartySetupUI : MonoBehaviour
{
    [Header("Roster")]
    public Transform rosterContainer;
    public GameObject rosterEntryPrefab;

    [Header("Info")]
    public TMP_Text selectedCountText;
    public Color selectedColor = new Color(0.6f, 0.85f, 0.6f);
    public Color unselectedColor = Color.white;

    private void OnEnable()
    {
        RefreshRoster();
    }

    private void RefreshRoster()
    {
        foreach (Transform child in rosterContainer)
            Destroy(child.gameObject);

        List<CharacterCardData> activeParty = PartyManager.Instance.GetActiveParty();

        foreach (var character in PartyManager.Instance.GetFullRoster())
        {
            GameObject buttonObj = Instantiate(rosterEntryPrefab, rosterContainer);

            bool isSelected = activeParty.Contains(character);

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = isSelected ? $"[Selected] {character.characterName}" : character.characterName;

            Image background = buttonObj.GetComponent<Image>();
            if (background != null)
                background.color = isSelected ? selectedColor : unselectedColor;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => ToggleCharacter(character));
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