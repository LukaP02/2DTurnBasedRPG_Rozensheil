using System.Collections.Generic;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [Header("Starting Party")]
    public CharacterCardData[] startingParty;

    [Header("Screens")]
    public GameObject overworldPanel;
    public GameObject combatScreen;
    public DefeatScreenUI defeatScreen;

    [Header("Controllers")]
    public DialogueController dialogueController;
    public EventController eventController;
    public CombatController combatController;
    public CombatUIManager combatUIManager;
    public OverworldMapUI overworldMapUI;

    private LevelData currentLevel;
    private int currentLevelIndex;

    private void Awake()
    {
        if (PartyManager.Instance != null)
        {
            PartyManager.Instance.InitializeParty(new List<CharacterCardData>(startingParty));
        }
        else
        {
            Debug.LogError("GameFlowManager: no PartyManager found in scene.");
        }

        if (defeatScreen != null)
        {
            defeatScreen.OnContinuePressed += OnDefeatContinuePressed;
        }
    }

    public void StartLevel(LevelData level, int levelIndex)
    {
        currentLevel = level;
        currentLevelIndex = levelIndex;

        overworldPanel.SetActive(false);

        if (level.preLevelEvent != null)
        {
            eventController.currentParty = PartyManager.Instance.GetPartyInstances();
            eventController.OnEventClosed += OnPreEventClosed;
            eventController.StartEvent(level.preLevelEvent);
        }
        else
        {
            BeginDialogue();
        }
    }

    private void OnPreEventClosed()
    {
        eventController.OnEventClosed -= OnPreEventClosed;
        BeginDialogue();
    }

    private void BeginDialogue()
    {
        if (currentLevel.introDialogue != null)
        {
            dialogueController.OnDialogueEnded += OnIntroDialogueEnded;
            dialogueController.StartDialogue(currentLevel.introDialogue);
        }
        else
        {
            BeginCombat();
        }
    }

    private void OnIntroDialogueEnded()
    {
        dialogueController.OnDialogueEnded -= OnIntroDialogueEnded;
        BeginCombat();
    }

    private void BeginCombat()
    {
        List<CharacterInstance> allies = PartyManager.Instance.GetPartyInstances();
        List<CharacterInstance> enemies = new List<CharacterInstance>();

        foreach (var data in currentLevel.enemies)
        {
            if (data != null) enemies.Add(new CharacterInstance(data));
        }

        combatController.ConfigureWaveEncounter(
            currentLevel.maxEnemiesOnField,
            currentLevel.reinforcementPool,
            currentLevel.killTarget,
            currentLevel.midBattleDialogue,
            currentLevel.wipeMessage);

        combatController.OnStateChanged += CheckCombatEnd;
        combatController.OnMidBattleDialogueRequested += HandleMidBattleDialogueRequested;
        combatController.StartCombat(allies, enemies);

        combatScreen.SetActive(true);

        combatUIManager.SetupCombatUI();
    }

    // Shows the mid-battle dialogue on top of the still-active combat screen (combat is not hidden).
    private void HandleMidBattleDialogueRequested(DialogueSequence sequence)
    {
        dialogueController.OnDialogueEnded += OnMidBattleDialogueEnded;
        dialogueController.StartDialogue(sequence);
    }

    private void OnMidBattleDialogueEnded()
    {
        dialogueController.OnDialogueEnded -= OnMidBattleDialogueEnded;
        combatController.ResolveMidBattleWipe();
    }

    private void CheckCombatEnd()
    {
        if (combatController.currentState == CombatState.Victory)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
            combatController.OnMidBattleDialogueRequested -= HandleMidBattleDialogueRequested;
            combatScreen.SetActive(false);
            OnCombatVictory();
        }
        else if (combatController.currentState == CombatState.Defeat)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
            combatController.OnMidBattleDialogueRequested -= HandleMidBattleDialogueRequested;
            combatScreen.SetActive(false);
            OnCombatDefeat();
        }
    }

    private void OnCombatVictory()
    {
        if (currentLevel.postLevelEvent != null)
        {
            eventController.currentParty = PartyManager.Instance.GetPartyInstances();
            eventController.OnEventClosed += OnPostEventClosed;
            eventController.StartEvent(currentLevel.postLevelEvent);
        }
        else
        {
            ReturnToOverworld();
        }
    }

    private void OnPostEventClosed()
    {
        eventController.OnEventClosed -= OnPostEventClosed;
        ReturnToOverworld();
    }

    private void OnCombatDefeat()
    {
        defeatScreen.Show();
    }

    private void OnDefeatContinuePressed()
    {
        defeatScreen.Hide();

        PartyManager.Instance.HealPartyFully();

        ReturnToOverworldWithoutUnlocking();
    }

    private void ReturnToOverworld()
    {
        PartyManager.Instance.UnlockUpTo(currentLevelIndex + 1);
        overworldMapUI.RefreshNodes();
        overworldPanel.SetActive(true);
    }

    private void ReturnToOverworldWithoutUnlocking()
    {
        overworldPanel.SetActive(true);
    }
}