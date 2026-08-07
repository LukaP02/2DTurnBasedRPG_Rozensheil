using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Button buyButton;
    public GameObject ownedLabel; // shown instead of buy button once purchased

    public void Bind(ItemData item, bool alreadyOwned, Action onBuy)
    {
        nameText.text = item.itemName;
        priceText.text = $"{item.price}g";

        if (icon != null)
            icon.sprite = item.icon;

        buyButton.gameObject.SetActive(!alreadyOwned);
        if (ownedLabel != null)
            ownedLabel.SetActive(alreadyOwned);

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke());
    }
}