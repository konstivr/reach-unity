using System.Collections.Generic;
using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Manifest of a story pack. Lists all characters and global pack settings.
    /// Swap this asset in PackLoader to switch the entire game content.
    /// </summary>
    [CreateAssetMenu(
        fileName = "StoryPack",
        menuName = "Reach/Story Pack",
        order = 1
    )]
    public class StoryPack : ScriptableObject
    {
        [Header("Pack Identity")]
        public string packName = "Untitled Pack";

        [TextArea(2, 5)]
        public string description = "";

        [Header("Language")]
        [Tooltip("Language code for STT (e.g. 'de', 'en'). Used by Whisper.")]
        public string language = "en";

        [Header("Characters")]
        [Tooltip("All characters available in this pack. The first one in this list is where the player starts.")]
        public List<CharacterDefinition> characters = new List<CharacterDefinition>();

        [Header("Music")]
        [Tooltip("Layered music clips, added one by one as the player reaches each new perspective.")]
        public List<AudioClip> musicLayers = new List<AudioClip>();

        [Header("Progression")]
        [Tooltip("Total perspectives to visit. Drives ChronoPerception filter reduction. " +
                 "Defaults to characters.Count if 0.")]
        public int maxPerspectives = 0;

        public int EffectiveMaxPerspectives =>
            maxPerspectives > 0 ? maxPerspectives : characters.Count;

        public CharacterDefinition StartCharacter =>
            characters != null && characters.Count > 0 ? characters[0] : null;
    }
}