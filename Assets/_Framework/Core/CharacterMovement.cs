using UnityEngine;
using Reach.Framework.InputSys;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Player movement when this character is controlled.
    /// Reads filtered input from GameContext.Input.
    /// Disabled by PossessableCharacter when the character is uncontrolled.
    ///
    /// Camera-relative third-person movement: WASD/stick moves in the direction
    /// the camera is facing, character rotates toward movement direction.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [Header("Speed")]
        public float walkSpeed   = 4.5f;
        public float sprintSpeed = 6.0f;

        [Tooltip("How fast the character turns to face movement direction (deg/sec).")]
        public float turnSpeed = 540f;

        [Tooltip("Acceleration / deceleration smoothing (higher = snappier).")]
        public float speedChangeRate = 10f;

        [Header("Gravity")]
        public float gravity = -15f;

        [Header("Grounding")]
        public float groundedOffset = -0.14f;
        public float groundedRadius = 0.28f;
        public LayerMask groundLayers = ~0;

        [Header("Camera")]
        [Tooltip("If true: movement is relative to the main camera's yaw (classic 3rd-person). " +
                 "If false: movement is relative to character's own forward (tank controls).")]
        public bool cameraRelative = true;

        // ============================================================
        // State
        // ============================================================

        CharacterController _controller;
        Camera _mainCamera;
        float _currentSpeed;
        float _verticalVelocity;
        bool _grounded;

        const float _threshold = 0.0001f;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _mainCamera = Camera.main;
        }

        void OnEnable()
        {
            // Reset state to avoid carrying over momentum from when component was disabled
            _currentSpeed = 0f;
            _verticalVelocity = 0f;
        }

        void Update()
        {
            var input = GameContext.Instance?.Input;
            if (input == null) return;

            GroundedCheck();
            ApplyGravity();
            ApplyMovement(input);
        }

        void GroundedCheck()
        {
            Vector3 spherePos = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            _grounded = Physics.CheckSphere(spherePos, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        }

        void ApplyGravity()
        {
            if (_grounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;
        }

        void ApplyMovement(InputReader input)
        {
            Vector2 m = input.Move;

            float targetSpeed = input.Sprint ? sprintSpeed : walkSpeed;
            if (m == Vector2.zero) targetSpeed = 0f;

            // Smooth current speed toward target
            float currentHoriz = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMag = m.magnitude;

            if (currentHoriz < targetSpeed - speedOffset || currentHoriz > targetSpeed + speedOffset)
            {
                _currentSpeed = Mathf.Lerp(currentHoriz, targetSpeed * inputMag, Time.deltaTime * speedChangeRate);
                _currentSpeed = Mathf.Round(_currentSpeed * 1000f) / 1000f;
            }
            else
            {
                _currentSpeed = targetSpeed;
            }

            // Compute world direction
            Vector3 moveWorld;
            if (cameraRelative && _mainCamera != null)
            {
                float camYaw = _mainCamera.transform.eulerAngles.y;
                moveWorld = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(m.x, 0f, m.y);
            }
            else
            {
                moveWorld = transform.TransformDirection(new Vector3(m.x, 0f, m.y));
            }

            // Rotate character toward movement direction
            if (moveWorld.sqrMagnitude > _threshold)
            {
                float desiredYaw = Mathf.Atan2(moveWorld.x, moveWorld.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredYaw, turnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            Vector3 moveDir = (moveWorld.sqrMagnitude > _threshold) ? moveWorld.normalized : Vector3.zero;

            _controller.Move(
                moveDir * (_currentSpeed * Time.deltaTime) +
                Vector3.up * (_verticalVelocity * Time.deltaTime)
            );
        }
    }
}