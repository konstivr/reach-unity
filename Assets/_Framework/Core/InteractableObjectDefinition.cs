using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Core
{
    public enum InteractActionMode
    {
        /// <summary>One press: action runs once, object disappears (if hideOnComplete).</summary>
        OneShot,

        /// <summary>First press: armed + first response. Second press: action runs, object disappears.</summary>
        TwoStep
    }

    /// <summary>
    /// Defines a world-placed object that any character can interact with.
    /// Each character can have its own audio + response text.
    /// Characters not listed fall back to defaultResponse.
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

        [Header("On Complete")]
        [Tooltip("If true: object hides itself after the (final) press.")]
        public bool hideOnComplete = true;

        [Header("Outreach Unlock")]
        [Tooltip("If true: completing this action unlocks outreach for the character that interacted with it.")]
        public bool unlocksOutreach = true;

        /// <summary>
        /// Find the response config for a given character.
        /// Falls back to defaultResponse if no specific entry exists.
        /// </summary>
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

    /// <summary>
    /// What happens when a specific character interacts with an object.
    /// </summary>
    [Serializable]
    public class CharacterResponse
    {
        [Tooltip("Which character this response is for. Leave null for the default/fallback response.")]
        public CharacterDefinition character;

        [Header("Response Text")]
        [TextArea(1, 4)]
        [Tooltip("Shown in HUD after the (final) press.")]
        public string responseText = "";

        [Tooltip("How long the response text stays before HUD returns to idle.")]
        public float responseDurationSeconds = 2.5f;

        [Header("Audio")]
        [Tooltip("Audio played on the (final) press.")]
        public AudioClip audioClip;

        [Tooltip("TwoStep mode only: audio for the first press.")]
        public AudioClip firstStepAudioClip;

        [Range(0f, 1f)]
        public float audioVolume = 1f;
    }
}