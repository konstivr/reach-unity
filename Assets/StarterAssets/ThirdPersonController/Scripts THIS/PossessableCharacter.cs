using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using StarterAssets;

public class PossessableCharacter : MonoBehaviour
{
    // Registry aller validen spielbaren Characters
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
    [Tooltip("Wenn true: NPC stoppt und schaut den aktiven Player an, sobald dieser in Reichweite ist.")]
    public bool enableProximityStopLook = true;

    [Tooltip("Radius, in dem der NPC stehen bleibt und schaut. 0 = aus (oder enable=false).")]
    public float proximityStopRadius = 3.0f;

    [Tooltip("Wie schnell er sich dreht (Grad/Sek).")]
    public float proximityTurnSpeed = 360f;

    [Tooltip("Blickpunkt-Höhe (z.B. 1.6 = Kopf).")]
    public float proximityLookHeight = 1.6f;

    [Tooltip("Optional: Wenn gesetzt, nutzt dieses Script diesen SwapManager statt FindObjectOfType.")]
    public PerspectiveSwapManager swapManager;

    // ============================================================
    // Ambient Loop (per character)
    // ============================================================
    [Header("Ambient Loop (per character)")]
    public AudioClip ambientLoop;

    [Range(0f, 1f)]
    public float ambientVolume = 0.25f;

    [Tooltip("Optional: kleine Random-Pitch-Variation")]
    [Range(0.8f, 1.2f)]
    public float ambientPitch = 1.0f;

    [Tooltip("Optional: Wenn leer, wird automatisch ein 2D AudioSource angelegt.")]
    public AudioSource ambientSource;

    [Header("Debug")]
    public bool debugLogs = true;

    public bool IsValid { get; private set; }

    // internal state
    bool _isControlled = false;
    bool _isProximityFrozen = false;

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
                $"CamTarget={(cameraTarget ? cameraTarget.name : "NULL")} | " +
                $"Ambient={(ambientLoop ? ambientLoop.name : "None")}"
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
            ambientSource.volume = ambientVolume;
            ambientSource.pitch = ambientPitch;
        }
    }

    void Start()
    {
        // Ambient starten (läuft dauerhaft leise im Hintergrund)
        ApplyAmbientSettings();
        TryStartAmbient();
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

        // Falls du Werte zur Laufzeit im Inspector änderst:
        ApplyAmbientSettings();
        TryStartAmbient();

        // Proximity Freeze nur für NPCs (nicht kontrolliert) und nur wenn enabled
        if (!enableProximityStopLook || _isControlled || proximityStopRadius <= 0f)
        {
            if (_isProximityFrozen) SetProximityFrozen(false);
            return;
        }

        if (!swapManager || swapManager.current == null) return;

        // Wenn dieser Character selbst der aktuell kontrollierte ist: niemals einfrieren
        if (swapManager.current == this)
        {
            if (_isProximityFrozen) SetProximityFrozen(false);
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

        // SUPER wichtig: Move Input aktiv auf 0 setzen, damit nichts weiter "driftet"
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        // Wander nur aktiv, wenn NPC und NICHT frozen
        ApplyWanderState();

        if (debugLogs)
            Debug.Log($"[Possessable] ProximityFrozen({frozen}) -> '{name}' | wander={(wander ? (wander.enabled ? "ON" : "OFF") : "NULL")}");
    }

    void ApplyWanderState()
    {
        // wander nur an wenn: nicht controlled UND nicht proximityFrozen
        if (wander)
            wander.enabled = (!_isControlled && !_isProximityFrozen);
    }

    /// <summary>
    /// controlled=true  -> Player steuert (PlayerInput an, NPCWander aus)
    /// controlled=false -> NPC läuft ruhig random (PlayerInput aus, NPCWander an) – außer proximity freeze
    /// </summary>
    public void SetControlled(bool controlled)
    {
        if (!IsValid)
        {
            if (debugLogs) Debug.LogWarning($"[Possessable] SetControlled ignored (invalid) -> '{name}'");
            return;
        }

        _isControlled = controlled;

        // Sobald wir kontrolliert werden: Proximity Freeze aus
        if (_isControlled && _isProximityFrozen)
            _isProximityFrozen = false;

        if (debugLogs) Debug.Log($"[Possessable] SetControlled({controlled}) -> '{name}'");

#if ENABLE_INPUT_SYSTEM
        if (playerInput) playerInput.enabled = controlled;
#endif

        // Inputs + Controller müssen für AI UND Player aktiv bleiben
        if (thirdPersonController) thirdPersonController.enabled = true;
        if (inputs) inputs.enabled = true;

        // Push nur beim kontrollierten Character
        if (rigidBodyPush)
        {
            rigidBodyPush.enabled = controlled;
            rigidBodyPush.canPush = controlled;
        }

        // Safety: keine „hängenden“ Inputs beim Wechsel
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }

        ApplyWanderState();
    }
}