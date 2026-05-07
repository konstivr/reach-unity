using UnityEngine;
using Reach.Framework.InputSys;
using Reach.Framework.Dialogue;
using Reach.Framework.HUD;
using Reach.Framework.Interaction;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Central service locator for the framework.
    /// </summary>
    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        [Header("Pack")]
        public StoryPack pack;

        [Header("Services (assigned in inspector)")]
        public InputReader input;

        // Runtime services
        public CharacterRegistry Characters { get; } = new CharacterRegistry();
        public InputReader Input => input;
        public IPerspectiveManager Perspective { get; set; }
        public SpeechSystem Speech { get; set; }
        public IHud Hud { get; set; }
        public IGateSystem Gate { get; set; }

        // Will be filled later:
        // public IPauseSystem Pause { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameContext] Duplicate instance on '{name}'.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (pack == null)
                Debug.LogWarning("[GameContext] No StoryPack assigned.");
            if (input == null)
                Debug.LogWarning("[GameContext] No InputReader assigned.");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}