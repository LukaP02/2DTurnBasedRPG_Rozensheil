using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemOptionUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public Button button;
    public GameObject equippedHighlight;

    public void Bind(ItemData item, bool isEquipped, Action onClick)
    {
        nameText.text = item.itemName;

        if (icon != null)
            icon.sprite = item.icon;

        if (equippedHighlight != null)
            equippedHighlight.SetActive(isEquipped);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}