using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 2.0f;
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns (deg/sec) to face movement direction")]
        public float TurnSpeed = 360f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;
        public float JumpTimeout = 0.50f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("This is the target that Cinemachine Virtual Camera FOLLOWS/LOOKS AT.")]
        public GameObject CinemachineCameraTarget;

        public float TopClamp = 70.0f;
        public float BottomClamp = -30.0f;
        public float CameraAngleOverride = 0.0f;

        [Tooltip("When true: camera rotation will NOT be updated")]
        public bool LockCameraPosition = false;

        [Header("Controlled Player Only")]
        [Tooltip("If FALSE, this controller will NOT rotate the CinemachineCameraTarget (use for NPCs).")]
        public bool UseMoveToDriveCameraYaw = true;

        [Header("Stick Filtering (fixes drift)")]
        [Range(0f, 0.5f)] public float MoveDeadzone = 0.22f;

        [Tooltip("Auto-calibrate small joystick center drift while stick is near zero.")]
        public bool AutoCalibrateDrift = true;

        [Range(0f, 0.2f)]
        public float CalibrateWhenBelow = 0.08f;

        [Range(0.01f, 20f)]
        public float CalibrateSpeed = 6f;

        [Tooltip("If stick is almost forward, kill tiny x drift (deg).")]
        [Range(0f, 20f)]
        public float ForwardSnapAngleDeg = 8f;

        [Header("Camera Target")]
        [Tooltip("Camera target yaw follows character yaw with this speed (deg/sec).")]
        public float CameraYawFollowSpeed = 240f;

        public bool KeepPitchFixed = true;
        public float FixedPitch = 8f;

        // camera target rotation state
        private float _camTargetYaw;
        private float _camTargetPitch;

        // drift calibration
        private Vector2 _moveBias;

        // player motion
        private float _speed;
        private float _animationBlend;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;

        private GameObject _mainCamera;

        private const float _threshold = 0.0001f;
        private bool _hasAnimator;

        private void Awake()
        {
            if (_mainCamera == null)
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Start()
        {
            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif

            AssignAnimationIDs();

            _camTargetYaw = transform.eulerAngles.y;
            _camTargetPitch = KeepPitchFixed ? FixedPitch : 0f;

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            if (!UseMoveToDriveCameraYaw) return;      // NPCs: don't touch camera target
            if (LockCameraPosition) return;
            if (CinemachineCameraTarget == null) return;

            // camera target follows CHARACTER yaw (stable, no feedback loop)
            float desiredYaw = transform.eulerAngles.y;
            _camTargetYaw = Mathf.MoveTowardsAngle(_camTargetYaw, desiredYaw, CameraYawFollowSpeed * Time.deltaTime);

            _camTargetPitch = KeepPitchFixed
                ? Mathf.Clamp(FixedPitch, BottomClamp, TopClamp)
                : Mathf.Clamp(_camTargetPitch, BottomClamp, TopClamp);

            CinemachineCameraTarget.transform.rotation =
                Quaternion.Euler(_camTargetPitch + CameraAngleOverride, _camTargetYaw, 0f);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (_hasAnimator)
                _animator.SetBool(_animIDGrounded, Grounded);
        }

        private Vector2 FilterMove(Vector2 raw)
        {
            // 1) learn drift center
            if (AutoCalibrateDrift && raw.magnitude < CalibrateWhenBelow)
            {
                _moveBias = Vector2.Lerp(_moveBias, raw, 1f - Mathf.Exp(-CalibrateSpeed * Time.deltaTime));
            }

            Vector2 v = raw - _moveBias;

            // 2) deadzone + rescale
            float mag = v.magnitude;
            if (mag < MoveDeadzone) return Vector2.zero;

            float scaled = Mathf.InverseLerp(MoveDeadzone, 1f, mag);
            v = v.normalized * scaled;

            // 3) snap forward to remove tiny x bias when pushing forward
            if (ForwardSnapAngleDeg > 0f)
            {
                float angle = Mathf.Abs(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg);
                if (angle < ForwardSnapAngleDeg) v.x = 0f;
            }

            return v;
        }

        private void Move()
        {
            Vector2 mRaw = _input.move;
            Vector2 m = FilterMove(mRaw);

            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
            if (m == Vector2.zero) targetSpeed = 0.0f;

            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? m.magnitude : (m == Vector2.zero ? 0f : 1f);

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else _speed = targetSpeed;

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // move in WORLD direction relative to camera yaw (classic third-person)
            float camYaw = (_mainCamera != null) ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            Vector3 moveWorld = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(m.x, 0f, m.y);

            // rotate character towards movement direction (not "transform + localYaw"!)
            if (moveWorld.sqrMagnitude > _threshold)
            {
                float desiredYaw = Mathf.Atan2(moveWorld.x, moveWorld.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredYaw, TurnSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            Vector3 dir = (moveWorld.sqrMagnitude > _threshold) ? moveWorld.normalized : Vector3.zero;

            _controller.Move(dir * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                if (_verticalVelocity < 0.0f)
                    _verticalVelocity = -2f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                    if (_hasAnimator) _animator.SetBool(_animIDJump, true);
                }

                if (_jumpTimeoutDelta >= 0.0f)
                    _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                    _fallTimeoutDelta -= Time.deltaTime;
                else if (_hasAnimator)
                    _animator.SetBool(_animIDFreeFall, true);

                _input.jump = false;
            }

            if (_verticalVelocity < _terminalVelocity)
                _verticalVelocity += Gravity * Time.deltaTime;
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && FootstepAudioClips != null && FootstepAudioClips.Length > 0)
            {
                var index = Random.Range(0, FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f && LandingAudioClip != null)
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
        }
    }
}