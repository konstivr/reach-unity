using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Defines a single character in a story pack.
    /// One CharacterDefinition asset per character.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterDefinition",
        menuName = "Reach/Character Definition",
        order = 10
    )]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name shown in debug logs and (optionally) in HUD.")]
        public string displayName = "Unnamed";

        [Tooltip("Unique ID within a pack. Used for save/state references. Lowercase, no spaces.")]
        public string characterId = "char_id";

        [Header("Prefab")]
        [Tooltip("The character prefab. Must contain PossessableCharacter component.")]
        public GameObject characterPrefab;

        [Header("Outreach Gate")]
        [TextArea(2, 6)]
        [Tooltip("Spoken by this character when the player tries to reach out.")]
        public string gateTtsLine = "Say something to enter my world.";

        [Tooltip("The phrase the player must speak to switch into this character.")]
        public string gatePassphrase = "say something";

        [Range(0.5f, 1f)]
        [Tooltip("How fuzzy the match can be (1.0 = exact, 0.5 = very loose).")]
        public float gateSimilarityThreshold = 0.82f;

        [Header("Chat (after switch)")]
        [TextArea(4, 12)]
        [Tooltip("System prompt sent to the LLM when the player has switched into this character.")]
        public string chatSystemPrompt = "You are a character. Reply briefly, in character.";

        [Header("Voice")]
        [Tooltip("Platform-specific voice name. e.g. 'Anna' on macOS, 'Hedda' on Windows.")]
        public string voiceMacOS = "Samantha";
        public string voiceWindows = "Zira";

        [Header("Ambient (per character)")]
        [Tooltip("Looping ambient audio while this character is controlled.")]
        public AudioClip ambientLoop;

        [Range(0f, 1f)]
        public float ambientVolume = 0.1f;

        [Range(0.5f, 1.5f)]
        public float ambientPitch = 1f;

        [Header("Interact Object")]
        [Tooltip("Definition of the one interactable object this character has.")]
        public InteractableObjectDefinition interactObject;
    }
}