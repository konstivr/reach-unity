using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// Routes Interact button presses to the right system based on context:
    ///   - If an InteractableObject is in range → run that
    ///   - Else if a gate target is in range → trigger the gate
    ///   - Else: nothing
    ///
    /// Also drives proximity-based HUD prompts when the HUD is free.
    /// </summary>
    public class InteractionRouter : MonoBehaviour
    {
        [Header("Scan")]
        [Tooltip("Maximum scan radius for finding the nearest InteractableObject.")]
        public float maxObjectScanRadius = 3f;

        [Header("HUD Prompts (fallbacks)")]
        [TextArea(1, 3)] public string promptReachOut = "Press Interact to reach out";
        [TextArea(1, 3)] public string promptGateWaiting = "Press Speak.";
        [TextArea(1, 3)] public string promptObjectFallback = "Press Interact";

        [Header("Debug")]
        public bool debugLogs = false;

        InteractableObject _nearestObject;

        void Update()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            var input = ctx.Input;
            var pm = ctx.Perspective;
            var gate = ctx.Gate;
            var hud = ctx.Hud;

            if (input == null || pm == null || pm.Current == null) return;

            var current = pm.Current;

            // Update nearest object (only one matching the current character)
            _nearestObject = FindNearestObject(current);

            bool inObjectRange = _nearestObject != null && _nearestObject.IsInRange(current);
            bool gateHasTarget = gate != null && gate.HasTargetInRange;
            bool gateBusy      = gate != null && gate.IsGateBusy;
            bool gateWaiting   = gate != null && gate.IsWaitingForPassphrase;

            // ----- HUD prompts (only if HUD is free) -----
            if (hud != null && hud.IsFree)
            {
                if (inObjectRange)
                {
                    string p = _nearestObject.GetPrompt();
                    hud.SetPrompt(!string.IsNullOrEmpty(p) ? p : promptObjectFallback);
                }
                else if (gateWaiting)
                {
                    hud.SetPrompt(promptGateWaiting);
                }
                else if (gateBusy)
                {
                    hud.SetPrompt("...");
                }
                else if (gateHasTarget)
                {
                    hud.SetPrompt(promptReachOut);
                }
                else
                {
                    hud.SetIdleAuto();
                }
            }

            // ----- Interact press routing -----
            if (!input.InteractDown) return;

            // Critical: if gate is waiting for passphrase, do NOT cancel — that press belongs to SpeechInput.
            if (gate != null && gateWaiting)
                return;

            // If gate is busy (TTS playing) but not waiting → press cancels the gate (safety escape).
            if (gate != null && gateBusy)
            {
                gate.CancelGate();
                return;
            }

            // Object has priority over gate-trigger.
            if (inObjectRange && _nearestObject.TryInteract(current))
                return;

            // Else: try the gate.
            if (gate != null && gateHasTarget)
            {
                gate.TryTriggerGate();
                return;
            }

            // Nothing to do.
        }

        InteractableObject FindNearestObject(PossessableCharacter current)
        {
            var all = FindObjectsOfType<InteractableObject>();
            InteractableObject best = null;
            float bestSqr = maxObjectScanRadius * maxObjectScanRadius;

            for (int i = 0; i < all.Length; i++)
            {
                var io = all[i];
                if (io == null || io.IsCompleted || io.IsBusy) continue;
                if (io.ownerCharacter != null && io.ownerCharacter != current) continue;

                float sqr = (io.transform.position - current.transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = io;
                }
            }
            return best;
        }
    }
}