using UnityEngine;

public enum StatusEffectCategory
{
    StatModifier,
    Shield,
    CrowdControl
}

public enum ModifiedStat
{
    Attack,
    Defense,
    Speed,
    MaxHP
}

[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "CardRPG/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("Identity")]
    public string effectName;
    public Sprite icon;
    [TextArea] public string description;

    public StatusEffectCategory category;
    public bool isDebuff;

    [Header("Duration")]
    [Tooltip("Ticks down by 1 at the start of the affected character's own turn.")]
    public int duration = 1;
    public bool stackable;
    public int maxStacks = 1;

    [Header("Stat Modifier (category = StatModifier)")]
    public ModifiedStat modifiedStat;
    public bool isPercent;
    public int flatAmount;
    [Range(-1f, 2f)] public float percentAmount;

    [Header("Shield (category = Shield)")]
    public int shieldAmount;

    [Header("Crowd Control (category = CrowdControl)")]
    public bool skipTurn;
}
