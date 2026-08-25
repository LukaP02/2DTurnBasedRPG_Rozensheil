using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single entry on the Party Setup screen: shows a character's card art + name, toggles
// selected/unselected for the active party, or shows locked (grayed out, unclickable) for a
// playable character that hasn't been recruited yet.
public class RosterCardUI : MonoBehaviour
{
    [Header("Visuals")]
    public Image artImage;
    public TMP_Text nameText;
    public Button button;

    [Header("Selected State")]
    public GameObject selectedHighlight;

    [Header("Locked State")]
    public GameObject lockedOverlay;
    [Range(0f, 1f)] public float lockedArtAlpha = 0.35f;

    public void Bind(CharacterCardData character, bool isSelected, bool isRecruited, System.Action onClick)
    {
        if (nameText != null)
            nameText.text = isRecruited ? character.characterName : "???";

        if (artImage != null)
        {
            artImage.sprite = character.cardArt;

            Color color = artImage.color;
            color.a = isRecruited ? 1f : lockedArtAlpha;
            artImage.color = color;
        }

        if (selectedHighlight != null)
            selectedHighlight.SetActive(isRecruited && isSelected);

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!isRecruited);

        if (button != null)
        {
            button.interactable = isRecruited;
            button.onClick.RemoveAllListeners();

            if (isRecruited)
                button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}