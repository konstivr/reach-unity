using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// When this character is uncontrolled and the controlled player gets close,
    /// stop wandering and turn to face the player. Used for "they notice you" feel
    /// while the gate is active.
    ///
    /// Place alongside CharacterWander on each character.
    /// Disables wander while frozen, re-enables when player walks away.
    /// </summary>
    public class CharacterProximityFreeze : MonoBehaviour
    {
        [Header("Proximity")]
        [Tooltip("Within this radius of the controlled player, this character freezes and turns.")]
        public float freezeRadius = 3.0f;

        [Tooltip("How fast the character rotates to face the player (deg/sec).")]
        public float turnSpeed = 360f;

        [Tooltip("Look-at height offset on the player (eyes vs feet).")]
        public float lookHeight = 1.5f;

        [Header("Refs (auto)")]
        public PossessableCharacter character;
        public CharacterWander wander;

        bool _isFrozen;

        void Awake()
        {
            if (character == null) character = GetComponent<PossessableCharacter>();
            if (wander == null) wander = GetComponent<CharacterWander>();
        }

        void Update()
        {
            if (character == null) return;

            // Don't touch wander on the controlled character — let PossessableCharacter own it.
            if (character.IsControlled)
            {
                _isFrozen = false;
                return;
            }

            var pm = GameContext.Instance?.Perspective;
            if (pm == null || pm.Current == null) return;

            var current = pm.Current;
            if (current == character) return; // defensive

            float dist = Vector3.Distance(transform.position, current.transform.position);
            bool shouldFreeze = dist <= freezeRadius;

            SetFrozen(shouldFreeze);

            if (_isFrozen)
                RotateTowards(current);
        }

        void SetFrozen(bool frozen)
        {
            if (_isFrozen == frozen) return;
            _isFrozen = frozen;

            if (wander != null)
                wander.enabled = !frozen;
        }

        void RotateTowards(PossessableCharacter target)
        {
            Vector3 targetPos = target.transform.position + Vector3.up * lookHeight;
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }
    }
}