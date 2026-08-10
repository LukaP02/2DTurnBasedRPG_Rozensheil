using System.Collections.Generic;

public enum StatusEffectType
{
    Freeze
}

public class StatusEffectInstance
{
    public string label;
    public int stackCount;
}

public class CharacterInstance
{
    public CharacterCardData data;
    public int currentHP;
    public int maxHP;
    public int currentSpeed;
    public int currentAttack;
    public int currentDefense;
    public List<AbilityData> activeAbilities;
    public bool isAlive => currentHP > 0;

    private HashSet<StatusEffectType> activeStatuses = new HashSet<StatusEffectType>();
    private Dictionary<CharacterInstance, int> marksBySource = new Dictionary<CharacterInstance, int>();

    public CharacterInstance(CharacterCardData sourceData)
    {
        data = sourceData;

        RecalculateStats();
        RefreshAbilities();

        currentHP = maxHP;
    }

    public void RecalculateStats()
    {
        int hpBonus = 0, attackBonus = 0, defenseBonus = 0, speedBonus = 0;

        if (data.isPlayableCharacter && PartyManager.Instance != null)
        {
            ItemData equippedItem = PartyManager.Instance.GetEquippedItem(data);
            if (equippedItem != null)
            {
                hpBonus += equippedItem.hpBonus;
                attackBonus += equippedItem.attackBonus;
                defenseBonus += equippedItem.defenseBonus;
                speedBonus += equippedItem.speedBonus;
            }
        }

        if (data.passive != null && data.passive.trigger == PassiveTrigger.PassiveStatScaling)
        {
            hpBonus += UnityEngine.Mathf.RoundToInt(data.maxHP * data.passive.bonusHPPercent);
            attackBonus += UnityEngine.Mathf.RoundToInt(data.attack * data.passive.bonusAttackPercent);
            defenseBonus += UnityEngine.Mathf.RoundToInt(data.defense * data.passive.bonusDefensePercent);
            speedBonus += UnityEngine.Mathf.RoundToInt(data.speed * data.passive.bonusSpeedPercent);
        }

        int previousMaxHP = maxHP;
        maxHP = data.maxHP + hpBonus;
        currentAttack = data.attack + attackBonus;
        currentDefense = data.defense + defenseBonus;
        currentSpeed = data.speed + speedBonus;

        if (previousMaxHP > 0 && currentHP > 0)
        {
            float ratio = (float)currentHP / previousMaxHP;
            currentHP = UnityEngine.Mathf.RoundToInt(maxHP * ratio);
        }
    }

    public void RefreshAbilities()
    {
        if (data.isPlayableCharacter && PartyManager.Instance != null)
        {
            activeAbilities = PartyManager.Instance.GetEquippedAbilities(data);
        }
        else
        {
            activeAbilities = new List<AbilityData>();
            if (data.basicAbility != null) activeAbilities.Add(data.basicAbility);
            if (data.defaultSkill != null) activeAbilities.Add(data.defaultSkill);
            if (data.defaultUltimate != null) activeAbilities.Add(data.defaultUltimate);
        }
    }

    public void TakeDamage(int amount)
    {
        currentHP -= amount;
        if (currentHP < 0) currentHP = 0;
    }

    public void Heal(int amount)
    {
        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;
    }

    // --- Status effects ---
    public void ApplyStatus(StatusEffectType status)
    {
        activeStatuses.Add(status);
    }

    public bool HasStatus(StatusEffectType status)
    {
        return activeStatuses.Contains(status);
    }

    public void RemoveStatus(StatusEffectType status)
    {
        activeStatuses.Remove(status);
    }

    // --- Marks (now stored on the character CARRYING the marks, keyed by who applied them) ---
    public void AddMarkStack(CharacterInstance source, int maxStacks)
    {
        if (!marksBySource.ContainsKey(source))
            marksBySource[source] = 0;

        marksBySource[source] = UnityEngine.Mathf.Min(marksBySource[source] + 1, maxStacks);
    }

    public int ConsumeMarkStacks(CharacterInstance source)
    {
        if (!marksBySource.TryGetValue(source, out int stacks))
            return 0;

        marksBySource[source] = 0;
        return stacks;
    }

    public int GetMarkStacks(CharacterInstance source)
    {
        return marksBySource.TryGetValue(source, out int stacks) ? stacks : 0;
    }

    // --- Generic display list for UI (Freeze, Marks, future statuses all feed this) ---
    public List<StatusEffectInstance> GetStatusDisplayList()
    {
        var list = new List<StatusEffectInstance>();

        if (HasStatus(StatusEffectType.Freeze))
        {
            list.Add(new StatusEffectInstance { label = "Freeze", stackCount = 1 });
        }

        foreach (var kvp in marksBySource)
        {
            if (kvp.Value > 0)
            {
                string sourceName = kvp.Key != null ? kvp.Key.data.characterName : "Unknown";
                list.Add(new StatusEffectInstance { label = $"Mark ({sourceName})", stackCount = kvp.Value });
            }
        }

        return list;
    }
}