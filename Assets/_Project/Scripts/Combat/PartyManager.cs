using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    public const int MaxActivePartySize = 3;
    public const int MaxEquippedItemsPerCharacter = 3;

    private Dictionary<CharacterCardData, CharacterLoadout> loadouts = new Dictionary<CharacterCardData, CharacterLoadout>();
    private Dictionary<CharacterCardData, List<ItemData>> equippedItems = new Dictionary<CharacterCardData, List<ItemData>>();
    private List<ItemData> ownedItems = new List<ItemData>();

    // Every recruited character gets a persistent CharacterInstance (HP, energy, etc. survive
    // being benched); activeParty is the subset (up to MaxActivePartySize) that fights.
    private Dictionary<CharacterCardData, CharacterInstance> allInstances = new Dictionary<CharacterCardData, CharacterInstance>();
    private List<CharacterCardData> fullRoster = new List<CharacterCardData>();
    private List<CharacterCardData> activeParty = new List<CharacterCardData>();

    public int Gold { get; private set; } = 0;

    // Overworld map nodes (levels and standalone events alike) unlocked so far. Branching:
    // each LevelData declares its own unlocksOnComplete list, so completing one node can open
    // several others at once instead of just "the next one in a line."
    private HashSet<LevelData> unlockedLevels = new HashSet<LevelData>();

    // Nodes the player has already finished. Unlike unlockedLevels, this never gates whether a
    // node is reachable - it's purely so the map can show a completed node grayed out and
    // unselectable instead of it staying clickable (or disappearing) forever.
    private HashSet<LevelData> completedLevels = new HashSet<LevelData>();

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
    public bool IsLevelUnlocked(LevelData level)
    {
        return level != null && unlockedLevels.Contains(level);
    }

    // Purely additive - safe to call repeatedly (e.g. re-seeding starting nodes) without
    // affecting anything already unlocked.
    public void UnlockLevels(IEnumerable<LevelData> levels)
    {
        if (levels == null) return;

        foreach (var level in levels)
        {
            if (level != null)
                unlockedLevels.Add(level);
        }
    }

    public bool IsLevelCompleted(LevelData level)
    {
        return level != null && completedLevels.Contains(level);
    }

    public void MarkLevelCompleted(LevelData level)
    {
        if (level != null)
            completedLevels.Add(level);
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

    // Toggles item on/off for this character: unequips it if already equipped, otherwise equips it
    // (up to MaxEquippedItemsPerCharacter - equipping a 4th while at the cap is a no-op).
    public void EquipItem(CharacterCardData character, ItemData item)
    {
        List<ItemData> items = GetEquippedItems(character);

        if (items.Contains(item))
        {
            items.Remove(item);
        }
        else
        {
            if (items.Count >= MaxEquippedItemsPerCharacter)
            {
                Debug.LogWarning($"PartyManager: {character.characterName} already has {MaxEquippedItemsPerCharacter} items equipped. Unequip one first.");
                return;
            }

            items.Add(item);
        }

        if (allInstances.TryGetValue(character, out var instance))
        {
            instance.RecalculateStats();
        }
    }

    public List<ItemData> GetEquippedItems(CharacterCardData character)
    {
        if (!equippedItems.TryGetValue(character, out var items))
        {
            items = new List<ItemData>();
            equippedItems[character] = items;
        }

        return items;
    }
}