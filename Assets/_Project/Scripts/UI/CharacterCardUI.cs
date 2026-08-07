using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterCardUI : MonoBehaviour
{
    [Header("Visuals")]
    public Image artImage;
    public TMP_Text nameText;
    public Slider hpSlider;
    public TMP_Text hpText;
    public GameObject activeTurnHighlight;

    [Header("Action Buttons")]
    public GameObject actionButtonsContainer;
    public Button basicButton;
    public Button skillButton;
    public Button ultButton;

    private CharacterInstance boundCharacter;
    private CombatUIManager uiManager;

    public CharacterInstance BoundCharacter => boundCharacter;

    public void Bind(CharacterInstance character, CombatUIManager manager)
    {
        boundCharacter = character;
        uiManager = manager;

        nameText.text = character.data.characterName;
        artImage.sprite = character.data.cardArt;

        RefreshHP();
        HideActionButtons();
    }

    public void RefreshHP()
    {
        if (boundCharacter == null) return;

        hpSlider.maxValue = boundCharacter.maxHP;
        hpSlider.value = boundCharacter.currentHP;
        hpText.text = $"{boundCharacter.currentHP} / {boundCharacter.maxHP}";
    }

    public void SetActiveTurn(bool isActive)
    {
        if (activeTurnHighlight != null)
            activeTurnHighlight.SetActive(isActive);
    }

    public void ShowActionButtons(AbilityData basic, AbilityData skill, AbilityData ult)
    {
        actionButtonsContainer.SetActive(true);
        SetupButton(basicButton, basic);
        SetupButton(skillButton, skill);
        SetupButton(ultButton, ult);
    }

    private void SetupButton(Button button, AbilityData ability)
    {
        if (ability == null)
        {
            button.gameObject.SetActive(false);
            return;
        }

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => uiManager.OnAbilitySelected(boundCharacter, ability));
    }

    public void HideActionButtons()
    {
        actionButtonsContainer.SetActive(false);
    }

    public void OnCardClicked()
    {
        uiManager.OnCardClicked(boundCharacter);
    }
}