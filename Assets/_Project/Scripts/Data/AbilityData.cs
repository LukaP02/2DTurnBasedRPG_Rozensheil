using UnityEngine;

public enum AbilityType { Basic, Skill, Ultimate }

[CreateAssetMenu(fileName = "NewAbility", menuName = "CardRPG/Ability")]
public class AbilityData : ScriptableObject
{
    public string abilityName;
    public Sprite icon;
    [TextArea] public string description;

    public AbilityType abilityType;
    public int spCost;
    public int spGain;

    public int power;
    public ElementType element = ElementType.Physical;

    [Header("Scaling")]
    [Tooltip("Multiplier applied to the caster's Attack stat and added to Power (1.0 = 100% ATK).")]
    public float attackScaling = 1f;
    [Tooltip("Multiplier applied to the caster's Max HP and added to Power (0 = no HP scaling).")]
    public float maxHPScaling = 0f;
    [Tooltip("Multiplier applied to the caster's Defense stat and added to Power (0 = no DEF scaling).")]
    public float defenseScaling = 0f;
    [Tooltip("Multiplier applied to the caster's Speed stat and added to Power (0 = no Speed scaling).")]
    public float speedScaling = 0f;

    [Header("Critical Hits")]
    [Tooltip("Whether this ability is allowed to roll a critical hit at all.")]
    public bool canCrit = true;

    [Header("Targeting")]
    public TargetShape targetShape = TargetShape.Single;
    public TargetSide targetSide = TargetSide.Enemy;

    [Header("Status Effects")]
    public StatusEffectData appliesStatusEffect;
    [Range(0f, 1f)] public float statusEffectChance = 1f;

    [Header("Marks (Abdul)")]
    public bool appliesMark;
    public int maxMarkStacks = 3;
    public bool consumesMark;
    public int bonusDamagePerMarkStack;

    [Header("Forms (Sicur)")]
    public bool triggersFormSwitch;
    public bool costsHPPercentOfMissing;
    [Range(0f, 1f)] public float hpCostPercent;
}