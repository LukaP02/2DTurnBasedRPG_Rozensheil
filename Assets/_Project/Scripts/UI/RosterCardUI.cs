using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// A single entry on the Party Setup screen: shows a character's card art + name, toggles
// selected/unselected for the active party, or shows locked (grayed out, unclickable) for a
// playable character that hasn't been recruited yet. Left click toggles selection; right click
// opens the same inspect/detail overlay used by CharacterCardUI in combat.
public class RosterCardUI : MonoBehaviour, IPointerClickHandler
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

    private bool isRecruited;
    private System.Action onInspect;

    public void Bind(CharacterCardData character, bool isSelected, bool isRecruited, System.Action onClick, System.Action onInspect)
    {
        this.isRecruited = isRecruited;
        this.onInspect = onInspect;

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && isRecruited)
            onInspect?.Invoke();
    }
}