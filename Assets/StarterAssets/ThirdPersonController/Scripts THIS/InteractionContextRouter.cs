using UnityEngine;

public class InteractionContextRouter : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public InteractionGateProximity gate;
    public HUDText hud;

    [Header("Prompts")]
    [TextArea(1, 3)]
    public string promptReachOut = "Press Interact to reach out";

    [TextArea(1, 3)]
    public string promptObject = "Press Interact";

    [TextArea(1, 3)]
    public string promptGateWaiting = "Press Speak";

    [Header("Scan")]
    public float maxObjectScanRadius = 3.0f;

    WorldTaskInteractable _nearestTask;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!gate) gate = FindObjectOfType<InteractionGateProximity>();
        if (!hud) hud = FindObjectOfType<HUDText>();
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        _nearestTask = null;
        if (hud) hud.SetIdlePerspective();
    }

    void Update()
    {
        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning) return;

        var current = swapManager.current;
        var inputs = current.inputs;

        _nearestTask = FindNearestTask(current);

        bool inTaskRange = _nearestTask != null && _nearestTask.IsInRange(current);

        bool gateHasTargetInRange = gate != null && gate.HasGateTargetInRange;
        bool gateWaiting = gate != null && gate.IsWaitingForPassphrase;
        bool gateBusy = gate != null && gate.IsGateBusy; // includes waiting + tts playing (as implemented in your gate)

        // -------------------------
        // HUD (only if free)
        // -------------------------
        if (hud != null)
        {
            if (!hud.IsLockedByFX && !hud.IsSticky && !hud.IsTimedLocked && !hud.IsIntroRunning)
            {
                if (inTaskRange)
                {
                    string p = _nearestTask.GetPrompt();
                    hud.SetPrompt(!string.IsNullOrEmpty(p) ? p : promptObject);
                }
                else if (gateWaiting)
                {
                    hud.SetPrompt(promptGateWaiting);
                }
                else if (gateBusy)
                {
                    hud.SetPrompt("...");
                }
                else if (gateHasTargetInRange)
                {
                    hud.SetPrompt(promptReachOut);
                }
                else
                {
                    hud.SetIdleAuto();
                }
            }
        }

        // -------------------------
        // Interact pressed?
        // -------------------------
        if (!inputs.interact) return;

        // 0) CRITICAL: If gate is WAITING -> do NOT cancel and do NOT restart.
        // Let SpeechInput consume/handle this press as "Speak".
        if (gate != null && gateWaiting)
        {
            return;
        }

        // 1) If gate is busy (but NOT waiting) -> Interact cancels gate (safety)
        if (gate != null && gateBusy)
        {
            gate.CancelGate();
            return;
        }

        // 2) Task priority
        if (inTaskRange)
        {
            bool consumed = _nearestTask.TryInteract(current, hud);
            if (consumed) return;
        }

        // 3) Gate trigger (reach out)
        if (gate != null && gateHasTargetInRange)
        {
            gate.TryTriggerGateFromInput();
            return;
        }

        // 4) nothing
    }

    WorldTaskInteractable FindNearestTask(PossessableCharacter current)
    {
        var all = FindObjectsOfType<WorldTaskInteractable>();
        WorldTaskInteractable best = null;
        float bestSqr = float.MaxValue;

        float rSqr = maxObjectScanRadius * maxObjectScanRadius;

        foreach (var t in all)
        {
            if (!t || t.IsCompleted || t.IsBusy) continue;
            if (t.ownerCharacter != null && t.ownerCharacter != current) continue;

            float dSqr = (t.transform.position - current.transform.position).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestSqr)
            {
                bestSqr = dSqr;
                best = t;
            }
        }
        return best;
    }
}