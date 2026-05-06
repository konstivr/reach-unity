using System.Threading.Tasks;
using UnityEngine;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Test STT backend. Always returns a fixed string.
    /// Useful for testing the pipeline without setting up Whisper.
    ///
    /// In the inspector you can override the returned text per scene
    /// (e.g. set it to the gate passphrase to auto-pass the gate).
    /// </summary>
    public class StubSpeechToText : MonoBehaviour, ISpeechToText
    {
        [Header("Stub")]
        [Tooltip("Text returned for every transcription request.")]
        [TextArea(1, 4)]
        public string fixedTranscript = "say something";

        [Tooltip("Simulated delay before returning (seconds).")]
        public float simulatedDelaySeconds = 0.3f;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady => true;

        public async Task<string> TranscribeAsync(string wavPath, string language)
        {
            if (debugLogs) Debug.Log($"[StubSTT] TranscribeAsync wav='{wavPath}' lang='{language}'");

            if (simulatedDelaySeconds > 0f)
                await Task.Delay(Mathf.RoundToInt(simulatedDelaySeconds * 1000f));

            if (debugLogs) Debug.Log($"[StubSTT] -> '{fixedTranscript}'");
            return fixedTranscript ?? "";
        }
    }
}