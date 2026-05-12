using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// A world-placed object that any character can interact with repeatedly.
    /// Each character gets their own audio + response text from the
    /// InteractableObjectDefinition.responsesPerCharacter list.
    ///
    /// State tracked:
    ///   - _busy: while an interaction is running (prevents re-entrancy)
    ///   - _armedSecondStep (TwoStep mode only): primed state per character
    ///   - _usedByCharacters: who has interacted at least once (for outreach lock)
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

        bool _busy;
        bool _armedSecondStep; // per-character would be nicer but TwoStep is rarely cross-character
        readonly HashSet<CharacterDefinition> _usedByCharacters = new HashSet<CharacterDefinition>();

        public bool IsCompleted => false; // never completed — always re-interactable
        public bool IsBusy => _busy;

        /// <summary>True if the given character has interacted with this object at least once.</summary>
        public bool HasBeenUsedBy(CharacterDefinition character) =>
            character != null && _usedByCharacters.Contains(character);

        /// <summary>True if any character has interacted with this object and it unlocks outreach.</summary>
        public bool HasUnlockedOutreachFor(CharacterDefinition character) =>
            definition != null && definition.unlocksOutreach && HasBeenUsedBy(character);

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
            if (_busy) return false;
            if (currentPlayer == null) return false;
            return IsInRange(currentPlayer);
        }

        public string GetPrompt()
        {
            if (definition == null) return "";

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
                        _armedSecondStep = false; // reset for next cycle
                    }
                    break;
            }

            if (charDef != null)
                _usedByCharacters.Add(charDef);

            if (debugLogs)
                Debug.Log($"[InteractableObject] '{name}' interaction done. usedBy count={_usedByCharacters.Count}");

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
    }
}