using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatusIconUI : MonoBehaviour
{
    public TMP_Text labelText;
    public TMP_Text countText;
    public Image iconImage;

    public void Bind(StatusEffectInstance status)
    {
        if (labelText != null)
            labelText.text = status.label;

        if (countText != null)
        {
            countText.gameObject.SetActive(true);
            countText.text = status.stackCount.ToString();
        }

        if (iconImage != null)
        {
            bool hasIcon = status.icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            if (hasIcon)
                iconImage.sprite = status.icon;
        }
    }
}