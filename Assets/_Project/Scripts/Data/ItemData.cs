using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "CardRPG/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;
    public int price;

    [Header("Stat Bonuses")]
    public int attackBonus;
    public int defenseBonus;
    public int hpBonus;
    public int speedBonus;
    [Tooltip("Added directly to Crit Rate, e.g. 0.1 = +10%")]
    public float critRateBonus;
    [Tooltip("Added directly to Energy Recharge Rate, e.g. 0.2 = +20%")]
    public float energyRechargeBonus;
}