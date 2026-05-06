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

    [Header("Stability")]
    [Tooltip("Wenn der NPC zu oft 'klebt', wird nach dieser Zeit eine neue Richtung gewählt.")]
    public float repickDirectionEverySeconds = 1.2f;

    [Tooltip("Minimale Input-Magnitude, unter der wir gar nicht bewegen (gegen micro jitter).")]
    [Range(0f, 0.3f)]
    public float inputDeadzone = 0.05f;

    [Header("Debug")]
    public bool debugLogs = false;

    private enum State { Idle, Walk }
    private State _state;
    private float _timer;

    private Vector3 _worldDir;
    private float _repickTimer;

    private float _origMoveSpeed;
    private float _origSprintSpeed;
    private bool _cachedSpeeds;

    private void Awake()
    {
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
        if (!thirdPersonController) thirdPersonController = GetComponent<ThirdPersonController>();

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

        // ✅ CRITICAL FIX:
        // NPCs dürfen NICHT die "Move -> CameraTargetYaw" Logik ausführen, sonst entsteht Feedback-Loop => Kreis drehen.
        if (thirdPersonController)
            thirdPersonController.UseMoveToDriveCameraYaw = false;

        if (!doNotTouchSpeeds)
            ApplyAISpeed(true);

        EnterIdle();

        if (debugLogs) Debug.Log($"[NPCWander] Enabled on '{name}'");
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

        // Optional: falls dieser NPC später wieder possesst wird,
        // kann eure Possess-Logik das wieder TRUE setzen.
        // (Hier NICHT erzwingen, sonst schießt ihr euch beim Enable/Disable von NPCs ins Knie.)
        // if (thirdPersonController) thirdPersonController.UseMoveToDriveCameraYaw = true;

        if (debugLogs) Debug.Log($"[NPCWander] Disabled on '{name}'");
    }

    private void Update()
    {
        if (!inputs) return;

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
            _repickTimer -= Time.deltaTime;

            if (avoidObstacles && IsBlocked(_worldDir))
            {
                if (debugLogs) Debug.Log($"[NPCWander] '{name}' blocked -> new direction");
                PickNewDirection();
            }
            else if (_repickTimer <= 0f)
            {
                // Sanfter refresh, verhindert langes "an einer Stelle drehen"
                PickNewDirection();
            }

            Vector2 move = WorldDirToInput_Local(transform, _worldDir, walkInputMagnitude);

            // deadzone gegen micro jitter
            if (move.sqrMagnitude < inputDeadzone * inputDeadzone)
                move = Vector2.zero;

            inputs.MoveInput(move);

            // NPCs nutzen keinen Look
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

        _repickTimer = Mathf.Max(0.1f, repickDirectionEverySeconds);
    }

    private bool IsBlocked(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * 0.25f;
        return Physics.Raycast(origin, dir, obstacleCheckDistance, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// ✅ Stabil: Welt-Richtung -> NPC Local Input (unabhängig von MainCamera).
    /// Das verhindert Camera-Yaw Feedback-Loops komplett.
    /// </summary>
    private static Vector2 WorldDirToInput_Local(Transform npc, Vector3 worldDir, float magnitude)
    {
        Vector3 local = npc.InverseTransformDirection(worldDir);
        Vector2 v = new Vector2(local.x, local.z);

        float mag = v.magnitude;
        if (mag > 1e-5f)
            v /= mag;

        return v * Mathf.Clamp01(magnitude);
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