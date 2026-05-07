using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

public class PossessableCharacter : MonoBehaviour
{
    public static readonly List<PossessableCharacter> ValidCharacters = new List<PossessableCharacter>();

    [Header("Auto-References (optional)")]
    public ThirdPersonController thirdPersonController;
    public StarterAssetsInputs inputs;
    public BasicRigidBodyPush rigidBodyPush;

#if ENABLE_INPUT_SYSTEM
    public PlayerInput playerInput;
#endif

    [Header("AI (NPC Wander)")]
    [Tooltip("Wenn gesetzt, übernimmt dieses Script das ruhige Random-Wandern, sobald der Charakter NICHT kontrolliert wird.")]
    public NPCWander wander;

    [Header("Camera Target")]
    public Transform cameraTarget;

    [Header("Perception (PostFX Profiles)")]
    public UnityEngine.Rendering.VolumeProfile possessedPerceptionProfile;
    public UnityEngine.Rendering.VolumeProfile proximityAuraProfile;
    public float proximityAuraRadiusOverride = 0f;
    [Range(0f, 1f)] public float proximityAuraMaxWeight = 1f;

    [Header("Proximity Reaction (Stop + LookAt)")]
    public bool enableProximityStopLook = true;
    public float proximityStopRadius = 3.0f;
    public float proximityTurnSpeed = 360f;
    public float proximityLookHeight = 1.6f;

    [Tooltip("Optional: Wenn gesetzt, nutzt dieses Script diesen SwapManager statt FindObjectOfType.")]
    public PerspectiveSwapManager swapManager;

    // ============================================================
    // Movement Speeds (Player vs NPC)
    // ============================================================
    [Header("Movement Speeds")]
    public float playerMoveSpeed = 4.5f;
    public float playerSprintSpeed = 6.0f;
    public float npcMoveSpeed = 2.2f;
    public float npcSprintSpeed = 3.0f;
    public bool applySpeedsOnControlChange = true;

    // ============================================================
    // Ambient Loop (per character)
    // ============================================================
    [Header("Ambient Loop (per character)")]
    public AudioClip ambientLoop;

    [Range(0f, 1f)]
    public float ambientVolume = 0.10f;

    [Range(0.8f, 1.2f)]
    public float ambientPitch = 1.0f;

    [Tooltip("Optional: Wenn leer, wird automatisch ein 2D AudioSource angelegt.")]
    public AudioSource ambientSource;

    [Tooltip("✅ Wenn true: Ambient läuft NUR beim kontrollierten Character (switcht clean).")]
    public bool ambientOnlyWhenControlled = true;

    [Header("Debug")]
    public bool debugLogs = true;

    public bool IsValid { get; private set; }

    bool _isControlled = false;
    bool _isProximityFrozen = false;

    // ✅ NEW: External freeze (e.g., GateFreeze). Independent from distance logic.
    bool _externalFrozen = false;
    string _externalFreezeReason = "";

    bool IsEffectivelyFrozen => _isProximityFrozen || _externalFrozen;

    void Awake()
    {
        if (!thirdPersonController) thirdPersonController = GetComponent<ThirdPersonController>();
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
        if (!rigidBodyPush) rigidBodyPush = GetComponent<BasicRigidBodyPush>();
        if (!wander) wander = GetComponent<NPCWander>();

#if ENABLE_INPUT_SYSTEM
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
#endif

        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();

        if (!cameraTarget && thirdPersonController && thirdPersonController.CinemachineCameraTarget != null)
            cameraTarget = thirdPersonController.CinemachineCameraTarget.transform;

        IsValid =
            thirdPersonController != null &&
            inputs != null &&
#if ENABLE_INPUT_SYSTEM
            playerInput != null &&
#endif
            cameraTarget != null;

        EnsureAmbientSource();

        if (debugLogs)
        {
            Debug.Log(
                $"[Possessable] Awake '{name}' | Valid={IsValid} | " +
                $"TPC={(thirdPersonController ? "OK" : "NULL")} | " +
                $"Inputs={(inputs ? "OK" : "NULL")} | " +
                $"PlayerInput={(playerInput ? "OK" : "NULL")} | " +
                $"Wander={(wander ? "OK" : "NULL")} | " +
                $"SwapManager={(swapManager ? swapManager.name : "NULL")} | " +
                $"CamTarget={(cameraTarget ? cameraTarget.name : "NULL")}"
            );
        }
    }

    void EnsureAmbientSource()
    {
        if (ambientSource == null)
        {
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f; // 2D
        }
    }

    void Start()
    {
        ApplyAmbientSettings();

        // ✅ WICHTIG: nicht automatisch überall starten.
        // Ambient startet/stoppt über SetControlled(), damit es wirklich "wechselt".
        if (!ambientOnlyWhenControlled)
        {
            TryStartAmbient();
        }
        else
        {
            StopAmbient();
        }

        if (applySpeedsOnControlChange)
            ApplyMovementSpeeds(_isControlled);
    }

    void ApplyAmbientSettings()
    {
        if (!ambientSource) return;

        ambientSource.volume = ambientVolume;
        ambientSource.pitch = ambientPitch;

        if (ambientLoop != null && ambientSource.clip != ambientLoop)
            ambientSource.clip = ambientLoop;
    }

