using UnityEngine;

public enum PassiveTrigger
{
    OnBattleStart,
    OnTurnStart,
    OnDamageTaken,
    OnDamageDealt,
    OnAllyDeath,
    OnKill
}

[CreateAssetMenu(fileName = "NewPassive", menuName = "CardRPG/Passive")]
public class PassiveData : ScriptableObject
{
    [Header("Identity")]
    public string passiveName;
    [TextArea] public string description;

    [Header("Behavior")]
    public PassiveTrigger trigger;
    public int value; // generic magnitude — reuse for %, flat HP, SP gain, etc.
}