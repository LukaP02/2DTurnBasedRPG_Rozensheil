using UnityEngine;

[CreateAssetMenu(fileName = "NewEvent", menuName = "CardRPG/Event")]
public class EventData : ScriptableObject
{
    [Header("Prompt")]
    public string title;
    [TextArea(2, 5)] public string description;
    public Sprite image;
    public Sprite backgroundImage;

    [Header("Choices")]
    public EventChoice[] choices;
}