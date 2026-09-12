using UnityEngine;

public enum PassiveTrigger
{
    OnBattleStart,
    OnTurnStart,
    OnDamageTaken,
    OnDamageDealt,
    OnAllyDeath,
    OnKill,
    OnAnyDeath,
    EnablesStains,
    PassiveStatScaling
}

[CreateAssetMenu(fileName = "NewPassive", menuName = "CardRPG/Passive")]
public class PassiveData : ScriptableObject
{
    [Header("Identity")]
    public string passiveName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Behavior")]
    public PassiveTrigger trigger;
    public int value;
    [Tooltip("Optional sound played when this passive actually triggers (currently only fires for OnAnyDeath - see PassiveTrigger).")]
    public AudioClip triggerSound;

    [Header("Stat Scaling (only used if trigger = PassiveStatScaling)")]
    [Range(0f, 1f)] public float bonusHPPercent;
    [Range(0f, 1f)] public float bonusAttackPercent;
    [Range(0f, 1f)] public float bonusDefensePercent;
    [Range(0f, 1f)] public float bonusSpeedPercent;

    [Header("Stain Combos (only used if trigger = EnablesStains)")]
    [Tooltip("Fire + Ice: single-target bonus damage")]
    public int fireIceBonusDamage;

    [Tooltip("Fire + Electro: bonus damage that spreads to the target's neighbors too")]
    public int fireElectroSpreadDamage;

    [Tooltip("Ice + Electro: DEF shred, as a percent of the target's Defense (0.2 = -20%).")]
    [Range(0f, 1f)] public float defShredPercent;
    public int defShredDuration;
    [Tooltip("Icon shown for the DEF Shred status effect in the card detail panel. Leave empty to show it with no icon, just the label.")]
    public Sprite defShredIcon;
}