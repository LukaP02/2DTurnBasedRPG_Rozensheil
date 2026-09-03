using UnityEngine;
using UnityEngine.UI;

public class CreditsPanel : MonoBehaviour
{
    public GameObject creditsPanel;
    public Button openButton;
    public Button closeButton;

    void Start()
    {
        creditsPanel.SetActive(false);

        if (openButton != null)
            openButton.onClick.AddListener(OpenPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
    }

    void OpenPanel()
    {
        creditsPanel.SetActive(true);
    }

    void ClosePanel()
    {
        creditsPanel.SetActive(false);
    }
}
