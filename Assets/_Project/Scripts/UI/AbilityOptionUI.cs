using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityOptionUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public Button button;
    public GameObject equippedHighlight;

    public void Bind(AbilityData ability, bool isEquipped, Action onClick)
    {
        nameText.text = ability.abilityName;

        if (icon != null)
            icon.sprite = ability.icon;

        if (equippedHighlight != null)
            equippedHighlight.SetActive(isEquipped);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}