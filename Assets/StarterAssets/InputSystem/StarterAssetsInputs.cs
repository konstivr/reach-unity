using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        // =========================
        // Movement
        // =========================
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        // =========================
        // Interaction
        // =========================
        [Header("Interaction")]
        public bool interact; // Pulse (1 Frame)

        // =========================
        // Dialogue
        // =========================
        [Header("Dialogue (Pulses + Held)")]
        public bool dialogueStart;          // Pulse (1 Frame)
        public bool dialogueCancel;         // Pulse (1 Frame)

        // Compatibility flags
        public bool dialogueConfirmHeld;    // TRUE solange Taste gehalten
        public bool dialogueConfirmDown;    // Pulse (Press)
        public bool dialogueConfirmUp;      // Pulse (Release)

        // =========================
        // Menu / Pause
        // =========================
        [Header("Menu")]
        public bool menu; // Pulse (1 Frame)

        // =========================
        // Debug
        // =========================
        [Header("Debug")]
        public bool debugLogs = false;

        // =========================
        // Settings
        // =========================
        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        // =========================
        // Robust pulse guards (same-frame)
        // =========================
        int _lastInteractFrame = -999;
        int _lastDialogueStartFrame = -999;
        int _lastDialogueCancelFrame = -999;
        int _lastMenuFrame = -999;
        int _lastConfirmFrame = -999;

        // =========================
        // Interact anti-spam (multi-frame hold latch + time debounce)
        // =========================
        [Header("Interact Debounce")]
        [Tooltip("If true: only ONE Interact pulse per press, even if action spams performed every frame while held.")]
        public bool interactUseHoldLatch = true;

        [Tooltip("Extra safety: minimum seconds between Interact pulses.")]
        public float interactDebounceSeconds = 0.12f;

        bool _interactLatched = false;
        float _interactLastDownTime = -999f;

        // =========================
        // DialogueConfirm anti-spam (multi-frame hold latch + time debounce)
        // =========================
        [Header("Dialogue Confirm Debounce")]
        [Tooltip("If true: only ONE DOWN event per press, even if action spams performed every frame while held (common with Value actions on gamepad).")]
        public bool dialogueConfirmUseHoldLatch = true;

        [Tooltip("Extra safety: minimum seconds between DOWN events (rare edge cases).")]
        public float dialogueConfirmDebounceSeconds = 0.12f;

        bool _dialogueConfirmLatched = false;
        float _dialogueConfirmLastDownTime = -999f;

#if ENABLE_INPUT_SYSTEM
        // =========================
        // Movement bindings
        // =========================
        public void OnMove(InputValue value) => MoveInput(value.Get<Vector2>());

        public void OnLook(InputValue value)
        {
            if (cursorInputForLook)
                LookInput(value.Get<Vector2>());
        }

        public void OnJump(InputValue value) => JumpInput(value.isPressed);
        public void OnSprint(InputValue value) => SprintInput(value.isPressed);

        // =========================
        // Interaction (F)  ✅ fixed: latch + debounce + release unlock
        // =========================
        public void OnInteract(InputValue value)
        {
            bool isPressed = value.isPressed;

            // Release -> unlock latch
            if (!isPressed)
            {
                _interactLatched = false;
                return;
            }

            // same-frame guard
            if (Time.frameCount == _lastInteractFrame) return;
            _lastInteractFrame = Time.frameCount;

            // hold latch: ignore repeated callbacks while held
            if (interactUseHoldLatch)
            {
                if (_interactLatched) return;
                _interactLatched = true;
            }

            // time debounce
            if (Time.time - _interactLastDownTime < interactDebounceSeconds)
                return;

            _interactLastDownTime = Time.time;

            interact = true;

            if (debugLogs)
                Debug.Log($"[Inputs] Interact DOWN-PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Start
        // =========================
        public void OnDialogueStart(InputValue value)
        {
            if (!value.isPressed) return;
            if (Time.frameCount == _lastDialogueStartFrame) return;
            _lastDialogueStartFrame = Time.frameCount;

            dialogueStart = true;

            if (debugLogs)
                Debug.Log($"[Inputs] DialogueStart DOWN-PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Cancel
        // =========================
        public void OnDialogueCancel(InputValue value)
        {
            if (!value.isPressed) return;
            if (Time.frameCount == _lastDialogueCancelFrame) return;
            _lastDialogueCancelFrame = Time.frameCount;

            dialogueCancel = true;

            if (debugLogs)
                Debug.Log($"[Inputs] DialogueCancel DOWN-PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Confirm (ENTER / Right Button)
        // Fix: Hold-latch => only 1 DOWN per press, even if callback spams every frame.
        // Release: unlock latch when value.isPressed == false (Value actions usually call on release too).
        // =========================
        public void OnDialogueConfirm(InputValue value)
        {
            bool isPressed = value.isPressed;

            // Release path -> unlock latch
            if (!isPressed)
            {
                _dialogueConfirmLatched = false;
                dialogueConfirmHeld = false;
                dialogueConfirmUp = true;
                return;
            }

            // Pressed path
            dialogueConfirmHeld = true;

            // same-frame guard
            if (Time.frameCount == _lastConfirmFrame) return;
            _lastConfirmFrame = Time.frameCount;

            // hold latch: ignore repeated callbacks while held
            if (dialogueConfirmUseHoldLatch)
            {
                if (_dialogueConfirmLatched) return;
                _dialogueConfirmLatched = true;
            }

            // time debounce
            if (Time.time - _dialogueConfirmLastDownTime < dialogueConfirmDebounceSeconds)
                return;

            _dialogueConfirmLastDownTime = Time.time;

            dialogueConfirmDown = true;

            if (debugLogs)
                Debug.Log($"[Inputs] DialogueConfirm DOWN on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Menu (Pause)
        // =========================
        public void OnMenu(InputValue value)
        {
            if (!value.isPressed) return;
            if (Time.frameCount == _lastMenuFrame) return;
            _lastMenuFrame = Time.frameCount;

            menu = true;

            if (debugLogs)
                Debug.Log($"[Inputs] Menu DOWN-PULSE on '{name}' frame {Time.frameCount}");
        }
#endif

        // =========================
        // Pulse reset (END OF FRAME)
        // =========================
        private void LateUpdate()
        {
            interact = false;
            dialogueStart = false;
            dialogueCancel = false;
            dialogueConfirmDown = false;
            dialogueConfirmUp = false;
            menu = false;

            // NOTE:
            // dialogueConfirmHeld wird NICHT mehr hier resetet,
            // damit "Held" wirklich über Frames true bleibt bis zum Release-Callback.
        }

        // =========================
        // Input setters
        // =========================
        public void MoveInput(Vector2 newMoveDirection) => move = newMoveDirection;
        public void LookInput(Vector2 newLookDirection) => look = newLookDirection;
        public void JumpInput(bool newJumpState) => jump = newJumpState;
        public void SprintInput(bool newSprintState) => sprint = newSprintState;

        private void OnApplicationFocus(bool hasFocus) => SetCursorState(cursorLocked);

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}