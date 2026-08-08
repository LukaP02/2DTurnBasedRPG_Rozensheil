using System.Collections.Generic;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [Header("Starting Party")]
    public CharacterCardData[] startingParty;

    [Header("Screens")]
    public GameObject overworldPanel;
    public GameObject combatScreen;

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

        combatController.OnStateChanged += CheckCombatEnd;
        combatController.StartCombat(allies, enemies);

        combatScreen.SetActive(true);

        combatUIManager.SetupCombatUI();
    }

    private void CheckCombatEnd()
    {
        if (combatController.currentState == CombatState.Victory)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
            combatScreen.SetActive(false);
            OnCombatVictory();
        }
        else if (combatController.currentState == CombatState.Defeat)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
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
        ReturnToOverworld();
    }

    private void ReturnToOverworld()
    {
        PartyManager.Instance.UnlockUpTo(currentLevelIndex + 1);
        overworldMapUI.RefreshNodes();
        overworldPanel.SetActive(true);
    }
}