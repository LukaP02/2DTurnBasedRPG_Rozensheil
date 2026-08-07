using UnityEngine;

public enum TargetType { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self }
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
    public TargetType targetType;
    public ElementType element = ElementType.Physical;
}