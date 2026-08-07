using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatUIManager : MonoBehaviour
{
    public CombatController combatController;

    [Header("Card Prefab & Containers")]
    public GameObject cardPrefab;
    public Transform allyContainer;
    public Transform enemyContainer;

    private Dictionary<CharacterInstance, CharacterCardUI> cardLookup = new Dictionary<CharacterInstance, CharacterCardUI>();

    private AbilityData selectedAbility;
    private bool waitingForTarget;

    private void Awake()
    {
        combatController.OnStateChanged += RefreshUI;
    }

    private void OnDestroy()
    {
        if (combatController != null)
            combatController.OnStateChanged -= RefreshUI;
    }

    // Call this right after combatController.StartCombat(...) each time a new fight begins
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

    private void RefreshUI()
    {
        foreach (var kvp in cardLookup)
        {
            kvp.Value.RefreshHP();
            kvp.Value.SetActiveTurn(kvp.Key == combatController.ActiveActor);
            kvp.Value.HideActionButtons();
        }

        selectedAbility = null;
        waitingForTarget = false;

        if (combatController.IsPlayerTurn && combatController.ActiveActor != null)
        {
            ShowActionButtonsFor(combatController.ActiveActor);
        }
    }

    private void ShowActionButtonsFor(CharacterInstance actor)
    {
        if (!cardLookup.TryGetValue(actor, out var card)) return;

        AbilityData basic = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Basic);
        AbilityData skill = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Skill);
        AbilityData ult = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Ultimate);

        card.ShowActionButtons(basic, skill, ult);
    }

    public void OnAbilitySelected(CharacterInstance user, AbilityData ability)
    {
        if (ability.targetType == TargetType.Self)
        {
            combatController.ResolvePlayerAction(ability, new List<CharacterInstance> { user });
        }
        else if (ability.targetType == TargetType.AllEnemies)
        {
            combatController.ResolvePlayerAction(ability, combatController.Enemies.Where(e => e.isAlive).ToList());
        }
        else if (ability.targetType == TargetType.AllAllies)
        {
            combatController.ResolvePlayerAction(ability, combatController.Allies.Where(a => a.isAlive).ToList());
        }
        else
        {
            selectedAbility = ability;
            waitingForTarget = true;
        }
    }

    public void OnCardClicked(CharacterInstance clickedCharacter)
    {
        if (!waitingForTarget || selectedAbility == null) return;

        bool validTarget =
            (selectedAbility.targetType == TargetType.SingleEnemy && combatController.Enemies.Contains(clickedCharacter)) ||
            (selectedAbility.targetType == TargetType.SingleAlly && combatController.Allies.Contains(clickedCharacter));

        if (!validTarget) return;

        combatController.ResolvePlayerAction(selectedAbility, new List<CharacterInstance> { clickedCharacter });
    }
}