using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "CardRPG/Level")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelName;

    [Header("Flow (all optional except enemies)")]
    public EventData preLevelEvent;
    public DialogueSequence introDialogue;
    public CharacterCardData[] enemies;
    public EventData postLevelEvent;

    [Header("Wave Encounter (optional; leave Max Enemies On Field at 0 for a normal fight)")]
    [Tooltip("Caps how many enemies can be alive on the field at once. Reinforcements fill empty slots as enemies die.")]
    public int maxEnemiesOnField = 0;
    [Tooltip("Drawn in order as slots open up, until this list runs out or the Kill Target is reached.")]
    public CharacterCardData[] reinforcementPool;
    [Tooltip("If > 0, once this many total enemies have been defeated, Mid Battle Dialogue plays (over the combat screen) and all remaining enemies are then wiped out.")]
    public int killTarget = 0;
    public DialogueSequence midBattleDialogue;
    [TextArea] public string wipeMessage = "The remaining enemies are struck down!";
}