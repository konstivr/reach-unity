using UnityEngine;
using Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;

public class PerspectiveSwapManager : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineVirtualCamera vcam;

    [Header("Active Character")]
    public PossessableCharacter current;

    [Header("Interaction")]
    public float interactRadius = 2.0f;

    [Header("Debug")]
    public bool debugLogs = false;

    // Event: (from, to)
    public event Action<PossessableCharacter, PossessableCharacter> Switched;

    private void Awake()
    {
        if (!vcam) vcam = FindObjectOfType<CinemachineVirtualCamera>();

        if (debugLogs)
            Debug.Log($"[Swap] Awake | vcam={(vcam ? vcam.name : "NULL")} | current={(current ? current.name : "NULL")}");
    }

    private void Start()
    {
        if (PossessableCharacter.ValidCharacters.Count == 0 && debugLogs)
            Debug.LogWarning("[Swap] ValidCharacters ist leer. Prüfe: PossessableCharacter ist auf allen Player-Roots aktiv und enabled.");

        // alle validen deaktivieren
        foreach (var p in PossessableCharacter.ValidCharacters)
            if (p != null && p.IsValid) p.SetControlled(false);

        // current aktivieren
        if (current != null && current.IsValid)
        {
            current.SetControlled(true);
            ApplyCameraTarget(current);

            if (debugLogs)
                Debug.Log($"[Swap] Current ACTIVE: '{current.name}'");
        }
        else
        {
            Debug.LogWarning("[Swap] current ist NULL oder invalid (kein TPC/Inputs/PlayerInput/CamTarget).");
        }
    }

    private void Update()
    {
        if (current == null || !current.IsValid) return;

        bool pressed = false;

        // 1) über Input Action (Interact)
        if (current.inputs != null && current.inputs.interact)
            pressed = true;

#if ENABLE_INPUT_SYSTEM
        // 2) fallback direkt über Keyboard
        if (!pressed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            pressed = true;
#endif

        if (!pressed) return;

        var target = FindNearestValidTarget();
        if (target == null)
        {
            if (debugLogs) Debug.Log("[Swap] Interact gedrückt, aber kein Target im Radius gefunden.");
            return;
        }

        SwitchTo(target);
    }

    private PossessableCharacter FindNearestValidTarget()
    {
        PossessableCharacter best = null;
        float bestDistSqr = float.MaxValue;

        Vector3 pos = current.transform.position;
        float rSqr = interactRadius * interactRadius;

        foreach (var p in PossessableCharacter.ValidCharacters)
        {
            if (p == null || !p.IsValid) continue;
            if (p == current) continue;

            float dSqr = (p.transform.position - pos).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = p;
            }
        }

        return best;
    }

    private void SwitchTo(PossessableCharacter next)
    {
        if (next == null || !next.IsValid) return;

        var prev = current;

        current.SetControlled(false);
        next.SetControlled(true);

        current = next;
        ApplyCameraTarget(current);

        Switched?.Invoke(prev, current);

        if (debugLogs)
            Debug.Log($"[Swap] Switched '{prev.name}' -> '{current.name}'");
    }

    private void ApplyCameraTarget(PossessableCharacter p)
    {
        if (!vcam)
        {
            Debug.LogWarning("[Swap] vcam ist NULL. Zieh deine CinemachineVirtualCamera in das Feld oder stell sicher, dass es genau eine in der Szene gibt.");
            return;
        }

        if (p == null || p.cameraTarget == null)
        {
            Debug.LogWarning("[Swap] cameraTarget ist NULL. Setze bei jedem Player cameraTarget (meist ThirdPersonController.CinemachineCameraTarget).");
            return;
        }

        vcam.Follow = p.cameraTarget;
        vcam.LookAt = p.cameraTarget;
    }

    private void OnDrawGizmosSelected()
    {
        if (current == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(current.transform.position, interactRadius);
    }
}
