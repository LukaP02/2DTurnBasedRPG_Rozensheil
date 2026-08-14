using UnityEngine;

// Runtime instance of a StatusEffectData applied to a CharacterInstance.
// Fields are copied from the source data at apply time so effects generated
// procedurally (e.g. elemental stain combos) don't need a backing asset.
public class ActiveStatusEffect
{
    public string label;
    public Sprite icon;
    public StatusEffectCategory category;
    public bool isDebuff;

    public ModifiedStat modifiedStat;
    public bool isPercent;
    public int flatAmount;
    public float percentAmount;

    public int shieldRemaining;

    public bool skipTurn;

    public int turnsRemaining;
    public int stackCount = 1;
    public int maxStacks = 1;
    public bool stackable;

    public CharacterInstance source;
}
