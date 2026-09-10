using System;
using System.Collections;
using UnityEngine;

// Bridges world-space Particle Systems into a Screen Space - Overlay Canvas. A plain
// ParticleSystem parented directly under an Overlay canvas doesn't composite reliably
// (Overlay has no camera to render it against), so instead this renders particles on a
// dedicated camera/layer into a Render Texture, displayed via a full-screen RawImage
// inside the canvas. Particles are positioned by converting a UI element's screen
// position into that camera's viewport space.
public class ParticleUIBridge : MonoBehaviour
{
    public static ParticleUIBridge Instance { get; private set; }

    [Tooltip("Renders only the VFX layer into the bridge's Render Texture.")]
    public Camera particleCamera;
    [Tooltip("How far in front of the particle camera spawned effects are placed. Only visually matters if the camera is Perspective - Orthographic ignores it.")]
    public float spawnDepth = 10f;
    [Tooltip("Layer spawned impact effects are moved to - must match Particle Camera's Culling Mask.")]
    public string vfxLayerName = "VFX";

    private int vfxLayer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        vfxLayer = LayerMask.NameToLayer(vfxLayerName);
    }

    // Spawns effectPrefab at screenAnchor's screen position, waits duration, then destroys
    // it and invokes onComplete - same contract CharacterCardUI's old direct-instantiate had.
    public void PlayImpactEffect(GameObject effectPrefab, Transform screenAnchor, float duration, Action onComplete)
    {
        if (effectPrefab == null || particleCamera == null || screenAnchor == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, screenAnchor.position);
        Vector3 viewportPos = new Vector3(screenPos.x / Screen.width, screenPos.y / Screen.height, spawnDepth);
        Vector3 worldPos = particleCamera.ViewportToWorldPoint(viewportPos);

        GameObject fx = Instantiate(effectPrefab, worldPos, Quaternion.identity);
        SetLayerRecursively(fx, vfxLayer);

        StartCoroutine(DespawnRoutine(fx, duration, onComplete));
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private IEnumerator DespawnRoutine(GameObject fx, float duration, Action onComplete)
    {
        yield return new WaitForSeconds(duration);

        if (fx != null)
            Destroy(fx);

        onComplete?.Invoke();
    }
}