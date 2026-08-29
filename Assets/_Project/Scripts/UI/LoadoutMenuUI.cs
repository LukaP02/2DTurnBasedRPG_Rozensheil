using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutMenuUI : MonoBehaviour
{
    [Header("Character Selection")]
    public Transform characterListContainer;
    public GameObject characterButtonPrefab; // uses LoadoutCharacterButtonUI

    [Header("Selected Character Card")]
    [Tooltip("Same prefab used for combat cards (CharacterCardUI). Shown read-only on the left, bound to the character's real persistent CharacterInstance (same HP/energy/status as combat).")]
    public GameObject characterCardPrefab;
    public Transform characterCardContainer;

    [Header("Basic Options")]
    public Transform basicOptionsContainer;

    [Header("Ability Lists")]
    public Transform skillOptionsContainer;
    public Transform ultimateOptionsContainer;
    public GameObject abilityOptionPrefab;

    [Header("Item List")]
    public Transform itemOptionsContainer;
    public GameObject itemOptionPrefab;

    [Header("Selected Character Info")]
    public TMP_Text selectedCharacterNameText;

    private CharacterCardData selectedCharacter;
    private CharacterCardUI selectedCharacterCard;

    // Refreshes every time the panel opens (not just once via Start) so newly-recruited
    // characters show up without needing a scene reload.
    private void OnEnable()
    {
        List<CharacterCardData> roster = PartyManager.Instance.GetFullRoster();

        if (selectedCharacter == null || !roster.Contains(selectedCharacter))
        {
            if (roster.Count > 0)
                SelectCharacter(roster[0]);
            else
                PopulateCharacterList();
        }
        else
        {
            SelectCharacter(selectedCharacter); // re-bind in case loadout/items changed elsewhere
        }
    }

    private void PopulateCharacterList()
    {
        foreach (Transform child in characterListContainer)
            Destroy(child.gameObject);

        foreach (var character in PartyManager.Instance.GetFullRoster())
        {
            if (character == null) continue;

            GameObject buttonObj = Instantiate(characterButtonPrefab, characterListContainer);
            LoadoutCharacterButtonUI buttonUI = buttonObj.GetComponent<LoadoutCharacterButtonUI>();

            bool isSelected = character == selectedCharacter;
            buttonUI.Bind(character, isSelected, () => SelectCharacter(character));
        }
    }

    private void SelectCharacter(CharacterCardData character)
    {
        selectedCharacter = character;

        if (selectedCharacterNameText != null)
            selectedCharacterNameText.text = character.characterName;

        PopulateCharacterList(); // rebuild so the new selection's highlight shows
        RefreshSelectedCharacterCard();
        RefreshAbilityLists();
        RefreshItemList();
    }

    // Read-only display of the selected character using the same CharacterCardUI prefab combat
    // uses, bound to their real persistent CharacterInstance (PartyManager.GetInstance) so it
    // shows actual current HP/energy/status rather than a fresh dummy. Passing null for the
    // CombatUIManager is safe - CharacterCardUI's manager calls are all null-conditional except
    // right-click inspect, which is guarded separately (see CharacterCardUI.OnPointerClick).
    private void RefreshSelectedCharacterCard()
    {
        if (characterCardPrefab == null || characterCardContainer == null) return;

        if (selectedCharacterCard == null)
        {
            GameObject cardObj = Instantiate(characterCardPrefab, characterCardContainer);
            selectedCharacterCard = cardObj.GetComponent<CharacterCardUI>();
            selectedCharacterCard.hoverScaleEnabled = false; // static display card - no hover-to-target here
        }

        CharacterInstance instance = PartyManager.Instance.GetInstance(selectedCharacter);
        if (instance != null)
            selectedCharacterCard.Bind(instance, null);

        selectedCharacterCard.SetActiveTurn(false);
        selectedCharacterCard.SetTargetHighlight(false, Color.clear);
    }

    private void RefreshAbilityLists()
    {
        bool basicIsSwappable = selectedCharacter.basicOptions != null && selectedCharacter.basicOptions.Length > 0;

        if (basicIsSwappable && basicOptionsContainer != null)
        {
            PopulateAbilityOptions(
                selectedCharacter.basicOptions,
                basicOptionsContainer,
                PartyManager.Instance.GetLoadout(selectedCharacter).equippedBasic,
                (ability) =>
                {
                    PartyManager.Instance.SetBasic(selectedCharacter, ability);
                    RefreshAbilityLists();
                });
        }
        else if (basicOptionsContainer != null)
        {
            foreach (Transform child in basicOptionsContainer)
                Destroy(child.gameObject);
        }

        PopulateAbilityOptions(
            selectedCharacter.skillOptions,
            skillOptionsContainer,
            PartyManager.Instance.GetLoadout(selectedCharacter).equippedSkill,
            (ability) =>
            {
                PartyManager.Instance.SetSkill(selectedCharacter, ability);
                RefreshAbilityLists();
            });

        PopulateAbilityOptions(
            selectedCharacter.ultimateOptions,
            ultimateOptionsContainer,
            PartyManager.Instance.GetLoadout(selectedCharacter).equippedUltimate,
            (ability) =>
            {
                PartyManager.Instance.SetUltimate(selectedCharacter, ability);
                RefreshAbilityLists();
            });
    }

    private void PopulateAbilityOptions(AbilityData[] options, Transform container, AbilityData currentlyEquipped, System.Action<AbilityData> onSelect)
    {
        foreach (Transform child in container)
            Destroy(child.gameObject);

        if (options == null) return;

        foreach (var ability in options)
        {
            if (ability == null) continue;

            GameObject optionObj = Instantiate(abilityOptionPrefab, container);
            AbilityOptionUI optionUI = optionObj.GetComponent<AbilityOptionUI>();

            bool isEquipped = ability == currentlyEquipped;
            optionUI.Bind(ability, isEquipped, () => onSelect(ability));
        }
    }

    private void RefreshItemList()
    {
        foreach (Transform child in itemOptionsContainer)
            Destroy(child.gameObject);

        List<ItemData> owned = PartyManager.Instance.GetOwnedItems();
        List<ItemData> equipped = PartyManager.Instance.GetEquippedItems(selectedCharacter);

        foreach (var item in owned)
        {
            if (item == null) continue;

            GameObject optionObj = Instantiate(itemOptionPrefab, itemOptionsContainer);
            ItemOptionUI optionUI = optionObj.GetComponent<ItemOptionUI>();

            bool isEquipped = equipped.Contains(item);
            optionUI.Bind(item, isEquipped, () =>
            {
                PartyManager.Instance.EquipItem(selectedCharacter, item);
                RefreshItemList();
            });
        }
    }
}