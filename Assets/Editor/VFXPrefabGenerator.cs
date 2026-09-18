// VFXPrefabGenerator.cs
//
// EDITOR-ONLY script. Place this file inside a folder named "Editor" anywhere under Assets,
// e.g. Assets/_Project/Editor/VFXPrefabGenerator.cs (create the "Editor" folder if it doesn't
// exist yet). Unity excludes anything inside an "Editor" folder from player builds and gives
// it access to the UnityEditor namespace, which this script needs.
//
// USAGE:
//   Unity menu bar -> Tools -> Rozensheil -> Generate Ability VFX Prefabs
//
// This creates 5 prefabs under Assets/_Project/Prefabs/VFX/:
//   FX_BuffCircle   -> assign to an ability's Impact Effect Prefab (self-buff / shield abilities)
//   FX_AOECircle    -> assign to an ability's Impact Effect Prefab (AOE damage abilities)
//   FX_HealPlus     -> assign to an ability's Impact Effect Prefab (heal abilities)
//   FX_Bolt         -> assign to an ability's Projectile Prefab (ranged abilities)
//   FX_Slash        -> assign to an ability's Impact Effect Prefab (melee abilities - leave
//                       that ability's Projectile Prefab empty, per AbilityData's own tooltip)
//
// These are functional starting points, not final art - open each in the Particle System
// Inspector afterward and tune Start Size / Start Color / Duration to match your card scale
// and art style. In particular:
//   - Start Size values below assume roughly "1 unit ~= 1 card width" in your particle camera's
//     view - if effects look too small/huge relative to your cards, that's the first knob to turn.
//   - If a glow effect (Bolt, AOE) looks too flat/dim, open its material and switch Surface
//     Inputs' Blend Mode to Additive - the exact property name/location can shift slightly
//     between URP versions, so it's easiest to just do this by eye in the Inspector.
//   - FX_BuffCircle, FX_AOECircle, and FX_Bolt have an ElementTintedParticle component so they
//     auto-color per the ability's element (see that script's color table). FX_HealPlus and
//     FX_Slash deliberately don't, so heals stay green and melee stays neutral - add the
//     component to either if you want them elemental too.
//
// IMPORTANT CAVEAT: CombatController.ExecuteAbilityRoutine only plays an Impact Effect when
// ability.power > 0. A pure shield/buff ability with power = 0 will not trigger FX_BuffCircle
// as-is - either give that ability a token power value, or that routine needs a small code
// change to also fire impact effects for status-only casts. That's a code change, not
// something this generator can fix by itself.

using System.IO;
using UnityEditor;
using UnityEngine;

