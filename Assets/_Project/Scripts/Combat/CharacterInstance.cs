using System.Collections.Generic;

public enum StatusEffectType
{
    Freeze
}

public enum CharacterForm
{
    Normal,
    Demon
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

    public CharacterForm currentForm = CharacterForm.Normal;

    private HashSet<StatusEffectType> activeStatuses = new HashSet<StatusEffectType>();
    private Dictionary<CharacterInstance, int> marksBySource = new Dictionary<CharacterInstance, int>();
    private bool deathProcessed = false;

    // --- Elemental stains ---
    private HashSet<ElementType> activeStains = new HashSet<ElementType>();

    // --- Temporary stat modifiers (e.g. DEF Shred) ---
    private class TempModifier
    {
        public int defenseDelta;
        public int turnsRemaining;
    }
    private List<TempModifier> tempModifiers = new List<TempModifier>();

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

        // Apply active temporary modifiers (e.g. DEF Shred) on top of base+item+passive
        foreach (var mod in tempModifiers)
        {
            defenseBonus += mod.defenseDelta;
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
        activeAbilities = new List<AbilityData>();

        if (data.hasForms)
        {
            AbilityData basic = currentForm == CharacterForm.Normal ? data.normalFormBasic : data.demonFormBasic;
            AbilityData skill = currentForm == CharacterForm.Normal ? data.normalFormSkill : data.demonFormSkill;

            if (basic != null) activeAbilities.Add(basic);
            if (skill != null) activeAbilities.Add(skill);

            AbilityData ultimate = (data.isPlayableCharacter && PartyManager.Instance != null)
                ? PartyManager.Instance.GetLoadout(data).equippedUltimate
                : data.defaultUltimate;

            if (ultimate != null) activeAbilities.Add(ultimate);
        }
        else if (data.isPlayableCharacter && PartyManager.Instance != null)
        {
            activeAbilities = PartyManager.Instance.GetEquippedAbilities(data);
        }
        else
        {
            if (data.basicAbility != null) activeAbilities.Add(data.basicAbility);
            if (data.defaultSkill != null) activeAbilities.Add(data.defaultSkill);
            if (data.defaultUltimate != null) activeAbilities.Add(data.defaultUltimate);
        }
    }

    public void ToggleForm()
    {
        currentForm = currentForm == CharacterForm.Normal ? CharacterForm.Demon : CharacterForm.Normal;
        RefreshAbilities();
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

    public bool CheckAndMarkDeath()
    {
        if (!isAlive && !deathProcessed)
        {
            deathProcessed = true;
            return true;
        }
        return false;
    }

    // --- Status effects (Freeze) ---
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

    // --- Marks ---
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

    // --- Elemental stains ---

    // Returns true if this element was NEWLY added (wasn't already present)
    public bool ApplyStain(ElementType element)
    {
        return activeStains.Add(element);
    }

    public bool HasStain(ElementType element)
    {
        return activeStains.Contains(element);
    }

    public List<ElementType> GetActiveStains()
    {
        return new List<ElementType>(activeStains);
    }

    public int StainCount()
    {
        return activeStains.Count;
    }

    public void ClearStains()
    {
        activeStains.Clear();
    }

    // --- Temporary modifiers (DEF Shred) ---
    public void ApplyDefenseShred(int amount, int duration)
    {
        tempModifiers.Add(new TempModifier { defenseDelta = -amount, turnsRemaining = duration });
        RecalculateStats();
    }

    // Call at the start of this character's own turn
    public void TickTempModifiers()
    {
        bool anyExpired = false;

        for (int i = tempModifiers.Count - 1; i >= 0; i--)
        {
            tempModifiers[i].turnsRemaining--;
            if (tempModifiers[i].turnsRemaining <= 0)
            {
                tempModifiers.RemoveAt(i);
                anyExpired = true;
            }
        }

        if (anyExpired)
            RecalculateStats();
    }

    // --- Status display for UI ---
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

        foreach (var stain in activeStains)
        {
            list.Add(new StatusEffectInstance { label = $"{stain} Stain", stackCount = 1 });
        }

        if (tempModifiers.Count > 0)
        {
            list.Add(new StatusEffectInstance { label = "DEF Shred", stackCount = tempModifiers.Count });
        }

        return list;
    }
}