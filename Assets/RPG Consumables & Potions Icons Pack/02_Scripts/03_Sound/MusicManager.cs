using UnityEngine;
using UnityEngine.UI;

public class MusicManager : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;

    [Header("Button")]
    public Button musicButton;
    public Sprite iconOn;
    public Sprite iconOff;

    private bool isMuted = false;

    void Start()
    {
        if (audioSource != null)
            audioSource.Play();

        if (musicButton != null)
            musicButton.onClick.AddListener(ToggleMusic);

        UpdateButton();
    }

    void ToggleMusic()
    {
        isMuted = !isMuted;

        if (audioSource != null)
            audioSource.mute = isMuted;

        UpdateButton();
    }

    void UpdateButton()
    {
        if (musicButton == null) return;

        Image img = musicButton.GetComponent<Image>();
        if (img == null) return;

        img.sprite = isMuted ? iconOff : iconOn;
    }
}