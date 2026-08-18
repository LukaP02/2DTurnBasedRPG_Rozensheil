using UnityEngine;


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
    public bool silences;
    public bool grantsDoubleAction;

    public int turnsRemaining;
    public int stackCount = 1;
    public int maxStacks = 1;
    public bool stackable;

    public CharacterInstance source;
}
