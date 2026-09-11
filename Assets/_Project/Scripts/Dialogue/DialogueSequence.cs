using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "CardRPG/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    public DialogueLine[] lines;
    [Tooltip("Music played while this sequence is up (crossfades in on start, stops when the sequence ends). Leave empty for silence/no change - e.g. mid-battle dialogue, which never touches music regardless of this field.")]
    public AudioClip music;
}