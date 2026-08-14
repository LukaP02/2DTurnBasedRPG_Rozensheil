using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterForm
{
    Normal,
    Demon
}

public class StatusEffectInstance
{
    public string label;
    public int stackCount;
    public Sprite icon;
}

public class CharacterInstance
{
    public CharacterCardData data;
    public int currentHP;
    public int maxHP;
    public int currentSpeed;
    public int currentAttack;
    public int currentDefense;
    public int currentEnergy;
    public int maxEnergy;
    public List<AbilityData> activeAbilities;
    public bool isAlive => currentHP > 0;

    public CharacterForm currentForm = CharacterForm.Normal;

    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    private Dictionary<CharacterInstance, int> marksBySource = new Dictionary<CharacterInstance, int>();
    private bool deathProcessed = false;

    // --- Elemental stains ---
    private HashSet<ElementType> activeStains = new HashSet<ElementType>();

    public CharacterInstance(CharacterCardData sourceData)
    {
        data = sourceData;

        RecalculateStats();
        RefreshAbilities();

        currentHP = maxHP;

        maxEnergy = data.maxEnergy > 0 ? data.maxEnergy : 100;
        currentEnergy = 0;
    }

    // --- Ultimate energy ---
    public bool IsUltimateReady => currentEnergy >= maxEnergy;

    public void GainEnergy(int amount)
    {
        currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
    }

    public void ConsumeEnergyForUltimate()
    {
        currentEnergy = 0;
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

        foreach (var effect in activeEffects)
        {
            if (effect.category != StatusEffectCategory.StatModifier) continue;

            int delta = effect.isPercent
                ? Mathf.RoundToInt(BaseValueFor(effect.modifiedStat) * effect.percentAmount)
                : effect.flatAmount;
            delta *= effect.stackCount;

            switch (effect.modifiedStat)
            {
                case ModifiedStat.Attack: attackBonus += delta; break;
                case ModifiedStat.Defense: defenseBonus += delta; break;
                case ModifiedStat.Speed: speedBonus += delta; break;
                case ModifiedStat.MaxHP: hpBonus += delta; break;
            }
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

    private int BaseValueFor(ModifiedStat stat)
    {
        switch (stat)
        {
            case ModifiedStat.Attack: return data.attack;
            case ModifiedStat.Defense: return data.defense;
            case ModifiedStat.Speed: return data.speed;
            case ModifiedStat.MaxHP: return data.maxHP;
            default: return 0;
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

    // --- Status effects (generic: stat modifiers, shields, crowd control) ---

    public void ApplyStatusEffect(StatusEffectData data, CharacterInstance source)
    {
        if (data == null) return;

        ActiveStatusEffect existing = activeEffects.FirstOrDefault(e => e.label == data.effectName);

        if (existing != null && data.stackable)
        {
            existing.stackCount = Mathf.Min(existing.stackCount + 1, data.maxStacks);
            existing.turnsRemaining = data.duration;
        }
        else if (existing != null)
        {
            existing.turnsRemaining = data.duration;
            if (data.category == StatusEffectCategory.Shield)
                existing.shieldRemaining = data.shieldAmount;
        }
        else
        {
            activeEffects.Add(new ActiveStatusEffect
            {
                label = data.effectName,
                icon = data.icon,
                category = data.category,
                isDebuff = data.isDebuff,
                modifiedStat = data.modifiedStat,
                isPercent = data.isPercent,
                flatAmount = data.flatAmount,
                percentAmount = data.percentAmount,
                shieldRemaining = data.shieldAmount,
                skipTurn = data.skipTurn,
                turnsRemaining = data.duration,
                stackCount = 1,
                maxStacks = data.maxStacks,
                stackable = data.stackable,
                source = source
            });
        }

        if (data.category == StatusEffectCategory.StatModifier)
            RecalculateStats();
    }

    // For procedurally generated modifiers that don't have a backing asset (e.g. stain-combo DEF shred).
    public void ApplyRawStatModifier(string label, ModifiedStat stat, int flatDelta, int duration, CharacterInstance source)
    {
        activeEffects.Add(new ActiveStatusEffect
        {
            label = label,
            category = StatusEffectCategory.StatModifier,
            isDebuff = flatDelta < 0,
            modifiedStat = stat,
            isPercent = false,
            flatAmount = flatDelta,
            turnsRemaining = duration,
            stackCount = 1,
            maxStacks = 1,
            stackable = false,
            source = source
        });

        RecalculateStats();
    }

    // Absorbs incoming damage into any active shields, oldest first, and returns the amount left to apply to HP.
    public int AbsorbDamage(int incomingDamage)
    {
        int remaining = incomingDamage;

        foreach (var shield in activeEffects.Where(e => e.category == StatusEffectCategory.Shield && e.shieldRemaining > 0))
        {
            if (remaining <= 0) break;

            int absorbed = Mathf.Min(shield.shieldRemaining, remaining);
            shield.shieldRemaining -= absorbed;
            remaining -= absorbed;
        }

        activeEffects.RemoveAll(e => e.category == StatusEffectCategory.Shield && e.shieldRemaining <= 0);

        return remaining;
    }

    public bool HasSkipTurnEffect()
    {
        return activeEffects.Any(e => e.category == StatusEffectCategory.CrowdControl && e.skipTurn);
    }

    // Call at the start of this character's own turn.
    public void TickStatusEffects()
    {
        bool anyStatModifierExpired = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            effect.turnsRemaining--;

            if (effect.turnsRemaining <= 0)
            {
                if (effect.category == StatusEffectCategory.StatModifier)
                    anyStatModifierExpired = true;

                activeEffects.RemoveAt(i);
            }
        }

        if (anyStatModifierExpired)
            RecalculateStats();
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

    // --- Status display for UI ---
    public List<StatusEffectInstance> GetStatusDisplayList()
    {
        var list = new List<StatusEffectInstance>();

        foreach (var effect in activeEffects)
        {
            string label = effect.category == StatusEffectCategory.Shield
                ? $"{effect.label} ({effect.shieldRemaining})"
                : effect.label;

            list.Add(new StatusEffectInstance { label = label, stackCount = effect.stackCount, icon = effect.icon });
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

        return list;
    }
}