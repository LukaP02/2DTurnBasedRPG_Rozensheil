using System.Collections.Generic;
using UnityEngine;

public class CombatBootstrap : MonoBehaviour
{
    [Header("References")]
    public CombatController combatController;

    [Header("Starting Party (used only if PartyManager isn't initialized yet)")]
    public CharacterCardData[] allyData;

    [Header("Test Enemies")]
    public CharacterCardData[] enemyData;

    private void Start()
    {
        if (PartyManager.Instance != null)
        {
            // Initialize once; if already initialized elsewhere, this call is ignored
            PartyManager.Instance.InitializeParty(new List<CharacterCardData>(allyData));
        }

        List<CharacterInstance> allies = PartyManager.Instance != null
            ? PartyManager.Instance.GetPartyInstances()
            : BuildInstances(allyData); // fallback if no PartyManager in scene

        List<CharacterInstance> enemies = BuildInstances(enemyData);

        combatController.StartCombat(allies, enemies);
    }

    private List<CharacterInstance> BuildInstances(CharacterCardData[] dataArray)
    {
        List<CharacterInstance> instances = new List<CharacterInstance>();

        foreach (var data in dataArray)
        {
            if (data == null) continue;
            instances.Add(new CharacterInstance(data));
        }

        return instances;
    }
}