using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OverworldMapUI : MonoBehaviour
{
    public LevelData[] levelsInOrder;
    public Transform nodeContainer;
    public GameObject nodeButtonPrefab;

    [Tooltip("Nodes unlocked from the very start of the game (e.g. Level1). Everything else stays locked until some node's Unlocks On Complete list opens it.")]
    public LevelData[] initiallyUnlockedLevels;

    [Header("Connection Lines")]
    [Tooltip("A thin stretchable Image (see setup notes) drawn between a node and each node it unlocks.")]
    public GameObject lineSegmentPrefab;

    public GameFlowManager gameFlowManager;

    private List<Button> spawnedButtons = new List<Button>();
    // Each line is only shown once its destination node is unlocked, so it's tracked alongside
    // that destination for RefreshNodes() to re-check.
    private List<(GameObject lineObj, LevelData destination)> spawnedLines = new List<(GameObject, LevelData)>();

    private void Start()
    {
        // Additive/idempotent, so re-seeding here on every load never undoes real progress.
        PartyManager.Instance.UnlockLevels(initiallyUnlockedLevels);

        SpawnConnections();
        SpawnNodes();
    }

    // Drawn before the node buttons (and pushed behind them, see DrawConnection) so lines never
    // block clicks on the nodes themselves.
    private void SpawnConnections()
    {
        if (lineSegmentPrefab == null) return;

        spawnedLines.Clear();

        foreach (var level in levelsInOrder)
        {
            if (level == null || level.unlocksOnComplete == null) continue;

            foreach (var unlocked in level.unlocksOnComplete)
            {
                if (unlocked == null) continue;

                GameObject lineObj = DrawConnection(level.mapPosition, unlocked.mapPosition);
                lineObj.SetActive(PartyManager.Instance.IsLevelUnlocked(unlocked));

                spawnedLines.Add((lineObj, unlocked));
            }
        }
    }

    // Stretches/rotates a copy of lineSegmentPrefab to span from -> to. Assumes the prefab's
    // RectTransform pivot is (0, 0.5) - left-center - so anchoring it at "from" and rotating
    // around that pivot sweeps its width toward "to".
    private GameObject DrawConnection(Vector2 from, Vector2 to)
    {
        GameObject lineObj = Instantiate(lineSegmentPrefab, nodeContainer);
        lineObj.transform.SetAsFirstSibling(); // render behind node buttons

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector2 direction = to - from;
            float distance = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(distance, rt.sizeDelta.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        return lineObj;
    }

    private void SpawnNodes()
    {
        foreach (Transform child in nodeContainer)
        {
            // Connection lines were just spawned into the same container - leave them alone,
            // only clear out old node buttons from a previous SpawnNodes() call.
            if (child.GetComponent<Button>() != null)
                Destroy(child.gameObject);
        }
        spawnedButtons.Clear();

        for (int i = 0; i < levelsInOrder.Length; i++)
        {
            int index = i;
            LevelData level = levelsInOrder[i];

            GameObject buttonObj = Instantiate(nodeButtonPrefab, nodeContainer);

            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = level.mapPosition;

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = level.levelName;

            Button button = buttonObj.GetComponent<Button>();
            bool unlocked = PartyManager.Instance.IsLevelUnlocked(level);
            button.interactable = unlocked;
            buttonObj.SetActive(unlocked); // hidden entirely until unlocked, not just unclickable
            button.onClick.AddListener(() => gameFlowManager.StartLevel(level, index));

            spawnedButtons.Add(button);
        }
    }

    public void RefreshNodes()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            bool unlocked = PartyManager.Instance.IsLevelUnlocked(levelsInOrder[i]);
            spawnedButtons[i].interactable = unlocked;
            spawnedButtons[i].gameObject.SetActive(unlocked);
        }

        foreach (var (lineObj, destination) in spawnedLines)
        {
            lineObj.SetActive(PartyManager.Instance.IsLevelUnlocked(destination));
        }
    }
}