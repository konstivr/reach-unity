using Reach.Framework.Core;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// Anything that can be interacted with via Interact button when in range.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>True if the given player character can interact with this object right now.</summary>
        bool CanInteract(PossessableCharacter currentPlayer);

        /// <summary>True if the player character is within interact range.</summary>
        bool IsInRange(PossessableCharacter currentPlayer);

        /// <summary>Text shown in the HUD when in range. May change based on internal state.</summary>
        string GetPrompt();

        /// <summary>True if this object has been completed (e.g. one-shot consumed).</summary>
        bool IsCompleted { get; }

        /// <summary>True while an action is currently running on this object.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Called by the Router when player presses Interact while in range.
        /// Returns true if the press was consumed.
        /// </summary>
        bool TryInteract(PossessableCharacter currentPlayer);
    }
}