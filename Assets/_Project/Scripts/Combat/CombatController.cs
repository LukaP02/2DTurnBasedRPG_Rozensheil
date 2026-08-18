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
    private const float RESISTANCE_MULTIPLIER = 0.5f;
    private const float CRIT_DAMAGE_MULTIPLIER = 1.5f;
    private const float DAMAGE_VARIANCE = 0.1f; // damage from every source rolls +/- this fraction

    private const int BASIC_ENERGY_GAIN = 20;
    private const int SKILL_ENERGY_GAIN = 10;
    private const int DAMAGE_TAKEN_ENERGY_GAIN = 10;

    // Enemy AI tuning: how often enemies prefer their Skill over their Basic (when both are usable),
    // and how often they focus the lowest-HP% target instead of picking by threat weight.
    private const float ENEMY_SKILL_PREFERENCE_CHANCE = 0.6f;
    private const float ENEMY_FOCUS_LOWEST_HP_CHANCE = 0.4f;

    [Header("Rewards")]
    public int goldReward = 50;

    // --- Wave encounter (optional, configured per level via ConfigureWaveEncounter) ---
    private int maxEnemiesOnField;
    private List<CharacterCardData> reinforcementQueue = new List<CharacterCardData>();
    private int killTarget;
    private int killCount;
    private DialogueSequence midBattleDialogue;
    private string wipeMessage;
    private bool waitingForMidBattleSequence;

    public event Action OnStateChanged;
    public event Action<CharacterInstance, int> OnHealApplied;
    public event Action<CharacterInstance, int, ElementType> OnDamageApplied;
    public event Action<string> OnCombatLogMessage;
    public event Action<DialogueSequence> OnMidBattleDialogueRequested;
    public event Action<CharacterInstance> OnEnemyReinforced;

    // Logs to the console and broadcasts to any on-screen combat log listener.
    private void LogMessage(string message)
    {
        Debug.Log(message);
        OnCombatLogMessage?.Invoke(message);
    }

    public CharacterInstance ActiveActor => activeActor;
    public int CurrentSkillPoints => partyState.currentSkillPoints;
    public int MaxSkillPoints => partyState.maxSkillPoints;
    public IReadOnlyList<CharacterInstance> Allies => turnOrder.allies;
    public IReadOnlyList<CharacterInstance> Enemies => turnOrder.enemies;
    public List<CharacterInstance> UpcomingTurnOrder => turnOrder.PeekUpcomingOrder();
    public bool IsPlayerTurn => currentState == CombatState.PlayerTurn;

    public void StartCombat(List<CharacterInstance> allies, List<CharacterInstance> enemies)
    {
        turnOrder = new TurnOrderManager(allies, enemies);
        partyState = new PartyState(allies);

        currentState = CombatState.Starting;
        AdvanceTurn();
    }

    // Call before StartCombat to opt this fight into a wave encounter. Pass maxOnField <= 0 to
    // leave it as a normal fight (default board-clear victory, no reinforcements).
    public void ConfigureWaveEncounter(int maxOnField, CharacterCardData[] reinforcements, int killTarget, DialogueSequence midBattleDialogue, string wipeMessage)
    {
        maxEnemiesOnField = maxOnField;
        reinforcementQueue = reinforcements != null ? new List<CharacterCardData>(reinforcements) : new List<CharacterCardData>();
        this.killTarget = killTarget;
        this.midBattleDialogue = midBattleDialogue;
        this.wipeMessage = string.IsNullOrEmpty(wipeMessage) ? "The remaining enemies are struck down!" : wipeMessage;
        killCount = 0;
        waitingForMidBattleSequence = false;
    }

    private void AdvanceTurn()
    {
        activeActor = turnOrder.GetNextActor();

        if (activeActor == null)
        {
            Debug.LogWarning("No actors left to take a turn.");
            return;
        }

        bool skipsTurn = activeActor.HasSkipTurnEffect();

        activeActor.TickStatusEffects();

        if (skipsTurn)
        {
            LogMessage($"{activeActor.data.characterName} is frozen and skips their turn.");

            currentState = CombatState.Resolving;
            OnStateChanged?.Invoke();

            EndTurn();
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

        if (ability.abilityType == AbilityType.Ultimate)
        {
            if (!activeActor.HasEnoughEnergyFor(ability))
            {
                Debug.LogWarning($"{ability.abilityName} is not charged yet.");
                return;
            }
        }
        else if (!partyState.CanUseAbility(ability))
        {
            Debug.LogWarning($"Not enough SP to use {ability.abilityName}.");
            return;
        }

        currentState = CombatState.Resolving;

        ExecuteAbility(activeActor, ability, targets);

        if (ability.abilityType != AbilityType.Ultimate)
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

        if (targets.Any(t => !t.isAlive))
        {
            Debug.LogWarning($"{ability.abilityName}: target list contains a dead character.");
            return false;
        }

        if (ability.targetSide == TargetSide.Self)
        {
            return targets.Count == 1 && targets[0] == activeActor;
        }

        bool allAreAllies = targets.All(t => turnOrder.allies.Contains(t));
        bool allAreEnemies = targets.All(t => turnOrder.enemies.Contains(t));

        if (!allAreAllies && !allAreEnemies)
        {
            Debug.LogWarning($"{ability.abilityName}: targets span both sides.");
            return false;
        }

        switch (ability.targetShape)
        {
            case TargetShape.Single:
                return targets.Count == 1;

            case TargetShape.Cleave:
                return targets.Count >= 1 && targets.Count <= 2;

            case TargetShape.Spread:
                return targets.Count >= 1 && targets.Count <= 3;

            case TargetShape.All:
                int expectedCount = allAreAllies
                    ? turnOrder.allies.Count(a => a.isAlive)
                    : turnOrder.enemies.Count(e => e.isAlive);
                return targets.Count == expectedCount;

            default:
                return false;
        }
    }

    private void ResolveEnemyAction()
    {
        currentState = CombatState.Resolving;

        AbilityData chosenAbility = ChooseEnemyAbility(activeActor);
        List<CharacterInstance> targets = ChooseEnemyTargets(activeActor, chosenAbility);

        if (chosenAbility != null && targets != null && targets.Count > 0)
        {
            ExecuteAbility(activeActor, chosenAbility, targets);
        }
        else
        {
            Debug.LogWarning($"{activeActor.data.characterName} had no valid action.");
        }

        OnStateChanged?.Invoke();

        EndTurn();
    }

    // Priority order: use a charged Ultimate whenever available, otherwise lean toward Skill
    // over Basic (ENEMY_SKILL_PREFERENCE_CHANCE of the time), falling back to whichever exists.
    // When a side has multiple abilities of the chosen type (e.g. a boss with several Skills),
    // one is picked at random from that pool rather than always using the first one.
    private AbilityData ChooseEnemyAbility(CharacterInstance enemy)
    {
        var validAbilities = enemy.activeAbilities
            .Where(a => a != null && (a.abilityType != AbilityType.Ultimate || enemy.IsUltimateReady))
            .ToList();
        if (validAbilities.Count == 0) return null;

        var ultimates = validAbilities.Where(a => a.abilityType == AbilityType.Ultimate).ToList();
        if (ultimates.Count > 0)
            return ultimates[UnityEngine.Random.Range(0, ultimates.Count)];

        var skills = validAbilities.Where(a => a.abilityType == AbilityType.Skill).ToList();
        var basics = validAbilities.Where(a => a.abilityType == AbilityType.Basic).ToList();

        List<AbilityData> pool = (skills.Count > 0 && basics.Count > 0)
            ? (UnityEngine.Random.value < ENEMY_SKILL_PREFERENCE_CHANCE ? skills : basics)
            : (skills.Count > 0 ? skills : basics);

        if (pool.Count > 0)
            return pool[UnityEngine.Random.Range(0, pool.Count)];

        return validAbilities[UnityEngine.Random.Range(0, validAbilities.Count)];
    }

    private List<CharacterInstance> ChooseEnemyTargets(CharacterInstance caster, AbilityData ability)
    {
        if (ability == null) return null;

        if (ability.targetSide == TargetSide.Self)
            return new List<CharacterInstance> { caster };

        TargetSide effectiveSide = ability.targetSide;
        if (effectiveSide == TargetSide.Either)
            effectiveSide = (UnityEngine.Random.value < 0.5f) ? TargetSide.Enemy : TargetSide.Ally;

        List<CharacterInstance> pool = ResolveSidePool(caster, effectiveSide);
        if (pool == null || pool.Count == 0) return null;

        if (ability.targetShape == TargetShape.All)
            return pool;

        CharacterInstance chosen = ChooseWeightedTarget(pool);

        if (ability.targetShape == TargetShape.Single)
            return new List<CharacterInstance> { chosen };

        return BuildTargetGroup(ability.targetShape, chosen);
    }

    // Picks a target from the pool: ENEMY_FOCUS_LOWEST_HP_CHANCE of the time it focuses whoever has
    // the lowest HP%, otherwise it rolls weighted by each candidate's threatWeight (higher = more likely).
    private CharacterInstance ChooseWeightedTarget(List<CharacterInstance> pool)
    {
        if (pool.Count == 1)
            return pool[0];

        if (UnityEngine.Random.value < ENEMY_FOCUS_LOWEST_HP_CHANCE)
            return pool.OrderBy(c => (float)c.currentHP / c.maxHP).First();

        float totalWeight = pool.Sum(c => Mathf.Max(c.data.threatWeight, 0.01f));
        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;

        foreach (var candidate in pool)
        {
            cumulative += Mathf.Max(candidate.data.threatWeight, 0.01f);
            if (roll <= cumulative)
                return candidate;
        }

        return pool[pool.Count - 1];
    }

    private List<CharacterInstance> ResolveSidePool(CharacterInstance caster, TargetSide side)
    {
        bool casterIsAlly = turnOrder.allies.Contains(caster);

        if (side == TargetSide.Enemy)
        {
            return casterIsAlly
                ? turnOrder.enemies.Where(e => e.isAlive).ToList()
                : turnOrder.allies.Where(a => a.isAlive).ToList();
        }

        if (side == TargetSide.Ally)
        {
            return casterIsAlly
                ? turnOrder.allies.Where(a => a.isAlive).ToList()
                : turnOrder.enemies.Where(e => e.isAlive).ToList();
        }

        return null;
    }

    public List<CharacterInstance> BuildTargetGroup(TargetShape shape, CharacterInstance clicked)
    {
        List<CharacterInstance> sideList = turnOrder.allies.Contains(clicked)
            ? turnOrder.allies.Where(a => a.isAlive).ToList()
            : turnOrder.enemies.Where(e => e.isAlive).ToList();

        int index = sideList.IndexOf(clicked);
        List<CharacterInstance> result = new List<CharacterInstance> { clicked };

        if (shape == TargetShape.Cleave)
        {
            List<CharacterInstance> neighbors = new List<CharacterInstance>();
            if (index - 1 >= 0) neighbors.Add(sideList[index - 1]);
            if (index + 1 < sideList.Count) neighbors.Add(sideList[index + 1]);

            if (neighbors.Count > 0)
                result.Add(neighbors[UnityEngine.Random.Range(0, neighbors.Count)]);
        }
        else if (shape == TargetShape.Spread)
        {
            if (index - 1 >= 0) result.Add(sideList[index - 1]);
            if (index + 1 < sideList.Count) result.Add(sideList[index + 1]);
        }

        return result;
    }

    private void ExecuteAbility(CharacterInstance user, AbilityData ability, List<CharacterInstance> targets)
    {
        LogMessage($"{user.data.characterName} uses {ability.abilityName}!");

        bool userIsAlly = turnOrder.allies.Contains(user);

        if (ability.costsHPPercentOfMissing)
        {
            int hpCost = Mathf.RoundToInt(user.maxHP * ability.hpCostPercent);

            if (hpCost > 0)
            {
                user.TakeDamage(hpCost);
                OnDamageApplied?.Invoke(user, hpCost, ElementType.Physical); // self-cost isn't elemental, treat as Physical


                if (user.CheckAndMarkDeath())
                    HandleDeath(user);
            }
        }

        foreach (var target in targets)
        {
            bool targetIsSameSideAsUser =
                (userIsAlly && turnOrder.allies.Contains(target)) ||
                (!userIsAlly && turnOrder.enemies.Contains(target));

            if (ability.power > 0)
            {
                if (targetIsSameSideAsUser)
                {
                    target.Heal(ability.power);
                    OnHealApplied?.Invoke(target, ability.power);
                }
                else
                {
                    int bonusFromMarks = 0;
                    if (ability.consumesMark)
                    {
                        int stacks = target.ConsumeMarkStacks(user);
                        bonusFromMarks = stacks * ability.bonusDamagePerMarkStack;
                    }

                    int rawDamage = CalculateDamage(user, target, ability) + bonusFromMarks;
                    DealDamage(target, rawDamage, ability.element);

                    if (ability.appliesMark)
                    {
                        target.AddMarkStack(user, ability.maxMarkStacks);
                    }

                    TryApplyElementalStain(user, target, ability.element);
                }
            }

            if (ability.appliesStatusEffect != null && UnityEngine.Random.value <= ability.statusEffectChance)
            {
                target.ApplyStatusEffect(ability.appliesStatusEffect, user);
            }
        }

        if (ability.triggersFormSwitch)
        {
            user.ToggleForm();
        }

        switch (ability.abilityType)
        {
            case AbilityType.Ultimate:
                user.ConsumeEnergyForUltimate(ability);
                break;
            case AbilityType.Basic:
                user.GainEnergy(BASIC_ENERGY_GAIN);
                break;
            case AbilityType.Skill:
                user.GainEnergy(SKILL_ENERGY_GAIN);
                break;
        }
    }

    // Single entry point for applying damage: rolls the +/-10% variance, absorbs into shields,
    // then HP, then fires the shared events. Every damage source (abilities, stain combos) funnels
    // through here, so the variance roll applies uniformly without each call site handling it.
    private void DealDamage(CharacterInstance target, int amount, ElementType element)
    {
        int variedAmount = ApplyDamageVariance(amount);
        int actualDamage = target.AbsorbDamage(variedAmount);
        target.TakeDamage(actualDamage);
        target.GainEnergy(DAMAGE_TAKEN_ENERGY_GAIN);
        OnDamageApplied?.Invoke(target, actualDamage, element);

        if (target.CheckAndMarkDeath())
            HandleDeath(target);
    }

    private int ApplyDamageVariance(int amount)
    {
        float multiplier = 1f + UnityEngine.Random.Range(-DAMAGE_VARIANCE, DAMAGE_VARIANCE);
        return Mathf.Max(1, Mathf.RoundToInt(amount * multiplier));
    }

    // Single entry point for any character's death: fires the existing on-any-death passives, and if
    // the dead character was an enemy, feeds the wave-encounter bookkeeping (kill count / reinforcements).
    private void HandleDeath(CharacterInstance character)
    {
        TriggerOnAnyDeathPassives();

        if (turnOrder.enemies.Contains(character))
            HandleEnemyDeath(character);
    }

    private void HandleEnemyDeath(CharacterInstance deadEnemy)
    {
        killCount++;

        if (!waitingForMidBattleSequence && killTarget > 0 && killCount >= killTarget)
        {
            waitingForMidBattleSequence = true;
            OnMidBattleDialogueRequested?.Invoke(midBattleDialogue);
            return;
        }

        if (!waitingForMidBattleSequence)
            TrySpawnReinforcement();
    }

    private void TrySpawnReinforcement()
    {
        if (maxEnemiesOnField <= 0 || reinforcementQueue.Count == 0)
            return;

        int aliveEnemies = turnOrder.enemies.Count(e => e.isAlive);
        if (aliveEnemies >= maxEnemiesOnField)
            return;

        CharacterCardData nextData = reinforcementQueue[0];
        reinforcementQueue.RemoveAt(0);

        CharacterInstance reinforcement = new CharacterInstance(nextData);
        turnOrder.AddEnemy(reinforcement);
        OnEnemyReinforced?.Invoke(reinforcement);

        LogMessage("More enemies have joined the battle!");
    }

    // Called by whoever shows the mid-battle dialogue once it's been closed. Instantly defeats every
    // remaining living enemy (narrative "finishing blow"), then resumes normal turn flow, which will
    // find the board clear and end combat in victory as usual.
    public void ResolveMidBattleWipe()
    {
        List<CharacterInstance> remaining = turnOrder.enemies.Where(e => e.isAlive).ToList();

        LogMessage(wipeMessage);

        foreach (var enemy in remaining)
        {
            int amount = enemy.currentHP;
            enemy.TakeDamage(amount);
            OnDamageApplied?.Invoke(enemy, amount, ElementType.Physical);

            if (enemy.CheckAndMarkDeath())
                TriggerOnAnyDeathPassives();
        }

        waitingForMidBattleSequence = false;
        OnStateChanged?.Invoke();
        EndTurn();
    }

    private CharacterInstance FindStainEnabler()
    {
        var all = turnOrder.allies.Concat(turnOrder.enemies);
        return all.FirstOrDefault(c => c.isAlive && c.data.passive != null && c.data.passive.trigger == PassiveTrigger.EnablesStains);
    }

    // Stains only function while the enabling character (Zavren, in the current content) is alive
    // and in the fight, but ANY character's elemental hit can apply/overwrite one while he's present.
    // Only the enabler's own hit can detonate a combo: if their hit lands a different element than the
    // one already on the target, it resolves immediately and clears the stain. Zavren can therefore
    // either land both halves of a combo himself, or land the second (triggering) half after an ally
    // already applied the first. A non-enabler landing a different element just overwrites the stain
    // (most-recent-wins) without triggering anything, per the single-stain-per-target rule.
    private void TryApplyElementalStain(CharacterInstance user, CharacterInstance target, ElementType element)
    {
        if (element != ElementType.Fire && element != ElementType.Ice && element != ElementType.Electro)
            return;

        CharacterInstance enabler = FindStainEnabler();
        if (enabler == null)
            return;

        ElementType? existingStain = target.CurrentStain;

        if (existingStain.HasValue && existingStain.Value != element && user == enabler)
        {
            ResolveStainCombo(enabler, target, existingStain.Value, element);
            target.ClearStain();
        }
        else
        {
            target.SetStain(element);
        }
    }

    private void ResolveStainCombo(CharacterInstance enabler, CharacterInstance target, ElementType a, ElementType b)
    {
        PassiveData passive = enabler.data.passive;

        bool isFireIce = (a == ElementType.Fire && b == ElementType.Ice) || (a == ElementType.Ice && b == ElementType.Fire);
        bool isFireElectro = (a == ElementType.Fire && b == ElementType.Electro) || (a == ElementType.Electro && b == ElementType.Fire);
        bool isIceElectro = (a == ElementType.Ice && b == ElementType.Electro) || (a == ElementType.Electro && b == ElementType.Ice);

        if (isFireIce)
        {
            Debug.Log($"Stain combo (Fire+Ice): bonus damage to {target.data.characterName}.");
            DealDamage(target, passive.fireIceBonusDamage, ElementType.Fire);
        }
        else if (isFireElectro)
        {
            Debug.Log($"Stain combo (Fire+Electro): spread damage from {target.data.characterName}.");

            List<CharacterInstance> spreadTargets = BuildTargetGroup(TargetShape.Spread, target);

            foreach (var spreadTarget in spreadTargets)
            {
                DealDamage(spreadTarget, passive.fireElectroSpreadDamage, ElementType.Fire);
            }
        }
        else if (isIceElectro)
        {
            Debug.Log($"Stain combo (Ice+Electro): {target.data.characterName} DEF shredded.");
            target.ApplyRawStatModifier("DEF Shred", ModifiedStat.Defense, -passive.defShredAmount, passive.defShredDuration, enabler);
        }
    }

    private void TriggerOnAnyDeathPassives()
    {
        List<CharacterInstance> everyone = new List<CharacterInstance>();
        everyone.AddRange(turnOrder.allies);
        everyone.AddRange(turnOrder.enemies);

        foreach (var character in everyone)
        {
            if (!character.isAlive) continue;
            if (character.data.passive == null) continue;
            if (character.data.passive.trigger != PassiveTrigger.OnAnyDeath) continue;

            character.Heal(character.data.passive.value);
            OnHealApplied?.Invoke(character, character.data.passive.value);
        }
    }

    private int CalculateDamage(CharacterInstance attacker, CharacterInstance defender, AbilityData ability)
    {
        int raw = ability.power
            + Mathf.RoundToInt(attacker.currentAttack * ability.attackScaling)
            + Mathf.RoundToInt(attacker.maxHP * ability.maxHPScaling)
            + Mathf.RoundToInt(attacker.currentDefense * ability.defenseScaling)
            + Mathf.RoundToInt(attacker.currentSpeed * ability.speedScaling)
            - defender.currentDefense;
        raw = Mathf.Max(1, raw);

        if (IsWeakTo(defender, ability.element))
        {
            raw = Mathf.RoundToInt(raw * WEAKNESS_MULTIPLIER);
        }
        else if (IsResistantTo(defender, ability.element))
        {
            raw = Mathf.RoundToInt(raw * RESISTANCE_MULTIPLIER);
        }

        if (ability.canCrit && UnityEngine.Random.value < attacker.currentCritRate)
        {
            raw = Mathf.RoundToInt(raw * CRIT_DAMAGE_MULTIPLIER);
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

    private bool IsResistantTo(CharacterInstance target, ElementType element)
    {
        if (target.data.resistances == null) return false;

        foreach (var r in target.data.resistances)
        {
            if (r == element) return true;
        }
        return false;
    }

    private void EndTurn()
    {
        // Combat is paused for the mid-battle dialogue; ResolveMidBattleWipe() will resume it once closed.
        if (waitingForMidBattleSequence)
            return;

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