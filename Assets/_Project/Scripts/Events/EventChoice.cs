using UnityEngine;

[System.Serializable]
public class EventChoice
{
    public string choiceText;       // shown on the button, e.g. "Fight the bandits"
    [TextArea(2, 4)] public string outcomeText; // shown after picking, e.g. "You win the scuffle and find gold."

    [Header("Effects (optional, leave 0 for none)")]
    public int goldChange;   // can be negative
    public int hpChangePercent; // flat % of max HP applied to whole party, can be negative

    [Header("Recruitment (optional, leave empty for none)")]
    public CharacterCardData recruitCharacter; // adds this character to the roster when this choice is picked
    [Header("Items (optional, leave empty for none)")]
    [Tooltip("Items granted to the player when this choice is picked. Grant Items alone is a pure reward.")]
    public ItemData[] grantItems;
    [Tooltip("Items removed from the player's inventory when this choice is picked. Set both Grant Items and Cost Items to make this a trade (give these, receive those).")]
    public ItemData[] costItems;
}