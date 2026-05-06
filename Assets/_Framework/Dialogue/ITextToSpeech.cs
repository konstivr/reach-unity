using System.Threading.Tasks;
using UnityEngine;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Text-to-speech backend. Implementations: Stub, MacSay, WindowsSAPI, ...
    /// </summary>
    public interface ITextToSpeech
    {
        bool IsReady { get; }

        /// <summary>
        /// Synthesize text into an AudioClip.
        /// 'voiceName' is platform-specific (e.g. "Samantha" on macOS, "Zira" on Windows).
        /// Returns null on failure.
        /// </summary>
        Task<AudioClip> SynthesizeAsync(string text, string voiceName);
    }
}