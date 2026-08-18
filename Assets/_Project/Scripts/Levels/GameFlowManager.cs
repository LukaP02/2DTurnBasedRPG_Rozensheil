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
    public VictoryScreenUI victoryScreen;

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

        if (victoryScreen != null)
        {
            victoryScreen.OnContinuePressed += OnVictoryContinuePressed;
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
            ProceedAfterIntro();
        }
    }

    private void OnIntroDialogueEnded()
    {
        dialogueController.OnDialogueEnded -= OnIntroDialogueEnded;
        ProceedAfterIntro();
    }

    // A level with no enemies is a no-combat node (event and/or dialogue only) — skip straight
    // to the post-level sequence instead of entering combat.
    private void ProceedAfterIntro()
    {
        bool hasCombat = currentLevel.enemies != null && System.Array.Exists(currentLevel.enemies, e => e != null);

        if (hasCombat)
            BeginCombat();
        else
            RunPostLevelSequence();
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
        combatController.OnPhaseTransitionRequested += HandlePhaseTransitionRequested;
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
    // Shows the boss's phase-transition dialogue over the still-active combat screen, same pattern
    // as the mid-battle wave-encounter dialogue. DialogueController itself no-ops instantly if the
    // boss's Phase Transition Dialogue field was left empty, so this works with or without one set.
    private void HandlePhaseTransitionRequested(DialogueSequence sequence)
    {
        dialogueController.OnDialogueEnded += OnPhaseTransitionDialogueEnded;
        dialogueController.StartDialogue(sequence);
    }

    private void OnPhaseTransitionDialogueEnded()
    {
        dialogueController.OnDialogueEnded -= OnPhaseTransitionDialogueEnded;
        combatController.ResolvePhaseTransition();
    }
    private void CheckCombatEnd()
    {
        if (combatController.currentState == CombatState.Victory)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
            combatController.OnMidBattleDialogueRequested -= HandleMidBattleDialogueRequested;
            combatController.OnPhaseTransitionRequested -= HandlePhaseTransitionRequested;
            combatScreen.SetActive(false);
            victoryScreen.Show(combatController.goldReward);
        }
        else if (combatController.currentState == CombatState.Defeat)
        {
            combatController.OnStateChanged -= CheckCombatEnd;
            combatController.OnMidBattleDialogueRequested -= HandleMidBattleDialogueRequested;
            combatController.OnPhaseTransitionRequested -= HandlePhaseTransitionRequested;
            combatScreen.SetActive(false);
            OnCombatDefeat();
        }
    }

    // Shared by both combat victory and no-combat levels: post-level event, then post-level
    // dialogue, then back to the overworld.
    private void RunPostLevelSequence()
    {
        if (currentLevel.postLevelEvent != null)
        {
            eventController.currentParty = PartyManager.Instance.GetPartyInstances();
            eventController.OnEventClosed += OnPostEventClosed;
            eventController.StartEvent(currentLevel.postLevelEvent);
        }
        else
        {
            BeginPostLevelDialogue();
        }
    }

    private void OnPostEventClosed()
    {
        eventController.OnEventClosed -= OnPostEventClosed;
        BeginPostLevelDialogue();
    }

    private void BeginPostLevelDialogue()
    {
        if (currentLevel.postLevelDialogue != null)
        {
            dialogueController.OnDialogueEnded += OnPostLevelDialogueEnded;
            dialogueController.StartDialogue(currentLevel.postLevelDialogue);
        }
        else
        {
            ReturnToOverworld();
        }
    }

    private void OnPostLevelDialogueEnded()
    {
        dialogueController.OnDialogueEnded -= OnPostLevelDialogueEnded;
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

    private void OnVictoryContinuePressed()
    {
        victoryScreen.Hide();
        RunPostLevelSequence();
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