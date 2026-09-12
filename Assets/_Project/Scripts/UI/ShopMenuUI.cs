using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopMenuUI : MonoBehaviour
{
    [Header("Items for sale")]
    public ItemData[] itemsForSale;

    [Header("UI References")]
    public Transform itemListContainer;
    public GameObject shopItemPrefab; // uses ShopItemUI
    public TMP_Text goldText;

    [Header("Selected Item Detail (left side panel)")]
    public GameObject selectedItemPanel;
    public Image selectedItemIcon;
    public TMP_Text selectedItemNameText;
    public TMP_Text selectedItemDescriptionText;

    // OnEnable rather than Start - the panel is only ever shown/hidden via SetActive (see
    // OverworldMenuUI), so Start would only ever run once at scene load and gold/stock would go
    // stale after that (e.g. gold earned from a level wouldn't show until the next scene reload).
    // OnEnable re-runs every time the shop is actually opened.
    private void OnEnable()
    {
        if (selectedItemPanel != null)
            selectedItemPanel.SetActive(false);

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
            itemUI.Bind(item, owned, () => TryBuy(item), () => SelectItem(item));
        }
    }

    private void SelectItem(ItemData item)
    {
        if (selectedItemPanel == null) return;

        selectedItemPanel.SetActive(true);

        if (selectedItemIcon != null)
            selectedItemIcon.sprite = item.icon;

        if (selectedItemNameText != null)
            selectedItemNameText.text = item.itemName;

        if (selectedItemDescriptionText != null)
            selectedItemDescriptionText.text = item.description;
    }

    private void TryBuy(ItemData item)
    {
        bool success = PartyManager.Instance.TryPurchase(item);

        if (success)
        {
            AudioManager.Instance?.PlaySFX(purchaseSound);
        }
        else
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