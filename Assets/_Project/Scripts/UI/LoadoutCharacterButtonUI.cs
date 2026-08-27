using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutCharacterButtonUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText; // optional
    public GameObject selectedHighlight;
    public Button button;

    public void Bind(CharacterCardData character, bool isSelected, System.Action onClick)
    {
        if (iconImage != null)
            iconImage.sprite = character.icon != null ? character.icon : character.cardArt;

        if (nameText != null)
            nameText.text = character.characterName;

        if (selectedHighlight != null)
            selectedHighlight.SetActive(isSelected);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}