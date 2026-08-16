using UnityEngine;

// Minecraft background
public class MainMenuBackground : MonoBehaviour
{
    [Header("Pan")]
    public float panRangeX = 60f;
    public float panRangeY = 30f;
    public float panSpeed = 0.05f;

    [Header("Zoom")]
    public float zoomAmount = 0.05f;
    public float zoomSpeed = 0.08f;

    private RectTransform rect;
    private Vector2 basePosition;
    private Vector3 baseScale;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        basePosition = rect.anchoredPosition;
        baseScale = rect.localScale;
    }

    private void Update()
    {
        float t = Time.time;

        float x = Mathf.Sin(t * panSpeed) * panRangeX;
        float y = Mathf.Sin(t * panSpeed * 0.6f + 1.3f) * panRangeY;
        rect.anchoredPosition = basePosition + new Vector2(x, y);

        float zoom = 1f + (Mathf.Sin(t * zoomSpeed) * 0.5f + 0.5f) * zoomAmount;
        rect.localScale = baseScale * zoom;
    }
}