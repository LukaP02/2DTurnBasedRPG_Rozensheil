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

        RunPreLevelSequence();
    }

    // Order controlled by LevelData.dialogueBeforePreEvent: normally the event plays first, then
    // the intro dialogue - flip it for a level where the dialogue should set up the event instead.
    private void RunPreLevelSequence()
    {
        if (currentLevel.dialogueBeforePreEvent)
            PlayDialogue(currentLevel.introDialogue, () => PlayEvent(currentLevel.preLevelEvent, ProceedAfterIntro));
        else
            PlayEvent(currentLevel.preLevelEvent, () => PlayDialogue(currentLevel.introDialogue, ProceedAfterIntro));
    }

    // A level with no enemies is a no-combat node (event and/or dialogue only) - skip straight
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

        combatUIManager.SetupCombatUI(currentLevel.combatBackground);
    }

    // Shows the mid-battle dialogue on top of the still-active combat screen (combat is not hidden).
    // suppressBackground: true so only the darken overlay dims the arena, instead of the
    // dialogue's own background art covering it.
    private void HandleMidBattleDialogueRequested(DialogueSequence sequence)
    {
        dialogueController.OnDialogueEnded += OnMidBattleDialogueEnded;
        dialogueController.StartDialogue(sequence, suppressBackground: true);
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
        dialogueController.StartDialogue(sequence, suppressBackground: true);
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

            PartyManager.Instance.HealPartyFully();

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

    // Shared by both combat victory and no-combat levels. Order controlled by
    // LevelData.dialogueBeforePostEvent, same idea as the pre-level sequence above.
    private void RunPostLevelSequence()
    {
        if (currentLevel.dialogueBeforePostEvent)
            PlayDialogue(currentLevel.postLevelDialogue, () => PlayEvent(currentLevel.postLevelEvent, ReturnToOverworld));
        else
            PlayEvent(currentLevel.postLevelEvent, () => PlayDialogue(currentLevel.postLevelDialogue, ReturnToOverworld));
    }

    // Generic building blocks for the pre/post-level sequences above: run an event (or dialogue),
    // then call onComplete. Either skips straight to onComplete if there's nothing to play.
    private void PlayEvent(EventData eventData, System.Action onComplete)
    {
        if (eventData == null)
        {
            onComplete();
            return;
        }

        eventController.currentParty = PartyManager.Instance.GetPartyInstances();

        void OnClosed()
        {
            eventController.OnEventClosed -= OnClosed;
            onComplete();
        }

        eventController.OnEventClosed += OnClosed;
        eventController.StartEvent(eventData);
    }

    private void PlayDialogue(DialogueSequence sequence, System.Action onComplete)
    {
        if (sequence == null)
        {
            onComplete();
            return;
        }

        void OnEnded()
        {
            dialogueController.OnDialogueEnded -= OnEnded;
            onComplete();
        }

        dialogueController.OnDialogueEnded += OnEnded;
        dialogueController.StartDialogue(sequence);
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
        PartyManager.Instance.MarkLevelCompleted(currentLevel);
        PartyManager.Instance.UnlockLevels(currentLevel.unlocksOnComplete);
        overworldMapUI.RefreshNodes();
        overworldPanel.SetActive(true);
    }

    private void ReturnToOverworldWithoutUnlocking()
    {
        overworldPanel.SetActive(true);
    }

}