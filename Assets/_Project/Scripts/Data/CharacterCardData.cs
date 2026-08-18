using UnityEngine;

public enum CharacterRole { Damage, Tank, Support, Utility }

[CreateAssetMenu(fileName = "NewCharacter", menuName = "CardRPG/Character")]
public class CharacterCardData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite cardArt;
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
    public ElementType[] weaknesses;
    public ElementType[] resistances;

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

    [Header("Enemy Multi-Ability Pool (non-playable characters, e.g. a boss with several skills)")]
    [Tooltip("Extra Basic abilities beyond Basic Ability. The AI rotates randomly among all available Basics.")]
    public AbilityData[] extraBasicAbilities;
    [Tooltip("Extra Skill abilities beyond Default Skill. The AI rotates randomly among all available Skills.")]
    public AbilityData[] extraSkillAbilities;
    [Tooltip("Extra Ultimate abilities beyond Default Ultimate. The AI rotates randomly among all available Ultimates.")]
    public AbilityData[] extraUltimateAbilities;


}