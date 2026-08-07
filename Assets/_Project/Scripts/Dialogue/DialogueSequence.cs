using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "CardRPG/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    public DialogueLine[] lines;
}