public enum TargetShape
{
    Single,
    Cleave,  // target + 1 random adjacent
    Spread,  // target + left neighbor + right neighbor (up to 3)
    All
}

public enum TargetSide
{
    Enemy,   // opposing side relative to caster
    Ally,    // caster's own side
    Self,    // caster only
    Either   // player chooses ally or enemy at cast time
}