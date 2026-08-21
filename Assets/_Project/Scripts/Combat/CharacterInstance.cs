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
    public float currentCritRate;
    public float currentEnergyRechargeRate;
    public List<AbilityData> activeAbilities;
    public bool isAlive => currentHP > 0;

    public CharacterForm currentForm = CharacterForm.Normal;

    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    private Dictionary<CharacterInstance, int> marksBySource = new Dictionary<CharacterInstance, int>();
    private bool deathProcessed = false;
    public bool phaseTransitionProcessed = false;

    // --- Elemental stain (only one active at a time; the most recent application replaces it) ---
    private ElementType? currentStain = null;

    public CharacterInstance(CharacterCardData sourceData)
    {
        data = sourceData;

        RecalculateStats();
        RefreshAbilities(); // also sets maxEnergy - see RefreshAbilities

        currentHP = maxHP;
        currentEnergy = 0;
    }

    // --- Ultimate energy ---
    // Each Ultimate ability sets its own energyCost, checked against this character's banked
    // currentEnergy (capped at maxEnergy). Lets different Ultimates - including several on the
    // same boss - require different amounts.
    public bool HasEnoughEnergyFor(AbilityData ability)
    {
        return ability != null && currentEnergy >= ability.energyCost;
    }

    // amount is scaled by this character's Energy Recharge Rate before being applied.
    public void GainEnergy(int amount)
    {
        int scaledAmount = Mathf.RoundToInt(amount * currentEnergyRechargeRate);
        currentEnergy = Mathf.Min(currentEnergy + scaledAmount, maxEnergy);
    }

    // Subtracts the specific Ultimate's cost rather than resetting to 0, so any banked energy
    // beyond that cost carries over (relevant once a character/boss has more than one Ultimate).
    public void ConsumeEnergyForUltimate(AbilityData ability)
    {
        currentEnergy = Mathf.Max(0, currentEnergy - ability.energyCost);
    }

    // For playable characters, the energy cap is derived from their currently equipped Ultimate
    // (Energy Cost, or Energy Cost x Max Energy Stacks if it's Stackable Energy) rather than a
    // separate Max Energy field. Non-playable characters (enemies) keep using
    // CharacterCardData.maxEnergy directly. Called from RefreshAbilities(), so the cap stays in
    // sync automatically whenever a player's equipped Ultimate changes via the loadout screen.
    private void RecalculateMaxEnergy()
    {
        if (data.isPlayableCharacter)
        {
            AbilityData ultimate = activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Ultimate);
            if (ultimate != null)
            {
                int stacks = ultimate.stackableEnergy ? Mathf.Max(1, ultimate.maxEnergyStacks) : 1;
                maxEnergy = ultimate.energyCost * stacks;
            }
            else
            {
                maxEnergy = data.maxEnergy > 0 ? data.maxEnergy : 100;
            }
        }
        else
        {
            maxEnergy = data.maxEnergy > 0 ? data.maxEnergy : 100;
        }

        currentEnergy = Mathf.Min(currentEnergy, maxEnergy);
    }
    public void RecalculateStats()
    {
        int hpBonus = 0, attackBonus = 0, defenseBonus = 0, speedBonus = 0;
        float critRateBonus = 0f, energyRechargeBonus = 0f;

        if (data.isPlayableCharacter && PartyManager.Instance != null)
        {
            foreach (var equippedItem in PartyManager.Instance.GetEquippedItems(data))
            {
                if (equippedItem == null) continue;

                hpBonus += equippedItem.hpBonus;
                attackBonus += equippedItem.attackBonus;
                defenseBonus += equippedItem.defenseBonus;
                speedBonus += equippedItem.speedBonus;
                critRateBonus += equippedItem.critRateBonus;
                energyRechargeBonus += equippedItem.energyRechargeBonus;
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
        currentCritRate = Mathf.Clamp01(data.critRate + critRateBonus);
        currentEnergyRechargeRate = Mathf.Max(0f, data.energyRechargeRate + energyRechargeBonus);

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
            if (data.extraBasicAbilities != null)
                activeAbilities.AddRange(data.extraBasicAbilities.Where(a => a != null));

            if (data.defaultSkill != null) activeAbilities.Add(data.defaultSkill);
            if (data.extraSkillAbilities != null)
                activeAbilities.AddRange(data.extraSkillAbilities.Where(a => a != null));

            if (data.defaultUltimate != null) activeAbilities.Add(data.defaultUltimate);
            if (data.extraUltimateAbilities != null)
                activeAbilities.AddRange(data.extraUltimateAbilities.Where(a => a != null));
        }

        RecalculateMaxEnergy();
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
            activeEffects.Clear(); // buffs/debuffs/shields don't persist on a dead character
            return true;
        }
        return false;
    }

    // --- Status effects (generic: stat modifiers, shields, crowd control) ---

    public void ApplyStatusEffect(StatusEffectData data, CharacterInstance source)
    {
        if (data == null) return;

        int scalingBonus = ComputeStatusScalingBonus(data, source);

        ActiveStatusEffect existing = activeEffects.FirstOrDefault(e => e.label == data.effectName);

        if (existing != null && data.stackable)
        {
            existing.stackCount = Mathf.Min(existing.stackCount + 1, data.maxStacks);
            existing.turnsRemaining = data.duration;

            // Stackable shields add their (scaled) amount on top of what's already up, capped at
            // this character's Max HP rather than being replaced by the fresh application.
            if (data.category == StatusEffectCategory.Shield)
            {
                int addedShield = data.shieldAmount + scalingBonus;
                existing.shieldRemaining = Mathf.Min(existing.shieldRemaining + addedShield, maxHP);
            }
        }
        else if (existing != null)
        {
            existing.turnsRemaining = data.duration;
            if (data.category == StatusEffectCategory.Shield)
                existing.shieldRemaining = Mathf.Min(data.shieldAmount + scalingBonus, maxHP);
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
                flatAmount = data.isPercent ? data.flatAmount : data.flatAmount + scalingBonus,
                percentAmount = data.percentAmount,
                shieldRemaining = Mathf.Min(data.shieldAmount + scalingBonus, maxHP),
                skipTurn = data.skipTurn,
                silences = data.silences,
                grantsDoubleAction = data.grantsDoubleAction,
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

    // Generic stat-scaling bonus for status effects (e.g. a shield that scales with the caster's
    // Defense). Applies to Shield Amount for shields and Flat Amount for non-percent stat modifiers.
    // Toggle the relevant "scale with X" box in the Inspector to opt an effect into this.
    private int ComputeStatusScalingBonus(StatusEffectData data, CharacterInstance source)
    {
        if (source == null) return 0;

        float bonus = 0f;
        if (data.scaleWithAttack) bonus += source.currentAttack * data.attackScaling;
        if (data.scaleWithDefense) bonus += source.currentDefense * data.defenseScaling;
        if (data.scaleWithMaxHP) bonus += source.maxHP * data.maxHPScaling;
        if (data.scaleWithSpeed) bonus += source.currentSpeed * data.speedScaling;

        return Mathf.RoundToInt(bonus);
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
    // Blocks Skill and Ultimate abilities; Basic is still usable.
    public bool IsSilenced()
    {
        return activeEffects.Any(e => e.category == StatusEffectCategory.CrowdControl && e.silences);
    }

    // While true, this character immediately acts again right after their own turn (Abdul's 3rd Ultimate option).
    public bool HasDoubleActionBuff()
    {
        return activeEffects.Any(e => e.grantsDoubleAction);
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

    // --- Elemental stain ---
    public ElementType? CurrentStain => currentStain;

    public void SetStain(ElementType element)
    {
        currentStain = element;
    }

    public void ClearStain()
    {
        currentStain = null;
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

        if (currentStain.HasValue)
        {
            list.Add(new StatusEffectInstance { label = $"{currentStain.Value} Stain", stackCount = 1 });
        }

        return list;
    }
}