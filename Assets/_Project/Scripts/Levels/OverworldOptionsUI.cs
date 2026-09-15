using UnityEngine;
using UnityEngine.UI;

// Lives on the Overworld screen. Same audio options as the Main Menu's OptionsMenuUI, but backing
// out returns to the Overworld panel instead of the Main Menu, and adds a Quit button that leaves
// the run and returns to the Main Menu panel.
public class OverworldOptionsUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject optionsPanel;
    public GameObject overworldPanel;
    public GameObject mainMenuPanel;

    [Header("Audio")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Buttons")]
    public Button backButton;
    public Button quitButton;

    private void Awake()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        if (backButton != null)
            backButton.onClick.AddListener(Back);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitToMainMenu);
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
        overworldPanel.SetActive(true);
    }

    // Leaves the current run: closes both the Options and Overworld panels and returns to the
    // Main Menu panel, mirroring MainMenuUI's own panel-toggle style rather than reloading a scene.
    public void QuitToMainMenu()
    {
        ScreenFader.Transition(() =>
        {
            optionsPanel.SetActive(false);
            overworldPanel.SetActive(false);
            mainMenuPanel.SetActive(true);
        });
    }
}