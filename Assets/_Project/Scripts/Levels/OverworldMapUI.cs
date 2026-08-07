using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OverworldMapUI : MonoBehaviour
{
    public LevelData[] levelsInOrder;
    public Transform nodeContainer;
    public GameObject nodeButtonPrefab;

    public GameFlowManager gameFlowManager;

    private List<Button> spawnedButtons = new List<Button>();

    private void Start()
    {
        SpawnNodes();
    }

    private void SpawnNodes()
    {
        foreach (Transform child in nodeContainer)
            Destroy(child.gameObject);
        spawnedButtons.Clear();

        for (int i = 0; i < levelsInOrder.Length; i++)
        {
            int index = i;
            LevelData level = levelsInOrder[i];

            GameObject buttonObj = Instantiate(nodeButtonPrefab, nodeContainer);

            TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = level.levelName;

            Button button = buttonObj.GetComponent<Button>();
            button.interactable = index <= PartyManager.Instance.UnlockedLevelIndex;
            button.onClick.AddListener(() => gameFlowManager.StartLevel(level, index));

            spawnedButtons.Add(button);
        }
    }

    public void RefreshNodes()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            spawnedButtons[i].interactable = i <= PartyManager.Instance.UnlockedLevelIndex;
        }
    }
}