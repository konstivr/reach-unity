using UnityEngine;
using UnityEngine.InputSystem;
using Reach.Framework.Core;

namespace Reach.Framework.InputSys
{
    /// <summary>
    /// Reads input from a Unity InputActions asset and exposes it as filtered,
    /// frame-stable properties for the rest of the framework.
    ///
    /// One InputReader per scene (registered in GameContext).
    /// Works with keyboard+mouse OR gamepad in the same build (Unity Input System
    /// auto-routes whichever device is active).
    ///
    /// Wire-up:
    ///   - Assign 'inputActions' in the inspector (the .inputactions asset)
    ///   - Set 'actionMapName' to "Player" (matches the asset)
    ///   - Action names below must match the asset exactly (case-sensitive)
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        [Header("Input Actions Asset")]
        [Tooltip("Drag the .inputactions asset here.")]
        public InputActionAsset inputActions;

        [Tooltip("Which action map to use. Default: 'Player'.")]
        public string actionMapName = "Player";

        [Header("Action Names (must match asset)")]
        public string moveActionName       = "Move";
        public string lookActionName       = "Look";
        public string interactActionName   = "Interact";
        public string speakActionName      = "Speak";
        public string cancelActionName     = "Cancel";
        public string sprintActionName     = "Sprint";
        public string jumpActionName       = "Jump";
        public string pauseActionName      = "Pause";

        [Header("Filtering")]
        public Vector2Filter moveFilter = new Vector2Filter
        {
            deadzone = 0.22f,
            autoCalibrate = true,
            calibrateWhenBelow = 0.08f,
            calibrateSpeed = 6f,
            forwardSnapAngleDeg = 8f
        };

        public Vector2Filter lookFilter = new Vector2Filter
        {
            deadzone = 0.06f,
            autoCalibrate = true,
            calibrateWhenBelow = 0.04f,
            calibrateSpeed = 6f,
            forwardSnapAngleDeg = 0f  // looking sideways is fine
        };

        // ============================================================
        // Public state (read by Character / Gate / SpeechInput / etc.)
        // ============================================================

        /// <summary>Filtered movement vector. (-1..1, -1..1)</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Filtered look/aim vector.</summary>
        public Vector2 Look { get; private set; }

        public bool Sprint { get; private set; }
        public bool Jump   { get; private set; }

        /// <summary>Interact: true ONLY in the frame it was pressed. (Pulse-like)</summary>
        public bool InteractDown { get; private set; }

        /// <summary>Speak: true ONLY in the frame it was pressed.</summary>
        public bool SpeakDown { get; private set; }

        /// <summary>Cancel: true ONLY in the frame it was pressed.</summary>
        public bool CancelDown { get; private set; }

        /// <summary>Pause: true ONLY in the frame it was pressed.</summary>
        public bool PauseDown { get; private set; }

        // ============================================================
        // Internals
        // ============================================================

        InputAction _move, _look, _sprint, _jump;
        InputAction _interact, _speak, _cancel, _pause;
        InputActionMap _map;

        bool _enabled;

        void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[InputReader] No InputActionAsset assigned. Disabling.");
                enabled = false;
                return;
            }

            _map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogError($"[InputReader] Action map '{actionMapName}' not found in asset.");
                enabled = false;
                return;
            }

            _move     = _map.FindAction(moveActionName,     throwIfNotFound: false);
            _look     = _map.FindAction(lookActionName,     throwIfNotFound: false);
            _sprint   = _map.FindAction(sprintActionName,   throwIfNotFound: false);
            _jump     = _map.FindAction(jumpActionName,     throwIfNotFound: false);
            _interact = _map.FindAction(interactActionName, throwIfNotFound: false);
            _speak    = _map.FindAction(speakActionName,    throwIfNotFound: false);
            _cancel   = _map.FindAction(cancelActionName,   throwIfNotFound: false);
            _pause    = _map.FindAction(pauseActionName,    throwIfNotFound: false);

            WarnIfMissing(_move,     moveActionName);
            WarnIfMissing(_look,     lookActionName);
            WarnIfMissing(_interact, interactActionName);
            WarnIfMissing(_speak,    speakActionName);
            WarnIfMissing(_cancel,   cancelActionName);
            WarnIfMissing(_pause,    pauseActionName);
            // Sprint+Jump are optional — no warning.
        }

        void OnEnable()
        {
            if (_map != null)
            {
                _map.Enable();
                _enabled = true;
            }
        }

        void OnDisable()
        {
            if (_map != null)
            {
                _map.Disable();
                _enabled = false;
            }
        }

        void Update()
        {
            if (!_enabled) return;

            float dt = Time.unscaledDeltaTime; // works during pause too

            Move = moveFilter.Process(_move != null ? _move.ReadValue<Vector2>() : Vector2.zero, dt);
            Look = lookFilter.Process(_look != null ? _look.ReadValue<Vector2>() : Vector2.zero, dt);

            Sprint = _sprint != null && _sprint.IsPressed();
            Jump   = _jump   != null && _jump.IsPressed();

            InteractDown = _interact != null && _interact.WasPressedThisFrame();
            SpeakDown    = _speak    != null && _speak.WasPressedThisFrame();
            CancelDown   = _cancel   != null && _cancel.WasPressedThisFrame();
            PauseDown    = _pause    != null && _pause.WasPressedThisFrame();
        }

        static void WarnIfMissing(InputAction a, string name)
        {
            if (a == null) Debug.LogWarning($"[InputReader] Action '{name}' not found in action map.");
        }

        // ============================================================
        // External resets (used when switching characters, etc.)
        // ============================================================

        public void ResetFilters()
        {
            moveFilter.ResetBias();
            lookFilter.ResetBias();
        }
    }
}