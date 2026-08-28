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
    public Slider hpSlider;
    public TMP_Text hpText;
    public Slider energySlider;
    public TMP_Text energyText;
    public GameObject activeTurnHighlight;

    [Header("Hover")]
    // Shown while an ability is selected and this card is a valid target for it.
    public GameObject targetHoverHighlight;
    public float hoverScale = 1.08f;
    public float hoverScaleSeconds = 0.12f;

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

    private CharacterInstance boundCharacter;
    private CombatUIManager uiManager;
    private RectTransform rectTransform;
    private Vector3 baseScale;
    private Coroutine scaleCoroutine;
    private Image targetHoverHighlightImage;

    public CharacterInstance BoundCharacter => boundCharacter;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;

        if (targetHoverHighlight != null)
            targetHoverHighlightImage = targetHoverHighlight.GetComponent<Image>();
    }

    public void Bind(CharacterInstance character, CombatUIManager manager)
    {
        boundCharacter = character;
        uiManager = manager;

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
            uiManager.OnInspectCard(boundCharacter);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayScale(baseScale * hoverScale);
        uiManager?.OnCardHoverEnter(boundCharacter, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
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

    public void RefreshArt()
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
        button.onClick.AddListener(() => uiManager.OnAbilitySelected(boundCharacter, ability));
    }

    public void HideActionButtons()
    {
        actionButtonsContainer.SetActive(false);
    }

    public void OnCardClicked()
    {
        uiManager.OnCardClicked(boundCharacter);
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
}