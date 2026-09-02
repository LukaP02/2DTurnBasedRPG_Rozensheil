using UnityEngine;

public enum CharacterRole { Damage, Tank, Sustain, Hybrid }

[CreateAssetMenu(fileName = "NewCharacter", menuName = "CardRPG/Character")]
public class CharacterCardData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite cardArt;
    [Tooltip("Small icon version of the art - used for turn order icons and the Loadout character list. Falls back to Card Art if left empty.")]
    public Sprite icon;
    [TextArea] public string description;

    [Header("Type")]
    public bool isPlayableCharacter = true;
    public CharacterRole role = CharacterRole.Damage;

    [Header("Base Stats")]
    public int maxHP;
    public int attack;
    public int defense;
    public int speed;

    [Header("Ultimate Energy")]
    [Tooltip("Energy required before this character's Ultimate can be used.")]
    public int maxEnergy = 100;
    [Tooltip("Energy gained from combat actions is multiplied by this (1.0 = 100%).")]
    public float energyRechargeRate = 1f;

    [Header("Critical Hits")]
    [Tooltip("Base chance to land a critical hit (0.05 = 5%).")]
    [Range(0f, 1f)] public float critRate = 0.05f;

    [Header("Targeting")]
    [Tooltip("Relative weight for being chosen as an enemy AI target. 1 = normal, 2 = twice as likely, etc.")]
    public float threatWeight = 1f;

    [Header("Elemental")]
    [Tooltip("Elements this character takes bonus damage from (see CombatController's fixed weakness multiplier).")]
    public ElementType[] weaknesses;
    [Tooltip("Resistance to elements not listed in Element Resistances below (and not a weakness) - 0 = no reduction, 1 = fully negated. E.g. a Gladiator resistant to everything a little, but especially to Fire/Holy: set this to 0.1, then add Fire and Holy to Element Resistances at 0.3 each.")]
    [Range(0f, 1f)] public float defaultResistancePercent = 0f;
    [Tooltip("Per-element resistance overrides - each replaces Default Resistance Percent above for that specific element. Leave empty to use the default for every non-weakness element.")]
    public ElementResistance[] resistances;

    [Header("Basic Attack - fixed (used if Basic Options is empty)")]
    public AbilityData basicAbility;

    [Header("Skill Tree - Unlockable Options (not used if Has Forms is true)")]
    public AbilityData[] basicOptions;
    public AbilityData[] skillOptions;
    public AbilityData[] ultimateOptions;

    [Header("Default / Starting Loadout")]
    public AbilityData defaultBasic;
    public AbilityData defaultSkill;
    public AbilityData defaultUltimate;
    public PassiveData passive;

    [Header("Forms (Sicur)")]
    public bool hasForms;
    public AbilityData normalFormBasic;
    public AbilityData normalFormSkill;
    public AbilityData demonFormBasic;
    public AbilityData demonFormSkill;
    public Sprite demonFormArt;

    [Header("Boss Phase Transition (optional, enemy-only)")]
    [Tooltip("If set, this enemy is swapped out for a brand-new card (its own HP/kit) once its HP drops to/below Phase Transition HP Percent. Does not count as a death.")]
    public CharacterCardData phaseTwoCard;
    [Tooltip("HP percent (0-1) at which the phase transition triggers.")]
    [Range(0f, 1f)] public float phaseTransitionHPPercent = 0.5f;
    [Tooltip("Optional dialogue that plays (pausing combat) during the transition. Leave empty to skip straight to Phase 2 with just the log message below.")]
    public DialogueSequence phaseTransitionDialogue;
    [TextArea] public string phaseTransitionMessage = "A new form emerges!";

    [Header("HP-Triggered Reinforcements (optional, enemy-only)")]
    [Tooltip("If set, this enemy calls in these reinforcements (added straight to the fight, this card is unaffected) the first time its HP drops to/below HP Reinforcement Percent. Independent of both the wave-encounter reinforcement pool and Boss Phase Transition above - use this for an enemy that calls for backup partway through the fight without itself changing.")]
    public CharacterCardData[] hpTriggeredReinforcements;
    [Tooltip("HP percent (0-1) at which the reinforcements are called in.")]
    [Range(0f, 1f)] public float hpTriggeredReinforcementPercent = 0.5f;
    [TextArea] public string hpTriggeredReinforcementMessage = "The Cyclops calls two wolves!";

    [Header("Enemy Multi-Ability Pool")]
    [Tooltip("Extra Basic abilities beyond Basic Ability. The AI rotates randomly among all available Basics.")]
    public AbilityData[] extraBasicAbilities;
    [Tooltip("Extra Skill abilities beyond Default Skill. The AI rotates randomly among all available Skills.")]
    public AbilityData[] extraSkillAbilities;
    [Tooltip("Extra Ultimate abilities beyond Default Ultimate. The AI rotates randomly among all available Ultimates.")]
    public AbilityData[] extraUltimateAbilities;
    [Tooltip("Enemy-only. Shows this character's HP on the boss health bar at the top of the combat screen (different art) instead of the normal per-card HP bar. If this character has a Phase Transition, flag Phase Two Card as a boss too so the top bar keeps showing after the swap.")]
    public bool isBoss = false;

    [System.Serializable]
    public class ElementResistance
    {
        public ElementType element;
        [Range(0f, 1f)] public float reductionPercent = 0f;
    }
}