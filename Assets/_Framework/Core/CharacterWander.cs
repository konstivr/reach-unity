using UnityEngine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// NPC wandering behavior when this character is not controlled.
    /// Disabled by PossessableCharacter when the character is controlled.
    ///
    /// Picks random directions, walks for a while, idles, repeat.
    /// Avoids obstacles via raycast.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterWander : MonoBehaviour
    {
        [Header("Speed")]
        public float wanderSpeed = 1.8f;
        public float turnSpeed   = 240f;

        [Header("Behaviour Timings")]
        public Vector2 idleTimeRange = new Vector2(0.6f, 2.0f);
        public Vector2 walkTimeRange = new Vector2(2.0f, 6.0f);

        [Header("Direction")]
        [Tooltip("Pick directions within this cone in front of the NPC, instead of fully random.")]
        public bool preferForwardCone = true;

        [Range(10f, 360f)]
        public float forwardConeAngle = 110f;

        [Tooltip("Re-pick direction every N seconds, even mid-walk (prevents getting stuck).")]
        public float repickEverySeconds = 1.2f;

        [Header("Obstacle Avoidance")]
        public bool avoidObstacles = true;
        public float obstacleCheckDistance = 1.2f;
        public LayerMask obstacleLayers = ~0;

        [Header("Gravity")]
        public float gravity = -15f;
        public float groundedOffset = -0.14f;
        public float groundedRadius = 0.28f;
        public LayerMask groundLayers = ~0;

        // ============================================================
        // State
        // ============================================================

        enum State { Idle, Walk }
        State _state;
        float _stateTimer;
        float _repickTimer;

        Vector3 _worldDir;
        float _verticalVelocity;
        bool _grounded;

        CharacterController _controller;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        void OnEnable()
        {
            EnterIdle();
            _verticalVelocity = 0f;
        }

        void Update()
        {
            _stateTimer -= Time.deltaTime;

            GroundedCheck();
            ApplyGravity();

            if (_state == State.Idle)
                UpdateIdle();
            else
                UpdateWalk();
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

        void UpdateIdle()
        {
            // Apply only gravity
            _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));

            if (_stateTimer <= 0f)
                EnterWalk();
        }

        void UpdateWalk()
        {
            _repickTimer -= Time.deltaTime;

            if (avoidObstacles && IsBlocked(_worldDir))
                PickNewDirection();
            else if (_repickTimer <= 0f)
                PickNewDirection();

            // Rotate toward movement direction
            if (_worldDir.sqrMagnitude > 0.001f)
            {
                float desiredYaw = Mathf.Atan2(_worldDir.x, _worldDir.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredYaw, turnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            _controller.Move(
                _worldDir * (wanderSpeed * Time.deltaTime) +
                Vector3.up * (_verticalVelocity * Time.deltaTime)
            );

            if (_stateTimer <= 0f)
                EnterIdle();
        }

        void EnterIdle()
        {
            _state = State.Idle;
            _stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        }

        void EnterWalk()
        {
            _state = State.Walk;
            _stateTimer = Random.Range(walkTimeRange.x, walkTimeRange.y);
            PickNewDirection();
        }

        void PickNewDirection()
        {
            float yaw = preferForwardCone
                ? transform.eulerAngles.y + Random.Range(-forwardConeAngle * 0.5f, forwardConeAngle * 0.5f)
                : Random.Range(0f, 360f);

            _worldDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            _worldDir.y = 0f;
            _worldDir.Normalize();

            _repickTimer = Mathf.Max(0.1f, repickEverySeconds);
        }

        bool IsBlocked(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            return Physics.Raycast(origin, dir, obstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
        }
    }
}