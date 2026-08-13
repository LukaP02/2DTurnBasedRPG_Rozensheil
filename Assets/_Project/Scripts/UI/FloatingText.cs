using System.Collections;
using UnityEngine;
using TMPro;

public class FloatingTextUI : MonoBehaviour
{
    public TMP_Text text;

    private float duration = 1f;
    private float riseDistance = 40f;

    public void Play(string content, Color color)
    {
        text.text = content;
        text.color = color;

        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 startPos = rect.anchoredPosition;
        Vector2 endPos = startPos + new Vector2(0f, riseDistance);

        float elapsed = 0f;
        Color startColor = text.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            Color c = startColor;
            c.a = Mathf.Lerp(1f, 0f, t);
            text.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}