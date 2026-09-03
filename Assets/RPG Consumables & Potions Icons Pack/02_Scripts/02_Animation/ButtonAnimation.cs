using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class ButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float duration = 0.05f;
    [SerializeField] private float scaleMultiplier = 0.85f;
    private Vector3 originalScale;

    void Awake() => originalScale = transform.localScale;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        ScaleTo(originalScale * scaleMultiplier);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        ScaleTo(originalScale);
    }

    public void PlayPressAnimation()
    {
        StopAllCoroutines();
        StartCoroutine(PressAndRelease());
    }

    IEnumerator PressAndRelease()
    {
        yield return Animate(originalScale * scaleMultiplier);
        yield return new WaitForSeconds(duration);
        yield return Animate(originalScale);
    }

    void ScaleTo(Vector3 target)
    {
        StopAllCoroutines();
        StartCoroutine(Animate(target));
    }

    IEnumerator Animate(Vector3 target)
    {
        Vector3 start = transform.localScale;
        for (float t = 0; t < 1; t += Time.deltaTime / duration)
        {
            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        transform.localScale = target;
    }
}