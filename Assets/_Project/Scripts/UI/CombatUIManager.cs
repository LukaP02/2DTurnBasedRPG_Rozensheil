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

    [Header("Combat Start Animation")]
    [Tooltip("How far off-screen (in UI units) cards start before sliding into their combat position.")]
    public float cardSlideDistance = 400f;
    [Tooltip("Delay added per card so the row slides in one after another instead of all at once.")]
    public float cardSlideStaggerSeconds = 0.06f;

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
        combatController.OnFormSwitched += HandleFormSwitched;
        combatController.OnCriticalOrWeaknessHit += HandleCriticalOrWeaknessHit;
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
            combatController.OnFormSwitched -= HandleFormSwitched;
            combatController.OnCriticalOrWeaknessHit -= HandleCriticalOrWeaknessHit;
        }
    }

    private void HandleFormSwitched(CharacterInstance character)
    {
        if (cardLookup.TryGetValue(character, out var card))
            card.PlayFormFlip();
    }

    private void HandleCriticalOrWeaknessHit(CharacterInstance character)
    {
        if (cardLookup.TryGetValue(character, out var card))
            card.PlayShiver();
    }

    // Alternates which side of the boss the next reinforcement lands on, so the boss's card
    // stays centered (roughly) as reinforcements pile up on both sides instead of the row just
    // growing off to one end.
    private bool nextReinforcementGoesRight = true;

    private void HandleEnemyReinforced(CharacterInstance enemy)
    {
        GameObject cardObj = Instantiate(cardPrefab, enemyContainer);
        CharacterCardUI card = cardObj.GetComponent<CharacterCardUI>();
        card.Bind(enemy, this);
        cardLookup[enemy] = card;

        ReorderEnemyCardsAroundBoss();

        // Slides the new card down into place from off the top of the screen (same direction
        // enemies enter from at combat start - see PlayCombatStartSlideIn) instead of just
        // popping in, so it visibly arrives while the card it's replacing fades out (see
        // RefreshUI's wave-encounter branch below).
        LayoutRebuilder.ForceRebuildLayoutImmediate(enemyContainer as RectTransform);
        card.PlaySlideIn(new Vector2(0f, cardSlideDistance));
    }

    // Rebuilds the enemy row's sibling order from scratch around the living boss, so it stays
    // centered no matter how the alive/dead mix changes - a reinforcement joining, or an enemy on
    // either side dying, both call this rather than nudging siblings incrementally (which drifted
    // the boss toward whichever side happened to lose a card).
    private void ReorderEnemyCardsAroundBoss()
    {
        CharacterInstance boss = combatController.Enemies.FirstOrDefault(e => e.isAlive && e.data.isBoss);
        if (boss == null || !cardLookup.TryGetValue(boss, out var bossCard))
            return; // no boss in this fight - leave whatever order the cards are already in

        // combatController.Enemies keeps its append order (spawn order) even as enemies die, so
        // filtering to the currently-alive ones gives a stable, reproducible ordering to split.
        List<CharacterInstance> others = combatController.Enemies
            .Where(e => e != boss && e.isAlive && cardLookup.ContainsKey(e))
            .ToList();

        List<CharacterInstance> left = new List<CharacterInstance>();
        List<CharacterInstance> right = new List<CharacterInstance>();
        for (int i = 0; i < others.Count; i++)
        {
            if (i % 2 == 0) right.Add(others[i]);
            else left.Add(others[i]);
        }
        left.Reverse(); // furthest-from-boss spawn ends up furthest-from-boss on screen

        int index = 0;
        foreach (var e in left)
            cardLookup[e].transform.SetSiblingIndex(index++);
        bossCard.transform.SetSiblingIndex(index++);
        foreach (var e in right)
            cardLookup[e].transform.SetSiblingIndex(index++);
    }

    private void PositionReinforcementCard(CharacterInstance reinforcement, Transform cardTransform)
    {
        CharacterInstance boss = combatController.Enemies.FirstOrDefault(e => e.isAlive && e.data.isBoss);
        if (boss == null || boss == reinforcement || !cardLookup.TryGetValue(boss, out var bossCard))
            return; // no boss in this fight, or the reinforcement IS the boss - keep default (end of row) position

        int bossIndex = bossCard.transform.GetSiblingIndex();
        // Right side: insert immediately after the boss, pushing any earlier right-side card further right.
        // Left side: insert at the boss's own index, which pushes the boss (and any earlier left-side card) further left.
        cardTransform.SetSiblingIndex(nextReinforcementGoesRight ? bossIndex + 1 : bossIndex);

        nextReinforcementGoesRight = !nextReinforcementGoesRight;
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
        // Allies are never removed from the field on death - no reinforcement concept for them -
        // so just dim/desaturate their card in place, same treatment as a fixed-roster enemy.
        foreach (var kvp in cardLookup)
        {
            if (!kvp.Key.isAlive && combatController.Allies.Contains(kvp.Key))
                kvp.Value.SetDeadVisual(true);
        }

        if (combatController.IsWaveEncounter)
        {
            // Wave encounters cycle enemies in/out via the reinforcement queue. A dead enemy's
            // card fades out (desaturated) instead of vanishing instantly, then is destroyed and
            // the row recenters around the boss - by then a reinforcement's card has usually
            // already started sliding in independently (see HandleEnemyReinforced), so the two
            // animations overlap.
            List<CharacterInstance> deadEnemies = cardLookup.Keys
                .Where(c => !c.isAlive && combatController.Enemies.Contains(c))
                .ToList();

            foreach (var dead in deadEnemies)
            {
                CharacterCardUI deadCard = cardLookup[dead];
                cardLookup.Remove(dead); // stop tracking now so this death isn't reprocessed next refresh

                deadCard.PlayDeathFadeOut(() =>
                {
                    Destroy(deadCard.gameObject);
                    ReorderEnemyCardsAroundBoss();
                });
            }
        }
        else
        {
            // Fixed-roster encounters (no reinforcements) keep every card in its original slot even
            // after death - just dimmed and non-interactive - so the row's layout, and the boss's
            // position in it, never shifts.
            foreach (var kvp in cardLookup)
            {
                if (!kvp.Key.isAlive && combatController.Enemies.Contains(kvp.Key))
                    kvp.Value.SetDeadVisual(true);
            }
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
        ReorderEnemyCardsAroundBoss(); // in case the level places the boss anywhere but the middle of its enemy list

        // Leaving a level's Combat Background empty keeps whatever's already set on the scene.
        if (background != null && backgroundImage != null)
            backgroundImage.sprite = background;

        RefreshUI();
        PlayCombatStartSlideIn();
    }

    // Allies sit at the bottom of the screen and enemies at the top (see AllyContainer / EnemyContainer
    // anchors), so allies slide up into place and enemies slide down, each card starting a beat after
    // the previous one for a "dealt into battle" feel instead of every card popping in at once.
    // Forcing an immediate layout rebuild first is required so each card's anchoredPosition already
    // reflects its real HorizontalLayoutGroup slot before we read it as the slide target.
    private void PlayCombatStartSlideIn()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(allyContainer as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(enemyContainer as RectTransform);

        int i = 0;
        foreach (var ally in combatController.Allies)
        {
            if (cardLookup.TryGetValue(ally, out var card))
                card.PlaySlideIn(new Vector2(0f, -cardSlideDistance), i++ * cardSlideStaggerSeconds);
        }

        i = 0;
        foreach (var enemy in combatController.Enemies)
        {
            if (cardLookup.TryGetValue(enemy, out var card))
                card.PlaySlideIn(new Vector2(0f, cardSlideDistance), i++ * cardSlideStaggerSeconds);
        }
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