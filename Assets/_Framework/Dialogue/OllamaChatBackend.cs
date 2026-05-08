using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Chat backend using a local Ollama server.
    /// Default endpoint: http://localhost:11434/api/chat
    /// </summary>
    public class OllamaChatBackend : MonoBehaviour, IChatBackend
    {
        [Header("Ollama")]
        public string model = "llama3";
        public string endpoint = "http://localhost:11434/api/chat";

        [Tooltip("HTTP timeout in seconds.")]
        public int timeoutSeconds = 60;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady => !string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(endpoint);

        // Wire-format DTOs
        [Serializable] class Msg { public string role; public string content; }
        [Serializable] class ChatRequest { public string model; public List<Msg> messages; public bool stream; }
        [Serializable] class ChatResponse { public Msg message; public bool done; }

        public async Task<string> ChatAsync(IReadOnlyList<ChatMessage> messages)
        {
            if (messages == null || messages.Count == 0)
            {
                Debug.LogError("[OllamaChat] Empty messages.");
                return "";
            }

            // Map to wire format
            var payload = new ChatRequest { model = model, stream = false, messages = new List<Msg>(messages.Count) };
            for (int i = 0; i < messages.Count; i++)
                payload.messages.Add(new Msg { role = messages[i].role, content = messages[i].content });

            string json = JsonUtility.ToJson(payload);
            if (debugLogs) Debug.Log($"[OllamaChat] POST {endpoint} model='{model}' msgs={messages.Count}");

            using var req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSeconds;

            await SendAsync(req);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[OllamaChat] HTTP error: {req.error}\n{req.downloadHandler.text}");
                return "";
            }

            string raw = req.downloadHandler.text;

            try
            {
                var parsed = JsonUtility.FromJson<ChatResponse>(raw);
                string content = parsed != null && parsed.message != null ? parsed.message.content : raw;
                if (debugLogs) Debug.Log($"[OllamaChat] OK chars={content?.Length ?? 0}");
                return content ?? "";
            }
            catch
            {
                if (debugLogs) Debug.LogWarning("[OllamaChat] JSON parse failed, returning raw text.");
                return raw;
            }
        }

        static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}