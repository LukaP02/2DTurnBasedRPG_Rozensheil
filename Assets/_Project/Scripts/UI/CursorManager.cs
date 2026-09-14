using System.Collections.Generic;
using UnityEngine;

// Singleton that owns the actual OS/software cursor swap - same pattern as AudioManager and
// ScreenFader. Other scripts never call Cursor.SetCursor directly; they call
// CursorManager.Instance.SetCursor(type) / ResetCursor() instead, so every cursor change goes
// through one place that already knows which texture and hotspot belongs to each type.
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    public enum CursorType
    {
        Default, Hand, Attack, Shoot, Basic, Defense, Flask, Move, Production, Settings, Book, Loot, Ship
    }

    [System.Serializable]
    public struct CursorEntry
    {
        public CursorType type;
        public Texture2D texture;
        [Tooltip("Pixel offset from the texture's top-left corner to the actual pointer tip (e.g. the crosshair center for Attack, the fingertip for Hand). Leave (0,0) for a texture already drawn with the tip at its top-left.")]
        public Vector2 hotspot;
    }

    [Tooltip("One entry per CursorType you want to support - drag the matching PNG from Assets/Cursors in here. Any type left unassigned is silently ignored by SetCursor.")]
    public CursorEntry[] cursors;
    [Tooltip("ForceSoftware renders the cursor through Unity itself at full resolution - use this with the 256px set for a crisp, large cursor. Auto lets the OS render it natively and may downscale large textures (safer with the 64px set).")]
    public CursorMode cursorMode = CursorMode.Auto;

    private Dictionary<CursorType, CursorEntry> lookup;
    private CursorType currentType = CursorType.Default;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        lookup = new Dictionary<CursorType, CursorEntry>();
        foreach (var entry in cursors)
            lookup[entry.type] = entry;

        // Force-apply once at startup rather than relying on the early-out in SetCursor (which
        // skips reapplying a type equal to currentType's default value).
        if (lookup.TryGetValue(CursorType.Default, out var defaultEntry) && defaultEntry.texture != null)
            Cursor.SetCursor(defaultEntry.texture, defaultEntry.hotspot, cursorMode);
    }

    public void SetCursor(CursorType type)
    {
        if (currentType == type) return;
        if (!lookup.TryGetValue(type, out var entry) || entry.texture == null) return;

        Cursor.SetCursor(entry.texture, entry.hotspot, cursorMode);
        currentType = type;
    }

    public void ResetCursor()
    {
        SetCursor(CursorType.Default);
    }
}