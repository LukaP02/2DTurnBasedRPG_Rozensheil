using System.Collections.Generic;
using System.Linq;

public enum CombatResult { Ongoing, Victory, Defeat }

public class TurnOrderManager
{
    public List<CharacterInstance> allies;
    public List<CharacterInstance> enemies;
    private List<CharacterInstance> combatants;
    private Queue<CharacterInstance> roundQueue;

    public CharacterInstance CurrentActor { get; private set; }

    public TurnOrderManager(List<CharacterInstance> allyList, List<CharacterInstance> enemyList)
    {
        allies = allyList;
        enemies = enemyList;
        combatants = new List<CharacterInstance>();
        combatants.AddRange(allies);
        combatants.AddRange(enemies);

        BuildRoundQueue();
    }

    public void BuildRoundQueue()
    {
        var ordered = combatants
            .Where(c => c.isAlive)
            .OrderByDescending(c => c.currentSpeed)
            .ToList();

        roundQueue = new Queue<CharacterInstance>(ordered);
    }

    public CharacterInstance GetNextActor()
    {
        while (roundQueue.Count > 0)
        {
            var next = roundQueue.Dequeue();
            if (next.isAlive)
            {
                CurrentActor = next;
                return next;
            }
        }

        BuildRoundQueue();
        return roundQueue.Count > 0 ? GetNextActor() : null;
    }

    public bool IsRoundOver()
    {
        return roundQueue.Count == 0;
    }

    // Adds a reinforcement mid-fight. They join the acting order starting next round.
    public void AddEnemy(CharacterInstance enemy)
    {
        enemies.Add(enemy);
        combatants.Add(enemy);
    }

    // Snapshot of the remaining acting order for this round (excludes whoever's turn it currently is)

    public List<CharacterInstance> PeekUpcomingOrder()
    {
        return roundQueue.Where(c => c.isAlive).ToList();
    }

    // Call this immediately after any turn resolves (damage, heal, etc.)
    public CombatResult CheckCombatState()
    {
        bool alliesAlive = allies.Any(a => a.isAlive);
        bool enemiesAlive = enemies.Any(e => e.isAlive);

        if (!alliesAlive) return CombatResult.Defeat;
        if (!enemiesAlive) return CombatResult.Victory;
        return CombatResult.Ongoing;
    }
}