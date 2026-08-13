using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DefeatScreenUI : MonoBehaviour
{
    public GameObject defeatPanel;
    public TMP_Text messageText;
    public Button continueButton;

    public event Action OnContinuePressed;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => OnContinuePressed?.Invoke());
    }

    public void Show()
    {
        if (messageText != null)
            messageText.text = "This is not how the story goes...";

        defeatPanel.SetActive(true);
    }

    public void Hide()
    {
        defeatPanel.SetActive(false);
    }
}