using System.Threading.Tasks;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Speech-to-text backend. Implementations: Stub, WhisperSubprocess, ...
    /// </summary>
    public interface ISpeechToText
    {
        /// <summary>True when this backend is ready to transcribe (binaries found, models loaded, etc.).</summary>
        bool IsReady { get; }

        /// <summary>
        /// Transcribe a WAV file (16-bit mono recommended).
        /// Returns the recognized text, or empty string on failure.
        /// </summary>
        Task<string> TranscribeAsync(string wavPath, string language);
    }
}