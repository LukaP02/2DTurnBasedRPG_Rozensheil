using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PatternLoopOptimized : MonoBehaviour
{
    [Header("Pattern Setup")]
    [Tooltip("Assign your square pattern texture here")]
    public Texture2D patternTexture;

    [Tooltip("Number of repeats along X and Y axes")]
    public Vector2 tiling = new Vector2(3, 3);

    [Header("Movement Settings")]
    public float minMoveDuration = 5f;
    public float maxMoveDuration = 10f;
    public float minHoldTime = 0.5f;
    public float maxHoldTime = 1.2f;
    public float maxDistance = 3f;

    private Image img;
    private Material mat;
    private Vector2 offset;
    private Vector2 startOffset;
    private Vector2 targetOffset;
    private float moveTimer = 0f;
    private float moveDuration = 1f;
    private float holdTimer = 0f;
    private bool isHolding = false;
    private System.Func<float, float> easeFunc;

    void Awake()
    {
        img = GetComponent<Image>();
        if (patternTexture == null)
        {
            Debug.LogError("❌ Please assign a square pattern texture!");
            enabled = false;
            return;
        }

        mat = new Material(Shader.Find("UI/Default"));
        mat.mainTexture = patternTexture;
        mat.mainTextureScale = tiling;

        img.material = mat;
        img.type = Image.Type.Tiled;

        PickNextMove();
    }

    void Update()
    {
        if (isHolding)
        {
            holdTimer -= Time.deltaTime;
            if (holdTimer <= 0f)
            {
                PickNextMove();
            }
        }
        else
        {
            moveTimer += Time.deltaTime;
            float t = Mathf.Clamp01(moveTimer / moveDuration);
            offset = Vector2.Lerp(startOffset, targetOffset, easeFunc(t));
            mat.mainTextureOffset = offset;

            if (t >= 1f)
            {
                isHolding = true;
                holdTimer = Random.Range(minHoldTime, maxHoldTime);
            }
        }
    }

    void PickNextMove()
    {
        isHolding = false;
        moveTimer = 0f;

        startOffset = offset;

        // Random direction and distance
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float distance = Random.Range(0.1f, maxDistance);
        targetOffset = startOffset + direction * distance;
        targetOffset.x = Mathf.Repeat(targetOffset.x, 1f);
        targetOffset.y = Mathf.Repeat(targetOffset.y, 1f);

        // Random duration and easing
        moveDuration = Random.Range(minMoveDuration, maxMoveDuration);
        easeFunc = PickRandomEase();
    }

    System.Func<float, float> PickRandomEase()
    {
        int choice = Random.Range(0, 3);
        switch (choice)
        {
            case 0: return Linear;
            case 1: return SmoothStep;
            case 2: return SmootherStep;
            default: return SmoothStep;
        }
    }

    float Linear(float t) => t;
    float SmoothStep(float t) => t * t * (3f - 2f * t);
    float SmootherStep(float t) => t * t * t * (t * (6f * t - 15f) + 10f);
}