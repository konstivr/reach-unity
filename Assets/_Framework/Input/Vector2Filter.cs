using UnityEngine;

namespace Reach.Framework.InputSys
{
    /// <summary>
    /// Filters a noisy Vector2 input (e.g. analog stick) into a clean signal.
    ///
    /// Solves three real-world stick problems:
    ///   1) Stick drift: tiny non-zero values when the stick is centered.
    ///      Auto-calibrates a per-axis bias offset over time.
    ///   2) Deadzone: ignores tiny inputs entirely, scales the rest to [0..1].
    ///   3) Forward-snap: when the stick is almost-but-not-quite forward,
    ///      kill tiny x bias so the character walks straight.
    ///
    /// Configure once per use-site (move vs look may want different settings).
    /// </summary>
    [System.Serializable]
    public class Vector2Filter
    {
        [Header("Deadzone")]
        [Tooltip("Magnitudes below this are treated as zero. Above, the input is rescaled to [0..1].")]
        [Range(0f, 0.5f)] public float deadzone = 0.22f;

        [Header("Drift Calibration")]
        [Tooltip("If true: continuously learns the stick's center bias while it's resting near zero.")]
        public bool autoCalibrate = true;

        [Tooltip("Stick is considered 'resting' when raw magnitude is below this.")]
        [Range(0f, 0.2f)] public float calibrateWhenBelow = 0.08f;

        [Tooltip("How fast the bias is learned. Higher = faster, lower = more stable.")]
        [Range(0.01f, 20f)] public float calibrateSpeed = 6f;

        [Header("Forward Snap (optional)")]
        [Tooltip("When the input is within this angle (deg) of straight forward/back, " +
                 "kill the x-component to prevent tiny sideways drift while walking forward. " +
                 "Set to 0 to disable.")]
        [Range(0f, 20f)] public float forwardSnapAngleDeg = 8f;

        Vector2 _bias;

        /// <summary>
        /// Run the raw input through the filter. Call once per frame with the latest raw stick value.
        /// </summary>
        public Vector2 Process(Vector2 raw, float deltaTime)
        {
            // 1) Auto-learn bias when stick rests near zero
            if (autoCalibrate && raw.magnitude < calibrateWhenBelow)
            {
                float t = 1f - Mathf.Exp(-calibrateSpeed * deltaTime);
                _bias = Vector2.Lerp(_bias, raw, t);
            }

            // 2) Subtract bias
            Vector2 v = raw - _bias;

            // 3) Deadzone + rescale
            float mag = v.magnitude;
            if (mag < deadzone) return Vector2.zero;

            float scaled = Mathf.InverseLerp(deadzone, 1f, mag);
            v = v.normalized * scaled;

            // 4) Forward snap
            if (forwardSnapAngleDeg > 0f)
            {
                float angle = Mathf.Abs(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg);
                if (angle < forwardSnapAngleDeg) v.x = 0f;
            }

            return v;
        }

        /// <summary>Reset the learned bias to zero. Useful when switching characters.</summary>
        public void ResetBias() => _bias = Vector2.zero;
    }
}