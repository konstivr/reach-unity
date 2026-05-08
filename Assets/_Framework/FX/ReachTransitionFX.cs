using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.FX
{
    /// <summary>
    /// Transition effect for perspective switches.
    ///
    /// Phases:
    ///   1) Build-up: PostFX volume ramps in, optional center-text + icon pulse appear
    ///   2) Black fade-in
    ///   3) Hold full black + (call switch internally)
    ///   4) Smooth blink-open: black fades out → blink → settle
    ///
    /// All visual elements are optional. Leave fields null to skip them.
    /// For DDR-style transitions: replace the heart icon, change text, swap volume profile.
    /// </summary>
    public class ReachTransitionFX : MonoBehaviour, IReachTransition
    {
        [Header("PostFX")]
        [Tooltip("Optional: a Volume that gets weighted in during the build-up.")]
        public Volume transitionVolume;

        [Range(0f, 1f)] public float transitionVolumeMax = 1f;

        [Header("Audio")]
        public AudioSource sfxSource;
        public AudioClip transitionSfx;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;

        [Header("UI Overlay (optional)")]
        public CanvasGroup blackFadeGroup;
        public CanvasGroup centerGroup;

        [Tooltip("Optional icon (e.g. heart). Pulse-animated during build-up if set.")]
        public RectTransform iconTransform;
        public Image iconImage;

        public TMP_Text centerText;

        [Header("Center Text")]
        [Tooltip("Text shown during the build-up. Empty = no text.")]
        public string centerMessage = "";

        public float centerTextScale = 1.25f;

        [Header("Icon Pulse (lub-dub heartbeat)")]
        public bool enableIconPulse = true;
        public float iconBaseScale = 1.0f;
        [Range(0f, 1f)] public float iconPulseStrength = 0.42f;
        public float bpm = 78f;
        [Range(0.05f, 0.2f)] public float peakWidth = 0.08f;
        public float dubDelay = 0.17f;
        [Range(0f, 1f)] public float dubStrength = 0.65f;

        [Header("Timing")]
        public float buildUpSeconds = 1.5f;
        public float blackFadeInSeconds = 0.9f;
        public float holdFullBlackSeconds = 0.35f;
        public float blackFadeOutSeconds = 0.65f;
        public float settleAfterSeconds = 0.25f;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        bool _isTransitioning;
        public bool IsTransitioning => _isTransitioning;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            // Reset any visuals to their resting state
            if (transitionVolume != null) transitionVolume.weight = 0f;
            if (blackFadeGroup != null) blackFadeGroup.alpha = 0f;
            if (centerGroup != null) centerGroup.alpha = 0f;
            if (iconImage != null) iconImage.enabled = false;
            if (iconTransform != null) iconTransform.localScale = Vector3.one * iconBaseScale;
            if (centerText != null) centerText.text = "";
        }

        // ============================================================
        // Public API
        // ============================================================

        public async Task<bool> PlayAndSwitchAsync(PossessableCharacter target)
        {
            if (_isTransitioning) return false;

            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null) return false;

            _isTransitioning = true;
            if (debugLogs) Debug.Log($"[ReachFX] Start transition to '{target?.name}'");

            // Lock HUD with the centerMessage as FX text (so HudText doesn't fight us)
            ctx.Hud?.SetFXOverride(centerMessage ?? "");

            // SFX
            if (sfxSource != null && transitionSfx != null)
                sfxSource.PlayOneShot(transitionSfx, sfxVolume);

            // Show center group + icon
            if (centerGroup != null) centerGroup.alpha = 1f;
            if (iconImage != null) iconImage.enabled = true;
            if (centerText != null)
            {
                centerText.text = centerMessage ?? "";
                centerText.transform.localScale = Vector3.one * centerTextScale;
            }

            // Phase 1+2: build-up + black fade-in
            await RunBuildUp();
            await RunBlackFadeIn();

            // Phase 3: hold black, switch internally
            await Wait(holdFullBlackSeconds);
            bool switched = ctx.Perspective.TrySwitchTo(target);

            // Phase 4: hide center group, fade out black
            if (centerGroup != null) centerGroup.alpha = 0f;
            if (iconImage != null) iconImage.enabled = false;

            await RunBlackFadeOut();
            await Wait(settleAfterSeconds);

            // Reset PostFX
            if (transitionVolume != null) transitionVolume.weight = 0f;

            // Release HUD
            ctx.Hud?.ClearFXOverride();

            _isTransitioning = false;
            if (debugLogs) Debug.Log($"[ReachFX] End transition (switch={switched})");
            return switched;
        }

        // ============================================================
        // Phases
        // ============================================================

        async Task RunBuildUp()
        {
            float t = 0f;
            while (t < buildUpSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / buildUpSeconds);
                float eased = EaseInOutCubic(k);

                if (transitionVolume != null)
                    transitionVolume.weight = Mathf.Lerp(0f, transitionVolumeMax, eased);

                PulseIcon();
                await Task.Yield();
            }
        }

        async Task RunBlackFadeIn()
        {
            if (blackFadeGroup == null) { await Wait(blackFadeInSeconds); return; }

            float t = 0f;
            while (t < blackFadeInSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / blackFadeInSeconds);
                blackFadeGroup.alpha = Mathf.Lerp(0f, 1f, EaseInCubic(k));
                PulseIcon();
                await Task.Yield();
            }
            blackFadeGroup.alpha = 1f;
        }

        async Task RunBlackFadeOut()
        {
            if (blackFadeGroup == null) { await Wait(blackFadeOutSeconds); return; }

            float t = 0f;
            while (t < blackFadeOutSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / blackFadeOutSeconds);
                blackFadeGroup.alpha = Mathf.Lerp(1f, 0f, EaseOutCubic(k));
                await Task.Yield();
            }
            blackFadeGroup.alpha = 0f;
        }

        // ============================================================
        // Icon pulse (lub-dub heartbeat)
        // ============================================================

        void PulseIcon()
        {
            if (!enableIconPulse || iconTransform == null) return;

            float beatsPerSecond = Mathf.Max(1f, bpm / 60f);
            float phase = Mathf.Repeat(Time.time * beatsPerSecond, 1f);

            float lub = Peak(phase, 0f, peakWidth);
            float dub = Peak(phase, dubDelay, peakWidth) * dubStrength;
            float pulse = Mathf.Clamp01(lub + dub);

            float scale = iconBaseScale * (1f + pulse * iconPulseStrength);
            iconTransform.localScale = Vector3.one * scale;
        }

        // ============================================================
        // Helpers
        // ============================================================

        static float Peak(float x, float center, float width)
        {
            float d = Mathf.Abs(x - center);
            d = Mathf.Min(d, 1f - d); // wrap
            float w = Mathf.Max(0.0001f, width);
            return Mathf.Exp(-(d * d) / (2f * w * w));
        }

        static float EaseInCubic(float x) => x * x * x;
        static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        static float EaseInOutCubic(float x) =>
            x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        static async Task Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                await Task.Yield();
            }
        }
    }
}