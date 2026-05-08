using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Default chat conversation manager.
    /// One per scene. Tracks chat history per controlled character (cleared on switch).
    /// Holds HUD with FXOverride while the synthesized response plays.
    /// </summary>
    public class DialogueManager : MonoBehaviour, IDialogueManager
    {
        [Header("History")]
        [Tooltip("How many recent messages to keep in the conversation context (excluding the system prompt).")]
        public int maxHistoryMessages = 14;

        [Header("Voice Audio")]
        [Tooltip("Audio source used to play the LLM reply. Auto-created on the controlled character if left empty.")]
        public AudioSource voiceSource;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        readonly List<ChatMessage> _messages = new List<ChatMessage>();
        PossessableCharacter _historyOwner;

        Coroutine _hudHoldRoutine;
        int _speakToken;

        public bool IsResponding { get; private set; }

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                Debug.LogError("[DialogueManager] No GameContext.Instance found.");
                enabled = false;
                return;
            }
            ctx.Dialogue = this;
        }

        void OnEnable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.Switched += OnSwitched;
        }

        void OnDisable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.Switched -= OnSwitched;
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            // Stop any ongoing response and rebuild history for the new character.
            Interrupt();
            RebuildHistoryFor(to);
        }

        // ============================================================
        // Public API
        // ============================================================

        public async Task PlayerSpokeAsync(string wavPath)
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            var pm = ctx.Perspective;
            if (pm == null || pm.Current == null)
            {
                if (debugLogs) Debug.Log("[DialogueManager] Skipped: no current character.");
                return;
            }

            // Don't chat in the very first perspective (player hasn't reached out yet)
            if (pm.VisitedCount <= 1)
            {
                if (debugLogs) Debug.Log("[DialogueManager] Skipped: still in initial perspective (no chat before first switch).");
                return;
            }

            // Don't chat while gate is busy (it owns the speech)
            if (ctx.Gate != null && ctx.Gate.IsGateBusy)
            {
                if (debugLogs) Debug.Log("[DialogueManager] Skipped: gate busy.");
                return;
            }

            var speech = ctx.Speech;
            if (speech == null || speech.STT == null || speech.Chat == null || speech.TTS == null)
            {
                Debug.LogError("[DialogueManager] SpeechSystem or one of its backends is missing.");
                return;
            }

            // Make sure history matches current character
            if (_historyOwner != pm.Current)
                RebuildHistoryFor(pm.Current);

            IsResponding = true;

            string lang = ctx.pack != null ? ctx.pack.language : "en";

            // 1) STT
            string playerText = await speech.STT.TranscribeAsync(wavPath, lang);
            if (debugLogs) Debug.Log($"[DialogueManager] PLAYER: '{playerText}'");

            if (string.IsNullOrWhiteSpace(playerText))
            {
                IsResponding = false;
                return;
            }

            _messages.Add(new ChatMessage("user", playerText));
            TrimHistory();

            // 2) LLM
            string reply = await speech.Chat.ChatAsync(_messages);
            if (debugLogs) Debug.Log($"[DialogueManager] NPC: '{reply}'");

            if (string.IsNullOrWhiteSpace(reply))
            {
                IsResponding = false;
                return;
            }

            _messages.Add(new ChatMessage("assistant", reply));
            TrimHistory();

            // 3) HUD shows the reply text, locked until audio finishes
            ctx.Hud?.SetFXOverride(reply);

            // 4) TTS synth + play on the current character's voice source
            string voice = ResolveVoice(pm.Current.Definition);
            AudioClip clip = await speech.TTS.SynthesizeAsync(reply, voice);

            if (clip != null)
            {
                var src = GetOrCreateVoiceSource(pm.Current);
                src.PlayOneShot(clip);
                StartHudHold(src, clip);
            }
            else
            {
                // No clip → release HUD immediately
                ctx.Hud?.ClearFXOverride();
            }

            IsResponding = false;
        }

        public void Interrupt()
        {
            // Cancel HUD hold
            _speakToken++;
            if (_hudHoldRoutine != null)
            {
                StopCoroutine(_hudHoldRoutine);
                _hudHoldRoutine = null;
            }

            // Stop voice playback
            if (voiceSource != null && voiceSource.isPlaying)
                voiceSource.Stop();

            // Release HUD lock
            GameContext.Instance?.Hud?.ClearFXOverride();

            IsResponding = false;
            if (debugLogs) Debug.Log("[DialogueManager] Interrupt");
        }

        // ============================================================
        // Internals
        // ============================================================

        void RebuildHistoryFor(PossessableCharacter character)
        {
            _messages.Clear();
            _historyOwner = character;

            if (character == null || character.Definition == null) return;

            string sys = character.Definition.chatSystemPrompt;
            if (!string.IsNullOrWhiteSpace(sys))
                _messages.Add(new ChatMessage("system", sys));

            if (debugLogs)
                Debug.Log($"[DialogueManager] History rebuilt for '{character.Definition.displayName}' " +
                          $"(systemLen={sys?.Length ?? 0})");
        }

        void TrimHistory()
        {
            // Always keep the system message at index 0; trim oldest user/assistant pairs first.
            int keep = Mathf.Max(3, maxHistoryMessages);
            int over = _messages.Count - (1 + keep);
            if (over > 0)
                _messages.RemoveRange(1, over);
        }

        AudioSource GetOrCreateVoiceSource(PossessableCharacter character)
        {
            if (voiceSource != null) return voiceSource;

            // Use a dedicated child AudioSource on the character.
            var holder = character.GetComponent<DialogueVoiceSource>();
            if (holder == null)
            {
                holder = character.gameObject.AddComponent<DialogueVoiceSource>();
                holder.Init();
            }
            return holder.Source;
        }

        void StartHudHold(AudioSource src, AudioClip expectedClip)
        {
            _speakToken++;
            int token = _speakToken;

            if (_hudHoldRoutine != null)
                StopCoroutine(_hudHoldRoutine);

            _hudHoldRoutine = StartCoroutine(CoHoldHud(src, expectedClip, token));
        }

        IEnumerator CoHoldHud(AudioSource src, AudioClip expectedClip, int token)
        {
            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Hud == null) yield break;

            // Wait one frame so PlayOneShot has scheduled
            yield return null;

            if (token != _speakToken) yield break;

            // Hold while playing the expected clip
            while (token == _speakToken && src != null && src.isPlaying)
                yield return null;

            if (token == _speakToken)
                ctx.Hud.ClearFXOverride();

            _hudHoldRoutine = null;
        }

        string ResolveVoice(CharacterDefinition def)
        {
            if (def == null) return "";
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return def.voiceMacOS;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return def.voiceWindows;
#else
            return def.voiceMacOS;
#endif
        }
    }

    /// <summary>
    /// Tiny holder for the per-character dialogue voice AudioSource.
    /// Created on demand by DialogueManager so we don't pollute the character with another raw AudioSource.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueVoiceSource : MonoBehaviour
    {
        public AudioSource Source { get; private set; }

        public void Init()
        {
            Source = gameObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = false;
            Source.spatialBlend = 1f; // 3D positional
        }
    }
}