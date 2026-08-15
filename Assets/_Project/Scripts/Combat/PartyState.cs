using System.Collections.Generic;

public class PartyState
{
    public List<CharacterInstance> members;
    public int currentSkillPoints;
    public int maxSkillPoints = 5;

    public PartyState(List<CharacterInstance> partyMembers)
    {
        members = partyMembers;
        currentSkillPoints = maxSkillPoints;
    }

    public bool CanUseAbility(AbilityData ability)
    {
        return currentSkillPoints >= ability.spCost;
    }

    public void ResolveAbilityCost(AbilityData ability)
    {
        if (ability.spCost > 0)
            SpendSkillPoints(ability.spCost);

        if (ability.spGain > 0)
            GainSkillPoints(ability.spGain);
    }

    public void SpendSkillPoints(int amount)
    {
        currentSkillPoints -= amount;
        if (currentSkillPoints < 0) currentSkillPoints = 0;
    }

    public void GainSkillPoints(int amount)
    {
        currentSkillPoints += amount;
        if (currentSkillPoints > maxSkillPoints) currentSkillPoints = maxSkillPoints;
    }
}