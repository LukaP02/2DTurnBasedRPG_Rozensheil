using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject optionsPanel;

    [Header("First Level")]
    public GameFlowManager gameFlowManager;
    public LevelData firstLevel;

    [Header("Buttons")]
    public Button startButton;
    public Button optionsButton;
    public Button quitButton;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
    }

    public void StartGame()
    {
        mainMenuPanel.SetActive(false);
        gameFlowManager.StartLevel(firstLevel, 0);
    }

    public void OpenOptions()
    {
        mainMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}