using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Test chat backend. Returns a fixed reply for every chat call.
    /// Useful for testing the dialogue flow without an LLM running.
    /// </summary>
    public class StubChatBackend : MonoBehaviour, IChatBackend
    {
        [Header("Stub")]
        [TextArea(2, 6)]
        public string fixedReply = "I hear you. (This is a stub reply.)";

        [Tooltip("Simulated thinking delay (seconds).")]
        public float simulatedDelaySeconds = 0.5f;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady => true;

        public async Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages)
        {
            if (debugLogs)
            {
                int userCount = 0;
                for (int i = 0; i < messages.Count; i++)
                    if (messages[i].role == "user") userCount++;
                Debug.Log($"[StubChat] ChatAsync messages={messages.Count} userCount={userCount}");
            }

            if (simulatedDelaySeconds > 0f)
                await Task.Delay(Mathf.RoundToInt(simulatedDelaySeconds * 1000f));

            if (debugLogs) Debug.Log($"[StubChat] -> '{fixedReply}'");
            return fixedReply ?? "";
        }
    }
}