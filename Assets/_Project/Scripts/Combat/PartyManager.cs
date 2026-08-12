using System.Collections.Generic;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    private Dictionary<CharacterCardData, CharacterLoadout> loadouts = new Dictionary<CharacterCardData, CharacterLoadout>();
    private Dictionary<CharacterCardData, ItemData> equippedItems = new Dictionary<CharacterCardData, ItemData>();
    private List<ItemData> ownedItems = new List<ItemData>();

    private List<CharacterInstance> partyInstances;

    public int Gold { get; private set; } = 0;
    public int UnlockedLevelIndex { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // --- Level progress ---
    public void UnlockUpTo(int index)
    {
        if (index > UnlockedLevelIndex)
            UnlockedLevelIndex = index;
    }

    // --- Persistent party instances ---
    public void InitializeParty(List<CharacterCardData> members)
    {
        if (partyInstances != null) return;

        partyInstances = new List<CharacterInstance>();
        foreach (var member in members)
        {
            partyInstances.Add(new CharacterInstance(member));
        }
    }

    public List<CharacterInstance> GetPartyInstances()
    {
        if (partyInstances == null)
        {
            Debug.LogWarning("PartyManager: party not initialized yet. Call InitializeParty first.");
            return new List<CharacterInstance>();
        }

        return partyInstances;
    }

    public void HealPartyFully()
    {
        if (partyInstances == null) return;

        foreach (var member in partyInstances)
        {
            member.Heal(member.maxHP);
        }
    }

    // --- Abilities / Loadout ---
    public CharacterLoadout GetLoadout(CharacterCardData data)
    {
        if (!loadouts.TryGetValue(data, out var loadout))
        {
            loadout = new CharacterLoadout
            {
                equippedBasic = data.defaultBasic != null ? data.defaultBasic : data.basicAbility,
                equippedSkill = data.defaultSkill,
                equippedUltimate = data.defaultUltimate
            };
            loadouts[data] = loadout;
        }

        return loadout;
    }

    public List<AbilityData> GetEquippedAbilities(CharacterCardData data)
    {
        var loadout = GetLoadout(data);
        var result = new List<AbilityData>();

        bool basicIsSwappable = data.basicOptions != null && data.basicOptions.Length > 0;
        AbilityData basic = basicIsSwappable ? loadout.equippedBasic : data.basicAbility;

        if (basic != null) result.Add(basic);
        if (loadout.equippedSkill != null) result.Add(loadout.equippedSkill);
        if (loadout.equippedUltimate != null) result.Add(loadout.equippedUltimate);

        return result;
    }

    public void SetBasic(CharacterCardData data, AbilityData newBasic)
    {
        GetLoadout(data).equippedBasic = newBasic;

        RefreshInstanceAbilities(data);
    }

    public void SetSkill(CharacterCardData data, AbilityData newSkill)
    {
        GetLoadout(data).equippedSkill = newSkill;

        RefreshInstanceAbilities(data);
    }

    public void SetUltimate(CharacterCardData data, AbilityData newUltimate)
    {
        GetLoadout(data).equippedUltimate = newUltimate;

        RefreshInstanceAbilities(data);
    }

    private void RefreshInstanceAbilities(CharacterCardData data)
    {
        if (partyInstances == null) return;

        var instance = partyInstances.Find(c => c.data == data);
        if (instance != null)
        {
            instance.RefreshAbilities();
        }
    }

    // --- Gold ---
    public void AddGold(int amount)
    {
        Gold += amount;
    }

    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        return true;
    }

    // --- Items ---
    public bool OwnsItem(ItemData item)
    {
        return ownedItems.Contains(item);
    }

    public bool TryPurchase(ItemData item)
    {
        if (item == null || OwnsItem(item)) return false;
        if (!SpendGold(item.price)) return false;

        ownedItems.Add(item);
        return true;
    }

    public List<ItemData> GetOwnedItems()
    {
        return ownedItems;
    }

    public void EquipItem(CharacterCardData character, ItemData item)
    {
        equippedItems[character] = item;

        if (partyInstances != null)
        {
            var instance = partyInstances.Find(c => c.data == character);
            if (instance != null)
            {
                instance.RecalculateStats();
            }
        }
    }

    public ItemData GetEquippedItem(CharacterCardData character)
    {
        equippedItems.TryGetValue(character, out var item);
        return item;
    }
}