using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small info panel shown when a node on the overworld map is clicked, before actually entering
// it - name, location, description, an inferred type label, and a small preview image. Confirm
// proceeds into the level; Close just dismisses the panel and leaves the player on the map.
public class LevelPreviewUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject panel;

    [Header("Info")]
    public TMP_Text nameText;
    public TMP_Text locationText;
    public TMP_Text descriptionText;
    public TMP_Text typeText;
    public Image previewImage;

    [Header("Buttons")]
    public Button confirmButton;
    public Button closeButton;

    private LevelData pendingLevel;
    private int pendingLevelIndex;
    private GameFlowManager gameFlowManager;

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    public void Show(LevelData level, int levelIndex, GameFlowManager flowManager)
    {
        if (level == null || panel == null) return;

        pendingLevel = level;
        pendingLevelIndex = levelIndex;
        gameFlowManager = flowManager;

        if (nameText != null) nameText.text = level.levelName;
        if (locationText != null) locationText.text = level.location;
        if (descriptionText != null) descriptionText.text = level.previewDescription;
        if (typeText != null) typeText.text = DescribeType(level);

        if (previewImage != null)
        {
            previewImage.sprite = level.previewImage;
            previewImage.enabled = level.previewImage != null;
        }

        panel.SetActive(true);
    }

    // No explicit "node type" field on LevelData - inferred from whichever optional flow fields
    // are actually set, so there's nothing extra to remember to fill in per level.
    private string DescribeType(LevelData level)
    {
        bool hasCombat = level.enemies != null && System.Array.Exists(level.enemies, e => e != null);
        if (hasCombat) return "Battle";

        bool hasEvent = level.preLevelEvent != null || level.postLevelEvent != null;
        if (hasEvent) return "Event";

        bool hasDialogue = level.introDialogue != null || level.postLevelDialogue != null;
        if (hasDialogue) return "Dialogue";

        return "Unknown";
    }

    private void Confirm()
    {
        panel.SetActive(false);
        gameFlowManager.StartLevel(pendingLevel, pendingLevelIndex);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}