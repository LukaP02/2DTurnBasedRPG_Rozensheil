using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBarUI : MonoBehaviour
{
    public GameObject root;
    public TMP_Text nameText;
    public Image fillImage; // Image Type: Filled, Fill Method: Horizontal
    public TMP_Text hpText;

    public void Show(CharacterInstance boss)
    {
        if (root != null)
            root.SetActive(true);

        if (nameText != null)
            nameText.text = boss.data.characterName;

        Refresh(boss);
    }

    public void Refresh(CharacterInstance boss)
    {
        if (fillImage != null)
            fillImage.fillAmount = boss.maxHP > 0 ? (float)boss.currentHP / boss.maxHP : 0f;

        if (hpText != null)
            hpText.text = $"{boss.currentHP} / {boss.maxHP}";
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}