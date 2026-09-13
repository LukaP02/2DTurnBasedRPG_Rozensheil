using UnityEngine;

public class ElementTintedParticle : MonoBehaviour
{
    [System.Serializable]
    public struct ElementColorEntry
    {
        public ElementType element;
        public Color color;
    }

    [Tooltip("Color used per element. Anything not listed here falls back to Default Color.")]
    public ElementColorEntry[] elementColors = new ElementColorEntry[]
    {
        new ElementColorEntry { element = ElementType.Fire, color = new Color(1f, 0.4f, 0.1f) },
        new ElementColorEntry { element = ElementType.Ice, color = new Color(0.5f, 0.85f, 1f) },
        new ElementColorEntry { element = ElementType.Electro, color = new Color(0.75f, 0.4f, 1f) },
        new ElementColorEntry { element = ElementType.Holy, color = new Color(1f, 0.95f, 0.6f) },
        new ElementColorEntry { element = ElementType.Shadow, color = new Color(0.5f, 0.1f, 0.6f) },
    };
    public Color defaultColor = Color.white;

    public void ApplyElementColor(ElementType element)
    {
        Color color = defaultColor;

        foreach (var entry in elementColors)
        {
            if (entry.element == element)
            {
                color = entry.color;
                break;
            }
        }

        foreach (var system in GetComponentsInChildren<ParticleSystem>())
        {
            var main = system.main;
            main.startColor = color;
        }
    }
}