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

    [Header("Status Icons")]
    public Transform statusIconContainer;
    public GameObject statusIconPrefab;

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
    private Coroutine shiverCoroutine;
    private Coroutine inspectZoomCoroutine;

    [Header("Slide-In")]
    public float slideInSeconds = 0.35f;

    [Header("Form Flip")]
    [Tooltip("Total time for the flip - half spent shrinking to edge-on, half spent unfolding back out. The sprite is swapped at the midpoint, when the card is invisible edge-on.")]
    public float formFlipSeconds = 0.3f;

    private bool isFlipping;

    [Header("Death Visual")]
    [Tooltip("Tint applied to a dead character's art - used both for a card left in place after death (fixed-roster enemies and allies) and for a wave-encounter card while it fades out before being replaced. White = no change; lower RGB = more washed-out/gray.")]
    public Color deadArtTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("How long a wave-encounter enemy's card takes to fade out before being destroyed and replaced by a reinforcement's card.")]
    public float deathFadeOutSeconds = 0.4f;

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
        Debug.Log($"[InspectZoomDebug] InspectZoomRoutine STARTED for {name}, frame {Time.frameCount}");
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
        Debug.Log($"[InspectZoomDebug] InspectZoomRoutine FINISHED for {name}, frame {Time.frameCount}, onComplete is {(onComplete == null ? "NULL" : "set")}");

        onComplete?.Invoke();
        Debug.Log($"[InspectZoomDebug] onComplete?.Invoke() returned for {name}, frame {Time.frameCount}");
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

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;

        if (targetHoverHighlight != null)
            targetHoverHighlightImage = targetHoverHighlight.GetComponent<Image>();

        // Added at runtime rather than requiring prefab wiring - used only to dim/disable a dead
        // card in a fixed-roster fight where it stays on screen instead of being destroyed.
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // Dims the card and blocks all pointer interaction (hover, click, right-click inspect) once a
    // dead enemy's card is left in place instead of being destroyed - see CombatUIManager.RefreshUI.
    public void SetDeadVisual(bool isDead)
    {
        canvasGroup.alpha = isDead ? 0.35f : 1f;
        canvasGroup.blocksRaycasts = !isDead;
        canvasGroup.interactable = !isDead;

        if (artImage != null)
            artImage.color = isDead ? deadArtTint : Color.white;
    }

    public void PlayDeathFadeOut(System.Action onComplete)
    {
        if (artImage != null)
            artImage.color = deadArtTint;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (deathFadeCoroutine != null)
            StopCoroutine(deathFadeCoroutine);

        deathFadeCoroutine = StartCoroutine(DeathFadeOutRoutine(onComplete));
    }

    private System.Collections.IEnumerator DeathFadeOutRoutine(System.Action onComplete)
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        float elapsed = 0f;

        while (elapsed < deathFadeOutSeconds)
        {
            elapsed += Time.deltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / deathFadeOutSeconds);
            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        onComplete?.Invoke();
    }

    public void Bind(CharacterInstance character, CombatUIManager manager)
    {
        boundCharacter = character;
        uiManager = manager;
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
        if (boundCharacter == null || statusIconContainer == null) return;

        foreach (Transform child in statusIconContainer)
            Destroy(child.gameObject);

        List<StatusEffectInstance> statuses = boundCharacter.GetStatusDisplayList();

        foreach (var status in statuses)
        {
            GameObject iconObj = Instantiate(statusIconPrefab, statusIconContainer);
            StatusIconUI iconUI = iconObj.GetComponent<StatusIconUI>();
            iconUI.Bind(status);
        }
    }

    // Guarded: while a form-flip animation is playing, it owns the sprite swap itself (timed to
    // the flip's midpoint via ApplyArtForCurrentForm below) - so RefreshUI's blanket per-card
    // refresh must not jump the sprite to the new form early and spoil the reveal.
    public void RefreshArt()
    {
        if (isFlipping) return;
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
        SetupButton(basicButton, basic);
        SetupButton(skillButton, skill);
        SetupButton(ultButton, ult);
    }

    private void SetupButton(Button button, AbilityData ability)
    {
        if (ability == null)
        {
            button.gameObject.SetActive(false);
            return;
        }

        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => uiManager?.OnAbilitySelected(boundCharacter, ability));
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