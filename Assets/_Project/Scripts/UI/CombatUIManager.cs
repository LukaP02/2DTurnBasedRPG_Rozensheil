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
    public Color activeTurnOrderIconColor = Color.yellow;
    public int turnOrderVisibleCount = 7; // total icons shown, including the currently-acting one

    [Header("Card Inspect")]
    public CardDetailUI cardDetailUI;

    [Header("Target Highlight Colors")]
    public Color allyTargetColor = Color.yellow;
    public Color enemyTargetColor = Color.red;

    [Header("Background")]
    public Image backgroundImage;

    [Header("Boss Health Bar")]
    [Tooltip("Top-of-screen HP display shown instead of the normal per-card bar for whichever living enemy has CharacterCardData.isBoss checked. Hidden when no enemy in the fight is a boss.")]
    public BossHealthBarUI bossHealthBar;
    [Tooltip("Same idea as Boss Health Bar, for energy. Leave empty until the art is ready.")]
    public BossEnergyBarUI bossEnergyBar;

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
        // Dead enemies are removed outright (not just hidden) so the container's layout closes
        // the gap and a later reinforcement's card naturally lands in the freed-up slot.
        List<CharacterInstance> deadEnemies = cardLookup.Keys
            .Where(c => !c.isAlive && combatController.Enemies.Contains(c))
            .ToList();

        foreach (var dead in deadEnemies)
        {
            Destroy(cardLookup[dead].gameObject);
            cardLookup.Remove(dead);
        }

        foreach (var kvp in cardLookup)
        {
            kvp.Value.RefreshHP();
            kvp.Value.RefreshEnergy();
            kvp.Value.RefreshStatuses();
            kvp.Value.RefreshArt();
            kvp.Value.SetActiveTurn(kvp.Key == combatController.ActiveActor);
            kvp.Value.HideActionButtons();
            kvp.Value.SetTargetHighlight(false, enemyTargetColor);
            kvp.Value.SetHPBarVisible(true); // reset; overridden below for the current boss, if any
            kvp.Value.SetEnergyBarVisible(true);
        }

        RefreshBossBars();

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

        if (combatController.ActiveActor != null)
            SpawnTurnOrderIcon(combatController.ActiveActor, true);

        int upcomingCount = Mathf.Max(0, turnOrderVisibleCount - (combatController.ActiveActor != null ? 1 : 0));

        foreach (var character in combatController.GetUpcomingTurnOrder(upcomingCount))
            SpawnTurnOrderIcon(character, false);
    }

    private void SpawnTurnOrderIcon(CharacterInstance character, bool isActive)
    {
        GameObject iconObj = Instantiate(turnOrderIconPrefab, turnOrderContainer);
        Image icon = iconObj.GetComponent<Image>();
        if (icon == null) return;

        icon.sprite = character.data.icon != null ? character.data.icon : character.data.cardArt;
        icon.color = isActive ? activeTurnOrderIconColor : Color.white;
    }

    private void ShowActionButtonsFor(CharacterInstance actor)
    {
        if (!cardLookup.TryGetValue(actor, out var card)) return;

        AbilityData basic = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Basic);
        AbilityData skill = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Skill);
        AbilityData ult = actor.activeAbilities.FirstOrDefault(a => a != null && a.abilityType == AbilityType.Ultimate);

        card.ShowActionButtons(basic, skill, ult);

        if (ult != null && card.ultButton != null)
            card.ultButton.interactable = actor.HasEnoughEnergyFor(ult);

        if (actor.IsSilenced())
        {
            if (skill != null && card.skillButton != null)
                card.skillButton.interactable = false;
            if (ult != null && card.ultButton != null)
                card.ultButton.interactable = false;
        }
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
        if (!IsValidTarget(clickedCharacter)) return;

        bool clickedIsAlly = combatController.Allies.Contains(clickedCharacter);

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

    // Shared by click-to-target and hover-to-preview so both agree on what counts as a legal target.
    private bool IsValidTarget(CharacterInstance candidate)
    {
        bool isAlly = combatController.Allies.Contains(candidate);
        bool isEnemy = combatController.Enemies.Contains(candidate);

        return selectedAbility.targetSide switch
        {
            TargetSide.Enemy => isEnemy,
            TargetSide.Ally => isAlly,
            TargetSide.Either => isAlly || isEnemy,
            _ => false
        };
    }

    public void OnCardHoverEnter(CharacterInstance character, CharacterCardUI card)
    {
        if (!waitingForTarget || selectedAbility == null || !IsValidTarget(character)) return;

        bool isAlly = combatController.Allies.Contains(character);
        card.SetTargetHighlight(true, isAlly ? allyTargetColor : enemyTargetColor);
    }

    public void OnCardHoverExit(CharacterInstance character, CharacterCardUI card)
    {
        card.SetTargetHighlight(false, enemyTargetColor);
    }

    public void OnInspectCard(CharacterInstance character)
    {
        if (cardDetailUI != null)
            cardDetailUI.Show(character);
    }

    public void SetupCombatUI(Sprite background = null)
    {
        ClearCards();

        SpawnCards(combatController.Allies, allyContainer);
        SpawnCards(combatController.Enemies, enemyContainer);

        // Leaving a level's Combat Background empty keeps whatever's already set on the scene.
        if (background != null && backgroundImage != null)
            backgroundImage.sprite = background;

        RefreshUI();
    }

    // At most one boss's bars are shown at a time - fine for current content (single-boss fights).
    // Re-picks the boss each refresh so a phase transition's new card (if also flagged isBoss) is
    // picked up automatically.
    private void RefreshBossBars()
    {
        CharacterInstance boss = combatController.Enemies.FirstOrDefault(e => e.isAlive && e.data.isBoss);

        if (boss == null)
        {
            if (bossHealthBar != null) bossHealthBar.Hide();
            if (bossEnergyBar != null) bossEnergyBar.Hide();
            return;
        }

        if (bossHealthBar != null) bossHealthBar.Show(boss);
        if (bossEnergyBar != null) bossEnergyBar.Show(boss);

        if (cardLookup.TryGetValue(boss, out var bossCard))
        {
            if (bossHealthBar != null) bossCard.SetHPBarVisible(false);
            if (bossEnergyBar != null) bossCard.SetEnergyBarVisible(false);
        }
    }
}