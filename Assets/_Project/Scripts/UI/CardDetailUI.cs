using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen "inspect" overlay: zooms a card's art to the center of the screen with a
// description box on one side and a stats/kit box on the other. Opened via each card's
// inspect button (CharacterCardUI.inspectButton -> CombatUIManager.OnInspectCard).
public class CardDetailUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject detailPanel;
    public Image zoomedArt;
    public Button closeButton;

    [Header("Left Box - Description")]
    public TMP_Text descriptionText;

    [Header("Right Box - Info")]
    public TMP_Text nameText;
    public TMP_Text roleText;
    public TMP_Text abilitiesText;
    public TMP_Text passiveText;
   

    private void Awake()
    {
        if (detailPanel != null)
            detailPanel.SetActive(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    public void Show(CharacterInstance character)
    {
        Debug.Log($"[InspectZoomDebug] CardDetailUI.Show ENTERED, character null? {character == null}, frame {Time.frameCount}");
        if (character == null || character.data == null) return;

        CharacterCardData data = character.data;

        if (zoomedArt != null)
        {
            zoomedArt.sprite = (character.currentForm == CharacterForm.Demon && data.demonFormArt != null)
                ? data.demonFormArt
                : data.cardArt;
        }

        if (descriptionText != null)
            descriptionText.text = data.description;

        if (nameText != null)
            nameText.text = data.characterName;

        if (roleText != null)
            roleText.text = data.role.ToString();

        if (abilitiesText != null)
            abilitiesText.text = BuildAbilitiesText(character);

        if (passiveText != null)
            passiveText.text = BuildPassiveText(data);
        Debug.Log($"[InspectZoomDebug] CardDetailUI.Show about to SetActive(true) on {detailPanel?.name}, frame {Time.frameCount}");
        detailPanel.SetActive(true);
        Debug.Log($"[InspectZoomDebug] CardDetailUI.Show SetActive(true) done, activeSelf now {detailPanel.activeSelf}, frame {Time.frameCount}");
    }

    private string BuildAbilitiesText(CharacterInstance character)
    {
        StringBuilder sb = new StringBuilder();

        foreach (var ability in character.activeAbilities)
        {
            if (ability == null) continue;

            sb.AppendLine($"<b>{ability.abilityName}</b> ({ability.abilityType})");
            if (!string.IsNullOrEmpty(ability.description))
                sb.AppendLine(ability.description);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildPassiveText(CharacterCardData data)
    {
        if (data.passive == null)
            return "None";

        string text = $"<b>{data.passive.passiveName}</b>";
        if (!string.IsNullOrEmpty(data.passive.description))
            text += $"\n{data.passive.description}";

        return text;
    }



    public void Hide()
    {
        detailPanel.SetActive(false);
    }
}