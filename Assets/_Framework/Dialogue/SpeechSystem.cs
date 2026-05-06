using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Central speech system. Holds references to the active STT/TTS/Chat backends
    /// and registers itself with GameContext.
    ///
    /// Backends are MonoBehaviours; just drop the ones you want to use onto
    /// this GameObject (or any GameObject) and link them in the inspector.
    /// </summary>
    public class SpeechSystem : MonoBehaviour
    {
        [Header("Active Backends")]
        [Tooltip("Drag the desired ISpeechToText component here (e.g. StubSpeechToText).")]
        public MonoBehaviour speechToText;

        [Tooltip("Drag the desired ITextToSpeech component here.")]
        public MonoBehaviour textToSpeech;

        [Tooltip("Drag the desired IChatBackend component here.")]
        public MonoBehaviour chatBackend;

        [Header("Debug")]
        public bool debugLogs = true;

        public ISpeechToText STT { get; private set; }
        public ITextToSpeech TTS { get; private set; }
        public IChatBackend  Chat { get; private set; }

        void Awake()
        {
            STT  = speechToText  as ISpeechToText;
            TTS  = textToSpeech  as ITextToSpeech;
            Chat = chatBackend   as IChatBackend;

            if (speechToText != null && STT == null)
                Debug.LogError($"[SpeechSystem] Assigned speechToText '{speechToText.GetType().Name}' does not implement ISpeechToText.");

            if (textToSpeech != null && TTS == null)
                Debug.LogError($"[SpeechSystem] Assigned textToSpeech '{textToSpeech.GetType().Name}' does not implement ITextToSpeech.");

            if (chatBackend != null && Chat == null)
                Debug.LogError($"[SpeechSystem] Assigned chatBackend '{chatBackend.GetType().Name}' does not implement IChatBackend.");

            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                Debug.LogError("[SpeechSystem] No GameContext.Instance found.");
                return;
            }

            ctx.Speech = this;

            if (debugLogs)
            {
                Debug.Log($"[SpeechSystem] Awake | STT={(STT != null ? STT.GetType().Name : "NULL")} | " +
                          $"TTS={(TTS != null ? TTS.GetType().Name : "NULL")} | " +
                          $"Chat={(Chat != null ? Chat.GetType().Name : "NULL")}");
            }
        }
    }
}