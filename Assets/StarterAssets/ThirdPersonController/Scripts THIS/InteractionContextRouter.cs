using UnityEngine;

public class InteractionContextRouter : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public InteractionGateProximity gate;
    public HUDText hud;

    [Header("Prompts")]
    [TextArea(1, 3)]
    public string promptReachOut = "Press input to reach out";

    [TextArea(1, 3)]
    public string promptObject = "Press input";

    [Header("Scan")]
    public float maxObjectScanRadius = 3.0f;

    PlayerAssignedWorldObject _nearestObj;
    PlayerAssignedWorldObject _lastObjInRange;
    bool _wasInObjRange = false;

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
        _nearestObj = null;
        _lastObjInRange = null;
        _wasInObjRange = false;
        if (hud) hud.SetIdlePerspective();
    }

    void Update()
    {
        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;

        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

        var current = swapManager.current;
        var inputs = current.inputs;

        _nearestObj = FindNearestAssignedObject(current);

        bool inObj = _nearestObj != null && _nearestObj.IsInRange(current);
        bool inGateRange = gate != null && gate.HasGateTargetInRange;

        // leaving object zone: use LAST object that was in range
        if (_wasInObjRange && !inObj)
        {
            if (_lastObjInRange != null)
                _lastObjInRange.ResetLocalStateOnExit();

            if (hud) hud.ClearSticky();
            _lastObjInRange = null;
        }

        if (inObj)
            _lastObjInRange = _nearestObj;

        _wasInObjRange = inObj;

        // HUD (if free)
        if (hud != null && !hud.IsLockedByFX && !hud.IsSticky)
        {
            if (inObj)
                hud.SetPrompt((_nearestObj.GetPrompt().Length > 0) ? _nearestObj.GetPrompt() : promptObject);
            else if (inGateRange)
                hud.SetPrompt(promptReachOut);
            else
                hud.SetIdleAuto();
        }

        // ✅ IMPORTANT: use EDGE
        if (!inputs.dialogueStart) return;

        // a) Object has priority
        if (inObj)
        {
            _nearestObj.HandleInputF(hud);
            return;
        }

        // b) Gate handles it internally (InteractionGateProximity)
    }

    PlayerAssignedWorldObject FindNearestAssignedObject(PossessableCharacter current)
    {
        var all = FindObjectsOfType<PlayerAssignedWorldObject>();
        PlayerAssignedWorldObject best = null;
        float bestSqr = float.MaxValue;

        float rSqr = maxObjectScanRadius * maxObjectScanRadius;

        foreach (var o in all)
        {
            if (!o || o.IsCompleted) continue;
            if (!o.assignedTo || o.assignedTo != current) continue;

            float dSqr = (o.transform.position - current.transform.position).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestSqr)
            {
                if (o.IsInRange(current))
                {
                    bestSqr = dSqr;
                    best = o;
                }
            }
        }
        return best;
    }
}