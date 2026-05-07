using UnityEngine;
using Cinemachine;

public class CinemachineFollowCurrentPlayer : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public CinemachineVirtualCamera vcam;

    [Header("Fallback")]
    public bool fallbackToCharacterTransform = true;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!vcam) vcam = GetComponent<CinemachineVirtualCamera>();
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;
        ApplyCurrent();
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        Apply(to);
    }

    void ApplyCurrent()
    {
        if (swapManager != null && swapManager.current != null)
            Apply(swapManager.current);
    }

    void Apply(PossessableCharacter c)
    {
        if (!vcam || !c) return;

        Transform target = c.cameraTarget;

        if (!target && fallbackToCharacterTransform)
            target = c.transform;

        if (!target) return;

        vcam.Follow = target;
        vcam.LookAt = target;
    }
}