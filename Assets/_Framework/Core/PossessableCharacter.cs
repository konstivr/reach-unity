using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// A character that can be possessed (controlled) by the player.
    /// Lives on a character GameObject in the scene.
    ///
    /// This is the SKELETON. Movement, control, ambient, freeze logic
    /// will be added in later iterations.
    /// </summary>
    public class PossessableCharacter : MonoBehaviour
    {
        [Header("Definition")]
        [Tooltip("The CharacterDefinition asset describing this character.")]
        public CharacterDefinition definition;

        public CharacterDefinition Definition => definition;

        /// <summary>True when this character is currently the one being controlled.</summary>
        public bool IsControlled { get; private set; }

        /// <summary>True when this character has all the components it needs to be possessed.</summary>
        public bool IsValid { get; protected set; } = true;

        protected virtual void OnEnable()
        {
            GameContext.Instance?.Characters.Register(this);
        }

        protected virtual void OnDisable()
        {
            GameContext.Instance?.Characters.Unregister(this);
        }

        /// <summary>
        /// Switch this character between controlled (player) and uncontrolled (NPC).
        /// Implementation will be expanded in a later iteration.
        /// </summary>
        public virtual void SetControlled(bool controlled)
        {
            IsControlled = controlled;
            // TODO: enable/disable input, swap movement speeds, start/stop ambient
        }
    }
}