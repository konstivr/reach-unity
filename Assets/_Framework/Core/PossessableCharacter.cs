using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// A character that can be possessed (controlled) by the player.
    /// One per character GameObject in the scene.
    ///
    /// Responsibilities:
    ///   - Hold a reference to its CharacterDefinition (the SO).
    ///   - Self-register with GameContext.Characters.
    ///   - Enable/disable its movement + ambient when control changes.
    ///   - Validate setup at runtime.
    ///
    /// Movement and NPC-wander logic live in separate components on the same
    /// GameObject. This script enables/disables them via SetControlled().
    /// </summary>
    public class PossessableCharacter : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("The CharacterDefinition asset describing this character.")]
        public CharacterDefinition definition;

        [Header("References (auto if left empty)")]
        [Tooltip("Component that drives movement when this character is controlled. " +
                 "Will be enabled/disabled by SetControlled. " +
                 "(Implementation comes in next iteration — leave empty for now.)")]
        public Behaviour movementComponent;

        [Tooltip("Component that drives NPC wandering when uncontrolled. Will be enabled/disabled inversely.")]
        public Behaviour wanderComponent;

        [Tooltip("Where the camera should follow / look at.")]
        public Transform cameraTarget;

        [Tooltip("Optional: per-character ambient AudioSource. Auto-created if left empty.")]
        public AudioSource ambientSource;

        [Header("Debug")]
        public bool debugLogs = false;

        // ============================================================
        // Public state
        // ============================================================

        public CharacterDefinition Definition => definition;
        public bool IsControlled { get; private set; }
        public bool IsValid { get; private set; }

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            ValidateSetup();
            EnsureAmbientSource();
            ApplyAmbientClipFromDefinition();
        }

        void OnEnable()
        {
            if (IsValid)
                GameContext.Instance?.Characters.Register(this);
        }

        void OnDisable()
        {
            GameContext.Instance?.Characters.Unregister(this);
        }

        // ============================================================
        // Validation
        // ============================================================

        void ValidateSetup()
        {
            bool ok = true;

            if (definition == null)
            {
                Debug.LogError($"[PossessableCharacter] '{name}': missing CharacterDefinition.");
                ok = false;
            }

            if (cameraTarget == null)
            {
                Debug.LogWarning($"[PossessableCharacter] '{name}': cameraTarget not assigned. Camera follow will fail.");
                // Not fatal: still allow registration
            }

            // movementComponent and wanderComponent are intentionally optional in this iteration;
            // they will be required once the movement system is wired.

            IsValid = ok;

            if (debugLogs)
                Debug.Log($"[PossessableCharacter] '{name}' valid={IsValid} def='{(definition ? definition.displayName : "NULL")}'");
        }

        // ============================================================
        // Ambient
        // ============================================================

        void EnsureAmbientSource()
        {
            if (ambientSource != null) return;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f; // 2D
        }

        void ApplyAmbientClipFromDefinition()
        {
            if (definition == null || ambientSource == null) return;

            ambientSource.clip   = definition.ambientLoop;
            ambientSource.volume = definition.ambientVolume;
            ambientSource.pitch  = definition.ambientPitch;
        }

        void StartAmbient()
        {
            if (ambientSource == null || ambientSource.clip == null) return;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }

        void StopAmbient()
        {
            if (ambientSource == null) return;
            if (ambientSource.isPlaying) ambientSource.Stop();
        }

        // ============================================================
        // Control
        // ============================================================

        /// <summary>
        /// Switch this character between controlled (player) and uncontrolled (NPC).
        /// </summary>
        public virtual void SetControlled(bool controlled)
        {
            IsControlled = controlled;

            // Movement / wander toggle
            if (movementComponent != null)
                movementComponent.enabled = controlled;

            if (wanderComponent != null)
                wanderComponent.enabled = !controlled;

            // Ambient: only the controlled character's bed plays
            if (controlled) StartAmbient();
            else StopAmbient();

            if (debugLogs)
                Debug.Log($"[PossessableCharacter] '{name}' SetControlled({controlled})");
        }
    }
}