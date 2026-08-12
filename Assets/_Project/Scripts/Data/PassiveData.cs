using UnityEngine;

public enum PassiveTrigger
{
    OnBattleStart,
    OnTurnStart,
    OnDamageTaken,
    OnDamageDealt,
    OnAllyDeath,
    OnKill,
    OnAnyDeath, // any character on the field dies, either side
    PassiveStatScaling
}

[CreateAssetMenu(fileName = "NewPassive", menuName = "CardRPG/Passive")]
public class PassiveData : ScriptableObject
{
    [Header("Identity")]
    public string passiveName;
    [TextArea] public string description;

    [Header("Behavior")]
    public PassiveTrigger trigger;
    public int value; // heal amount for OnAnyDeath, generic magnitude otherwise

    [Header("Stat Scaling (only used if trigger = PassiveStatScaling)")]
    [Range(0f, 1f)] public float bonusHPPercent;
    [Range(0f, 1f)] public float bonusAttackPercent;
    [Range(0f, 1f)] public float bonusDefensePercent;
    [Range(0f, 1f)] public float bonusSpeedPercent;
}