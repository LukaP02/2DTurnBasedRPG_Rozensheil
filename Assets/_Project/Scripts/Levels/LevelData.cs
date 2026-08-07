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
}