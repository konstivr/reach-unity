using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Default implementation of IPerspectiveManager.
    /// Place one in the scene; it self-registers with GameContext.
    /// </summary>
    public class PerspectiveManager : MonoBehaviour, IPerspectiveManager
    {
        [Header("Starting State")]
        [Tooltip("Optional: which character to start as. If null, the first character in the StoryPack is used.")]
        public PossessableCharacter startCharacter;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        readonly HashSet<PossessableCharacter> _visited = new HashSet<PossessableCharacter>();

        public PossessableCharacter Current { get; private set; }
        public int VisitedCount => _visited.Count;
        public int MaxPerspectives => GameContext.Instance?.pack?.EffectiveMaxPerspectives ?? 0;
        public bool IsComplete => MaxPerspectives > 0 && VisitedCount >= MaxPerspectives;

        public float Progress01
        {
            get
            {
                int max = MaxPerspectives;
                if (max <= 1) return 1f;
                return Mathf.Clamp01((VisitedCount - 1f) / (max - 1f));
            }
        }

        public event Action<PossessableCharacter, PossessableCharacter> Switched;
        public event Action<int, int, float> ProgressChanged;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                Debug.LogError("[PerspectiveManager] No GameContext.Instance found. Make sure GameContext awakes before this.");
                enabled = false;
                return;
            }

            ctx.Perspective = this;
        }

        void Start()
        {
            // Initial activation: deactivate everyone, activate the starting character.
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            foreach (var c in ctx.Characters.All)
            {
                if (c == null || !c.IsValid) continue;
                c.SetControlled(false);
            }

            var start = ResolveStartCharacter();
            if (start == null)
            {
                Debug.LogWarning("[PerspectiveManager] No starting character could be resolved.");
                return;
            }

            ActivateInitial(start);
        }

        PossessableCharacter ResolveStartCharacter()
        {
            // 1) Inspector override
            if (startCharacter != null && startCharacter.IsValid)
                return startCharacter;

            // 2) From the StoryPack
            var ctx = GameContext.Instance;
            var pack = ctx?.pack;
            if (pack != null && pack.StartCharacter != null)
            {
                var match = ctx.Characters.FindByDefinition(pack.StartCharacter);
                if (match != null) return match;
            }

            // 3) First registered character (fallback)
            if (ctx != null && ctx.Characters.Count > 0)
                return ctx.Characters.All[0];

            return null;
        }

        // ============================================================
        // Public API
        // ============================================================

        public bool HasVisited(PossessableCharacter character) =>
            character != null && _visited.Contains(character);

        public bool TrySwitchTo(PossessableCharacter target)
        {
            if (target == null || !target.IsValid)
            {
                if (debugLogs) Debug.Log("[PerspectiveManager] Switch refused: target invalid.");
                return false;
            }

            if (Current == null)
            {
                if (debugLogs) Debug.Log("[PerspectiveManager] Switch refused: no current character.");
                return false;
            }

            if (target == Current)
            {
                if (debugLogs) Debug.Log("[PerspectiveManager] Switch refused: already controlling target.");
                return false;
            }

            if (_visited.Contains(target))
            {
                if (debugLogs) Debug.Log($"[PerspectiveManager] Switch refused: '{target.name}' already visited.");
                return false;
            }

            int max = MaxPerspectives;
            if (max > 0 && VisitedCount >= max)
            {
                if (debugLogs) Debug.Log("[PerspectiveManager] Switch refused: all perspectives visited.");
                return false;
            }

            return DoSwitch(Current, target);
        }

        // ============================================================
        // Internals
        // ============================================================

        void ActivateInitial(PossessableCharacter target)
        {
            target.SetControlled(true);
            Current = target;

            _visited.Add(target);

            if (debugLogs)
                Debug.Log($"[PerspectiveManager] Initial activation: '{target.name}' (visited={VisitedCount}/{MaxPerspectives})");

            Switched?.Invoke(null, target);
            FireProgressChanged();
        }

        bool DoSwitch(PossessableCharacter from, PossessableCharacter to)
        {
            from.SetControlled(false);
            to.SetControlled(true);

            Current = to;
            _visited.Add(to);

            if (debugLogs)
                Debug.Log($"[PerspectiveManager] Switched '{from.name}' -> '{to.name}' (visited={VisitedCount}/{MaxPerspectives})");

            Switched?.Invoke(from, to);
            FireProgressChanged();
            return true;
        }

        void FireProgressChanged()
        {
            ProgressChanged?.Invoke(VisitedCount, MaxPerspectives, Progress01);
        }
    }
}