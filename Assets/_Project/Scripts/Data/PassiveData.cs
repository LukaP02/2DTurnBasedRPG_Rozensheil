using UnityEngine;

public enum PassiveTrigger
{
    OnBattleStart,
    OnTurnStart,
    OnDamageTaken,
    OnDamageDealt,
    OnAllyDeath,
    OnKill,
    PassiveStatScaling // always-on, applied during stat calculation rather than a combat event
}

[CreateAssetMenu(fileName = "NewPassive", menuName = "CardRPG/Passive")]
public class PassiveData : ScriptableObject
{
    [Header("Identity")]
    public string passiveName;
    [TextArea] public string description;

    [Header("Behavior")]
    public PassiveTrigger trigger;
    public int value; // generic magnitude for trigger-based passives (unused for stat scaling)

    [Header("Stat Scaling (only used if trigger = PassiveStatScaling)")]
    [Range(0f, 1f)] public float bonusHPPercent;
    [Range(0f, 1f)] public float bonusAttackPercent;
    [Range(0f, 1f)] public float bonusDefensePercent;
    [Range(0f, 1f)] public float bonusSpeedPercent;
}