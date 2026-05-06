using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Central registry of all spawned characters in the active scene.
    /// Characters register/unregister themselves on Enable/Disable.
    /// Other systems query the registry instead of FindObjectsOfType.
    /// </summary>
    public class CharacterRegistry
    {
        readonly List<PossessableCharacter> _characters = new List<PossessableCharacter>();

        /// <summary>Fired whenever a character is added or removed.</summary>
        public event Action Changed;

        public IReadOnlyList<PossessableCharacter> All => _characters;
        public int Count => _characters.Count;

        public void Register(PossessableCharacter character)
        {
            if (character == null) return;
            if (_characters.Contains(character)) return;

            _characters.Add(character);
            Changed?.Invoke();
        }

        public void Unregister(PossessableCharacter character)
        {
            if (character == null) return;
            if (_characters.Remove(character))
                Changed?.Invoke();
        }

        public PossessableCharacter FindByDefinition(CharacterDefinition def)
        {
            if (def == null) return null;
            for (int i = 0; i < _characters.Count; i++)
            {
                if (_characters[i] != null && _characters[i].Definition == def)
                    return _characters[i];
            }
            return null;
        }

        public PossessableCharacter FindNearest(Vector3 position, float maxRadius, PossessableCharacter exclude = null)
        {
            PossessableCharacter best = null;
            float bestSqr = maxRadius * maxRadius;

            for (int i = 0; i < _characters.Count; i++)
            {
                var c = _characters[i];
                if (c == null || c == exclude) continue;

                float sqr = (c.transform.position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        public void Clear()
        {
            _characters.Clear();
            Changed?.Invoke();
        }
    }
}