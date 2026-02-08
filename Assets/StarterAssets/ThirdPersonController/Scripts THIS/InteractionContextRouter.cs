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
        _wasInObjRange = false;
        if (hud) hud.SetIdlePerspective(); // nach Perspektivenwechsel “Talk to me”
    }

    void Update()
    {
        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;

        // Transition blockt UI-Routing (FX übernimmt)
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

        var current = swapManager.current;
        var inputs = current.inputs;

        // 1) Nearest assigned object finden
        _nearestObj = FindNearestAssignedObject(current);

        bool inObj = _nearestObj != null && _nearestObj.IsInRange(current);
        bool inGateRange = gate != null && gate.HasGateTargetInRange;

        // Wenn man einen Objekt-Radius verlässt: sticky zurücksetzen + obj local state reset
        if (_wasInObjRange && !inObj)
        {
            if (_nearestObj != null) _nearestObj.ResetLocalStateOnExit();
            if (hud) hud.ClearSticky();
        }
        _wasInObjRange = inObj;

        // 2) HUD setzen (nur wenn nicht Sticky/FX locked)
        if (hud != null && !hud.IsLockedByFX && !hud.IsSticky)
        {
            if (inObj)
                hud.SetPrompt(_nearestObj.GetPrompt().Length > 0 ? _nearestObj.GetPrompt() : promptObject);
            else if (inGateRange)
                hud.SetPrompt(promptReachOut);
            else
                hud.SetIdleAuto();
        }

        // 3) Input F routing (dialogueStart)
        if (inputs.dialogueStart)
        {
            // a) Object hat Vorrang
            if (inObj)
            {
                _nearestObj.HandleInputF(hud);
                return;
            }

            // b) Gate handled F selbst (StartGateFor)
            // -> kein extra call nötig
        }
    }

    PlayerAssignedWorldObject FindNearestAssignedObject(PossessableCharacter current)
    {
        var all = FindObjectsOfType<PlayerAssignedWorldObject>();
        PlayerAssignedWorldObject best = null;
        float bestSqr = float.MaxValue;

        foreach (var o in all)
        {
            if (!o || o.IsCompleted) continue;
            if (!o.assignedTo || o.assignedTo != current) continue;

            float dSqr = (o.transform.position - current.transform.position).sqrMagnitude;
            if (dSqr <= maxObjectScanRadius * maxObjectScanRadius && dSqr < bestSqr)
            {
                // und wirklich in range?
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