public static class VFXPrefabGenerator
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";
    private const string TextureFolder = "Assets/_Project/Prefabs/VFX/Textures";

    [MenuItem("Tools/Rozensheil/Generate Ability VFX Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/_Project");
        EnsureFolder("Assets/_Project/Prefabs");
        EnsureFolder(PrefabFolder);
        EnsureFolder(TextureFolder);

        Texture2D circleTex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
        Texture2D plusTex = GetOrCreateTexture("PlusTex", DrawPlus);
        Texture2D slashTex = GetOrCreateTexture("SlashTex", DrawSlash);

        CreateBuffCircle(circleTex);
        CreateAOECircle(circleTex);
        CreateHealPlus(plusTex);
        CreateBolt(circleTex);
        CreateSlash(slashTex);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[VFXPrefabGenerator] Done. 5 prefabs saved to " + PrefabFolder + ":\n" +
            "  FX_BuffCircle, FX_AOECircle, FX_HealPlus -> Impact Effect Prefab\n" +
            "  FX_Bolt -> Projectile Prefab\n" +
            "  FX_Slash -> Impact Effect Prefab (leave Projectile Prefab empty for melee)\n" +
            "Open each prefab and tune Start Size/Color/Duration to taste before assigning them " +
            "to your AbilityData assets.");
    }

    // ---------------------------------------------------------------------
    // Individual prefab builders
    // ---------------------------------------------------------------------

    private static void CreateBuffCircle(Texture2D circleTex)
    {
        GameObject go = CreateParticleSystemObject("FX_BuffCircle", circleTex, Color.white);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.6f;
        main.startLifetime = 0.6f;
        main.startSpeed = 0f;
        main.startSize = 1.5f; // ~one card width - tune to match your card scale

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.75f;
        shape.radiusThickness = 1f; // fill the disc rather than spawning only on the rim

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        go.AddComponent<ElementTintedParticle>();

        SaveAsPrefab(go, "FX_BuffCircle");
    }

    private static void CreateAOECircle(Texture2D circleTex)
    {
        GameObject go = CreateParticleSystemObject("FX_AOECircle", circleTex, new Color(1f, 0.6f, 0.2f));
        ParticleSystem ps = go.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.7f;
        main.startLifetime = 0.7f;
        main.startSpeed = 1.5f;
        main.startSize = 3f; // deliberately bigger than a single card

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.2f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        go.AddComponent<ElementTintedParticle>();

        SaveAsPrefab(go, "FX_AOECircle");
    }

    private static void CreateHealPlus(Texture2D plusTex)
    {
        GameObject go = CreateParticleSystemObject("FX_HealPlus", plusTex, new Color(0.3f, 1f, 0.4f));
        ParticleSystem ps = go.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.8f;
        main.startLifetime = 0.8f;
        main.startSpeed = 0.3f;
        main.startSize = 0.25f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI); // random 0-360deg

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        ParticleSystem.VelocityOverLifetimeModule vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.y = new ParticleSystem.MinMaxCurve(0.5f); // gentle upward drift

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        Color healColor = new Color(0.3f, 1f, 0.4f);
        grad.SetKeys(
            new[] { new GradientColorKey(healColor, 0f), new GradientColorKey(healColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // Deliberately no ElementTintedParticle - heals should stay green regardless of element.

        SaveAsPrefab(go, "FX_HealPlus");
    }

    private static void CreateBolt(Texture2D circleTex)
    {
        // Root MUST have a RectTransform: CombatUIManager.HandleRequestProjectile casts
        // fx.transform to RectTransform and bails out (with a warning) if that fails.
        GameObject root = new GameObject("FX_Bolt", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(50f, 50f);

        GameObject psGO = new GameObject("Glow", typeof(ParticleSystem));
        psGO.transform.SetParent(root.transform, false);

        ParticleSystem ps = psGO.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 0.2f;
        main.startSpeed = 0f;
        main.startSize = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        ParticleSystem.TrailModule trails = ps.trails;
        trails.enabled = true;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.15f);
        AnimationCurve widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, widthCurve);

        ParticleSystemRenderer renderer = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Material mat = CreateParticleMaterial("FX_Bolt_Mat", circleTex, Color.white);
        renderer.material = mat;
        renderer.trailMaterial = mat;

        root.AddComponent<ElementTintedParticle>();

        SaveAsPrefab(root, "FX_Bolt");
    }

    private static void CreateSlash(Texture2D slashTex)
    {
        GameObject go = CreateParticleSystemObject("FX_Slash", slashTex, Color.white);
        ParticleSystem ps = go.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.25f;
        main.startLifetime = 0.2f;
        main.startSpeed = 0f;
        main.startSize = 1.5f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = false; // single particle spawns at the anchor's exact position

        ParticleSystem.RotationOverLifetimeModule rol = ps.rotationOverLifetime;
        rol.enabled = true;
        rol.z = new ParticleSystem.MinMaxCurve(-90f * Mathf.Deg2Rad, 90f * Mathf.Deg2Rad);

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(1f, 1.1f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        // Optional: add ElementTintedParticle here if you want elemental weapon skills to
        // tint the slash color instead of always being white.

        SaveAsPrefab(go, "FX_Slash");
    }

    // ---------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------

    private static GameObject CreateParticleSystemObject(string name, Texture2D tex, Color color)
    {
        GameObject go = new GameObject(name, typeof(ParticleSystem));
        ParticleSystem ps = go.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = true;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateParticleMaterial(name + "_Mat", tex, color);

        return go;
    }

    private static Material CreateParticleMaterial(string name, Texture2D tex, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);

        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        string path = $"{TextureFolder}/{name}.mat";
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SaveAsPrefab(GameObject go, string name)
    {
        string path = $"{PrefabFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    // ---------------------------------------------------------------------
    // Procedural texture generation (plus sign, diagonal slash streak)
    // ---------------------------------------------------------------------

    private static Texture2D GetOrCreateTexture(string name, System.Action<Texture2D> drawFunc)
    {
        const int size = 64;
        string path = $"{TextureFolder}/{name}.png";

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] clear = new Color32[size * size];
        for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(255, 255, 255, 0);
        tex.SetPixels32(clear);

        drawFunc(tex);
        tex.Apply();

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void DrawPlus(Texture2D tex)
    {
        int size = tex.width;
        float thickness = size / 5f;
        float center = size / 2f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dx = Mathf.Abs(x - center);
                float dy = Mathf.Abs(y - center);

                bool inHorizontalArm = dy < thickness / 2f;
                bool inVerticalArm = dx < thickness / 2f;

                if (inHorizontalArm || inVerticalArm)
                {
                    float distFromArmCenter = inHorizontalArm ? dy : dx;
                    float alpha = 1f - Mathf.Clamp01(distFromArmCenter / (thickness / 2f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }
    }

    private static void DrawSlash(Texture2D tex)
    {
        int size = tex.width;
        float bandWidth = 6f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // Diagonal band from bottom-left to top-right, tapering to nothing at both ends.
                float diagPos = (x + y) / 2f;
                float distFromDiagLine = Mathf.Abs(x - y);
                float taper = 1f - Mathf.Abs((diagPos - size / 2f) / (size / 2f));
                float alpha = Mathf.Clamp01((bandWidth - distFromDiagLine) / bandWidth) *
                              Mathf.Clamp01(taper * 1.5f);

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
    }
}
