using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutMenuUI : MonoBehaviour
{
    [Header("Party to manage")]
    public CharacterCardData[] playableCharacters;

    [Header("Character Selection")]
    public Transform characterListContainer;
    public GameObject characterButtonPrefab;

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

    private void Start()
    {
        PopulateCharacterList();

        if (playableCharacters.Length > 0)
        {
            SelectCharacter(playableCharacters[0]);
        }
    }

    private void PopulateCharacterList()
    {
        foreach (Transform child in characterListContainer)
            Destroy(child.gameObject);

        foreach (var character in playableCharacters)
        {
            GameObject buttonObj = Instantiate(characterButtonPrefab, characterListContainer);

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = character.characterName;

            Button button = buttonObj.GetComponent<Button>();
            button.onClick.AddListener(() => SelectCharacter(character));
        }
    }

    private void SelectCharacter(CharacterCardData character)
    {
        selectedCharacter = character;

        if (selectedCharacterNameText != null)
            selectedCharacterNameText.text = character.characterName;

        RefreshAbilityLists();
        RefreshItemList();
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