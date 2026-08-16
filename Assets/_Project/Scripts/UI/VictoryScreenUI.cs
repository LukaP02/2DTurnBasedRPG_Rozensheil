using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VictoryScreenUI : MonoBehaviour
{
    public GameObject victoryPanel;
    public TMP_Text messageText;
    public TMP_Text goldEarnedText;
    public Button continueButton;

    public event Action OnContinuePressed;

    private void Awake()
    {
        if (continueButton != null)
            continueButton.onClick.AddListener(() => OnContinuePressed?.Invoke());
    }

    public void Show(int goldEarned)
    {
        if (messageText != null)
            messageText.text = "Victory!";

        if (goldEarnedText != null)
            goldEarnedText.text = $"Gold Earned: {goldEarned}";

        victoryPanel.SetActive(true);
    }

    public void Hide()
    {
        victoryPanel.SetActive(false);
    }
}