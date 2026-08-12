using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "CardRPG/Character")]
public class CharacterCardData : ScriptableObject
{
    [Header("Identity")]
    public string characterName;
    public Sprite cardArt;
    [TextArea] public string description;

    [Header("Type")]
    public bool isPlayableCharacter = true;

    [Header("Base Stats")]
    public int maxHP;
    public int attack;
    public int defense;
    public int speed;

    [Header("Targeting")]
    [Tooltip("Relative weight for being chosen as an enemy AI target. 1 = normal, 2 = twice as likely, etc.")]
    public float threatWeight = 1f;

    [Header("Elemental")]
    public ElementType[] weaknesses;

    [Header("Basic Attack (fixed, not swappable) - used only if Has Forms is false")]
    public AbilityData basicAbility;

    [Header("Skill Tree - Unlockable Options (not used if Has Forms is true)")]
    public AbilityData[] skillOptions;
    public AbilityData[] ultimateOptions;

    [Header("Default / Starting Loadout")]
    public AbilityData defaultSkill;
    public AbilityData defaultUltimate;

    [Header("Forms (Sicur)")]
    public bool hasForms;
    public AbilityData normalFormBasic;
    public AbilityData normalFormSkill;
    public AbilityData demonFormBasic;
    public AbilityData demonFormSkill;

    public PassiveData passive;
}