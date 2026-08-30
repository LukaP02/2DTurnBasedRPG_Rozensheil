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

    [Header("Combat Tuning")]
    [Range(0f, 1f)]
    [Tooltip("Damage reduction fraction applied when a target resists the ability's element - 0 = no reduction (full damage), 1 = fully negated. Replaces the old fixed 50% reduction.")]
    public float resistanceReduction = 0.5f;

    // --- Wave encounter (optional, configured per level via ConfigureWaveEncounter) ---
    private int maxEnemiesOnField;
    private List<CharacterCardData> reinforcementQueue = new List<CharacterCardData>();
    private int killTarget;
    private int killCount;
    private DialogueSequence midBattleDialogue;
    private string wipeMessage;
    private bool waitingForMidBattleSequence;

    // --- Boss phase transition (configured per-enemy via CharacterCardData.phaseTwoCard) ---
    private bool waitingForPhaseTransition;
    private CharacterInstance pendingPhaseTransitionEnemy;

    public event Action OnStateChanged;
    public event Action<CharacterInstance, int> OnHealApplied;
    public event Action<CharacterInstance, int, ElementType> OnDamageApplied;
    public event Action<string> OnCombatLogMessage;
    public event Action<DialogueSequence> OnMidBattleDialogueRequested;
    public event Action<CharacterInstance> OnEnemyReinforced;
    public event Action<DialogueSequence> OnPhaseTransitionRequested;
    public event Action<CharacterInstance> OnEnemyRemoved;
    public event Action<CharacterInstance> OnFormSwitched;
    public event Action<CharacterInstance> OnCriticalOrWeaknessHit;
    // --- Bonus action (Abdul's 3rd Ultimate option: act twice per turn for X turns) ---
    private bool bonusActionAvailableThisTurn;

    // Logs to the console and broadcasts to any on-screen combat log listener.
    private void LogMessage(string message)
    {
        Debug.Log(message);
        OnCombatLogMessage?.Invoke(message);
    }

    public CharacterInstance ActiveActor => activeActor;
    // Null-safe: partyState isn't created until StartCombat() runs, but a UI observer (e.g.
    // SPDisplayUI) can have its OnEnable fire before that - e.g. if left active in the scene
    // outside of an actual fight - so read 0 instead of throwing in that window.
    public int CurrentSkillPoints => partyState?.currentSkillPoints ?? 0;
    public int MaxSkillPoints => partyState?.maxSkillPoints ?? 0;
    // True only for a level that called ConfigureWaveEncounter with a positive field cap - i.e.
    // an encounter that cycles enemies in/out via the reinforcement queue. A fixed-roster fight
    // (like a boss with a couple of adds) never sets this, even if it uses HP-triggered or
    // phase-transition reinforcements separately.
    public bool IsWaveEncounter => maxEnemiesOnField > 0;
    public IReadOnlyList<CharacterInstance> Allies => turnOrder.allies;
    public IReadOnlyList<CharacterInstance> Enemies => turnOrder.enemies;
    public List<CharacterInstance> GetUpcomingTurnOrder(int count) => turnOrder.PeekUpcomingOrder(count);
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
        waitingForPhaseTransition = false;
        pendingPhaseTransitionEnemy = null;
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

        bonusActionAvailableThisTurn = activeActor.HasDoubleActionBuff();
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
        if ((ability.abilityType == AbilityType.Skill || ability.abilityType == AbilityType.Ultimate) && activeActor.IsSilenced())
        {
            Debug.LogWarning($"{activeActor.data.characterName} is silenced and cannot use {ability.abilityName}.");
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
        else if (ability.spGain > 0)
            partyState.GainSkillPoints(ability.spGain);

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
        bool isSilenced = enemy.IsSilenced();

        var validAbilities = enemy.activeAbilities
            .Where(a => a != null
                && (a.abilityType != AbilityType.Ultimate || enemy.HasEnoughEnergyFor(a))
                && !(isSilenced && (a.abilityType == AbilityType.Skill || a.abilityType == AbilityType.Ultimate)))
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
        int totalDamageDealt = 0;
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

                    int rawDamage = CalculateDamage(user, target, ability, out bool wasCritOrWeakness) + bonusFromMarks;
                    totalDamageDealt += DealDamage(target, rawDamage, ability.element);

                    // Skip the shiver on a killing blow - the death-fade/dim visual takes over instead.
                    if (wasCritOrWeakness && target.isAlive)
                        OnCriticalOrWeaknessHit?.Invoke(target);

                    // A killing hit clears the target's marks/stain/status in DealDamage -> don't

                    // A killing hit clears the target's marks/stain/status in DealDamage -> don't
                    // let this same action's on-hit effects immediately re-apply them to the corpse.
                    if (target.isAlive)
                    {
                        if (ability.appliesMark)
                        {
                            target.AddMarkStack(user, ability.maxMarkStacks);
                        }

                        TryApplyElementalStain(user, target, ability.element);
                    }
                }
            }

            if (target.isAlive && ability.appliesStatusEffect != null && UnityEngine.Random.value <= ability.statusEffectChance)
            {
                target.ApplyStatusEffect(ability.appliesStatusEffect, user);
            }
        }
        // Lifesteal heals the caster based on the total damage dealt this cast, across every target hit.
        if (ability.lifestealPercent > 0f && totalDamageDealt > 0 && user.isAlive)
        {
            int healAmount = Mathf.RoundToInt(totalDamageDealt * ability.lifestealPercent);
            if (healAmount > 0)
            {
                user.Heal(healAmount);
                OnHealApplied?.Invoke(user, healAmount);
            }
        }
        if (ability.triggersFormSwitch)
        {
            user.ToggleForm();
            OnFormSwitched?.Invoke(user);
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
    private int DealDamage(CharacterInstance target, int amount, ElementType element)
    {
        int variedAmount = ApplyDamageVariance(amount);
        int actualDamage = target.AbsorbDamage(variedAmount);
        target.TakeDamage(actualDamage);
        target.GainEnergy(DAMAGE_TAKEN_ENERGY_GAIN);
        OnDamageApplied?.Invoke(target, actualDamage, element);

        if (target.CheckAndMarkDeath())
            HandleDeath(target);
        else
        {
            TryTriggerPhaseTransition(target);
            TryTriggerHpReinforcements(target);
        }
        return actualDamage;

    }

    // Checks a still-living enemy's HP against its configured HP Triggered Reinforcement Percent and
    // calls in CharacterCardData.hpTriggeredReinforcements once, the first time HP drops to/below
    // that threshold. Unlike TrySpawnReinforcement (kill-count/field-cap driven) and the phase
    // transition system (swaps the card out), this just adds fixed reinforcements alongside an
    // otherwise-unchanged boss - e.g. Cyclops calling in wolves at half HP.
    private void TryTriggerHpReinforcements(CharacterInstance target)
    {
        if (target.hpReinforcementProcessed) return;
        if (target.data.hpTriggeredReinforcements == null || target.data.hpTriggeredReinforcements.Length == 0) return;
        if (!turnOrder.enemies.Contains(target)) return;

        float hpPercent = (float)target.currentHP / target.maxHP;
        if (hpPercent > target.data.hpTriggeredReinforcementPercent) return;

        target.hpReinforcementProcessed = true;

        if (!string.IsNullOrEmpty(target.data.hpTriggeredReinforcementMessage))
            LogMessage(target.data.hpTriggeredReinforcementMessage);

        foreach (var reinforcementData in target.data.hpTriggeredReinforcements)
        {
            if (reinforcementData == null) continue;

            CharacterInstance reinforcement = new CharacterInstance(reinforcementData);
            turnOrder.AddEnemy(reinforcement);
            OnEnemyReinforced?.Invoke(reinforcement);
        }
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
        Debug.Log($"[WaveEncounter] {deadEnemy.data.characterName} died. killCount={killCount}, killTarget={killTarget}, waitingForMidBattleSequence={waitingForMidBattleSequence}");

        if (!waitingForMidBattleSequence && killTarget > 0 && killCount >= killTarget)
        {
            waitingForMidBattleSequence = true;
            Debug.Log($"[WaveEncounter] Kill target reached, requesting mid-battle dialogue. Sequence assigned: {midBattleDialogue != null}, subscriber count: {OnMidBattleDialogueRequested?.GetInvocationList().Length ?? 0}");
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

        LogMessage("More wolves appear!");

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

    // Checks a still-living enemy's HP against its configured Phase Transition HP Percent. Fires at
    // most once per enemy (phaseTransitionProcessed) and pauses combat for the transition dialogue
    // via the same request/resolve pattern as the mid-battle wave-encounter dialogue.
    private void TryTriggerPhaseTransition(CharacterInstance target)
    {
        if (waitingForPhaseTransition) return;
        if (target.phaseTransitionProcessed) return;
        if (target.data.phaseTwoCard == null) return;
        if (!turnOrder.enemies.Contains(target)) return;

        float hpPercent = (float)target.currentHP / target.maxHP;
        if (hpPercent > target.data.phaseTransitionHPPercent) return;

        target.phaseTransitionProcessed = true;
        waitingForPhaseTransition = true;
        pendingPhaseTransitionEnemy = target;

        if (!string.IsNullOrEmpty(target.data.phaseTransitionMessage))
            LogMessage(target.data.phaseTransitionMessage);

        OnPhaseTransitionRequested?.Invoke(target.data.phaseTransitionDialogue);
    }

    // Called once the phase-transition dialogue (if any) has closed. Removes the Phase 1 enemy from
    // the field without it counting as a real death (no kill count, no on-any-death passives), then
    // brings in Phase 2 as a fresh reinforcement with its own full HP and kit.
    public void ResolvePhaseTransition()
    {
        CharacterInstance phaseOne = pendingPhaseTransitionEnemy;
        pendingPhaseTransitionEnemy = null;
        waitingForPhaseTransition = false;

        if (phaseOne == null)
        {
            EndTurn();
            return;
        }

        turnOrder.RemoveEnemy(phaseOne);
        OnEnemyRemoved?.Invoke(phaseOne);

        CharacterInstance phaseTwo = new CharacterInstance(phaseOne.data.phaseTwoCard);
        turnOrder.AddEnemy(phaseTwo);
        OnEnemyReinforced?.Invoke(phaseTwo);

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

    private int CalculateDamage(CharacterInstance attacker, CharacterInstance defender, AbilityData ability, out bool wasCritOrWeakness)
    {
        wasCritOrWeakness = false;

        int raw = ability.power
            + Mathf.RoundToInt(attacker.currentAttack * ability.attackScaling)
            + Mathf.RoundToInt(attacker.maxHP * ability.maxHPScaling)
            + Mathf.RoundToInt(attacker.currentDefense * ability.defenseScaling)
            + Mathf.RoundToInt(attacker.currentSpeed * ability.speedScaling);

        // Diminishing-returns defense: def = 100 / (100 + defense). Each point of the defender's
        // Defense reduces incoming damage by a shrinking amount instead of 1-for-1, so defense
        // stays useful at high values instead of being able to no-sell damage outright the way the
        // old flat subtraction could.
        float defenseFactor = 100f / (100f + defender.currentDefense);
        raw = Mathf.RoundToInt(raw * defenseFactor);
        raw = Mathf.Max(1, raw);

        if (IsWeakTo(defender, ability.element))
        {
            raw = Mathf.RoundToInt(raw * WEAKNESS_MULTIPLIER);
            wasCritOrWeakness = true;
        }
        else if (IsResistantTo(defender, ability.element))
        {
            raw = Mathf.RoundToInt(raw * (1f - resistanceReduction));
        }

        if (ability.canCrit && UnityEngine.Random.value < attacker.currentCritRate)
        {
            raw = Mathf.RoundToInt(raw * CRIT_DAMAGE_MULTIPLIER);
            wasCritOrWeakness = true;
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

        // Combat is paused for a boss phase transition; ResolvePhaseTransition() will resume it once closed.
        if (waitingForPhaseTransition)
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
        // Abdul's 3rd Ultimate option: while active, the same actor immediately acts again instead
        // of the turn passing on. Consumed here so it grants exactly one extra action per turn; it's
        // re-checked fresh (via HasDoubleActionBuff()) each time this character's turn comes around
        // in AdvanceTurn(), so it keeps applying every turn the buff's Duration is still active.
        if (bonusActionAvailableThisTurn && activeActor != null && activeActor.isAlive)
        {
            bonusActionAvailableThisTurn = false;

            LogMessage($"{activeActor.data.characterName} acts again!");

            bool isPlayerControlled = turnOrder.allies.Contains(activeActor);
            currentState = isPlayerControlled ? CombatState.PlayerTurn : CombatState.EnemyTurn;

            OnStateChanged?.Invoke();

            if (!isPlayerControlled)
            {
                ResolveEnemyAction();
            }
            return;
        }
        AdvanceTurn();
    }
}