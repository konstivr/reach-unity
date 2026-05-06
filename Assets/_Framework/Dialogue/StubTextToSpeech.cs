using System.Threading.Tasks;
using UnityEngine;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Test TTS backend. Returns a synthesized beep AudioClip whose duration
    /// is roughly proportional to text length.
    /// Useful for testing the pipeline without setting up real TTS.
    /// </summary>
    public class StubTextToSpeech : MonoBehaviour, ITextToSpeech
    {
        [Header("Beep")]
        [Tooltip("Frequency of the beep in Hz (440 = A4).")]
        public float frequency = 440f;

        [Tooltip("Seconds per character (controls clip length per text length).")]
        public float secondsPerChar = 0.05f;

        [Tooltip("Minimum clip length.")]
        public float minSeconds = 0.4f;

        [Tooltip("Maximum clip length.")]
        public float maxSeconds = 4.0f;

        [Range(0f, 1f)]
        public float amplitude = 0.2f;

        public int sampleRate = 22050;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady => true;

        public Task<AudioClip> SynthesizeAsync(string text, string voiceName)
        {
            if (debugLogs) Debug.Log($"[StubTTS] SynthesizeAsync voice='{voiceName}' textLen={text?.Length ?? 0}");

            text = text ?? "";
            float dur = Mathf.Clamp(text.Length * secondsPerChar, minSeconds, maxSeconds);
            int samples = Mathf.CeilToInt(sampleRate * dur);

            var clip = AudioClip.Create("StubTTS", samples, 1, sampleRate, false);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * amplitude;

            clip.SetData(data, 0);

            // Task.FromResult — no actual async work, but we honor the signature
            return Task.FromResult(clip);
        }
    }
}