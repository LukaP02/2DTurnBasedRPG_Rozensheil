using UnityEngine;
using UnityEngine.UI;

public class OptionsMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject optionsPanel;
    public GameObject mainMenuPanel;

    [Header("Audio")]
    public Slider volumeSlider;

    private void Awake()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void OnEnable()
    {
        if (volumeSlider != null && AudioManager.Instance != null)
            volumeSlider.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
    }

    private void OnVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);
    }

    public void Back()
    {
        optionsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}