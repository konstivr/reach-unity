using UnityEngine;
using Reach.Framework.InputSys;
using Reach.Framework.Dialogue;
using Reach.Framework.HUD;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Central service locator for the framework.
    /// One instance lives in the scene; all framework systems read from it.
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        [Header("Pack")]
        [Tooltip("The active StoryPack. Swap this asset to switch all game content.")]
        public StoryPack pack;

        [Header("Services (assigned in inspector)")]
        [Tooltip("Cross-platform input reader.")]
        public InputReader input;

        // ============================================================
        // Runtime services
        // ============================================================

        public CharacterRegistry Characters { get; } = new CharacterRegistry();

        public InputReader Input => input;

        public IPerspectiveManager Perspective { get; set; }

        public SpeechSystem Speech { get; set; }

        public IHud Hud { get; set; }

        // Will be filled in later häppchen:
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

            if (input == null)
                Debug.LogWarning("[GameContext] No InputReader assigned. Input will not work.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}