    void TryStartAmbient()
    {
        if (!ambientSource) return;
        if (ambientSource.clip == null) return;

        if (!ambientSource.isPlaying)
            ambientSource.Play();
    }

    void StopAmbient()
    {
        if (!ambientSource) return;
        if (ambientSource.isPlaying)
            ambientSource.Stop();
    }

    void OnEnable()
    {
        if (IsValid && !ValidCharacters.Contains(this))
        {
            ValidCharacters.Add(this);
            if (debugLogs) Debug.Log($"[Possessable] Registered VALID '{name}'. Total={ValidCharacters.Count}");
        }
    }

    void OnDisable()
    {
        if (ValidCharacters.Contains(this))
        {
            ValidCharacters.Remove(this);
            if (debugLogs) Debug.Log($"[Possessable] Unregistered '{name}'. Total={ValidCharacters.Count}");
        }
    }

    void Update()
    {
        if (!IsValid) return;

        // Live Inspector Änderungen übernehmen
        ApplyAmbientSettings();

        // Proximity Freeze nur für NPCs (nicht controlled)
        if (!enableProximityStopLook || _isControlled || proximityStopRadius <= 0f)
        {
            // ✅ only release proximity-freeze (not external freeze)
            if (_isProximityFrozen) SetProximityFrozen(false);
            ApplyWanderState();
            return;
        }

        if (!swapManager || swapManager.current == null) return;

        if (swapManager.current == this)
        {
            if (_isProximityFrozen) SetProximityFrozen(false);
            ApplyWanderState();
            return;
        }

        float dist = Vector3.Distance(transform.position, swapManager.current.transform.position);
        bool shouldFreeze = dist <= proximityStopRadius;

        if (shouldFreeze != _isProximityFrozen)
            SetProximityFrozen(shouldFreeze);

        if (_isProximityFrozen)
            RotateTowardsActive();
    }

    void RotateTowardsActive()
    {
        if (!swapManager || swapManager.current == null) return;

        Vector3 activePos = swapManager.current.transform.position + Vector3.up * proximityLookHeight;
        Vector3 dir = activePos - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, proximityTurnSpeed * Time.deltaTime);
    }

    void SetProximityFrozen(bool frozen)
    {
        _isProximityFrozen = frozen;

        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        ApplyWanderState();

        if (debugLogs)
            Debug.Log($"[Possessable] ProximityFrozen({frozen}) -> '{name}' | wander={(wander ? (wander.enabled ? "ON" : "OFF") : "NULL")}");
    }

    void ApplyWanderState()
    {
        if (wander)
            wander.enabled = (!_isControlled && !IsEffectivelyFrozen);
    }

    void ApplyMovementSpeeds(bool controlled)
    {
        if (!thirdPersonController) return;

        if (controlled)
        {
            thirdPersonController.MoveSpeed = playerMoveSpeed;
            thirdPersonController.SprintSpeed = playerSprintSpeed;
        }
        else
        {
            thirdPersonController.MoveSpeed = npcMoveSpeed;
            thirdPersonController.SprintSpeed = npcSprintSpeed;
        }

        if (debugLogs)
        {
            Debug.Log($"[Possessable] ApplyMovementSpeeds -> '{name}' controlled={controlled} MoveSpeed={thirdPersonController.MoveSpeed:0.00} SprintSpeed={thirdPersonController.SprintSpeed:0.00}");
        }
    }

    /// <summary>
    /// ✅ External freeze (e.g., GateFreeze). This does NOT depend on distance logic.
    /// When frozen: NPC wander is OFF, even if proximity logic would unfreeze.
    /// </summary>
    public void SetExternalFrozen(bool frozen, string reason = "external")
    {
        _externalFrozen = frozen;
        _externalFreezeReason = frozen ? reason : "";

        // safe: stop residual inputs
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        ApplyWanderState();

        if (debugLogs)
            Debug.Log($"[Possessable] ExternalFrozen({frozen}) reason='{_externalFreezeReason}' -> '{name}' | wander={(wander ? (wander.enabled ? "ON" : "OFF") : "NULL")}");
    }

    /// <summary>
    /// controlled=true  -> Player steuert
    /// controlled=false -> NPC wandert (außer proximity/external freeze)
    /// </summary>
    public void SetControlled(bool controlled)
    {
        if (!IsValid)
        {
            if (debugLogs) Debug.LogWarning($"[Possessable] SetControlled ignored (invalid) -> '{name}'");
            return;
        }

        _isControlled = controlled;

        if (_isControlled && _isProximityFrozen)
            _isProximityFrozen = false;

        if (debugLogs) Debug.Log($"[Possessable] SetControlled({controlled}) -> '{name}'");

#if ENABLE_INPUT_SYSTEM
        if (playerInput) playerInput.enabled = controlled;
#endif

        if (thirdPersonController) thirdPersonController.enabled = true;
        if (inputs) inputs.enabled = true;

        if (rigidBodyPush)
        {
            rigidBodyPush.enabled = controlled;
            rigidBodyPush.canPush = controlled;
        }

        if (applySpeedsOnControlChange)
            ApplyMovementSpeeds(controlled);

        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        ApplyWanderState();

        // ✅ Ambient switching logic:
        if (ambientOnlyWhenControlled)
        {
            if (controlled) TryStartAmbient();
            else StopAmbient();
        }
        else
        {
            // old behavior: everyone plays
            TryStartAmbient();
        }
    }
}