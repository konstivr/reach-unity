using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
public class NPCWander : MonoBehaviour
{
    [Header("References (auto if left empty)")]
    public StarterAssetsInputs inputs;
    public ThirdPersonController thirdPersonController;

    [Header("Behaviour (calm)")]
    [Tooltip("NPC pauses sometimes (seconds).")]
    public Vector2 idleTimeRange = new Vector2(0.6f, 2.0f);

    [Tooltip("NPC walks in one direction (seconds).")]
    public Vector2 walkTimeRange = new Vector2(2.0f, 6.0f);

    [Tooltip("Input magnitude (0..1). Higher = more natural walking pace.")]
    [Range(0.1f, 1f)]
    public float walkInputMagnitude = 0.78f;

    [Tooltip("Prefer directions roughly forward relative to current facing (feels less random).")]
    public bool preferForwardCone = true;

    [Tooltip("Cone angle around forward (degrees). 90–120 looks natural.")]
    [Range(10f, 180f)]
    public float forwardConeAngle = 110f;

    [Header("NPC Speed Profile (used while this script is enabled)")]
    [Tooltip("Move speed while NPCWander is enabled.")]
    public float npcMoveSpeed = 1.8f;

    [Tooltip("Sprint speed while NPCWander is enabled (NPC usually doesn't sprint, but keep sane).")]
    public float npcSprintSpeed = 2.5f;

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

    // Cache original speeds so we can restore them when disabled
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
            inputs.analogMovement = true; // required for magnitude-based calm movement

        ApplyNPCSpeeds(true);
        EnterIdle();

        if (debugLogs) Debug.Log($"[NPCWander] Enabled on '{name}' | npcMove={npcMoveSpeed:0.00}");
    }

    private void OnDisable()
    {
        // Reset inputs so nothing "sticks"
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        ApplyNPCSpeeds(false);

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
        // ThirdPersonController moves relative to camera yaw.
        float camYaw = (_mainCam != null) ? _mainCam.transform.eulerAngles.y : 0f;
        float desiredYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;

        float localYaw = Mathf.DeltaAngle(camYaw, desiredYaw);
        float rad = localYaw * Mathf.Deg2Rad;

        // x = strafe, y = forward in StarterAssetsInputs
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * Mathf.Clamp01(magnitude);
    }

    private void ApplyNPCSpeeds(bool npcActive)
    {
        if (!thirdPersonController || !_cachedSpeeds) return;

        if (npcActive)
        {
            thirdPersonController.MoveSpeed = npcMoveSpeed;
            thirdPersonController.SprintSpeed = npcSprintSpeed;
        }
        else
        {
            // Restore original values (PossessableCharacter will set correct profile anyway)
            thirdPersonController.MoveSpeed = _origMoveSpeed;
            thirdPersonController.SprintSpeed = _origSprintSpeed;
        }
    }
}