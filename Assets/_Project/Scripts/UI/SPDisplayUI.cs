using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SPDisplayUI : MonoBehaviour
{
    public CombatController combatController;

    [Header("Pip Setup")]
    public Transform pipContainer;
    public GameObject pipPrefab; // a simple Image

    [Header("Pip Colors")]
    public Color filledColor = Color.yellow;
    public Color emptyColor = Color.gray;

    private List<Image> pips = new List<Image>();

    private void OnEnable()
    {
        if (combatController != null)
            combatController.OnStateChanged += RefreshDisplay;

        BuildPips();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (combatController != null)
            combatController.OnStateChanged -= RefreshDisplay;
    }

    private void BuildPips()
    {
        // Clear any existing pips (e.g. if max SP changes between fights)
        foreach (Transform child in pipContainer)
            Destroy(child.gameObject);
        pips.Clear();

        if (combatController == null) return;

        for (int i = 0; i < combatController.MaxSkillPoints; i++)
        {
            GameObject pipObj = Instantiate(pipPrefab, pipContainer);
            Image pipImage = pipObj.GetComponent<Image>();
            pips.Add(pipImage);
        }
    }

    private void RefreshDisplay()
    {
        if (combatController == null) return;

        // Rebuild if pip count doesn't match max SP (safety net)
        if (pips.Count != combatController.MaxSkillPoints)
        {
            BuildPips();
        }

        int current = combatController.CurrentSkillPoints;

        for (int i = 0; i < pips.Count; i++)
        {
            pips[i].color = i < current ? filledColor : emptyColor;
        }
    }
}