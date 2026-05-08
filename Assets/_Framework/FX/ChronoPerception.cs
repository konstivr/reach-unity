using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Reach.Framework.Core;

namespace Reach.Framework.FX
{
    /// <summary>
    /// Progressive PostFX reduction tied to perspective progress.
    /// At progress=0 (start) all filters are at their authored max.
    /// At progress=1 (all perspectives visited) filters fade toward neutral.
    ///
    /// Optional: bonus reduction when the controlled player is near other characters
    /// ("clarity by proximity" feel from the original Reach concept).
    /// </summary>
    public class ChronoPerception : MonoBehaviour
    {
        [Header("Volume")]
        [Tooltip("URP Volume containing the filters that should fade with progress.")]
        public Volume volume;

        [Header("Smoothing")]
        [Tooltip("How quickly the chrono factor reacts to progress changes.")]
        public float smoothSpeed = 4f;

        [Header("Proximity Bonus (optional)")]
        public bool enableProximityBonus = true;

        [Tooltip("Maximum extra reduction added when standing right next to another character.")]
        [Range(0f, 1f)] public float proximityMaxReduction = 0.35f;

        [Tooltip("Distance within which the proximity bonus kicks in.")]
        public float proximityRadius = 2.5f;

        public AnimationCurve proximityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Debug")]
        public bool debugLogs = false;

        // ============================================================
        // State
        // ============================================================

        ColorAdjustments _color;
        Vignette _vignette;
        ChromaticAberration _chromatic;
        DepthOfField _dof;
        LensDistortion _lens;

        float _satStart;
        float _vignetteStart;
        float _chromaticStart;
        float _lensStart;
        float _dofApertureStart;
        float _dofFocalStart;

        float _chronoFactor = 1f;
        float _chronoTarget = 1f;

        bool _ready;

        void Awake()
        {
            if (volume == null) volume = GetComponent<Volume>();
            if (volume == null || volume.profile == null)
            {
                Debug.LogWarning("[ChronoPerception] No Volume / profile. Disabling.");
                enabled = false;
                return;
            }

            var p = volume.profile;
            p.TryGet(out _color);
            p.TryGet(out _vignette);
            p.TryGet(out _chromatic);
            p.TryGet(out _dof);
            p.TryGet(out _lens);

            if (_color != null) _satStart = _color.saturation.value;
            if (_vignette != null) _vignetteStart = _vignette.intensity.value;
            if (_chromatic != null) _chromaticStart = _chromatic.intensity.value;
            if (_lens != null) _lensStart = _lens.intensity.value;
            if (_dof != null)
            {
                _dofApertureStart = _dof.aperture.value;
                _dofFocalStart = _dof.focalLength.value;
            }

            _ready = true;
        }

        void OnEnable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.ProgressChanged += OnProgressChanged;
        }

        void OnDisable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.ProgressChanged -= OnProgressChanged;
        }

        void OnProgressChanged(int visited, int max, float progress01)
        {
            // progress01: 0 at start, 1 at end → invert: 1 = full filter, 0 = clean
            _chronoTarget = 1f - progress01;

            if (debugLogs)
                Debug.Log($"[Chrono] Progress {visited}/{max} → target factor {_chronoTarget:0.00}");
        }

        void Update()
        {
            if (!_ready) return;

            _chronoFactor = Mathf.Lerp(
                _chronoFactor, _chronoTarget,
                1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );

            float proximityReduction = ComputeProximityReduction();
            float effective = Mathf.Clamp01(_chronoFactor - proximityReduction);

            Apply(effective);
        }

        float ComputeProximityReduction()
        {
            if (!enableProximityBonus) return 0f;

            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null || ctx.Perspective.Current == null) return 0f;

            var current = ctx.Perspective.Current;
            float bestDist = float.MaxValue;

            foreach (var c in ctx.Characters.All)
            {
                if (c == null || c == current || !c.IsValid) continue;
                float d = Vector3.Distance(current.transform.position, c.transform.position);
                if (d < bestDist) bestDist = d;
            }

            if (bestDist > proximityRadius) return 0f;

            float t = 1f - Mathf.Clamp01(bestDist / proximityRadius);
            return proximityCurve.Evaluate(t) * proximityMaxReduction;
        }

        void Apply(float f)
        {
            // f=1 → full filter (max), f=0 → neutral
            if (_color != null)
                _color.saturation.value = Mathf.Lerp(0f, _satStart, f);

            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(0f, _vignetteStart, f);

            if (_chromatic != null)
                _chromatic.intensity.value = Mathf.Lerp(0f, _chromaticStart, f);

            if (_lens != null)
                _lens.intensity.value = Mathf.Lerp(0f, _lensStart, f);

            if (_dof != null)
            {
                float neutralAperture = 16f;
                float neutralFocal = 50f;
                _dof.aperture.value = Mathf.Lerp(neutralAperture, _dofApertureStart, f);
                _dof.focalLength.value = Mathf.Lerp(neutralFocal, _dofFocalStart, f);
            }
        }
    }
}