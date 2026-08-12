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

    [Header("Targeting")]
    public TargetShape targetShape = TargetShape.Single;
    public TargetSide targetSide = TargetSide.Enemy;

    [Header("Status Effects")]
    [Range(0f, 1f)] public float freezeChance;

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