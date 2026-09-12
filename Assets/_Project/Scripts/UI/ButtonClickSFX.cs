using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays a click sound through AudioManager whenever this Button is clicked. Uses
// IPointerClickHandler instead of Button.onClick because prefab-based menu items
// (RosterCardUI, ShopItemUI, LoadoutCharacterButtonUI, etc.) call
// button.onClick.RemoveAllListeners() in their own Bind/Setup - that would wipe out an
// onClick-based listener the moment the prefab gets bound, but never touches this.
// Attach to menu buttons only - combat action buttons deliberately don't have this.
[RequireComponent(typeof(Button))]
public class ButtonClickSFX : MonoBehaviour, IPointerClickHandler
{
    public AudioClip clickSound;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button.interactable)
            AudioManager.Instance?.PlaySFX(clickSound);
    }
}