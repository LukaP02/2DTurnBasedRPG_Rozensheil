using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "CardRPG/Character")]
public class CharacterCardData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite cardArt;
    [TextArea] public string description;

    [Header("Type")]
    public bool isPlayableCharacter = true; // false for enemies

    [Header("Base Stats")]
    public int maxHP;
    public int attack;
    public int defense;
    public int speed;

    [Header("Elemental")]
    public ElementType[] weaknesses;

    [Header("Basic Attack (fixed, not swappable)")]
    public AbilityData basicAbility;

    [Header("Skill Tree - Unlockable Options")]
    public AbilityData[] skillOptions;
    public AbilityData[] ultimateOptions;

    [Header("Default / Starting Loadout")]
    public AbilityData defaultSkill;
    public AbilityData defaultUltimate;

    public PassiveData passive;
}