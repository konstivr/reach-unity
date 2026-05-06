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
    ///   - Enable/disable its movement + wander when control changes.
    ///   - Validate setup at runtime.
    /// </summary>
    public class PossessableCharacter : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("The CharacterDefinition asset describing this character.")]
        public CharacterDefinition definition;

        [Header("References (auto-resolved if left empty)")]
        [Tooltip("Component that drives movement when controlled. Auto-found: CharacterMovement.")]
        public Behaviour movementComponent;

        [Tooltip("Component that drives wandering when uncontrolled. Auto-found: CharacterWander.")]
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
            AutoResolveReferences();
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
        // Setup
        // ============================================================

        void AutoResolveReferences()
        {
            if (movementComponent == null)
                movementComponent = GetComponent<CharacterMovement>();

            if (wanderComponent == null)
                wanderComponent = GetComponent<CharacterWander>();
        }

        void ValidateSetup()
        {
            bool ok = true;

            if (definition == null)
            {
                Debug.LogError($"[PossessableCharacter] '{name}': missing CharacterDefinition.");
                ok = false;
            }

            if (cameraTarget == null)
                Debug.LogWarning($"[PossessableCharacter] '{name}': cameraTarget not assigned. Camera follow will fall back to transform.");

            if (movementComponent == null)
                Debug.LogWarning($"[PossessableCharacter] '{name}': no CharacterMovement found. Player will not be able to move when controlling this character.");

            if (wanderComponent == null && debugLogs)
                Debug.Log($"[PossessableCharacter] '{name}': no CharacterWander (NPC will be static when not controlled).");

            IsValid = ok;
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
            ambientSource.spatialBlend = 0f;
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

        public virtual void SetControlled(bool controlled)
        {
            IsControlled = controlled;

            if (movementComponent != null)
                movementComponent.enabled = controlled;

            if (wanderComponent != null)
                wanderComponent.enabled = !controlled;

            if (controlled) StartAmbient();
            else StopAmbient();

            if (debugLogs)
                Debug.Log($"[PossessableCharacter] '{name}' SetControlled({controlled})");
        }
    }
}