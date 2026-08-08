using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Starting, WaitingForActor, PlayerTurn, EnemyTurn, Resolving, Victory, Defeat }

public class CombatController : MonoBehaviour
{
    private TurnOrderManager turnOrder;
    private PartyState partyState;

    public CombatState currentState;
    private CharacterInstance activeActor;

    private const float WEAKNESS_MULTIPLIER = 1.5f;

    [Header("Rewards")]
    public int goldReward = 50;

    public event Action OnStateChanged;

    public CharacterInstance ActiveActor => activeActor;
    public int CurrentSkillPoints => partyState.currentSkillPoints;
    public int MaxSkillPoints => partyState.maxSkillPoints;
    public IReadOnlyList<CharacterInstance> Allies => turnOrder.allies;
    public IReadOnlyList<CharacterInstance> Enemies => turnOrder.enemies;
    public bool IsPlayerTurn => currentState == CombatState.PlayerTurn;

    public void StartCombat(List<CharacterInstance> allies, List<CharacterInstance> enemies)
    {
        turnOrder = new TurnOrderManager(allies, enemies);
        partyState = new PartyState(allies);

        currentState = CombatState.Starting;
        AdvanceTurn();
    }

    private void AdvanceTurn()
    {
        activeActor = turnOrder.GetNextActor();

        if (activeActor == null)
        {
            Debug.LogWarning("No actors left to take a turn.");
            return;
        }

        bool isPlayerControlled = turnOrder.allies.Contains(activeActor);
        currentState = isPlayerControlled ? CombatState.PlayerTurn : CombatState.EnemyTurn;

        OnStateChanged?.Invoke();

        if (!isPlayerControlled)
        {
            ResolveEnemyAction();
        }
    }

    public void ResolvePlayerAction(AbilityData ability, List<CharacterInstance> targets)
    {
        if (!ValidateTargets(ability, targets))
        {
            Debug.LogWarning($"Invalid targets for {ability.abilityName}, action cancelled.");
            return;
        }

        if (!partyState.CanUseAbility(ability))
        {
            Debug.LogWarning($"Not enough SP to use {ability.abilityName}.");
            return;
        }

        currentState = CombatState.Resolving;

        ExecuteAbility(activeActor, ability, targets);
        partyState.ResolveAbilityCost(ability);

        OnStateChanged?.Invoke();

        EndTurn();
    }

    private bool ValidateTargets(AbilityData ability, List<CharacterInstance> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning($"{ability.abilityName}: no targets provided.");
            return false;
        }

        switch (ability.targetType)
        {
            case TargetType.SingleEnemy:
                return targets.Count == 1 && turnOrder.enemies.Contains(targets[0]);

            case TargetType.AllEnemies:
                return targets.Count == turnOrder.enemies.Count(e => e.isAlive) &&
                       targets.All(t => turnOrder.enemies.Contains(t));

            case TargetType.SingleAlly:
                return targets.Count == 1 && turnOrder.allies.Contains(targets[0]);

            case TargetType.AllAllies:
                return targets.Count == turnOrder.allies.Count(a => a.isAlive) &&
                       targets.All(t => turnOrder.allies.Contains(t));

            case TargetType.Self:
                return targets.Count == 1 && targets[0] == activeActor;

            default:
                return false;
        }
    }

    private void ResolveEnemyAction()
    {
        currentState = CombatState.Resolving;

        AbilityData chosenAbility = ChooseEnemyAbility(activeActor);
        List<CharacterInstance> targets = ChooseEnemyTargets(chosenAbility);

        if (chosenAbility != null && targets != null && targets.Count > 0)
        {
            Debug.Log($"{activeActor.data.characterName} (enemy) uses {chosenAbility.abilityName}.");
            ExecuteAbility(activeActor, chosenAbility, targets);
        }
        else
        {
            Debug.LogWarning($"{activeActor.data.characterName} had no valid action.");
        }

        OnStateChanged?.Invoke();

        EndTurn();
    }

    private AbilityData ChooseEnemyAbility(CharacterInstance enemy)
    {
        var validAbilities = enemy.activeAbilities.Where(a => a != null).ToList();
        if (validAbilities.Count == 0) return null;

        return validAbilities[UnityEngine.Random.Range(0, validAbilities.Count)];
    }

    private List<CharacterInstance> ChooseEnemyTargets(AbilityData ability)
    {
        if (ability == null) return null;

        switch (ability.targetType)
        {
            case TargetType.SingleEnemy:
                var aliveAllies = turnOrder.allies.Where(a => a.isAlive).ToList();
                if (aliveAllies.Count == 0) return null;
                return new List<CharacterInstance> { aliveAllies[UnityEngine.Random.Range(0, aliveAllies.Count)] };

            case TargetType.AllEnemies:
                return turnOrder.allies.Where(a => a.isAlive).ToList();

            case TargetType.SingleAlly:
                var aliveEnemies = turnOrder.enemies.Where(e => e.isAlive).ToList();
                if (aliveEnemies.Count == 0) return null;
                return new List<CharacterInstance> { aliveEnemies[UnityEngine.Random.Range(0, aliveEnemies.Count)] };

            case TargetType.AllAllies:
                return turnOrder.enemies.Where(e => e.isAlive).ToList();

            case TargetType.Self:
                return new List<CharacterInstance> { activeActor };

            default:
                return null;
        }
    }

    private void ExecuteAbility(CharacterInstance user, AbilityData ability, List<CharacterInstance> targets)
    {
        foreach (var target in targets)
        {
            if (ability.power <= 0) continue;

            bool isHeal = IsHealingAbility(ability, user, target);

            if (isHeal)
            {
                target.Heal(ability.power);
            }
            else
            {
                int damage = CalculateDamage(user, target, ability);
                target.TakeDamage(damage);
            }
        }
    }

    private int CalculateDamage(CharacterInstance attacker, CharacterInstance defender, AbilityData ability)
    {
        int raw = ability.power + attacker.currentAttack - defender.currentDefense;
        raw = Mathf.Max(1, raw);

        if (IsWeakTo(defender, ability.element))
        {
            raw = Mathf.RoundToInt(raw * WEAKNESS_MULTIPLIER);
        }

        return raw;
    }

    private bool IsWeakTo(CharacterInstance target, ElementType element)
    {
        if (target.data.weaknesses == null) return false;

        foreach (var w in target.data.weaknesses)
        {
            if (w == element) return true;
        }
        return false;
    }

    private bool IsHealingAbility(AbilityData ability, CharacterInstance user, CharacterInstance target)
    {
        return ability.targetType == TargetType.SingleAlly ||
               ability.targetType == TargetType.AllAllies ||
               ability.targetType == TargetType.Self;
    }

    private void EndTurn()
    {
        CombatResult result = turnOrder.CheckCombatState();

        if (result == CombatResult.Victory)
        {
            currentState = CombatState.Victory;
            Debug.Log("Victory!");

            if (PartyManager.Instance != null)
                PartyManager.Instance.AddGold(goldReward);

            OnStateChanged?.Invoke();
            return;
        }
        if (result == CombatResult.Defeat)
        {
            currentState = CombatState.Defeat;
            Debug.Log("Defeat.");
            OnStateChanged?.Invoke();
            return;
        }

        AdvanceTurn();
    }
}