using UnityEngine;

public class DialogueTestTrigger : MonoBehaviour
{
    public DialogueController dialogueController;
    public DialogueSequence testSequence;

    private void Start()
    {
        if (dialogueController != null && testSequence != null)
        {
            dialogueController.StartDialogue(testSequence);
        }
        else
        {
            Debug.LogWarning("DialogueTestTrigger: missing references.");
        }
    }
}