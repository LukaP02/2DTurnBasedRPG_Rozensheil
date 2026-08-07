using UnityEngine;
using TMPro;

public class ShopMenuUI : MonoBehaviour
{
    [Header("Items for sale")]
    public ItemData[] itemsForSale;

    [Header("UI References")]
    public Transform itemListContainer;
    public GameObject shopItemPrefab; // uses ShopItemUI
    public TMP_Text goldText;

    private void Start()
    {
        RefreshShop();
    }

    private void RefreshShop()
    {
        foreach (Transform child in itemListContainer)
            Destroy(child.gameObject);

        UpdateGoldText();

        foreach (var item in itemsForSale)
        {
            if (item == null) continue;

            GameObject itemObj = Instantiate(shopItemPrefab, itemListContainer);
            ShopItemUI itemUI = itemObj.GetComponent<ShopItemUI>();

            bool owned = PartyManager.Instance.OwnsItem(item);
            itemUI.Bind(item, owned, () => TryBuy(item));
        }
    }

    private void TryBuy(ItemData item)
    {
        bool success = PartyManager.Instance.TryPurchase(item);

        if (!success)
        {
            Debug.Log($"Could not purchase {item.itemName} (insufficient gold or already owned).");
        }

        RefreshShop();
    }

    private void UpdateGoldText()
    {
        if (goldText != null)
            goldText.text = $"Gold: {PartyManager.Instance.Gold}";
    }
}