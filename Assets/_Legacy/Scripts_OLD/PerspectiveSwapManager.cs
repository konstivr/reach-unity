using UnityEngine;
using Cinemachine;
using System;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PerspectiveSwapManager : MonoBehaviour
{
    [Header("Cinemachine")]
    public CinemachineVirtualCamera vcam;

    [Header("Active Character")]
    public PossessableCharacter current;

    [Header("Progress (Chronological)")]
    public int maxPerspectives = 4;

    [Header("Debug")]
    public bool debugLogs = true;

    [Header("Legacy / FX")]
    [Tooltip("Legacy Radius for proximity FX scripts (e.g., ProximityScreenFX).")]
    public float interactRadius = 2.5f;

    public event Action<PossessableCharacter, PossessableCharacter> Switched;
    public event Action<int, int, float> ProgressChanged;

    readonly HashSet<PossessableCharacter> _visited = new HashSet<PossessableCharacter>();
    public int EnteredCount => _visited.Count;

    // ✅ BACK: required by DialogueManager.cs
    public DialogueAgent CurrentAgent
    {
        get
        {
            if (current == null) return null;
            return current.GetComponentInChildren<DialogueAgent>();
        }
    }

    public bool IsComplete => EnteredCount >= maxPerspectives;

    public float Progress01
    {
        get
        {
            if (maxPerspectives <= 1) return 1f;
            return Mathf.Clamp01((EnteredCount - 1f) / (maxPerspectives - 1f));
        }
    }

    void Awake()
    {
        if (!vcam) vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (debugLogs) Debug.Log($"[Swap] Awake | vcam={(vcam ? vcam.name : "NULL")} current={(current ? current.name : "NULL")}");
    }

    void Start()
    {
        // 1) ALLE deaktivieren
        foreach (var p in PossessableCharacter.ValidCharacters)
        {
            if (p != null && p.IsValid)
                SetCharacterControlled(p, false, resetInput: true);
        }

        // 2) current aktivieren
        if (current != null && current.IsValid)
        {
            SetCharacterControlled(current, true, resetInput: true);
            ApplyCameraTarget(current);

            MarkVisited(current);
            FireProgressChanged();

            if (debugLogs)
                Debug.Log($"[Swap] Start ACTIVE '{current.name}' | visited={EnteredCount}/{maxPerspectives} progress={Progress01:0.00}");
        }
        else
        {
            Debug.LogWarning("[Swap] current ist NULL/invalid.");
        }
    }

    public bool HasVisited(PossessableCharacter p) => p != null && _visited.Contains(p);

    public bool TrySwitchTo(PossessableCharacter next)
    {
        if (next == null || !next.IsValid) return false;
        if (current == null || !current.IsValid) return false;

        if (_visited.Contains(next))
        {
            if (debugLogs) Debug.Log($"[Swap] '{next.name}' already visited -> BLOCKED.");
            return false;
        }

        if (EnteredCount >= maxPerspectives)
        {
            if (debugLogs) Debug.Log("[Swap] All perspectives visited -> BLOCKED.");
            return false;
        }

        var prev = current;

        // 1) prev deaktivieren + input resetten
        SetCharacterControlled(prev, false, resetInput: true);

        // 2) current setzen
        current = next;

        // 3) next aktivieren + input resetten
        SetCharacterControlled(current, true, resetInput: true);

        // 4) camera
        ApplyCameraTarget(current);

        // 5) progress
        MarkVisited(current);
        FireProgressChanged();

        // 6) notify listeners
        Switched?.Invoke(prev, current);

        if (debugLogs)
            Debug.Log($"[Swap] Switched '{prev.name}' -> '{current.name}' | visited={EnteredCount}/{maxPerspectives} progress={Progress01:0.00}");

        return true;
    }

    void SetCharacterControlled(PossessableCharacter p, bool controlled, bool resetInput)
    {
        if (p == null || !p.IsValid) return;

        // A) eure Logik (NPCWander usw.)
        p.SetControlled(controlled);

#if ENABLE_INPUT_SYSTEM
        // B) InputSystem: sauber aktiv/deaktiv
        var pi = p.GetComponent<PlayerInput>();
        if (pi != null)
        {
            if (controlled)
            {
                pi.enabled = true;
                pi.ActivateInput();
            }
            else
            {
                pi.DeactivateInput();
                pi.enabled = false;
            }
        }
#endif

        // C) Input reset
        if (resetInput && p.inputs != null)
        {
            p.inputs.MoveInput(Vector2.zero);
            p.inputs.LookInput(Vector2.zero);
            p.inputs.JumpInput(false);
            p.inputs.SprintInput(false);

            p.inputs.dialogueConfirmHeld = false;
            p.inputs.dialogueConfirmDown = false;
            p.inputs.dialogueConfirmUp = false;

            p.inputs.interact = false;
            p.inputs.dialogueStart = false;
            p.inputs.dialogueCancel = false;
            p.inputs.menu = false;
        }
    }

    void MarkVisited(PossessableCharacter p)
    {
        if (!p) return;
        _visited.Add(p);
    }

    void FireProgressChanged()
    {
        ProgressChanged?.Invoke(EnteredCount, maxPerspectives, Progress01);
    }

    void ApplyCameraTarget(PossessableCharacter p)
    {
        if (!vcam)
        {
            Debug.LogWarning("[Swap] vcam NULL.");
            return;
        }

        if (p == null || p.cameraTarget == null)
        {
            Debug.LogWarning("[Swap] cameraTarget NULL.");
            return;
        }

        vcam.Follow = p.cameraTarget;
        vcam.LookAt = p.cameraTarget;
    }
}