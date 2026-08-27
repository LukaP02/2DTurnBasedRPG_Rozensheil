using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    public string speakerName;
    public Sprite speakerPortrait;
    [TextArea(2, 5)] public string text;
    [Tooltip("Optional. Leave empty to keep empty (ugly).")]
    public Sprite backgroundImage;
    [Header("Screen Flash (optional, for story beats)")]
    [Tooltip("If Alpha > 0, flashes the whole screen this color when this line appears (e.g. a green flash on a key line, a yellow flash on a boss phase transition). Leave Alpha at 0 for no flash.")]
    public Color flashColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Per-line overrides for this flash's timing. Leave any of these at -1 to use DialogueController's default for that value.")]
    public float flashFadeInSeconds = -1f;
    public float flashHoldSeconds = -1f;
    public float flashFadeOutSeconds = -1f;
}