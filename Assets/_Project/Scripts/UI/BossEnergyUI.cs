using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossEnergyBarUI : MonoBehaviour
{
    public GameObject root;
    public Image fillImage; // Image Type: Filled, Fill Method: Horizontal
    public TMP_Text energyText;

    public void Show(CharacterInstance boss)
    {
        if (root != null)
            root.SetActive(true);

        Refresh(boss);
    }

    public void Refresh(CharacterInstance boss)
    {
        if (fillImage != null)
            fillImage.fillAmount = boss.maxEnergy > 0 ? (float)boss.currentEnergy / boss.maxEnergy : 0f;

        if (energyText != null)
            energyText.text = $"{boss.currentEnergy} / {boss.maxEnergy}";
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }
}