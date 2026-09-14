using UnityEngine;
using UnityEngine.EventSystems;

// Drop onto any UI element (a button, a shop item, an enemy card, a map node) to swap the mouse
// cursor while hovering it, reverting to the default cursor on exit - same drop-in-component
// pattern as ButtonClickSFX. Needs a Graphic (Image, TextMeshProUGUI, etc.) on this object or a
// parent for pointer events to reach it at all, same requirement any other IPointerEnterHandler has.
public class HoverCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CursorManager.CursorType cursorType = CursorManager.CursorType.Hand;

    public void OnPointerEnter(PointerEventData eventData)
    {
        CursorManager.Instance?.SetCursor(cursorType);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CursorManager.Instance?.ResetCursor();
    }
}