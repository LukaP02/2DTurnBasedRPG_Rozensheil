using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CharacterCardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Visuals")]
    public Image artImage;
    public TMP_Text nameText;
    [Tooltip("Wraps hpSlider + hpText so both can be hidden together for a boss whose HP is shown on the top boss health bar instead.")]
    public GameObject hpBarContainer;
    [Tooltip("Wraps energySlider + energyText so both can be hidden together for a boss whose energy is shown on the top boss energy bar instead.")]
    public GameObject energyBarContainer;
    public Slider hpSlider;
    public TMP_Text hpText;
    public Slider energySlider;
    public TMP_Text energyText;
    public GameObject activeTurnHighlight;

    [Header("Ghost HP")]
    [Tooltip("Optional trailing bar drawn behind the main HP bar (Image Type Filled, Horizontal) - holds at the old HP level for a beat after damage, then drains down to match, leaving a 'chip damage' afterimage of what was just lost. Leave empty to skip the effect.")]
    public Image ghostHpFillImage;
    [Tooltip("How long the ghost bar holds at the old HP level before it starts draining down.")]
    public float ghostHpDelaySeconds = 0.4f;
    [Tooltip("How long the ghost bar takes to drain down to the new HP level once it starts.")]
    public float ghostHpDrainSeconds = 0.5f;

    [Header("Boss")]
    [Tooltip("Resting-size multiplier applied when this card is bound to a boss (CharacterCardData.isBoss). Layered under Hover Scale, so hovering still grows a boss card a bit further from its own larger base size.")]
    public float bossCardScale = 1.25f;

    [Header("Hover")]
    // Shown while an ability is selected and this card is a valid target for it.
    public GameObject targetHoverHighlight;
    public float hoverScale = 1.08f;
    public float hoverScaleSeconds = 0.12f;
    [Tooltip("Turn off to disable the hover scale-up entirely for this card instance (e.g. a read-only display card, like the one in LoadoutMenuUI, where hover-to-target doesn't apply).")]
    public bool hoverScaleEnabled = true;

    [Header("Action Buttons")]
    public GameObject actionButtonsContainer;
    public Button basicButton;
    public Button skillButton;
    public Button ultButton;
    [Tooltip("Icon Image on each ability button - set to that ability's AbilityData.icon whenever the buttons are (re)shown. Leave a slot empty if that button has no separate icon child.")]
    public Image basicButtonIcon;
    public Image skillButtonIcon;
    public Image ultButtonIcon;

    [Header("Status Icons")]
    public Transform statusIconContainer;
    public GameObject statusIconPrefab;
    [Tooltip("Icon shown on a Fire stain's status icon (see CombatController's stain/combo system, e.g. Zavren). Leave empty to show it with no icon, just the label.")]
    public Sprite fireStainIcon;
    [Tooltip("Icon shown on an Ice stain's status icon. Leave empty to show it with no icon, just the label.")]
    public Sprite iceStainIcon;
    [Tooltip("Icon shown on an Electro stain's status icon. Leave empty to show it with no icon, just the label.")]
    public Sprite electroStainIcon;

    [Header("Floating Text")]
    public Transform floatingTextAnchor;
    public GameObject floatingTextPrefab;
    [Header("Impact Effect")]
    [Tooltip("Where an ability's impact effect prefab is spawned when this card is hit - defaults to this card's own transform if left empty.")]
    public Transform impactEffectAnchor;
    private Coroutine impactEffectCoroutine;
    private CharacterInstance boundCharacter;
    private CombatUIManager uiManager;
    private RectTransform rectTransform;
    private Vector3 baseScale;
    private Coroutine scaleCoroutine;
    private Image targetHoverHighlightImage;
    private CanvasGroup canvasGroup;
    private Coroutine slideInCoroutine;
    private Coroutine flipCoroutine;
    private Coroutine deathFadeCoroutine;
    private bool hasAppliedDeadVisual;
    private Coroutine shiverCoroutine;
    private Coroutine inspectZoomCoroutine;
    private Coroutine ghostHpCoroutine;
    private float lastGhostHpFraction = -1f;

    [Header("Slide-In")]
    public float slideInSeconds = 0.35f;

    [Header("Form Flip")]
    [Tooltip("Total time for the flip - half spent shrinking to edge-on, half spent unfolding back out. The sprite is swapped at the midpoint, when the card is invisible edge-on.")]
    public float formFlipSeconds = 0.3f;

    private bool isFlipping;

    [Header("Death Visual")]
    [Tooltip("Fallback tint applied to a dead character's art when that character has no CharacterCardData.deadArt configured - used both for a card left in place after death (fixed-roster enemies and allies) and for a wave-encounter card before being replaced. White = no change; lower RGB = more washed-out/gray.")]
    public Color deadArtTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("How long a wave-encounter enemy's card holds on its dead-card art before being destroyed and replaced by a reinforcement's card. Should be >= Dead Art Fade Seconds.")]
    public float deathFadeOutSeconds = 0.4f;
    [Tooltip("How long the crossfade from live art to dead art takes (split evenly between fading the old art out and the dead art in). Only used for a character with Dead Art configured - the tint-only fallback swaps instantly.")]
    public float deadArtFadeSeconds = 0.3f;

    [Header("Shiver")]
    [Tooltip("How long the shiver (crit hit or elemental weakness hit) animation lasts.")]
    public float shiverSeconds = 0.3f;
    [Tooltip("How far side to side the card jitters during a shiver, in UI units.")]
    public float shiverDistance = 8f;

    [Header("Inspect Zoom")]
    [Tooltip("How long the on-field card takes to zoom toward the inspect anchor before the inspect panel opens.")]
    public float inspectZoomSeconds = 0.3f;
    [Tooltip("Scale the card reaches (relative to its own current scale) once fully zoomed in.")]
    public float inspectZoomScale = 2.2f;

    private Transform preInspectParent;
    private int preInspectSiblingIndex;
    private Vector2 preInspectAnchoredPosition;
    private Vector3 preInspectScale;
    private bool isZoomedForInspect;

    public void PlayInspectZoom(RectTransform zoomAnchor, System.Action onComplete)
    {
        if (rectTransform == null || zoomAnchor == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (inspectZoomCoroutine != null)
            StopCoroutine(inspectZoomCoroutine);

        inspectZoomCoroutine = StartCoroutine(InspectZoomRoutine(zoomAnchor, onComplete));
    }

    private System.Collections.IEnumerator InspectZoomRoutine(RectTransform zoomAnchor, System.Action onComplete)
    {
        preInspectParent = rectTransform.parent;
        preInspectSiblingIndex = rectTransform.GetSiblingIndex();
        preInspectAnchoredPosition = rectTransform.anchoredPosition;
        preInspectScale = rectTransform.localScale;
        isZoomedForInspect = true;

        rectTransform.SetParent(zoomAnchor, true);
        rectTransform.SetAsLastSibling();

        Vector2 startAnchoredPos = rectTransform.anchoredPosition;
        Vector3 startScale = rectTransform.localScale;
        Vector3 targetScale = preInspectScale * inspectZoomScale;

        float elapsed = 0f;
        while (elapsed < inspectZoomSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / inspectZoomSeconds);
            float eased = t * t * (3f - 2f * t);

            rectTransform.anchoredPosition = Vector2.Lerp(startAnchoredPos, Vector2.zero, eased);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, eased);
            yield return null;
        }

        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localScale = targetScale;

        onComplete?.Invoke();
    }

    public void ResetInspectZoom()
    {
        if (!isZoomedForInspect) return;
        isZoomedForInspect = false;

        if (inspectZoomCoroutine != null)
        {
            StopCoroutine(inspectZoomCoroutine);
            inspectZoomCoroutine = null;
        }

        if (rectTransform == null || preInspectParent == null) return;

        rectTransform.SetParent(preInspectParent, false);
        rectTransform.SetSiblingIndex(preInspectSiblingIndex);
        rectTransform.anchoredPosition = preInspectAnchoredPosition;
        rectTransform.localScale = preInspectScale;
    }

    // Plays a quick, damped side-to-side shudder - see CombatController.OnCriticalOrWeaknessHit.
    public void PlayShiver()
    {
        if (rectTransform == null) return;

        if (shiverCoroutine != null)
            StopCoroutine(shiverCoroutine);

        shiverCoroutine = StartCoroutine(ShiverRoutine());
    }

    private System.Collections.IEnumerator ShiverRoutine()
    {
        Vector2 basePos = rectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shiverSeconds)
        {
            elapsed += Time.deltaTime;
            float damped = 1f - (elapsed / shiverSeconds); // shudder settles down toward the end
            float offsetX = Mathf.Sin(elapsed * 60f) * shiverDistance * damped;
            rectTransform.anchoredPosition = basePos + new Vector2(offsetX, 0f);
            yield return null;
        }

        rectTransform.anchoredPosition = basePos;
    }
    // Plays a card-flip (scale X down to edge-on, swap art, scale back out) when a character's
    // Normal/Demon form changes (e.g. Sicur) - see CombatController.OnFormSwitched.
    public void PlayFormFlip()
    {
        if (rectTransform == null)
        {
            ApplyArtForCurrentForm(); // no transform to animate - at least keep the art correct
            return;
        }

        if (flipCoroutine != null)
            StopCoroutine(flipCoroutine);

        flipCoroutine = StartCoroutine(FlipRoutine());
    }


    private System.Collections.IEnumerator FlipRoutine()
    {
        isFlipping = true;
        try
        {
            Vector3 startScale = rectTransform.localScale;
            float half = formFlipSeconds * 0.5f;

            yield return ScaleXTo(0f, startScale, half);

            ApplyArtForCurrentForm(); // swap the sprite while edge-on and invisible

            yield return ScaleXTo(startScale.x, startScale, half);

            rectTransform.localScale = startScale;
        }
        finally
        {
            // Guaranteed to run even if this coroutine gets stopped externally mid-flip, so
            // RefreshArt() can never get stuck skipped forever again.
            isFlipping = false;
        }
    }

    private System.Collections.IEnumerator ScaleXTo(float targetX, Vector3 baseScaleForYZ, float duration)
    {
        float startX = rectTransform.localScale.x;
        float elapsed = 0f;

        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, elapsed / duration);
            rectTransform.localScale = new Vector3(x, baseScaleForYZ.y, baseScaleForYZ.z);
            yield return null;
        }

        rectTransform.localScale = new Vector3(targetX, baseScaleForYZ.y, baseScaleForYZ.z);
    }

    // Slides the card in from fromOffset (relative to its real, already-laid-out position) while
    // fading it in, optionally after a delay so a row of cards can be staggered in one after another.
    // See CombatUIManager.PlayCombatStartSlideIn.
    public void PlaySlideIn(Vector2 fromOffset, float delay = 0f)
    {
        if (rectTransform == null) return;

        if (slideInCoroutine != null)
            StopCoroutine(slideInCoroutine);

        slideInCoroutine = StartCoroutine(SlideInRoutine(fromOffset, delay));
    }

    private System.Collections.IEnumerator SlideInRoutine(Vector2 fromOffset, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector2 targetPos = rectTransform.anchoredPosition;
        Vector2 startPos = targetPos + fromOffset;

        rectTransform.anchoredPosition = startPos;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < slideInSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideInSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out: fast start, settles gently into place

            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
            canvasGroup.alpha = eased;
            yield return null;
        }

        rectTransform.anchoredPosition = targetPos;
        canvasGroup.alpha = 1f;
    }

    public CharacterInstance BoundCharacter => boundCharacter;
    // Used by CombatUIManager to find where this card actually is on screen - e.g. so a
    // projectile effect knows where to travel from/to. See CombatUIManager.HandleRequestProjectile.
    public RectTransform CardRectTransform => rectTransform;
    private void Awake()
    {
        rectTransform = transform as RectTransform;
        baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;

        // Added at runtime rather than requiring prefab wiring - used only to dim/disable a dead
        // card in a fixed-roster fight where it stays on screen instead of being destroyed.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // Crossfades in this character's dedicated dead-card art (CharacterCardData.deadArt) and blocks
    // all pointer interaction (hover, click, right-click inspect) once a dead enemy's card is left
    // in place instead of being destroyed - see CombatUIManager.RefreshUI. Falls back to an instant
    // dim/gray-tint for a character that doesn't have dead art configured yet.
    // RefreshUI calls this with isDead:true on every refresh for as long as the character stays
    // dead (not just once at the moment of death) - hasAppliedDeadVisual guards the fade so it
    // plays exactly once instead of restarting from scratch on every subsequent turn.
    public void SetDeadVisual(bool isDead)
    {
        canvasGroup.blocksRaycasts = !isDead;
        canvasGroup.interactable = !isDead;

        if (!isDead)
        {
            hasAppliedDeadVisual = false;

            if (deathFadeCoroutine != null)
            {
                StopCoroutine(deathFadeCoroutine);
                deathFadeCoroutine = null;
            }

            canvasGroup.alpha = 1f;
            if (artImage != null) artImage.color = Color.white;
            RefreshArt(); // restore the correct live sprite (current form) when un-dimming
            return;
        }

        canvasGroup.alpha = 1f;

        bool hasDeadArt = boundCharacter != null && boundCharacter.data.deadArt != null;

        if (!hasDeadArt)
        {
            canvasGroup.alpha = 0.35f;
            if (artImage != null) artImage.color = deadArtTint;
            return;
        }

        if (hasAppliedDeadVisual) return; // transition already played - leave it alone
        hasAppliedDeadVisual = true;

        if (deathFadeCoroutine != null)
            StopCoroutine(deathFadeCoroutine);

        deathFadeCoroutine = StartCoroutine(FadeToDeadArtRoutine(boundCharacter.data.deadArt, Color.white, 0f, null));
    }

    // Crossfades to the dead-card art (or falls back to an instant gray tint if this character has
    // none configured), holding for the remainder of deathFadeOutSeconds after the fade finishes,
    // then calls onComplete - keeps the same overall timing the wave-encounter reinforcement
    // slide-in relies on.
    public void PlayDeathFadeOut(System.Action onComplete)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        bool hasDeadArt = boundCharacter != null && boundCharacter.data.deadArt != null;

        if (deathFadeCoroutine != null)
            StopCoroutine(deathFadeCoroutine);

        if (hasDeadArt)
        {
            float holdAfter = Mathf.Max(0f, deathFadeOutSeconds - deadArtFadeSeconds);
            deathFadeCoroutine = StartCoroutine(FadeToDeadArtRoutine(boundCharacter.data.deadArt, Color.white, holdAfter, onComplete));
        }
        else
        {
            if (artImage != null) artImage.color = deadArtTint;
            deathFadeCoroutine = StartCoroutine(DeathFadeOutRoutine(onComplete));
        }
    }

    // Fades artImage's alpha to 0, swaps to newSprite, then fades back up to targetColor - a
    // crossfade in everything but name, since a single Image can't blend between two different
    // sprites directly. Optionally holds for holdAfterSeconds more before calling onComplete.
    private System.Collections.IEnumerator FadeToDeadArtRoutine(Sprite newSprite, Color targetColor, float holdAfterSeconds, System.Action onComplete)
    {
        float half = deadArtFadeSeconds * 0.5f;
        Color startColor = artImage.color;
        float elapsed = 0f;

        while (half > 0f && elapsed < half)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(startColor.a, 0f, elapsed / half);
            artImage.color = new Color(startColor.r, startColor.g, startColor.b, a);
            yield return null;
        }
        artImage.color = new Color(startColor.r, startColor.g, startColor.b, 0f);

        artImage.sprite = newSprite;

        elapsed = 0f;
        while (half > 0f && elapsed < half)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(0f, targetColor.a, elapsed / half);
            artImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, a);
            yield return null;
        }
        artImage.color = targetColor;

        if (holdAfterSeconds > 0f)
            yield return new WaitForSeconds(holdAfterSeconds);

        deathFadeCoroutine = null;
        onComplete?.Invoke();
    }

    private System.Collections.IEnumerator DeathFadeOutRoutine(System.Action onComplete)
    {
        yield return new WaitForSeconds(deathFadeOutSeconds);

        deathFadeCoroutine = null;
        onComplete?.Invoke();
    }

    public void Bind(CharacterInstance character, CombatUIManager manager)
    {
        boundCharacter = character;
        uiManager = manager;
        hasAppliedDeadVisual = false;
        // Enlarges the card's resting size for a boss. Updates baseScale (not just the live
        // transform) so hover-in/out still scales relative to this bigger size instead of
        // fighting against it or snapping back to the normal card size on hover-exit.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        if (rectTransform != null && character.data.isBoss)
        {
            baseScale *= bossCardScale;
            rectTransform.localScale = baseScale;
        }
        if (nameText != null)
            nameText.text = character.data.characterName;

        artImage.sprite = character.data.cardArt;

        RefreshHP();
        RefreshEnergy();
        RefreshStatuses();
        HideActionButtons();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            uiManager?.OnInspectCard(boundCharacter);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverScaleEnabled)
            PlayScale(baseScale * hoverScale);

        uiManager?.OnCardHoverEnter(boundCharacter, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverScaleEnabled)
            PlayScale(baseScale);

        uiManager?.OnCardHoverExit(boundCharacter, this);
    }

    private void PlayScale(Vector3 targetScale)
    {
        if (rectTransform == null) return;

        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(ScaleTo(targetScale, hoverScaleSeconds));
    }

    private System.Collections.IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        Vector3 startScale = rectTransform.localScale;
        float elapsed = 0f;

        while (duration > 0f && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }

        rectTransform.localScale = targetScale;
    }

    public void SetTargetHighlight(bool show, Color color)
    {
        if (targetHoverHighlight == null) return;

        if (targetHoverHighlightImage != null)
            targetHoverHighlightImage.color = color;

        targetHoverHighlight.SetActive(show);
    }

    public void RefreshHP()
    {
        if (boundCharacter == null) return;

        hpSlider.maxValue = boundCharacter.maxHP;
        hpSlider.value = boundCharacter.currentHP;
        hpText.text = $"{boundCharacter.currentHP} / {boundCharacter.maxHP}";

        UpdateGhostHP();
    }

    // Lets the ghost bar trail behind on damage - holds at the old fraction, then drains down to
    // the new one after a delay, instead of snapping instantly like the main bar. Tracks its own
    // "last known" fraction rather than reading the previous value back off hpSlider, because
    // RefreshHP fires twice per hit: once from CombatController.OnTargetUpdated right as the
    // damage lands, then again moments later from the broader OnStateChanged/RefreshUI pass at
    // the end of the action. hpSlider already shows the new value by that second call, so
    // comparing against it would see "no change" and stomp the ghost mid-drain before it's had
    // any time to show.
    private void UpdateGhostHP()
    {
        if (ghostHpFillImage == null || boundCharacter.maxHP <= 0) return;

        float newFraction = (float)boundCharacter.currentHP / boundCharacter.maxHP;

        if (Mathf.Approximately(newFraction, lastGhostHpFraction))
            return; // already processed this HP value - leave whatever's currently showing alone

        float previousFraction = lastGhostHpFraction < 0f ? newFraction : lastGhostHpFraction;
        lastGhostHpFraction = newFraction;

        if (ghostHpCoroutine != null)
            StopCoroutine(ghostHpCoroutine);

        if (newFraction < previousFraction)
        {
            ghostHpFillImage.fillAmount = previousFraction;
            ghostHpCoroutine = StartCoroutine(GhostHpDrainRoutine(previousFraction, newFraction));
        }
        else
        {
            ghostHpFillImage.fillAmount = newFraction;
        }
    }

    private System.Collections.IEnumerator GhostHpDrainRoutine(float from, float to)
    {
        if (ghostHpDelaySeconds > 0f)
            yield return new WaitForSeconds(ghostHpDelaySeconds);

        float elapsed = 0f;
        while (elapsed < ghostHpDrainSeconds)
        {
            elapsed += Time.deltaTime;
            ghostHpFillImage.fillAmount = Mathf.Lerp(from, to, elapsed / ghostHpDrainSeconds);
            yield return null;
        }

        ghostHpFillImage.fillAmount = to;
        ghostHpCoroutine = null;
    }

    public void RefreshEnergy()
    {
        if (boundCharacter == null || energySlider == null) return;

        energySlider.maxValue = boundCharacter.maxEnergy;
        energySlider.value = boundCharacter.currentEnergy;

        if (energyText != null)
            energyText.text = $"{boundCharacter.currentEnergy} / {boundCharacter.maxEnergy}";
    }

    public void RefreshStatuses()
    {
        if (boundCharacter == null) return;

        ApplyStatusTint();

        if (statusIconContainer == null) return;

        foreach (Transform child in statusIconContainer)
            Destroy(child.gameObject);

        List<StatusEffectInstance> statuses = boundCharacter.GetStatusDisplayList();

        foreach (var status in statuses)
        {
            // Stains and marks arrive with icon left null (see CharacterInstance.GetStatusDisplayList) -
            // resolve the actual sprite here instead, since the source character/element only
            // makes sense to look up at the UI layer, not baked into the data-side status class.
            if (status.icon == null)
            {
                if (status.stainElement.HasValue)
                    status.icon = GetStainIcon(status.stainElement.Value);
                else if (status.markSourceCharacter != null)
                    status.icon = status.markSourceCharacter.markIcon;
            }

            GameObject iconObj = Instantiate(statusIconPrefab, statusIconContainer);
            StatusIconUI iconUI = iconObj.GetComponent<StatusIconUI>();
            iconUI.Bind(status);
        }
    }

    private Sprite GetStainIcon(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire: return fireStainIcon;
            case ElementType.Ice: return iceStainIcon;
            case ElementType.Electro: return electroStainIcon;
            default: return null;
        }
    }

    // Tints the card art for whichever active status effect wants one (e.g. Freeze/Petrified - see
    // StatusEffectData.tintsCardArt), reverting to white when none do. Skipped once the character
    // is dead - SetDeadVisual/PlayDeathFadeOut own the art color from that point on.
    private void ApplyStatusTint()
    {
        if (artImage == null || !boundCharacter.isAlive) return;

        artImage.color = boundCharacter.GetCardTintColor() ?? Color.white;
    }

    // Guarded: while a form-flip animation is playing, it owns the sprite swap itself (timed to
    // the flip's midpoint via ApplyArtForCurrentForm below) - so RefreshUI's blanket per-card
    // refresh must not jump the sprite to the new form early and spoil the reveal. Also skipped
    // once the character is dead - SetDeadVisual/PlayDeathFadeOut own the sprite from that point
    // on, and RefreshUI calls this every refresh, which would otherwise stomp the dead-art swap
    // straight back to the live sprite on the very next turn.
    public void RefreshArt()
    {
        if (isFlipping) return;
        if (boundCharacter != null && !boundCharacter.isAlive) return;
        ApplyArtForCurrentForm();
    }

    private void ApplyArtForCurrentForm()
    {
        if (boundCharacter == null || artImage == null) return;

        if (boundCharacter.data.hasForms && boundCharacter.currentForm == CharacterForm.Demon && boundCharacter.data.demonFormArt != null)
        {
            artImage.sprite = boundCharacter.data.demonFormArt;
        }
        else
        {
            artImage.sprite = boundCharacter.data.cardArt;
        }
    }

    public void SetActiveTurn(bool isActive)
    {
        if (activeTurnHighlight != null)
            activeTurnHighlight.SetActive(isActive);
    }

    public void ShowActionButtons(AbilityData basic, AbilityData skill, AbilityData ult)
    {
        actionButtonsContainer.SetActive(true);
        SetupButton(basicButton, basicButtonIcon, basic);
        SetupButton(skillButton, skillButtonIcon, skill);
        SetupButton(ultButton, ultButtonIcon, ult);
    }

    private void SetupButton(Button button, Image icon, AbilityData ability)
    {
        if (ability == null)
        {
            button.gameObject.SetActive(false);
            return;
        }

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => uiManager?.OnAbilitySelected(boundCharacter, ability));

        if (icon != null)
            icon.sprite = ability.icon;
    }

    public void HideActionButtons()
    {
        actionButtonsContainer.SetActive(false);
    }

    public void OnCardClicked()
    {
        uiManager?.OnCardClicked(boundCharacter);
    }
    // Spawns an ability's impact effect prefab on this card and calls onComplete once it's had
    // its configured duration to play - see CombatController.OnRequestImpactEffect. Resolves
    // immediately with no visual if effectPrefab is null (ability has none configured).
    public void PlayImpactEffect(GameObject effectPrefab, float duration, System.Action onComplete)
    {
        if (effectPrefab == null)
        {
            onComplete?.Invoke();
            return;
        }

        Transform anchor = impactEffectAnchor != null ? impactEffectAnchor : transform;
        GameObject fx = Instantiate(effectPrefab, anchor);
        fx.transform.localPosition = Vector3.zero;

        if (impactEffectCoroutine != null)
            StopCoroutine(impactEffectCoroutine);

        impactEffectCoroutine = StartCoroutine(ImpactEffectRoutine(fx, duration, onComplete));
    }

    private System.Collections.IEnumerator ImpactEffectRoutine(GameObject fx, float duration, System.Action onComplete)
    {
        yield return new WaitForSeconds(duration);

        if (fx != null)
            Destroy(fx);

        onComplete?.Invoke();
    }
    public void ShowFloatingText(int amount, bool isHeal, ElementType element)
    {
        if (floatingTextAnchor == null || floatingTextPrefab == null) return;

        GameObject textObj = Instantiate(floatingTextPrefab, floatingTextAnchor);
        FloatingTextUI floatingText = textObj.GetComponent<FloatingTextUI>();

        string content = isHeal ? $"+{amount}" : $"-{amount}";
        Color color = isHeal ? Color.green : GetElementColor(element);

        floatingText.Play(content, color);
    }

    private Color GetElementColor(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:
                return new Color(1f, 0.4f, 0.1f);      // orange
            case ElementType.Ice:
                return new Color(0.4f, 0.85f, 1f);      // cyan
            case ElementType.Electro:
                return new Color(0.7f, 0.3f, 1f);       // purple
            case ElementType.Holy:
                return new Color(1f, 0.95f, 0.5f);      // pale gold
            case ElementType.Shadow:
                return new Color(0.5f, 0.1f, 0.6f);     // dark violet
            case ElementType.Physical:
            default:
                return Color.white;
        }
    }

    // Hidden for a boss (CharacterCardData.isBoss) whose HP is instead shown on the top boss
    // health bar - visible by default, so every other card keeps its normal HP bar.
    public void SetHPBarVisible(bool visible)
    {
        if (hpBarContainer != null)
            hpBarContainer.SetActive(visible);
    }
    // Same idea as SetHPBarVisible, for the boss energy bar.
    public void SetEnergyBarVisible(bool visible)
    {
        if (energyBarContainer != null)
            energyBarContainer.SetActive(visible);
    }
}
