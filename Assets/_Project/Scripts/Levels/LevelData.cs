using UnityEngine;

[CreateAssetMenu(fileName = "NewLevel", menuName = "CardRPG/Level")]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelName;
    [Tooltip("Shown on the node preview panel under the level name (e.g. \"The Cave\" -> \"Northern Foothills\"). Purely descriptive, optional.")]
    public string location;
    [Tooltip("Flavor text shown on the node preview panel before the player commits to entering.")]
    [TextArea] public string previewDescription;
    [Tooltip("Small image shown on the node preview panel. Leave empty to hide the image area.")]
    public Sprite previewImage;

    [Header("Map Node")]
    [Tooltip("Anchored position of this level's node on the overworld map (OverworldMapUI positions the spawned button here).")]
    public Vector2 mapPosition;
    [Tooltip("Other map nodes (levels or standalone events) that become unlocked once this one is completed. Can list several at once for branching.")]
    public LevelData[] unlocksOnComplete;

    [Header("Visuals")]
    [Tooltip("Shown behind the combat screen for this level. Leave empty to keep whatever background is already set on the combat scene.")]
    public Sprite combatBackground;

    [Header("Flow (all optional)")]
    public EventData preLevelEvent;
    public DialogueSequence introDialogue;
    [Tooltip("If true, Intro Dialogue plays before Pre Level Event. Default (false) plays the event first, then the dialogue.")]
    public bool dialogueBeforePreEvent = false;
    [Tooltip("Leave empty for a no-combat node (event and/or dialogue only) - combat is skipped entirely.")]
    public CharacterCardData[] enemies;
    public EventData postLevelEvent;
    public DialogueSequence postLevelDialogue;
    [Tooltip("If true, Post Level Dialogue plays before Post Level Event. Default (false) plays the event first, then the dialogue.")]
    public bool dialogueBeforePostEvent = false;

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