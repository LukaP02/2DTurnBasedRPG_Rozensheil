using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GridManager : MonoBehaviour
{
    public GameObject iconPrefab;
    public Transform gridParent;
    public CategoryData[] categories;
    public PreviewPanel previewPanel;
    public TextMeshProUGUI categoryNameText;


    void Start()
    {
        if (previewPanel != null)
            previewPanel.SetDefault();

        LoadCategory(0);
    }

    public void LoadCategory(int index)
    {
        if (categories == null || categories.Length == 0) return;
        if (index < 0 || index >= categories.Length) return;
        if (categories[index] == null) return;

        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        if (categoryNameText != null)
            categoryNameText.text = categories[index].categoryName;

        if (categories[index].icons == null) return;

        foreach (Sprite icon in categories[index].icons)
        {
            if (icon == null) continue;
            GameObject cell = Instantiate(iconPrefab, gridParent);
            Image img = cell.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;

            Sprite capturedIcon = icon;
            cell.GetComponent<Button>().onClick.AddListener(() =>
            {
                previewPanel.ShowIcon(capturedIcon, capturedIcon.name);
            });
        }
    }
}