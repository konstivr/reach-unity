using UnityEngine;
using Cinemachine;
using System;
using System.Collections.Generic;

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
    public float interactRadius = 2.5f; // nur für ProximityScreenFX/Visuals

    public event Action<PossessableCharacter, PossessableCharacter> Switched;
    public event Action<int, int, float> ProgressChanged;

    readonly HashSet<PossessableCharacter> _visited = new HashSet<PossessableCharacter>();
    public int EnteredCount => _visited.Count;

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
        foreach (var p in PossessableCharacter.ValidCharacters)
            if (p != null && p.IsValid) p.SetControlled(false);

        if (current != null && current.IsValid)
        {
            current.SetControlled(true);
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

        prev.SetControlled(false);
        next.SetControlled(true);

        current = next;
        ApplyCameraTarget(current);

        MarkVisited(current);
        FireProgressChanged();

        Switched?.Invoke(prev, current);

        if (debugLogs)
            Debug.Log($"[Swap] Switched '{prev.name}' -> '{current.name}' | visited={EnteredCount}/{maxPerspectives} progress={Progress01:0.00}");

        return true;
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