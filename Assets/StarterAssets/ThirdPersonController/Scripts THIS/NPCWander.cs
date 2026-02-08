using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
public class NPCWander : MonoBehaviour
{
    [Header("References (auto if left empty)")]
    public StarterAssetsInputs inputs;
    public ThirdPersonController thirdPersonController;

    [Header("Behaviour (calm)")]
    public Vector2 idleTimeRange = new Vector2(0.6f, 2.0f);
    public Vector2 walkTimeRange = new Vector2(2.0f, 6.0f);

    [Range(0.1f, 1f)]
    public float walkInputMagnitude = 0.78f;

    public bool preferForwardCone = true;

    [Range(10f, 180f)]
    public float forwardConeAngle = 110f;

    [Header("Speed Handling")]
    [Tooltip("Wenn TRUE: NPCWander verändert KEINE MoveSpeed/SprintSpeed mehr.\n" +
             "Die Speed-Steuerung passiert zentral über PossessableCharacter.SetControlled().")]
    public bool doNotTouchSpeeds = true;

    [Tooltip("Nur relevant wenn doNotTouchSpeeds = false.")]
    [Range(0.2f, 3f)]
    public float aiMoveSpeedMultiplier = 1.0f;

    [Header("Obstacle Avoidance (recommended)")]
    public bool avoidObstacles = true;
    public float obstacleCheckDistance = 1.2f;
    public LayerMask obstacleLayers = ~0;

    [Header("Debug")]
    public bool debugLogs = false;

    private enum State { Idle, Walk }
    private State _state;
    private float _timer;

    private Vector3 _worldDir;
    private Camera _mainCam;

    private float _origMoveSpeed;
    private float _origSprintSpeed;
    private bool _cachedSpeeds;

    private void Awake()
    {
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
        if (!thirdPersonController) thirdPersonController = GetComponent<ThirdPersonController>();

        _mainCam = Camera.main;

        if (thirdPersonController && !_cachedSpeeds)
        {
            _origMoveSpeed = thirdPersonController.MoveSpeed;
            _origSprintSpeed = thirdPersonController.SprintSpeed;
            _cachedSpeeds = true;
        }
    }

    private void OnEnable()
    {
        if (inputs)
            inputs.analogMovement = true;

        if (!doNotTouchSpeeds)
            ApplyAISpeed(true);

        EnterIdle();

        if (debugLogs) Debug.Log($"[NPCWander] Enabled on '{name}' doNotTouchSpeeds={doNotTouchSpeeds}");
    }

    private void OnDisable()
    {
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        if (!doNotTouchSpeeds)
            ApplyAISpeed(false);

        if (debugLogs) Debug.Log($"[NPCWander] Disabled on '{name}'");
    }

    private void Update()
    {
        if (!inputs) return;
        if (_mainCam == null) _mainCam = Camera.main;

        _timer -= Time.deltaTime;

        if (_state == State.Idle)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);

            if (_timer <= 0f)
                EnterWalk();
        }
        else // Walk
        {
            if (avoidObstacles && IsBlocked(_worldDir))
            {
                if (debugLogs) Debug.Log($"[NPCWander] '{name}' blocked -> new direction");
                PickNewDirection();
            }

            Vector2 move = WorldDirToInput(_worldDir, walkInputMagnitude);
            inputs.MoveInput(move);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);

            if (_timer <= 0f)
                EnterIdle();
        }
    }

    private void EnterIdle()
    {
        _state = State.Idle;
        _timer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        if (debugLogs) Debug.Log($"[NPCWander] '{name}' -> IDLE ({_timer:0.0}s)");
    }

    private void EnterWalk()
    {
        _state = State.Walk;
        _timer = Random.Range(walkTimeRange.x, walkTimeRange.y);
        PickNewDirection();
        if (debugLogs) Debug.Log($"[NPCWander] '{name}' -> WALK ({_timer:0.0}s)");
    }

    private void PickNewDirection()
    {
        float yaw;

        if (preferForwardCone)
        {
            float half = forwardConeAngle * 0.5f;
            yaw = transform.eulerAngles.y + Random.Range(-half, half);
        }
        else
        {
            yaw = Random.Range(0f, 360f);
        }

        _worldDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        _worldDir.y = 0f;
        _worldDir.Normalize();
    }

    private bool IsBlocked(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        return Physics.Raycast(origin, dir, obstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    private Vector2 WorldDirToInput(Vector3 worldDir, float magnitude)
    {
        float camYaw = (_mainCam != null) ? _mainCam.transform.eulerAngles.y : 0f;
        float desiredYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;

        float localYaw = Mathf.DeltaAngle(camYaw, desiredYaw);
        float rad = localYaw * Mathf.Deg2Rad;

        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * Mathf.Clamp01(magnitude);
    }

    private void ApplyAISpeed(bool aiActive)
    {
        if (!thirdPersonController || !_cachedSpeeds) return;

        if (aiActive)
        {
            thirdPersonController.MoveSpeed = _origMoveSpeed * aiMoveSpeedMultiplier;
            thirdPersonController.SprintSpeed = _origSprintSpeed * aiMoveSpeedMultiplier;
        }
        else
        {
            thirdPersonController.MoveSpeed = _origMoveSpeed;
            thirdPersonController.SprintSpeed = _origSprintSpeed;
        }
    }
}