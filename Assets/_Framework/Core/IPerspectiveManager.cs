using System;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Manages the currently controlled character and tracks progression
    /// (which characters have been visited).
    /// </summary>
    public interface IPerspectiveManager
    {
        /// <summary>The character currently controlled by the player. Null before initialization.</summary>
        PossessableCharacter Current { get; }

        /// <summary>Number of unique characters the player has been into (including starting one).</summary>
        int VisitedCount { get; }

        /// <summary>Maximum number of perspectives in the active pack.</summary>
        int MaxPerspectives { get; }

        /// <summary>Progress 0..1 across all perspectives.</summary>
        float Progress01 { get; }

        /// <summary>True if all perspectives have been visited.</summary>
        bool IsComplete { get; }

        /// <summary>Whether the player has already been into this character.</summary>
        bool HasVisited(PossessableCharacter character);

        /// <summary>
        /// Try to switch the player into the given character.
        /// Returns true on success, false if blocked (e.g. already visited, invalid).
        /// </summary>
        bool TrySwitchTo(PossessableCharacter target);

        /// <summary>Fired AFTER a successful switch. (from, to) — 'from' may be null on first activation.</summary>
        event Action<PossessableCharacter, PossessableCharacter> Switched;

        /// <summary>Fired AFTER VisitedCount changes. (visited, max, progress01).</summary>
        event Action<int, int, float> ProgressChanged;
    }
}