using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PreviewPanel : MonoBehaviour
{
    public Image previewImage;
    public TextMeshProUGUI previewName;

  

    public void SetDefault()
    {
        previewImage.gameObject.SetActive(false);
        previewName.text = "Select Icon";
    }

    public void ShowIcon(Sprite icon, string iconName)
    {
        previewImage.gameObject.SetActive(true);
        previewImage.sprite = icon;
        previewImage.preserveAspect = true;
        previewName.text = iconName;
    }
}