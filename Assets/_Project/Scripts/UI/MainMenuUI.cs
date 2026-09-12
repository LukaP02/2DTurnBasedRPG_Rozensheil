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
    // Debug/dev shortcut - unlocks every level/event node, recruits every playable character,
    // grants a pile of gold, and jumps straight to the overworld, skipping the normal
    // intro-dialogue/first-level flow. Kept in intentionally for thesis demo/testing purposes.
    public Button unlockAllButton;
   
    [Header("Music")]
    public AudioClip menuMusic;

    [Header("Debug")]
    [Tooltip("Every playable character the debug button should unlock - populate with all recruitable CharacterCardData assets.")]
    public CharacterCardData[] allPlayableCharacters;
    public int debugGoldAmount = 9999;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (unlockAllButton != null) unlockAllButton.onClick.AddListener(UnlockAllAndGoToOverworld);
    }
    private void Start()
    {
        AudioManager.Instance?.PlayMusic(menuMusic);
        ScreenFader.Instance?.FadeInFromBlack();
    }

    public void StartGame()
    {
        ScreenFader.Transition(() =>
        {
            mainMenuPanel.SetActive(false);
            PartyManager.Instance.UnlockLevels(new LevelData[] { firstLevel });
            gameFlowManager.StartLevel(firstLevel, 0);
        });
    }

    public void UnlockAllAndGoToOverworld()
    {
        mainMenuPanel.SetActive(false);
        gameFlowManager.overworldPanel.SetActive(true);

        // Activating overworldPanel above guarantees OverworldMapUI.Start() has run (spawning its
        // nodes) before this, so RefreshNodes() below always has something to update.
        PartyManager.Instance.UnlockLevels(gameFlowManager.overworldMapUI.levelsInOrder);
        gameFlowManager.overworldMapUI.RefreshNodes();

        PartyManager.Instance.AddGold(debugGoldAmount);

        foreach (var character in allPlayableCharacters)
            PartyManager.Instance.RecruitCharacter(character);
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