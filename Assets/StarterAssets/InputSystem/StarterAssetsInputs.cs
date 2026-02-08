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

        public bool dialogueConfirmHeld;    // TRUE solange Taste gehalten
        public bool dialogueConfirmDown;    // Pulse (Press)
        public bool dialogueConfirmUp;      // Pulse (Release)

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
        // Interaction
        // =========================
        public void OnInteract(InputValue value)
        {
            if (!value.isPressed) return;

            interact = true;

            if (debugLogs)
                Debug.Log($"[Inputs] Interact PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Start
        // =========================
        public void OnDialogueStart(InputValue value)
        {
            if (!value.isPressed) return;

            dialogueStart = true;

            if (debugLogs)
                Debug.Log($"[Inputs] DialogueStart PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Cancel
        // =========================
        public void OnDialogueCancel(InputValue value)
        {
            if (!value.isPressed) return;

            dialogueCancel = true;

            if (debugLogs)
                Debug.Log($"[Inputs] DialogueCancel PULSE on '{name}' frame {Time.frameCount}");
        }

        // =========================
        // Dialogue Confirm (HOLD + DOWN + UP)
        // =========================
        public void OnDialogueConfirm(InputValue value)
        {
            bool pressed = value.isPressed;

            // ---- DOWN ----
            if (pressed && !dialogueConfirmHeld)
            {
                dialogueConfirmHeld = true;
                dialogueConfirmDown = true;

                if (debugLogs)
                    Debug.Log($"[Inputs] DialogueConfirm DOWN on '{name}' frame {Time.frameCount}");

                return;
            }

            // ---- UP ----
            if (!pressed && dialogueConfirmHeld)
            {
                dialogueConfirmHeld = false;
                dialogueConfirmUp = true;

                if (debugLogs)
                    Debug.Log($"[Inputs] DialogueConfirm UP on '{name}' frame {Time.frameCount}");

                return;
            }

            // ---- HELD (stabil, kein Spam) ----
            dialogueConfirmHeld = pressed;
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
        }

        // =========================
        // Input setters
        // =========================
        public void MoveInput(Vector2 newMoveDirection) => move = newMoveDirection;
        public void LookInput(Vector2 newLookDirection) => look = newLookDirection;
        public void JumpInput(bool newJumpState) => jump = newJumpState;
        public void SprintInput(bool newSprintState) => sprint = newSprintState;

        // =========================
        // Cursor handling
        // =========================
        private void OnApplicationFocus(bool hasFocus) => SetCursorState(cursorLocked);

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}