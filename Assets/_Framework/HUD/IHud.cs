namespace Reach.Framework.HUD
{
    /// <summary>
    /// Interface for the HUD text system.
    /// One text element, multiple modes with explicit lock semantics so that
    /// different systems (router, gate, speech, dialogue) can write without
    /// fighting each other.
    ///
    /// Lock priority (high to low): FXOverride > Sticky > TimedLock > Intro > IdleAuto / Prompt
    ///
    /// Convention: only set text if you own the highest-priority mode currently active,
    /// or check IsLockedByFX/IsSticky/IsTimedLocked/IsIntroRunning before writing in IdleAuto/Prompt mode.
    /// </summary>
    public interface IHud
    {
        // ============================================================
        // State queries (read before writing!)
        // ============================================================

        bool IsLockedByFX { get; }
        bool IsSticky { get; }
        bool IsTimedLocked { get; }
        bool IsIntroRunning { get; }

        /// <summary>True if the HUD is "free" (not locked). Useful as a single check.</summary>
        bool IsFree { get; }

        // ============================================================
        // Write modes
        // ============================================================

        /// <summary>Resolve and set the default idle text for the current state.</summary>
        void SetIdleAuto();

        /// <summary>Set the perspective-idle text (used after the player has switched once).</summary>
        void SetIdlePerspective();

        /// <summary>Set a transient prompt (e.g. "Press Interact"). Cleared by SetIdleAuto.</summary>
        void SetPrompt(string text);

        /// <summary>
        /// Sticky text persists until ClearSticky() is called.
        /// Used by Gate (gate line, "no match"), SpeechInput (recording state), etc.
        /// </summary>
        void SetSticky(string text);
        void ClearSticky();

        /// <summary>
        /// FX-Override is the highest-priority mode. Used by transition FX
        /// and by NPC speech (HUD held while audio plays).
        /// Other systems must not overwrite while this is active.
        /// </summary>
        void SetFXOverride(string text);
        void ClearFXOverride();

        /// <summary>
        /// Show text for N seconds, then auto-return to idle.
        /// Does not override Sticky or FXOverride.
        /// </summary>
        void SetTimed(string text, float seconds);

        /// <summary>Hard reset: clear all locks, return to idle.</summary>
        void ForceResetToIdle();

        /// <summary>Clear text completely (no idle restore).</summary>
        void ClearText();
    }
}