using System.Collections;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// A world-placed object that any character can interact with.
    /// Each character can have its own audio and response text
    /// (configured in the InteractableObjectDefinition.responsesPerCharacter list).
    ///
    /// Workflow:
    ///   Player walks into range
    ///   → HUD shows promptText
    ///   → Player presses Interact
    ///   → Look up which character is controlled, find their response
    ///   → OneShot: play audio, show text, hide
    ///   → TwoStep: first press primes, second press resolves
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Definition")]
        [Tooltip("The InteractableObjectDefinition SO that drives this object's behavior.")]
        public InteractableObjectDefinition definition;

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
        bool _armedSecondStep;
        CharacterDefinition _interactedBy; // who triggered (most recent)

        public bool IsCompleted => _completed;
        public bool IsBusy => _busy;

        /// <summary>True after a successful (final) interact, IF the definition unlocks outreach.</summary>
        public bool HasUnlockedOutreach => _completed && definition != null && definition.unlocksOutreach;

        /// <summary>The character that last interacted with this object (used by GateSystem for outreach lock).</summary>
        public CharacterDefinition InteractedBy => _interactedBy;

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
                    audioSource.spatialBlend = 1f;
                }
            }
        }

        // ============================================================
        // IInteractable
        // ============================================================

        public bool IsInRange(PossessableCharacter currentPlayer)
        {
            if (currentPlayer == null || definition == null) return false;

            float distSqr = (transform.position - currentPlayer.transform.position).sqrMagnitude;
            float radius = definition.interactRadius;
            return distSqr <= radius * radius;
        }

        public bool CanInteract(PossessableCharacter currentPlayer)
        {
            if (_completed || _busy) return false;
            if (currentPlayer == null) return false;
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
            if (!CanInteract(currentPlayer) || definition == null) return false;

            StartCoroutine(CoRun(currentPlayer));
            return true;
        }

        // ============================================================
        // Run
        // ============================================================

        IEnumerator CoRun(PossessableCharacter player)
        {
            _busy = true;
            var charDef = player != null ? player.Definition : null;
            var response = definition.GetResponseFor(charDef);

            if (debugLogs)
                Debug.Log($"[InteractableObject] '{name}' triggered by '{charDef?.displayName ?? "?"}' " +
                          $"(specific response: {(response != definition.defaultResponse ? "yes" : "fallback")})");

            switch (definition.mode)
            {
                case InteractActionMode.OneShot:
                    yield return RunOneShot(response);
                    break;

                case InteractActionMode.TwoStep:
                    if (!_armedSecondStep)
                    {
                        yield return RunFirstStep(response);
                        _armedSecondStep = true;
                        _busy = false;
                        yield break;
                    }
                    else
                    {
                        yield return RunSecondStep(response);
                    }
                    break;
            }

            _interactedBy = charDef;
            CompleteAndApply();
            _busy = false;
        }

        IEnumerator RunOneShot(CharacterResponse response)
        {
            ShowResponseText(response);

            if (response.audioClip != null)
            {
                PlayAudio(response.audioClip, response.audioVolume);
                yield return new WaitForSeconds(response.audioClip.length);
            }
            else if (!string.IsNullOrEmpty(response.responseText))
            {
                yield return new WaitForSeconds(response.responseDurationSeconds);
            }
        }

        IEnumerator RunFirstStep(CharacterResponse response)
        {
            ShowResponseText(response);

            if (response.firstStepAudioClip != null)
            {
                PlayAudio(response.firstStepAudioClip, response.audioVolume);
                yield return new WaitForSeconds(response.firstStepAudioClip.length);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }
        }

        IEnumerator RunSecondStep(CharacterResponse response)
        {
            ShowResponseText(response);

            if (response.audioClip != null)
            {
                PlayAudio(response.audioClip, response.audioVolume);
                yield return new WaitForSeconds(response.audioClip.length);
            }
            else if (!string.IsNullOrEmpty(response.responseText))
            {
                yield return new WaitForSeconds(response.responseDurationSeconds);
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        void ShowResponseText(CharacterResponse response)
        {
            if (response == null || string.IsNullOrEmpty(response.responseText)) return;

            var hud = GameContext.Instance?.Hud;
            if (hud == null) return;

            hud.SetTimed(response.responseText, response.responseDurationSeconds);
        }

        void PlayAudio(AudioClip clip, float volume)
        {
            if (audioSource == null || clip == null) return;
            audioSource.volume = volume;
            audioSource.PlayOneShot(clip);
        }

        void CompleteAndApply()
        {
            _completed = true;

            if (debugLogs) Debug.Log($"[InteractableObject] '{name}' completed " +
                                      $"(by={_interactedBy?.displayName ?? "?"}, unlocksOutreach={definition.unlocksOutreach})");

            if (definition.hideOnComplete)
                gameObject.SetActive(false);
        }
    }
}