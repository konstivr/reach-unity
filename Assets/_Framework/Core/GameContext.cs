using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Central service locator for the framework.
    /// One instance lives in the scene; all framework systems read from it.
    ///
    /// Replace the previous "FindObjectOfType everywhere" pattern with:
    ///     GameContext.Instance.Characters
    ///     GameContext.Instance.Pack
    ///     GameContext.Instance.Hud
    ///     ...
    ///
    /// Service refs are populated by their respective systems on Awake/Enable.
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        [Header("Pack")]
        [Tooltip("The active StoryPack. Swap this asset to switch all game content.")]
        public StoryPack pack;

        // ============================================================
        // Runtime services (populated by systems, not by inspector)
        // ============================================================

        /// <summary>All currently spawned PossessableCharacters.</summary>
        public CharacterRegistry Characters { get; } = new CharacterRegistry();

        // Will be filled in later häppchen:
        // public IHud Hud { get; set; }
        // public ISpeechSystem Speech { get; set; }
        // public IPerspectiveManager Perspective { get; set; }
        // public IGateSystem Gate { get; set; }
        // public IPauseSystem Pause { get; set; }

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameContext] Duplicate instance on '{name}'. Destroying.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (pack == null)
                Debug.LogWarning("[GameContext] No StoryPack assigned. Framework will not load any content.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}