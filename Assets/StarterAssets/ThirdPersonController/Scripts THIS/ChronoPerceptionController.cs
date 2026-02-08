using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ChronoPerceptionController : MonoBehaviour
{
    [Header("References")]
    public PerspectiveSwapManager swapManager;
    public Volume volume;

    [Header("Chronological Progress")]
    [Tooltip("Wie schnell reagiert der Filter auf Perspektivwechsel")]
    public float chronoSmoothSpeed = 4f;

    [Header("Proximity Effect")]
    [Tooltip("Maximaler zusätzlicher Abbau durch Nähe (0–1)")]
    public float proximityMaxReduction = 0.35f;

    [Tooltip("Radius für Nähe-Effekt")]
    public float proximityRadius = 2.5f;

    [Tooltip("Wie weich der Nähe-Effekt einsetzt")]
    public AnimationCurve proximityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool debugLogs = false;

    // ===== Overrides =====
    ColorAdjustments color;
    Vignette vignette;
    ChromaticAberration chromatic;
    DepthOfField dof;
    LensDistortion lens;

    // ===== Startwerte =====
    float satStart;
    float vignetteStart;
    float chromaticStart;
    float lensStart;
    float dofApertureStart;
    float dofFocalStart;

    float chronoFactor = 1f;   // 1 = max Filter, 0 = clean
    float chronoTarget = 1f;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!volume) volume = GetComponent<Volume>();
        if (!volume) volume = FindObjectOfType<Volume>();

        if (!volume || volume.profile == null)
        {
            Debug.LogError("[ChronoPerception] Volume oder Profile fehlt.");
            enabled = false;
            return;
        }

        var p = volume.profile;

        p.TryGet(out color);
        p.TryGet(out vignette);
        p.TryGet(out chromatic);
        p.TryGet(out dof);
        p.TryGet(out lens);

        if (color != null) satStart = color.saturation.value;
        if (vignette != null) vignetteStart = vignette.intensity.value;
        if (chromatic != null) chromaticStart = chromatic.intensity.value;
        if (lens != null) lensStart = lens.intensity.value;

        if (dof != null)
        {
            dofApertureStart = dof.aperture.value;
            dofFocalStart = dof.focalLength.value;
        }
    }

    void OnEnable()
    {
        if (swapManager != null)
            swapManager.ProgressChanged += OnProgressChanged;
    }

    void OnDisable()
    {
        if (swapManager != null)
            swapManager.ProgressChanged -= OnProgressChanged;
    }

    void OnProgressChanged(int entered, int max, float progress01)
    {
        // progress01: 0 = Start, 1 = Ende
        chronoTarget = 1f - progress01;

        if (debugLogs)
            Debug.Log($"[Chrono] Progress {entered}/{max} → factor {chronoTarget:0.00}");
    }

    void Update()
    {
        chronoFactor = Mathf.Lerp(
            chronoFactor,
            chronoTarget,
            1f - Mathf.Exp(-chronoSmoothSpeed * Time.deltaTime)
        );

        float proximityFactor = ComputeProximityReduction();
        float finalFactor = Mathf.Clamp01(chronoFactor - proximityFactor);

        Apply(finalFactor);
    }

    float ComputeProximityReduction()
    {
        if (swapManager == null || swapManager.current == null)
            return 0f;

        var current = swapManager.current;
        float bestDist = float.MaxValue;

        foreach (var p in PossessableCharacter.ValidCharacters)
        {
            if (p == null || p == current || !p.IsValid) continue;

            float d = Vector3.Distance(
                current.transform.position,
                p.transform.position
            );

            if (d < bestDist)
                bestDist = d;
        }

        if (bestDist > proximityRadius)
            return 0f;

        float t = 1f - Mathf.Clamp01(bestDist / proximityRadius);
        return proximityCurve.Evaluate(t) * proximityMaxReduction;
    }

    void Apply(float f)
    {
        // Saturation: geht gegen 0 (neutral)
        if (color != null)
            color.saturation.value = Mathf.Lerp(0f, satStart, f);

        if (vignette != null)
            vignette.intensity.value = Mathf.Lerp(0f, vignetteStart, f);

        if (chromatic != null)
            chromatic.intensity.value = Mathf.Lerp(0f, chromaticStart, f);

        if (lens != null)
            lens.intensity.value = Mathf.Lerp(0f, lensStart, f);

        if (dof != null)
        {
            float neutralAperture = 16f;
            float neutralFocal = 50f;

            dof.aperture.value = Mathf.Lerp(neutralAperture, dofApertureStart, f);
            dof.focalLength.value = Mathf.Lerp(neutralFocal, dofFocalStart, f);
        }
    }
}