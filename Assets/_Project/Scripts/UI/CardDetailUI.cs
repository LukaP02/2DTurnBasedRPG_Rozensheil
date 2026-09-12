using System.Collections.Generic;
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
    [Tooltip("Shown directly under the description - lists the status effects currently on the inspected character (e.g. 'Burn x2, Fire Stain'). Empty/hidden when there are none.")]
    public TMP_Text statusText;
    [Tooltip("Icon row for the same status effects listed in Status Text. Reuse the same prefab CharacterCardUI.statusIconPrefab points at.")]
    public Transform statusIconContainer;
    public GameObject statusIconPrefab;
    [Tooltip("Icon shown on a Fire stain's status icon. Leave empty to show it with no icon, just the label.")]
    public Sprite fireStainIcon;
    [Tooltip("Icon shown on an Ice stain's status icon. Leave empty to show it with no icon, just the label.")]
    public Sprite iceStainIcon;
    [Tooltip("Icon shown on an Electro stain's status icon. Leave empty to show it with no icon, just the label.")]
    public Sprite electroStainIcon;

    [Header("Right Box - Info")]
    public TMP_Text nameText;
    public TMP_Text roleText;
    [Tooltip("Container the ability rows get instantiated into - one AbilityRowUI per active ability.")]
    public Transform abilityListContainer;
    public GameObject abilityRowPrefab;
    public Image passiveIcon;
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

        RefreshStatuses(character);

        if (nameText != null)
            nameText.text = data.characterName;

        if (roleText != null)
            roleText.text = data.role.ToString();

        PopulateAbilityRows(character);

        PopulateAbilityRows(character);

        if (passiveIcon != null)
        {
            bool hasIcon = data.passive != null && data.passive.icon != null;
            passiveIcon.gameObject.SetActive(hasIcon);
            if (hasIcon)
                passiveIcon.sprite = data.passive.icon;
        }

        if (passiveText != null)
            passiveText.text = BuildPassiveText(data);
    }

    private void RefreshStatuses(CharacterInstance character)
    {
        List<StatusEffectInstance> statuses = character.GetStatusDisplayList();

        if (statusText != null)
        {
            if (statuses.Count == 0)
            {
                statusText.text = "No active status effects";
            }
            else
            {
                var labels = new List<string>();
                foreach (var status in statuses)
                    labels.Add(status.stackCount > 1 ? $"{status.label} x{status.stackCount}" : status.label);

                statusText.text = string.Join(", ", labels);
            }
        }

        if (statusIconContainer == null || statusIconPrefab == null) return;

        foreach (Transform child in statusIconContainer)
            Destroy(child.gameObject);

        foreach (var status in statuses)
        {
            // Stains and marks arrive with icon left null (see CharacterInstance.GetStatusDisplayList) -
            // resolve the actual sprite here, same as CharacterCardUI.RefreshStatuses does.
            if (status.icon == null)
            {
                if (status.stainElement.HasValue)
                    status.icon = GetStainIcon(status.stainElement.Value);
                else if (status.markSourceCharacter != null)
                    status.icon = status.markSourceCharacter.markIcon;
            }

            GameObject iconObj = Instantiate(statusIconPrefab, statusIconContainer);
            StatusIconUI iconUI = iconObj.GetComponent<StatusIconUI>();
            iconUI.Bind(status);
        }
    }

    private Sprite GetStainIcon(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return fireStainIcon;
            case ElementType.Ice: return iceStainIcon;
            case ElementType.Electro: return electroStainIcon;
            default: return null;
        }
    }

    private void PopulateAbilityRows(CharacterInstance character)
    {
        if (abilityListContainer == null || abilityRowPrefab == null) return;

        foreach (Transform child in abilityListContainer)
            Destroy(child.gameObject);

        foreach (var ability in character.activeAbilities)
        {
            if (ability == null) continue;

            GameObject rowObj = Instantiate(abilityRowPrefab, abilityListContainer);
            AbilityRowUI rowUI = rowObj.GetComponent<AbilityRowUI>();
            rowUI.Bind(ability);
        }
    }

    private string BuildPassiveText(CharacterCardData data)
    {
        if (data.passive == null)
            return "None";

        string text = $"<b>{data.passive.passiveName} (Passive)</b>";
        if (!string.IsNullOrEmpty(data.passive.description))
            text += $"\n{data.passive.description}";

        return text;
    }



    public void Hide()
    {
        detailPanel.SetActive(false);
    }
}