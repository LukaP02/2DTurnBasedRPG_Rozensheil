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
    [Header("Projectiles")]
    [Tooltip("Parent a Projectile Prefab is spawned under and travels across - a RectTransform on the same Canvas as the cards (a dedicated empty layer above the cards works well, so the projectile draws over them). Only needed for abilities that set AbilityData.projectilePrefab.")]
    public RectTransform projectileLayer;

    [Header("Combat Log")]
    public TMP_Text combatLogText;

    [Header("Turn Order")]
    public Transform turnOrderContainer;
    public GameObject turnOrderIconPrefab;
    public Color activeTurnOrderIconColor = Color.yellow;
    public int turnOrderVisibleCount = 7; // total icons shown, including the currently-acting one
    [Tooltip("How long the turn order track's icons take to slide up one slot when a turn passes.")]
    public float turnOrderShiftSeconds = 0.25f;

    [Header("Card Inspect")]
    public CardDetailUI cardDetailUI;
    [Tooltip("Small, screen-centered RectTransform (anchors and pivot at 0.5, 0.5) that an inspected card temporarily reparents to and grows toward - see CharacterCardUI.PlayInspectZoom.")]
    public RectTransform inspectZoomAnchor;

    [Header("Target Highlight Colors")]
    public Color allyTargetColor = Color.yellow;
    public Color enemyTargetColor = Color.red;

    [Header("Audio")]
    public AudioClip hitSound;
    public AudioClip healSound;
    public AudioClip critSound;

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

    [Tooltip("Vertical distance between each turn order icon's slot, in UI units. Should be roughly the icon's own height plus whatever gap you want between icons - the icons are positioned directly by this value rather than through a Vertical Layout Group.")]
    public float turnOrderSlotHeight = 60f;
    [Tooltip("How much bigger (scale multiplier) the current actor's turn order slot is than the others. Increase Turn Order Slot Height above too if this starts overlapping neighboring slots.")]
    public float activeTurnOrderIconScale = 1.3f;


    private Dictionary<CharacterInstance, CharacterCardUI> cardLookup = new Dictionary<CharacterInstance, CharacterCardUI>();
    [Tooltip("Horizontal distance between each fixed enemy slot, in UI units.")]
    public float enemySlotSpacing = 340f;

    private Dictionary<CharacterInstance, int> enemySlotAssignment = new Dictionary<CharacterInstance, int>();
    private int enemySlotCount;
    private int bossSlotIndex;
    private AbilityData selectedAbility;
    private bool waitingForTarget;
    // Fixed-size pool, built once and reused for the rest of the fight - never destroyed/recreated
    // per refresh like the old version, so a specific icon's identity can persist across a refresh
    // and be animated sliding from one slot to the next instead of just popping into place.
    private List<Image> turnOrderIcons = new List<Image>();
    // Root RectTransform of each pooled slot (background art + the "Icon" child on top of it) -
    // separate from turnOrderIcons because that list now points at the character-art child, not
    // the slot itself. Positioning/scaling always targets the slot as a whole.
    private List<RectTransform> turnOrderSlotRects = new List<RectTransform>();
    private List<Vector2> turnOrderIconHomePositions = new List<Vector2>();
    private List<CharacterInstance> lastTurnOrderCharacters = new List<CharacterInstance>();
    private Coroutine turnOrderShiftCoroutine;
    private List<int> enemySlotFillOrder = new List<int>();
    private int enemySlotFillDistance;

    private void Awake()
    {
        combatController.OnStateChanged += RefreshUI;
        combatController.OnDamageApplied += HandleDamageApplied;
        combatController.OnHealApplied += HandleHealApplied;
        combatController.OnCombatLogMessage += HandleCombatLogMessage;
        combatController.OnEnemyReinforced += HandleEnemyReinforced;
        combatController.OnEnemyRemoved += HandleEnemyRemoved;
        combatController.OnFormSwitched += HandleFormSwitched;
        combatController.OnCriticalOrWeaknessHit += HandleCriticalOrWeaknessHit;
        combatController.OnRequestImpactEffect += HandleRequestImpactEffect;
        combatController.OnRequestProjectile += HandleRequestProjectile;
        combatController.OnTargetUpdated += HandleTargetUpdated;
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
            combatController.OnEnemyRemoved -= HandleEnemyRemoved;
            combatController.OnFormSwitched -= HandleFormSwitched;
            combatController.OnCriticalOrWeaknessHit -= HandleCriticalOrWeaknessHit;
            combatController.OnRequestImpactEffect -= HandleRequestImpactEffect;
            combatController.OnRequestProjectile -= HandleRequestProjectile;
            combatController.OnTargetUpdated -= HandleTargetUpdated;
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

        AudioManager.Instance?.PlaySFX(critSound);
    }

    private void HandleEnemyReinforced(CharacterInstance enemy)
    {
        GameObject cardObj = Instantiate(cardPrefab, enemyContainer);
        CharacterCardUI card = cardObj.GetComponent<CharacterCardUI>();
        card.Bind(enemy, this);
        cardLookup[enemy] = card;

        AssignEnemySlot(enemy);
        card.CardRectTransform.anchoredPosition = GetEnemySlotPosition(enemySlotAssignment[enemy]);
        PushEnemyVisualOrderToController();

        // Slides the new card down into place from off the top of the screen (same direction
        // enemies enter from at combat start - see PlayCombatStartSlideIn) instead of just
        // popping in, so it visibly arrives while the card it's replacing fades out (see
        // RefreshUI's wave-encounter branch below).
        card.PlaySlideIn(new Vector2(0f, cardSlideDistance));
    }

    // Rebuilds the enemy row's sibling order from scratch around the living boss, so it stays
    // centered no matter how the alive/dead mix changes - a reinforcement joining, or an enemy on
    // either side dying, both call this rather than nudging siblings incrementally (which drifted
    // the boss toward whichever side happened to lose a card).
    // Builds the fight's fixed enemy slots: the boss always takes the middle slot, everyone else
    // fills the remaining slots in spawn order. A card's slot never changes once assigned - a
    // reinforcement just claims whichever slot opened up (see AssignEnemySlot), so simultaneous
    // deaths/reinforcements from one AoE can't scramble spacing or make cards collide.
    private void SetupEnemySlots(IReadOnlyList<CharacterInstance> enemies)
    {
        enemySlotAssignment.Clear();
        enemySlotCount = combatController.IsWaveEncounter ? combatController.MaxEnemiesOnField : enemies.Count;
        bossSlotIndex = enemySlotCount / 2;
        BuildEnemySlotFillOrder();

        CharacterInstance boss = enemies.FirstOrDefault(e => e.data.isBoss);
        if (boss != null)
            enemySlotAssignment[boss] = bossSlotIndex;

        foreach (var enemy in enemies)
        {
            if (enemy != boss)
                AssignEnemySlot(enemy);
        }

        PushEnemyVisualOrderToController();
    
}

    // Gives this enemy the lowest-numbered free slot (boss slot excluded) - used both for the
    // fight's starting roster and for a reinforcement taking over whatever slot its predecessor's
    // death just freed up.
    private void AssignEnemySlot(CharacterInstance enemy)
    {
        while (true)
        {
            foreach (int slot in enemySlotFillOrder)
            {
                if (!IsEnemySlotFree(slot)) continue;

                enemySlotAssignment[enemy] = slot;
                return;
            }

            ExtendEnemySlotFillOrder(allowGrowth: true);
        }
    }
    // A slot counts as free as soon as whoever held it dies - no separate bookkeeping needed since
    // CharacterInstance.isAlive already flips the moment DealDamage kills them, before any
    // reinforcement this death triggers ever asks for a slot.
    private bool IsEnemySlotFree(int slot)
    {
        foreach (var kvp in enemySlotAssignment)
        {
            if (kvp.Value == slot && kvp.Key.isAlive) return false;
        }
        return true;
    }

    private Vector2 GetEnemySlotPosition(int slotIndex)
    {
        float x = (slotIndex - bossSlotIndex) * enemySlotSpacing;
        return new Vector2(x, 0f);
    }

    private void HandleCombatLogMessage(string message)
    {
        if (combatLogText != null)
            combatLogText.text = message;
    }

    private void HandleDamageApplied(CharacterInstance target, int amount, ElementType element, AbilityData ability)
    {
        if (cardLookup.TryGetValue(target, out var card))
        {
            card.ShowFloatingText(amount, false, element);
        }

        AudioClip sound = (ability != null && ability.impactSound != null) ? ability.impactSound : hitSound;
        AudioManager.Instance?.PlaySFX(sound);
    }

    private void HandleHealApplied(CharacterInstance target, int amount)
    {
        if (cardLookup.TryGetValue(target, out var card))
        {
            card.ShowFloatingText(amount, true, ElementType.Physical); // element unused for heals
        }

        AudioManager.Instance?.PlaySFX(healSound);
    }

    private void ClearCards()
    {
        ClearContainer(allyContainer);
        ClearContainer(enemyContainer);
        cardLookup.Clear();
    }

    // Destroy() is deferred to the end of the frame - the old cards would still count as children
    // of the container for the rest of this frame otherwise, so a same-frame forced layout rebuild
    // (see PlayCombatStartSlideIn, called right after SetupCombatUI respawns cards for the new
    // fight) would compute slot positions based on old-plus-new children coexisting, landing the
    // new cards in the wrong spots. Detaching immediately removes them from the layout right away.
    private void ClearContainer(Transform container)
    {
        List<Transform> children = new List<Transform>();
        foreach (Transform child in container)
            children.Add(child);

        foreach (var child in children)
        {
            child.SetParent(null);
            Destroy(child.gameObject);
        }
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
            // Reset to alive-matching visibility; overridden below for the current living boss's
            // own bars (replaced by the dedicated boss bar UI) - a dead card just stays hidden.
            kvp.Value.SetHPBarVisible(kvp.Key.isAlive);
            kvp.Value.SetEnergyBarVisible(kvp.Key.isAlive);
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

    // Builds the fixed-size icon pool once (on first use) and forces an immediate layout rebuild
    // so each icon's VerticalLayoutGroup-assigned slot position is known and cached as its "home" -
    // the position the shift animation animates to/from later.
    // Positions each pooled icon directly by index instead of relying on a live VerticalLayoutGroup
    // rebuild - that approach measured each icon's slot position before it had a sprite assigned,
    // which could give Unity's layout system inconsistent preferred sizes to work with and produce
    // wrong, overlapping, or wildly displaced icons. This is fully deterministic instead.
    private void EnsureTurnOrderIconPool(List<CharacterInstance> initialOrder)
    {
        if (turnOrderIcons.Count > 0) return;

        for (int i = 0; i < turnOrderVisibleCount; i++)
        {
            GameObject iconObj = Instantiate(turnOrderIconPrefab, turnOrderContainer);
            RectTransform rt = iconObj.GetComponent<RectTransform>();
            // The per-character icon is a child named "Icon" layered on top of the slot's own
            // background Image (on the root) - children render after their parent, so this is
            // what actually puts the character art above the background frame.
            Image icon = iconObj.transform.Find("Icon").GetComponent<Image>();

            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -i * turnOrderSlotHeight);

            turnOrderIcons.Add(icon);
            turnOrderSlotRects.Add(rt);
            turnOrderIconHomePositions.Add(rt.anchoredPosition);

            ApplyTurnOrderIcon(icon, rt, i < initialOrder.Count ? initialOrder[i] : null, i == 0);
        }
    }

    private void RefreshTurnOrder()
    {
        if (turnOrderContainer == null || turnOrderIconPrefab == null) return;

        List<CharacterInstance> newOrder = new List<CharacterInstance>();
        if (combatController.ActiveActor != null)
            newOrder.Add(combatController.ActiveActor);

        int upcomingCount = Mathf.Max(0, turnOrderVisibleCount - newOrder.Count);
        newOrder.AddRange(combatController.GetUpcomingTurnOrder(upcomingCount));

        EnsureTurnOrderIconPool(newOrder);

        // A "simple shift" is the common case: the previous head just acted and everyone else
        // moved up one slot, with one new character appearing at the tail. Only that specific
        // case gets the sliding animation - anything else (reinforcements changing the queue, a
        // speed change reordering things, the very first refresh) just snaps instantly instead of
        // trying to animate an arbitrary reshuffle.
        bool isSimpleShift = turnOrderIcons.Count >= 2
            && lastTurnOrderCharacters.Count == turnOrderIcons.Count
            && newOrder.Count > 0
            && lastTurnOrderCharacters.Skip(1).SequenceEqual(newOrder.Take(turnOrderIcons.Count - 1));

        if (isSimpleShift)
            PlayTurnOrderShift(newOrder);
        else
            SnapTurnOrderIcons(newOrder);

        lastTurnOrderCharacters = newOrder;
    }

    private void ApplyTurnOrderIcon(Image icon, RectTransform slotRect, CharacterInstance character, bool isActive)
    {
        // Scales the whole slot (background + icon together), not just the character art, so the
        // active actor's frame visibly grows too rather than the art just looking bigger inside a
        // same-size frame.
        slotRect.localScale = Vector3.one * (isActive ? activeTurnOrderIconScale : 1f);

        if (character == null)
        {
            icon.enabled = false;
            return;
        }

        icon.enabled = true;
        icon.sprite = character.data.icon != null ? character.data.icon : character.data.cardArt;
        icon.color = isActive ? activeTurnOrderIconColor : Color.white;
    }

    private void SnapTurnOrderIcons(List<CharacterInstance> order)
    {
        if (turnOrderShiftCoroutine != null)
        {
            StopCoroutine(turnOrderShiftCoroutine);
            turnOrderShiftCoroutine = null;
        }

        for (int i = 0; i < turnOrderIcons.Count; i++)
        {
            ApplyTurnOrderIcon(turnOrderIcons[i], turnOrderSlotRects[i], i < order.Count ? order[i] : null, i == 0);
            turnOrderSlotRects[i].anchoredPosition = turnOrderIconHomePositions[i];
        }
    }

    private void PlayTurnOrderShift(List<CharacterInstance> newOrder)
    {
        if (turnOrderShiftCoroutine != null)
            StopCoroutine(turnOrderShiftCoroutine);

        turnOrderShiftCoroutine = StartCoroutine(TurnOrderShiftRoutine(newOrder));
    }

    private System.Collections.IEnumerator TurnOrderShiftRoutine(List<CharacterInstance> newOrder)
    {
        float slotHeight = turnOrderIconHomePositions[0].y - turnOrderIconHomePositions[1].y;
        int lastIndex = turnOrderIcons.Count - 1;

        // Every slot already holds the character sliding up into it - that's exactly newOrder[i],
        // since this is a one-slot shift - so assign content now and animate every icon (including
        // slot 0, the new active actor) the same way: from one slot below its resting spot up to
        // it. The last slot is a brand new entry that wasn't visible before, so it also fades in
        // while it slides instead of just appearing.
        for (int i = 0; i <= lastIndex; i++)
        {
            ApplyTurnOrderIcon(turnOrderIcons[i], turnOrderSlotRects[i], i < newOrder.Count ? newOrder[i] : null, i == 0);
            turnOrderSlotRects[i].anchoredPosition = turnOrderIconHomePositions[i] + new Vector2(0f, slotHeight);
        }

        Image lastIcon = turnOrderIcons[lastIndex];
        Color lastColor = lastIcon.color;
        lastIcon.color = new Color(lastColor.r, lastColor.g, lastColor.b, 0f);

        float elapsed = 0f;
        while (elapsed < turnOrderShiftSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / turnOrderShiftSeconds);
            float eased = t * t * (3f - 2f * t);

            for (int i = 0; i <= lastIndex; i++)
            {
                Vector2 home = turnOrderIconHomePositions[i];
                turnOrderSlotRects[i].anchoredPosition = Vector2.Lerp(home + new Vector2(0f, slotHeight), home, eased);
            }

            lastIcon.color = new Color(lastColor.r, lastColor.g, lastColor.b, eased);
            yield return null;
        }

        for (int i = 0; i <= lastIndex; i++)
            turnOrderSlotRects[i].anchoredPosition = turnOrderIconHomePositions[i];

        lastIcon.color = lastColor;
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
        if (cardDetailUI == null) return;

        if (inspectZoomAnchor != null && cardLookup.TryGetValue(character, out var card))
        {
            card.PlayInspectZoom(inspectZoomAnchor, () =>
            {
                // Snap the field card back to its combat row slot right as the panel takes over -
                // the panel is about to cover the whole screen anyway, so this is invisible. No
                // need to wait for the panel to close later.
                card.ResetInspectZoom();
                cardDetailUI.Show(character);
            });
        }
        else
        {
            cardDetailUI.Show(character);
        }
    }

    public void SetupCombatUI(Sprite background = null)
    {
        ClearCards();
        lastTurnOrderCharacters.Clear(); // don't treat this fight's first turn order as a shift from the last fight's

        SpawnCards(combatController.Allies, allyContainer);
        SpawnCards(combatController.Enemies, enemyContainer);
        SetupEnemySlots(combatController.Enemies);
        foreach (var enemy in combatController.Enemies)
        {
            if (cardLookup.TryGetValue(enemy, out var card) && enemySlotAssignment.TryGetValue(enemy, out int slot))
                card.CardRectTransform.anchoredPosition = GetEnemySlotPosition(slot);
        }
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
    // Combat holds the hit here until onComplete is called - see CombatController.OnRequestImpactEffect.
    // A target with no card on screen (shouldn't normally happen) just resolves instantly.
    private void HandleRequestImpactEffect(CharacterInstance target, AbilityData ability, System.Action onComplete)
    {
        if (cardLookup.TryGetValue(target, out var card))
            card.PlayImpactEffect(ability.impactEffectPrefab, ability.impactEffectDuration, onComplete);
        else
            onComplete?.Invoke();
    }
    // Spawns the ability's projectile prefab on the caster's card and animates it to the target's
    // card before calling onArrived - combat holds the hit here until then, same idea as
    // HandleRequestImpactEffect. Resolves instantly (no visual) if Projectile Layer isn't set, or
    // either side has no card on screen.
    private void HandleRequestProjectile(CharacterInstance user, CharacterInstance target, AbilityData ability, System.Action onArrived)
    {
        if (projectileLayer == null
            || !cardLookup.TryGetValue(user, out var userCard)
            || !cardLookup.TryGetValue(target, out var targetCard))
        {
            onArrived?.Invoke();
            return;
        }

        GameObject fx = Instantiate(ability.projectilePrefab, projectileLayer);
        RectTransform fxRect = fx.transform as RectTransform;

        if (fxRect == null)
        {
            Debug.LogWarning($"{ability.abilityName}: Projectile Prefab has no RectTransform (isn't a UI prefab) - destroying it and skipping the travel animation.");
            Destroy(fx);
            onArrived?.Invoke();
            return;
        }

        StartCoroutine(ProjectileTravelRoutine(fxRect, userCard.CardRectTransform, targetCard.CardRectTransform, ability.projectileTravelSeconds, onArrived));
    }

    private System.Collections.IEnumerator ProjectileTravelRoutine(RectTransform fx, RectTransform from, RectTransform to, float duration, System.Action onArrived)
    {
        Vector2 startPos = WorldRectToLocalPointInLayer(from);
        Vector2 endPos = WorldRectToLocalPointInLayer(to);

        fx.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fx.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        fx.anchoredPosition = endPos;

        if (fx != null)
            Destroy(fx.gameObject);

        onArrived?.Invoke();
    }

    // Converts a card's position into Projectile Layer's local space, so the projectile can be
    // parented under a different RectTransform than the cards yet still line up on screen.
    // Assumes a Screen Space - Overlay canvas (no camera needed for the screen-point conversion) -
    // if your Canvas is Screen Space - Camera or World Space instead, pass that canvas's camera in
    // place of the two `null`s below.
    private Vector2 WorldRectToLocalPointInLayer(RectTransform source)
    {
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(projectileLayer, screenPoint, null, out Vector2 localPoint);
        return localPoint;
    }

    // Fires right after a target's HP/energy/status actually changed and its number's already
    // showing - refreshes just that one card's bars/icons rather than waiting for the full
    // end-of-turn RefreshUI, so the HP/status update lands right after the number as its own step.
    private void HandleTargetUpdated(CharacterInstance character)
    {
        if (cardLookup.TryGetValue(character, out var card))
        {
            card.RefreshHP();
            card.RefreshEnergy();
            card.RefreshStatuses();
        }

        RefreshBossBars();
    }
    // Assigns a stable left/right side for this enemy, decided once here rather than recomputed
    // from list position on every reorder - a card never flips sides mid-fight just because other
    // enemies happened to die or spawn around it (which used to scramble spacing whenever several
    // reinforcements arrived from one AoE kill).


    // Non-boss slots fill outward from the boss instead of strictly left-to-right - e.g. with 5
    // slots (boss at 2) the priority is 1, 3, 0, 4: immediate neighbors first, then the next ring
    // out, left before right at each distance - keeps the row looking populated around the boss
    // instead of lopsided while enemies are still being added.
    private void BuildEnemySlotFillOrder()
    {
        enemySlotFillOrder.Clear();
        enemySlotFillOrder.Add(bossSlotIndex);
        enemySlotFillDistance = 0;

        int initialMaxDistance = Mathf.Max(bossSlotIndex, enemySlotCount - 1 - bossSlotIndex);
        while (enemySlotFillDistance < initialMaxDistance)
            ExtendEnemySlotFillOrder(allowGrowth: false);
    }

    // Adds the next ring outward from the boss (one slot further left, one further right) to the
    // fill order. Called up front to cover the fight's planned slot count (allowGrowth: false -
    // a candidate past the planned edge is just skipped, same as before), and again on demand if
    // AssignEnemySlot ever runs out (allowGrowth: true - a candidate past the current edge grows
    // the row instead of being skipped) - same left-before-right, ring-by-ring symmetry either way,
    // so an HP-triggered reinforcement on a small fixed-roster fight still lands symmetrically
    // around the boss instead of piling onto one side.
    private void ExtendEnemySlotFillOrder(bool allowGrowth)
    {
        enemySlotFillDistance++;
        int left = bossSlotIndex - enemySlotFillDistance;
        int right = bossSlotIndex + enemySlotFillDistance;

        if (left >= 0 || allowGrowth)
            enemySlotFillOrder.Add(left);

        if (right < enemySlotCount || allowGrowth)
        {
            enemySlotFillOrder.Add(right);
            if (right >= enemySlotCount)
                enemySlotCount = right + 1;
        }
    }
    // Keeps CombatController's targeting logic in sync with the actual on-screen order, since
    // enemy slots fill outward from the boss rather than left-to-right by spawn - call this
    // anytime enemySlotAssignment changes.
    private void PushEnemyVisualOrderToController()
    {
        List<CharacterInstance> ordered = enemySlotAssignment
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();

        combatController.SetEnemyVisualOrder(ordered);
    }
    private void HandleEnemyRemoved(CharacterInstance enemy)
    {
        if (!cardLookup.TryGetValue(enemy, out var card)) return;

        cardLookup.Remove(enemy);
        enemySlotAssignment.Remove(enemy);

        card.PlayDeathFadeOut(() => Destroy(card.gameObject));
    }
}