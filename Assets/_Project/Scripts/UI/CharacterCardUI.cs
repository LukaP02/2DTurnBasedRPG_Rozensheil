using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterCardUI : MonoBehaviour
{
    [Header("Visuals")]
    public Image artImage;
    public TMP_Text nameText;
    public Slider hpSlider;
    public TMP_Text hpText;
    public Slider energySlider;
    public TMP_Text energyText;
    public GameObject activeTurnHighlight;

    [Header("Action Buttons")]
    public GameObject actionButtonsContainer;
    public Button basicButton;
    public Button skillButton;
    public Button ultButton;

    [Header("Inspect")]
    public Button inspectButton;

    [Header("Status Icons")]
    public Transform statusIconContainer;
    public GameObject statusIconPrefab;

    [Header("Floating Text")]
    public Transform floatingTextAnchor;
    public GameObject floatingTextPrefab;

    private CharacterInstance boundCharacter;
    private CombatUIManager uiManager;

    public CharacterInstance BoundCharacter => boundCharacter;

    public void Bind(CharacterInstance character, CombatUIManager manager)
    {
        boundCharacter = character;
        uiManager = manager;

        nameText.text = character.data.characterName;
        artImage.sprite = character.data.cardArt;

        RefreshHP();
        RefreshEnergy();
        RefreshStatuses();
        HideActionButtons();

        if (inspectButton != null)
        {
            inspectButton.onClick.RemoveAllListeners();
            inspectButton.onClick.AddListener(() => uiManager.OnInspectCard(boundCharacter));
        }
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

    public void ShowFloatingText(int amount, bool isHeal)
    {
        if (floatingTextAnchor == null || floatingTextPrefab == null) return;

        GameObject textObj = Instantiate(floatingTextPrefab, floatingTextAnchor);
        FloatingTextUI floatingText = textObj.GetComponent<FloatingTextUI>();

        string content = isHeal ? $"+{amount}" : $"-{amount}";
        Color color = isHeal ? Color.green : Color.red;

        floatingText.Play(content, color);
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
}