using System.Threading.Tasks;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Manages chat conversations with the currently controlled character.
    /// Receives a player WAV recording, runs it through STT → Chat → TTS,
    /// plays the response, and holds the HUD until audio finishes.
    /// </summary>
    public interface IDialogueManager
    {
        /// <summary>True while a response is being computed or played.</summary>
        bool IsResponding { get; }

        /// <summary>
        /// Process the player's spoken WAV: transcribe, send to LLM, synthesize reply,
        /// play it, and hold the HUD while it plays.
        /// </summary>
        Task PlayerSpokeAsync(string wavPath);

        /// <summary>Stop any current response (audio + HUD lock). Called on Cancel or character switch.</summary>
        void Interrupt();
    }
}