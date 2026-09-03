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
    [Tooltip("Blocks Skill and Ultimate abilities (Basic still usable) for the duration.")]
    public bool silences;

    [Header("Bonus Action (category = CrowdControl; buff, not a debuff)")]
    [Tooltip("While active, this character immediately acts again right after their own turn, for the duration.")]
    public bool grantsDoubleAction;

    [Header("Scaling with stats")]
    public bool scaleWithAttack;
    [Tooltip("Multiplier applied to the caster's Attack, added on top of the base amount.")]
    public float attackScaling = 0f;
    public bool scaleWithDefense;
    [Tooltip("Multiplier applied to the caster's Defense, added on top of the base amount.")]
    public float defenseScaling = 0f;
    public bool scaleWithMaxHP;
    [Tooltip("Multiplier applied to the caster's Max HP, added on top of the base amount.")]
    public float maxHPScaling = 0f;
    public bool scaleWithSpeed;
    [Tooltip("Multiplier applied to the caster's Speed, added on top of the base amount.")]
    public float speedScaling = 0f;
    [Header("Card Visual (optional)")]
    [Tooltip("If checked, this effect tints the affected character's card art for as long as it's active - e.g. bluish for Freeze, grayish for Petrified. Reverts to normal automatically once the effect ends.")]
    public bool tintsCardArt;
    [Tooltip("Only used if Tints Card Art is checked. Keep Alpha at 255 - this replaces the art's color outright rather than blending.")]
    public Color cardTintColor = Color.white;
}
