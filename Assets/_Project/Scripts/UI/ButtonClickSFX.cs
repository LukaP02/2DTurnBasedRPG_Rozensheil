using UnityEngine;
using UnityEngine.UI;

// Plays a click sound through AudioManager whenever this Button is clicked. Attach to menu
// buttons only (main menu, options, shop, loadout, party setup, dialogue skip, etc.) - combat
// action buttons deliberately don't have this.
[RequireComponent(typeof(Button))]
public class ButtonClickSFX : MonoBehaviour
{
    public AudioClip clickSound;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlayClick);
    }

    private void PlayClick()
    {
        AudioManager.Instance?.PlaySFX(clickSound);
    }
}