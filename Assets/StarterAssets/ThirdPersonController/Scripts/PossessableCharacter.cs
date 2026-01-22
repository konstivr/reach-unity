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

    [Header("Debug")]
    public bool debugLogs = true;

    public bool IsValid { get; private set; }

    private void Awake()
    {
        if (!thirdPersonController) thirdPersonController = GetComponent<ThirdPersonController>();
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
        if (!rigidBodyPush) rigidBodyPush = GetComponent<BasicRigidBodyPush>();
        if (!wander) wander = GetComponent<NPCWander>();

#if ENABLE_INPUT_SYSTEM
        if (!playerInput) playerInput = GetComponent<PlayerInput>();
#endif

        if (!cameraTarget && thirdPersonController && thirdPersonController.CinemachineCameraTarget != null)
            cameraTarget = thirdPersonController.CinemachineCameraTarget.transform;

        IsValid =
            thirdPersonController != null &&
            inputs != null &&
#if ENABLE_INPUT_SYSTEM
            playerInput != null &&
#endif
            cameraTarget != null;

        if (debugLogs)
        {
            Debug.Log($"[Possessable] Awake '{name}' | Valid={IsValid} | " +
                      $"TPC={(thirdPersonController ? "OK" : "NULL")} | " +
                      $"Inputs={(inputs ? "OK" : "NULL")} | " +
                      $"PlayerInput={(playerInput ? "OK" : "NULL")} | " +
                      $"Wander={(wander ? "OK" : "NULL")} | " +
                      $"CamTarget={(cameraTarget ? cameraTarget.name : "NULL")}");
        }
    }

    private void OnEnable()
    {
        if (IsValid && !ValidCharacters.Contains(this))
        {
            ValidCharacters.Add(this);
            if (debugLogs) Debug.Log($"[Possessable] Registered VALID '{name}'. Total={ValidCharacters.Count}");
        }
    }

    private void OnDisable()
    {
        if (ValidCharacters.Contains(this))
        {
            ValidCharacters.Remove(this);
            if (debugLogs) Debug.Log($"[Possessable] Unregistered '{name}'. Total={ValidCharacters.Count}");
        }
    }

    /// <summary>
    /// controlled=true  -> Player steuert (PlayerInput an, NPCWander aus)
    /// controlled=false -> NPC läuft ruhig random (PlayerInput aus, NPCWander an)
    /// </summary>
    public void SetControlled(bool controlled)
    {
        if (!IsValid)
        {
            if (debugLogs) Debug.LogWarning($"[Possessable] SetControlled ignored (invalid) -> '{name}'");
            return;
        }

        if (debugLogs) Debug.Log($"[Possessable] SetControlled({controlled}) -> '{name}'");

        // PlayerInput nur beim kontrollierten Character aktiv
#if ENABLE_INPUT_SYSTEM
        if (playerInput) playerInput.enabled = controlled;
#endif

        // Inputs + Controller müssen für AI UND Player aktiv bleiben
        if (thirdPersonController) thirdPersonController.enabled = true;
        if (inputs) inputs.enabled = true;

        // Push nur beim kontrollierten Character (sonst schubsen NPCs alles weg)
        if (rigidBodyPush)
        {
            rigidBodyPush.enabled = controlled;
            rigidBodyPush.canPush = controlled;
        }

        // AI an/aus
        if (wander) wander.enabled = !controlled;

        // Safety: keine „hängenden“ Inputs beim Wechsel
        if (inputs)
        {
            inputs.MoveInput(Vector2.zero);
            inputs.LookInput(Vector2.zero);
            inputs.SprintInput(false);
            inputs.JumpInput(false);
        }
    }
}
