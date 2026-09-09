using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject optionsPanel;
    public GameObject mainMenuPanel;

    [Header("Audio")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Buttons")]
    public Button backButton;

    private void Awake()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        if (backButton != null)
            backButton.onClick.AddListener(Back);
    }

    private void OnEnable()
    {
        if (AudioManager.Instance == null) return;

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSFXVolume(value);
    }

    public void Back()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}