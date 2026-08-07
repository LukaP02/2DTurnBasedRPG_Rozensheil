using UnityEngine;

public class EventTestTrigger : MonoBehaviour
{
    public EventController eventController;
    public EventData testEvent;

    private void Start()
    {
        if (eventController != null && testEvent != null)
        {
            if (PartyManager.Instance != null)
            {
                eventController.currentParty = PartyManager.Instance.GetPartyInstances();
            }

            eventController.StartEvent(testEvent);
        }
        else
        {
            Debug.LogWarning("EventTestTrigger: missing references.");
        }
    }
}