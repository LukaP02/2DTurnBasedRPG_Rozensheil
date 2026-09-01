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
    // Debug/dev shortcut - unlocks every level/event node and jumps straight to the overworld,
    // skipping the normal intro-dialogue/first-level flow. Fine to leave wired up during
    // development; just don't hook it up in a shipped build.
    public Button unlockAllButton;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (unlockAllButton != null) unlockAllButton.onClick.AddListener(UnlockAllAndGoToOverworld);
    }

    public void StartGame()
    {
        mainMenuPanel.SetActive(false);

        // StartGame jumps straight into firstLevel, bypassing the normal unlock-gated flow
        // entirely - so firstLevel must be explicitly marked unlocked here, or it never actually
        // lands in PartyManager's unlocked set and its node silently never appears on the map
        // once you return to it (even though you just completed it).
        PartyManager.Instance.UnlockLevels(new LevelData[] { firstLevel });

        gameFlowManager.StartLevel(firstLevel, 0);
    }

    public void UnlockAllAndGoToOverworld()
    {
        mainMenuPanel.SetActive(false);
        gameFlowManager.overworldPanel.SetActive(true);

        // Activating overworldPanel above guarantees OverworldMapUI.Start() has run (spawning its
        // nodes) before this, so RefreshNodes() below always has something to update.
        PartyManager.Instance.UnlockLevels(gameFlowManager.overworldMapUI.levelsInOrder);
        gameFlowManager.overworldMapUI.RefreshNodes();
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