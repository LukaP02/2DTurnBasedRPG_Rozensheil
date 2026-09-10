using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One row in CardDetailUI's abilities list - icon plus name/type/description text.
public class AbilityRowUI : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text bodyText;

    public void Bind(AbilityData ability)
    {
        if (iconImage != null)
        {
            bool hasIcon = ability.icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            if (hasIcon)
                iconImage.sprite = ability.icon;
        }

        if (nameText != null)
            nameText.text = $"{ability.abilityName} ({ability.abilityType})";

        if (bodyText != null)
            bodyText.text = ability.description;
    }
}