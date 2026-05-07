using System.Collections;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// The single interactable object belonging to one character.
    /// Configured by an InteractableObjectDefinition (SO).
    ///
    /// Workflow:
    ///   Player walks into range (assigned character only)
    ///   → HUD shows promptText
    ///   → Player presses Interact
    ///   → OneShot: action runs once, object hides (if configured)
    ///   → TwoStep: first press primes, second press runs the action
    ///
    /// On completion, optionally unlocks outreach for the owning character.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Definition")]
        [Tooltip("The InteractableObjectDefinition SO that drives this object's behavior.")]
        public InteractableObjectDefinition definition;

        [Header("Owner")]
        [Tooltip("The character who owns this object — only they can interact with it.")]
        public PossessableCharacter ownerCharacter;

        [Header("Audio")]
        [Tooltip("Auto-created if left empty.")]
        public AudioSource audioSource;

        [Header("Debug")]
        public bool debugLogs = false;

        // ============================================================
        // State
        // ============================================================

        bool _completed;
        bool _busy;
        bool _armedSecondStep; // for TwoStep mode

        public bool IsCompleted => _completed;
        public bool IsBusy => _busy;

        /// <summary>True after the (final) press has been processed and unlocksOutreach is set in the definition.</summary>
        public bool HasUnlockedOutreach => _completed && definition != null && definition.unlocksOutreach;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            if (definition == null)
                Debug.LogError($"[InteractableObject] '{name}': missing InteractableObjectDefinition.");

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 1f; // 3D positional
                }
            }
        }

        // ============================================================
        // IInteractable
        // ============================================================

        public bool IsInRange(PossessableCharacter currentPlayer)
        {
            if (currentPlayer == null) return false;
            if (definition == null) return false;

            float distSqr = (transform.position - currentPlayer.transform.position).sqrMagnitude;
            float radius = definition.interactRadius;
            return distSqr <= radius * radius;
        }

        public bool CanInteract(PossessableCharacter currentPlayer)
        {
            if (_completed || _busy) return false;
            if (currentPlayer == null) return false;
            if (ownerCharacter != null && currentPlayer != ownerCharacter) return false;
            return IsInRange(currentPlayer);
        }

        public string GetPrompt()
        {
            if (_completed || definition == null) return "";

            if (definition.mode == InteractActionMode.TwoStep && _armedSecondStep)
                return definition.secondStepPromptText;

            return definition.promptText;
        }

        public bool TryInteract(PossessableCharacter currentPlayer)
        {
            if (!CanInteract(currentPlayer)) return false;
            if (definition == null) return false;

            StartCoroutine(CoRun(currentPlayer));
            return true;
        }

        // ============================================================
        // Run
        // ============================================================

        IEnumerator CoRun(PossessableCharacter player)
        {
            _busy = true;

            switch (definition.mode)
            {
                case InteractActionMode.OneShot:
                    yield return RunOneShot();
                    break;

                case InteractActionMode.TwoStep:
                    if (!_armedSecondStep)
                    {
                        yield return RunFirstStep();
                        _armedSecondStep = true;
                        _busy = false;
                        yield break;
                    }
                    else
                    {
                        yield return RunSecondStep();
                    }
                    break;
            }

            CompleteAndApply();
            _busy = false;
        }

        IEnumerator RunOneShot()
        {
            ShowResponseText();

            if (definition.audioClip != null)
            {
                PlayAudio(definition.audioClip);
                yield return new WaitForSeconds(definition.audioClip.length);
            }
            else if (!string.IsNullOrEmpty(definition.responseText))
            {
                yield return new WaitForSeconds(definition.responseDurationSeconds);
            }
        }

        IEnumerator RunFirstStep()
        {
            // First press: play first-step audio if any, show response (which is reused as priming hint)
            ShowResponseText();

            if (definition.firstStepAudioClip != null)
            {
                PlayAudio(definition.firstStepAudioClip);
                yield return new WaitForSeconds(definition.firstStepAudioClip.length);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        IEnumerator RunSecondStep()
        {
            ShowResponseText();

            if (definition.audioClip != null)
            {
                PlayAudio(definition.audioClip);
                yield return new WaitForSeconds(definition.audioClip.length);
            }
            else if (!string.IsNullOrEmpty(definition.responseText))
            {
                yield return new WaitForSeconds(definition.responseDurationSeconds);
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        void ShowResponseText()
        {
            if (string.IsNullOrEmpty(definition.responseText)) return;

            var hud = GameContext.Instance?.Hud;
            if (hud == null) return;

            hud.SetTimed(definition.responseText, definition.responseDurationSeconds);
        }

        void PlayAudio(AudioClip clip)
        {
            if (audioSource == null || clip == null) return;
            audioSource.volume = definition.audioVolume;
            audioSource.PlayOneShot(clip);
        }

        void CompleteAndApply()
        {
            _completed = true;

            if (debugLogs) Debug.Log($"[InteractableObject] '{name}' completed (unlocksOutreach={definition.unlocksOutreach})");

            if (definition.hideOnComplete)
                gameObject.SetActive(false);
        }
    }
}