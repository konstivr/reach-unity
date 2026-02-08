#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        // -------------------------
        // Custom Interaction Inputs (state + edges)
        // -------------------------
        [Header("Interaction (States)")]
        public bool interact;
        public bool dialogueStart;
        public bool dialogueCancel;
        public bool dialogueConfirm; // "held"

        [Header("Interaction (Edges - true for ONE frame)")]
        public bool interactPressed;
        public bool dialogueStartPressed;
        public bool dialogueCancelPressed;
        public bool dialogueConfirmPressed;
        public bool dialogueConfirmReleased;

#if ENABLE_INPUT_SYSTEM
        // These method names MUST match your InputAction "Behavior: Send Messages"
        public void OnMove(InputValue value) => MoveInput(value.Get<Vector2>());

        // Fix for MissingMethodException: this MUST exist and be public
        public void OnLook(InputValue value) => LookInput(value.Get<Vector2>());

        public void OnJump(InputValue value) => JumpInput(value.isPressed);
        public void OnSprint(InputValue value) => SprintInput(value.isPressed);

        public void OnInteract(InputValue value)
        {
            bool pressed = value.isPressed;
            if (pressed && !interact) interactPressed = true;
            interact = pressed;
        }

        public void OnDialogueStart(InputValue value)
        {
            bool pressed = value.isPressed;
            if (pressed && !dialogueStart) dialogueStartPressed = true;
            dialogueStart = pressed;
        }

        public void OnDialogueCancel(InputValue value)
        {
            bool pressed = value.isPressed;
            if (pressed && !dialogueCancel) dialogueCancelPressed = true;
            dialogueCancel = pressed;
        }

        public void OnDialogueConfirm(InputValue value)
        {
            bool pressed = value.isPressed;

            if (pressed && !dialogueConfirm)
                dialogueConfirmPressed = true;

            if (!pressed && dialogueConfirm)
                dialogueConfirmReleased = true;

            dialogueConfirm = pressed;
        }
#endif

        public void MoveInput(Vector2 newMoveDirection) => move = newMoveDirection;

        public void LookInput(Vector2 newLookDirection)
        {
            if (cursorInputForLook)
                look = newLookDirection;
        }

        public void JumpInput(bool newJumpState) => jump = newJumpState;
        public void SprintInput(bool newSprintState) => sprint = newSprintState;

        void LateUpdate()
        {
            // Reset EDGE flags each frame
            interactPressed = false;
            dialogueStartPressed = false;
            dialogueCancelPressed = false;
            dialogueConfirmPressed = false;
            dialogueConfirmReleased = false;
        }

        void OnApplicationFocus(bool hasFocus) => SetCursorState(cursorLocked);

        void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}