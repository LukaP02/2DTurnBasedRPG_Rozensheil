using UnityEngine;
using TMPro;

public class StatusIconUI : MonoBehaviour
{
    public TMP_Text labelText;
    public TMP_Text countText;

    public void Bind(StatusEffectInstance status)
    {
        if (labelText != null)
            labelText.text = status.label;

        if (countText != null)
        {
            countText.gameObject.SetActive(true);
            countText.text = status.stackCount.ToString();
        }
    }
}