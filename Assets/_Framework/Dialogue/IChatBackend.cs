using System.Collections.Generic;
using System.Threading.Tasks;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// LLM chat backend. Implementations: Stub, Ollama, ...
    /// </summary>
    public interface IChatBackend
    {
        bool IsReady { get; }

        /// <summary>
        /// Send a chat history and get a single response.
        /// Roles: "system", "user", "assistant".
        /// Returns empty string on failure.
        /// </summary>
        Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages);
    }

    public struct ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
}