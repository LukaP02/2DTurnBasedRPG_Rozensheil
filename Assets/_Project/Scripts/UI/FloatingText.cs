using System.Collections;
using UnityEngine;
using TMPro;

public class FloatingTextUI : MonoBehaviour
{
    public TMP_Text text;

    private const float POP_DURATION = 0.12f;
    private const float POP_SCALE = 1.5f;
    private const float RISE_DURATION = 0.85f;
    private const float RISE_DISTANCE = 40f;
    private const float OUTLINE_WIDTH = 0.2f;

    public void Play(string content, Color color)
    {
        text.text = content;
        text.color = color;
        text.outlineColor = Color.black;
        text.outlineWidth = OUTLINE_WIDTH;

        transform.localScale = Vector3.one * POP_SCALE;

        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        RectTransform rect = GetComponent<RectTransform>();

        // Punchy scale-in: snap in oversized, settle to normal size.
        float popElapsed = 0f;
        while (popElapsed < POP_DURATION)
        {
            popElapsed += Time.deltaTime;
            float t = popElapsed / POP_DURATION;
            transform.localScale = Vector3.one * Mathf.Lerp(POP_SCALE, 1f, t);
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Rise and fade.
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, RISE_DISTANCE);

        float elapsed = 0f;
        Color startColor = text.color;

        while (elapsed < RISE_DURATION)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / RISE_DURATION;

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            text.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}