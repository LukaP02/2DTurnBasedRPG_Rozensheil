using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    public const int MaxActivePartySize = 4;

    private Dictionary<CharacterCardData, CharacterLoadout> loadouts = new Dictionary<CharacterCardData, CharacterLoadout>();
    private Dictionary<CharacterCardData, ItemData> equippedItems = new Dictionary<CharacterCardData, ItemData>();
    private List<ItemData> ownedItems = new List<ItemData>();

    // Every recruited character gets a persistent CharacterInstance (HP, energy, etc. survive
    // being benched); activeParty is the subset (up to MaxActivePartySize) that fights.
    private Dictionary<CharacterCardData, CharacterInstance> allInstances = new Dictionary<CharacterCardData, CharacterInstance>();
    private List<CharacterCardData> fullRoster = new List<CharacterCardData>();
    private List<CharacterCardData> activeParty = new List<CharacterCardData>();

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

    // --- Roster & active party ---
    public void InitializeParty(List<CharacterCardData> members)
    {
        if (fullRoster.Count > 0) return;

        fullRoster = members;

        foreach (var member in fullRoster)
        {
            allInstances[member] = new CharacterInstance(member);
        }

        activeParty = fullRoster.Take(MaxActivePartySize).ToList();
    }

    public List<CharacterCardData> GetFullRoster()
    {
        return fullRoster;
    }

    public List<CharacterCardData> GetActiveParty()
    {
        return activeParty;
    }

    // Returns false (leaving the active party unchanged) if the selection is empty or over the cap.
    public bool SetActiveParty(List<CharacterCardData> selected)
    {
        if (selected == null || selected.Count == 0 || selected.Count > MaxActivePartySize)
            return false;

        activeParty = new List<CharacterCardData>(selected);
        return true;
    }

    public List<CharacterInstance> GetPartyInstances()
    {
        if (fullRoster.Count == 0)
        {
            Debug.LogWarning("PartyManager: party not initialized yet. Call InitializeParty first.");
            return new List<CharacterInstance>();
        }

        return activeParty.Select(c => allInstances[c]).ToList();
    }

    public void HealPartyFully()
    {
        foreach (var instance in allInstances.Values)
        {
            instance.Heal(instance.maxHP);
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
        if (allInstances.TryGetValue(data, out var instance))
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

        if (allInstances.TryGetValue(character, out var instance))
        {
            instance.RecalculateStats();
        }
    }

    public ItemData GetEquippedItem(CharacterCardData character)
    {
        equippedItems.TryGetValue(character, out var item);
        return item;
    }
}