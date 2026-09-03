using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float scaleMultiplier = 1.2f;
    public float speed = 10f;

    private Vector3 originalScale;
    private Vector3 targetScale;

    void Start()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
    }

    void OnEnable()
    {
        // Reset immediately when panel reappears
        if (originalScale != Vector3.zero)
        {
            targetScale = originalScale;
            transform.localScale = originalScale;
        }
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * scaleMultiplier;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }
}