using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Single source of truth for pause state.
    /// Toggle with the Pause input action (default key: P).
    /// Pauses Time.timeScale and AudioListener.
    ///
    /// Other systems can read PauseSystem.IsPaused or check GameContext.Pause.IsPaused.
    /// </summary>
    public class PauseSystem : MonoBehaviour, IPauseSystem
    {
        [Header("Behaviour")]
        [Tooltip("Pause AudioListener so all audio is silenced while paused.")]
        public bool pauseAudioListener = true;

        [Tooltip("Time scale while paused (0 = full freeze).")]
        [Range(0f, 1f)] public float pausedTimeScale = 0f;

        [Header("Visual (optional)")]
        [Tooltip("GameObject to show/hide as pause overlay (canvas, panel, etc.). Optional.")]
        public GameObject pauseOverlay;

        [Header("Debug")]
        public bool debugLogs = false;

        public bool IsPaused { get; private set; }

        float _previousTimeScale = 1f;

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx != null) ctx.Pause = this;

            if (pauseOverlay != null) pauseOverlay.SetActive(false);
        }

        void Update()
        {
            var input = GameContext.Instance?.Input;
            if (input == null) return;

            if (input.PauseDown)
                Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            _previousTimeScale = Time.timeScale;
            Time.timeScale = pausedTimeScale;

            if (pauseAudioListener) AudioListener.pause = true;
            if (pauseOverlay != null) pauseOverlay.SetActive(true);

            if (debugLogs) Debug.Log("[PauseSystem] Paused");
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = Mathf.Approximately(_previousTimeScale, 0f) ? 1f : _previousTimeScale;
            if (pauseAudioListener) AudioListener.pause = false;
            if (pauseOverlay != null) pauseOverlay.SetActive(false);

            if (debugLogs) Debug.Log("[PauseSystem] Resumed");
        }

        void OnApplicationQuit()
        {
            // Make sure timeScale is restored even on quit (some platforms persist this).
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}