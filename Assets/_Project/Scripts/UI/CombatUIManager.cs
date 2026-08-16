using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatUIManager : MonoBehaviour
{
    public CombatController combatController;

    [Header("Card Prefab & Containers")]
    public GameObject cardPrefab;
    public Transform allyContainer;
    public Transform enemyContainer;

    [Header("Combat Log")]
    public TMP_Text combatLogText;

    [Header("Turn Order")]
    public Transform turnOrderContainer;
    public GameObject turnOrderIconPrefab;

    [Header("Card Inspect")]
    public CardDetailUI cardDetailUI;



    private Dictionary<CharacterInstance, CharacterCardUI> cardLookup = new Dictionary<CharacterInstance, CharacterCardUI>();

    private AbilityData selectedAbility;
    private bool waitingForTarget;

    private void Awake()
    {
        combatController.OnStateChanged += RefreshUI;
        combatController.OnDamageApplied += HandleDamageApplied;
        combatController.OnHealApplied += HandleHealApplied;
        combatController.OnCombatLogMessage += HandleCombatLogMessage;
        combatController.OnEnemyReinforced += HandleEnemyReinforced;
    }

    private void OnDestroy()
    {
        if (combatController != null)
        {
            combatController.OnStateChanged -= RefreshUI;
            combatController.OnDamageApplied -= HandleDamageApplied;
            combatController.OnHealApplied -= HandleHealApplied;
            combatController.OnCombatLogMessage -= HandleCombatLogMessage;
            combatController.OnEnemyReinforced -= HandleEnemyReinforced;
        }
    }

    private void HandleEnemyReinforced(CharacterInstance enemy)
    {
        GameObject cardObj = Instantiate(cardPrefab, enemyContainer);
        CharacterCardUI card = cardObj.GetComponent<CharacterCardUI>();
        card.Bind(enemy, this);
        cardLookup[enemy] = card;
    }

    private void HandleCombatLogMessage(string message)
    {
        if (combatLogText != null)
            combatLogText.text = message;
    }

    private void HandleDamageApplied(CharacterInstance target, int amount, ElementType element)
    {
        if (cardLookup.TryGetValue(target, out var card))
        {
            card.ShowFloatingText(amount, false, element);
        }
    }

    private void HandleHealApplied(CharacterInstance target, int amount)
    {
        if (cardLookup.TryGetValue(target, out var card))
        {
            card.ShowFloatingText(amount, true, ElementType.Physical); // element unused for heals
        }
    }

    public void SetupCombatUI()
    {
        ClearCards();

        SpawnCards(combatController.Allies, allyContainer);
        SpawnCards(combatController.Enemies, enemyContainer);

        RefreshUI();
    }

    private void ClearCards()
    {
        foreach (Transform child in allyContainer) Destroy(child.gameObject);
        foreach (Transform child in enemyContainer) Destroy(child.gameObject);
        cardLookup.Clear();
    }

    private void SpawnCards(IReadOnlyList<CharacterInstance> characters, Transform container)
    {
        foreach (var character in characters)
        {
            GameObject cardObj = Instantiate(cardPrefab, container);
            CharacterCardUI card = cardObj.GetComponent<CharacterCardUI>();
            card.Bind(character, this);
            cardLookup[character] = card;
        }
    }

    private void HandleDamageApplied(CharacterInstance target, int amount)
    {
        if (cardLookup.TryGetValue(target, out var card))
        {
            card.ShowFloatingText(amount, false);
        }
    }



    private void RefreshUI()
    {
        foreach (var kvp in cardLookup)
        {
            kvp.Value.RefreshHP();
            kvp.Value.RefreshEnergy();
            kvp.Value.RefreshStatuses();
            kvp.Value.RefreshArt();
            kvp.Value.SetActiveTurn(kvp.Key == combatController.ActiveActor);
            kvp.Value.HideActionButtons();
        }

        selectedAbility = null;
        waitingForTarget = false;

        if (combatController.IsPlayerTurn && combatController.ActiveActor != null)
        {
            ShowActionButtonsFor(combatController.ActiveActor);
        }

        RefreshTurnOrder();
    }

    private void RefreshTurnOrder()
    {
        if (turnOrderContainer == null || turnOrderIconPrefab == null) return;

        foreach (Transform child in turnOrderContainer)
            Destroy(child.gameObject);

        foreach (var character in combatController.UpcomingTurnOrder)
        {
            GameObject iconObj = Instantiate(turnOrderIconPrefab, turnOrderContainer);
            Image icon = iconObj.GetComponent<Image>();
            if (icon != null)
                icon.sprite = character.data.cardArt;
        }
    }

    private void ShowActionButtonsFor(CharacterInstance actor)
    {
        if (!cardLookup.TryGetValue(actor, out var card)) return;

        AbilityData basic = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Basic);
        AbilityData skill = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Skill);
        AbilityData ult = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Ultimate);

        card.ShowActionButtons(basic, skill, ult);

        if (ult != null && card.ultButton != null)
            card.ultButton.interactable = actor.IsUltimateReady;
    }

    public void OnAbilitySelected(CharacterInstance user, AbilityData ability)
    {
        if (ability.targetSide == TargetSide.Self)
        {
            combatController.ResolvePlayerAction(ability, new List<CharacterInstance> { user });
            return;
        }

        selectedAbility = ability;
        waitingForTarget = true;
    }

    public void OnCardClicked(CharacterInstance clickedCharacter)
    {
        if (!waitingForTarget || selectedAbility == null) return;

        bool clickedIsAlly = combatController.Allies.Contains(clickedCharacter);
        bool clickedIsEnemy = combatController.Enemies.Contains(clickedCharacter);

        bool sideAllowed = selectedAbility.targetSide switch
        {
            TargetSide.Enemy => clickedIsEnemy,
            TargetSide.Ally => clickedIsAlly,
            TargetSide.Either => clickedIsAlly || clickedIsEnemy,
            _ => false
        };

        if (!sideAllowed) return;

        List<CharacterInstance> targets;

        if (selectedAbility.targetShape == TargetShape.All)
        {
            targets = clickedIsAlly
                ? combatController.Allies.Where(a => a.isAlive).ToList()
                : combatController.Enemies.Where(e => e.isAlive).ToList();
        }
        else if (selectedAbility.targetShape == TargetShape.Single)
        {
            targets = new List<CharacterInstance> { clickedCharacter };
        }
        else
        {
            targets = combatController.BuildTargetGroup(selectedAbility.targetShape, clickedCharacter);
        }

        combatController.ResolvePlayerAction(selectedAbility, targets);
    }
    public void OnInspectCard(CharacterInstance character)
    {
        if (cardDetailUI != null)
            cardDetailUI.Show(character);
    }
}