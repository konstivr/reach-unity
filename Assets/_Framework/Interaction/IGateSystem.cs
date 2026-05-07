using System.Threading.Tasks;
using Reach.Framework.Core;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// The Outreach gate: when player gets close to an unvisited character and presses Interact,
    /// the target speaks a "gate line" (TTS), then waits for the player to speak the passphrase.
    /// On match, the player switches into that character's perspective.
    /// </summary>
    public interface IGateSystem
    {
        /// <summary>The character currently being approached (in range, unvisited). Null if none.</summary>
        PossessableCharacter NearestTarget { get; }

        /// <summary>True if a gate target is in range right now.</summary>
        bool HasTargetInRange { get; }

        /// <summary>True while gate-line TTS is playing or we're waiting for the passphrase.</summary>
        bool IsGateBusy { get; }

        /// <summary>True specifically while waiting for the player to speak the passphrase.</summary>
        bool IsWaitingForPassphrase { get; }

        /// <summary>True if chat/non-gate speech should be blocked right now.</summary>
        bool ShouldBlockChat { get; }

        /// <summary>
        /// Try to start the gate process. Called by the InteractionRouter when player presses Interact
        /// and a target is in range. Returns true if the press was consumed.
        /// </summary>
        bool TryTriggerGate();

        /// <summary>
        /// Check the player's spoken WAV against the expected passphrase.
        /// Called by SpeechInput when waiting for passphrase and a recording finished.
        /// Returns true if the gate consumed the press (whether match or not).
        /// </summary>
        Task<bool> TryHandlePassphraseAsync(string wavPath);

        /// <summary>Cancel any active gate (called on character switch, Cancel button, etc.).</summary>
        void CancelGate();

        /// <summary>
        /// Suspend the passphrase timeout (used while the player is recording — they shouldn't get timed out mid-speech).
        /// </summary>
        void SetTimeoutSuspended(bool suspended);
    }
}