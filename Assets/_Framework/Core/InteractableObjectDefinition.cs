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
    /// Defines the single interactable object belonging to a character.
    /// Reference one of these from CharacterDefinition.interactObject.
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

        [Header("Prompts")]
        [TextArea(1, 3)]
        public string promptText = "Press Interact";

        [TextArea(1, 3)]
        [Tooltip("Only used in TwoStep mode after the first press.")]
        public string secondStepPromptText = "Press again";

        [Header("Response Text (HUD)")]
        [TextArea(1, 4)]
        [Tooltip("Text shown after the (final) press. Empty = no response text.")]
        public string responseText = "";

        [Tooltip("How long the response text stays before HUD returns to idle.")]
        public float responseDurationSeconds = 2.5f;

        [Header("Audio")]
        [Tooltip("Audio played on (final) press. Optional.")]
        public AudioClip audioClip;

        [Tooltip("Only used in TwoStep mode: audio for the first press. Optional.")]
        public AudioClip firstStepAudioClip;

        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Header("On Complete")]
        [Tooltip("If true: object hides itself after the (final) press.")]
        public bool hideOnComplete = true;

        [Header("Outreach Unlock")]
        [Tooltip("If true: completing this action is required before player can reach out to a new character.")]
        public bool unlocksOutreach = true;
    }
}