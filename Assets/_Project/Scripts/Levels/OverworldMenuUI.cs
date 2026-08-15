using UnityEngine;
using UnityEngine.UI;

// Lives on the Overworld screen. Owns the buttons that open/close the Shop, Loadout, and
// Party Setup panels, wiring them in code so nothing needs to be configured via the
// Inspector's OnClick list.
public class OverworldMenuUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject shopMenuPanel;
    public GameObject loadoutMenuPanel;
    public GameObject partySetupPanel;

    [Header("Open Buttons (on the Overworld screen)")]
    public Button openShopButton;
    public Button openLoadoutButton;
    public Button openPartySetupButton;

    [Header("Close Buttons (on each panel)")]
    public Button closeShopButton;
    public Button closeLoadoutButton;
    public Button closePartySetupButton;

    private void Awake()
    {
        if (openShopButton != null) openShopButton.onClick.AddListener(() => shopMenuPanel.SetActive(true));
        if (openLoadoutButton != null) openLoadoutButton.onClick.AddListener(() => loadoutMenuPanel.SetActive(true));
        if (openPartySetupButton != null) openPartySetupButton.onClick.AddListener(() => partySetupPanel.SetActive(true));

        if (closeShopButton != null) closeShopButton.onClick.AddListener(() => shopMenuPanel.SetActive(false));
        if (closeLoadoutButton != null) closeLoadoutButton.onClick.AddListener(() => loadoutMenuPanel.SetActive(false));
        if (closePartySetupButton != null) closePartySetupButton.onClick.AddListener(() => partySetupPanel.SetActive(false));
    }
}