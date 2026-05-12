using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Core
{
    public enum InteractActionMode
    {
        /// <summary>One press = one full response.</summary>
        OneShot,

        /// <summary>First press primes, second press resolves. Resets after resolve so the cycle can repeat.</summary>
        TwoStep
    }

    /// <summary>
    /// A world-placed object that any character can interact with.
    /// Object persists — every character can interact with it repeatedly,
    /// each playing their own per-character response.
    /// </summary>
    [CreateAssetMenu(
        fileName = "InteractableObjectDefinition",
        menuName = "Reach/Interactable Object",
        order = 11
    )]
    public class InteractableObjectDefinition : ScriptableObject
    {
        [Header("Prefab")]
        [Tooltip("Prefab of the object placed in the world. Must contain InteractableObject component.")]
        public GameObject objectPrefab;

        [Header("Behavior")]
        public InteractActionMode mode = InteractActionMode.OneShot;

        [Tooltip("Distance in meters within which the player can interact.")]
        public float interactRadius = 2.5f;

        [Header("Default Prompt (shown when in range)")]
        [TextArea(1, 3)]
        public string promptText = "Press Interact";

        [TextArea(1, 3)]
        [Tooltip("Only used in TwoStep mode after the first press.")]
        public string secondStepPromptText = "Press again";

        [Header("Per-Character Responses")]
        [Tooltip("Each entry defines what happens when a specific character interacts with this object. " +
                 "Characters not listed here use 'defaultResponse'.")]
        public List<CharacterResponse> responsesPerCharacter = new List<CharacterResponse>();

        [Header("Default Response (fallback)")]
        [Tooltip("Used when the current character is not in responsesPerCharacter.")]
        public CharacterResponse defaultResponse = new CharacterResponse();

        [Header("Outreach Unlock")]
        [Tooltip("If true: a character that has interacted with this object at least once can reach out to others.")]
        public bool unlocksOutreach = true;

        public CharacterResponse GetResponseFor(CharacterDefinition character)
        {
            if (character != null && responsesPerCharacter != null)
            {
                for (int i = 0; i < responsesPerCharacter.Count; i++)
                {
                    var r = responsesPerCharacter[i];
                    if (r != null && r.character == character)
                        return r;
                }
            }
            return defaultResponse;
        }
    }

    [Serializable]
    public class CharacterResponse
    {
        [Tooltip("Which character this response is for. Leave null for the default/fallback response.")]
        public CharacterDefinition character;

        [Header("Response Text")]
        [TextArea(1, 4)]
        public string responseText = "";

        public float responseDurationSeconds = 2.5f;

        [Header("Audio")]
        public AudioClip audioClip;

        [Tooltip("TwoStep mode only: audio for the first press.")]
        public AudioClip firstStepAudioClip;

        [Range(0f, 1f)]
        public float audioVolume = 1f;
    }
}