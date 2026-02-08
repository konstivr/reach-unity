using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Alias: kein Debug-Konflikt
using UDebug = UnityEngine.Debug;

public class OllamaClient : MonoBehaviour
{
    public static OllamaClient Instance;

    [Header("Ollama")]
    public string model = "llama3";
    public string endpointChat = "http://localhost:11434/api/chat";
    public string endpointGenerate = "http://localhost:11434/api/generate";
    public float requestTimeoutSeconds = 60f;

    [Header("Debug")]
    public bool debugLogs = true;

    void Awake()
    {
        Instance = this;
        if (debugLogs) UDebug.Log($"[OllamaClient] Awake | model='{model}' chat='{endpointChat}'");
    }

    // ---------- CHAT API ----------
    [Serializable]
    public class ChatMessage
    {
        public string role;    // "system" | "user" | "assistant"
        public string content;
    }

    [Serializable]
    class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public bool stream;
    }

    [Serializable]
    class ChatResponse
    {
        public ChatMessage message;
        public bool done;
    }

    public async Task<string> ChatOnce(List<ChatMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            UDebug.LogError("[OllamaClient] ChatOnce: messages empty");
            return "";
        }

        var payload = new ChatRequest
        {
            model = model,
            messages = messages,
            stream = false
        };

        string json = JsonUtility.ToJson(payload);

        if (debugLogs)
            UDebug.Log($"[OllamaClient] -> POST {endpointChat} model='{model}' msgs={messages.Count} jsonLen={json.Length}");

        using var req = new UnityWebRequest(endpointChat, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

        await SendWebRequestAsync(req);

        if (req.result != UnityWebRequest.Result.Success)
        {
            UDebug.LogError($"[OllamaClient] HTTP Error: {req.error}\n{req.downloadHandler.text}");
            return "";
        }

        string raw = req.downloadHandler.text;

        try
        {
            var parsed = JsonUtility.FromJson<ChatResponse>(raw);
            string content = parsed != null && parsed.message != null ? parsed.message.content : raw;

            if (debugLogs)
                UDebug.Log($"[OllamaClient] <- OK chars={content?.Length ?? 0}");

            return content ?? "";
        }
        catch
        {
            if (debugLogs)
                UDebug.LogWarning("[OllamaClient] JSON parse failed, returning raw text");
            return raw;
        }
    }

    // ---------- GENERATE API (optional legacy) ----------
    [Serializable]
    class GenerateRequest
    {
        public string model;
        public string prompt;
        public bool stream;
        public string system; // optional
    }

    [Serializable]
    class GenerateResponse
    {
        public string response;
        public bool done;
    }

    public async Task<string> SendPrompt(string prompt, bool system)
    {
        var payload = new GenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = false,
            system = system ? "You are an NPC in a game. Answer concisely and in character." : null
        };

        string json = JsonUtility.ToJson(payload);

        if (debugLogs)
            UDebug.Log($"[OllamaClient] -> POST {endpointGenerate} model='{model}' system={system} promptLen={(prompt?.Length ?? 0)}");

        using var req = new UnityWebRequest(endpointGenerate, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = Mathf.CeilToInt(requestTimeoutSeconds);

        await SendWebRequestAsync(req);

        if (req.result != UnityWebRequest.Result.Success)
        {
            UDebug.LogError($"[OllamaClient] HTTP Error: {req.error}\n{req.downloadHandler.text}");
            return "";
        }

        return req.downloadHandler.text;
    }

    public static string ExtractResponseText(string rawJson)
    {
        if (string.IsNullOrEmpty(rawJson)) return "";

        try
        {
            var parsed = JsonUtility.FromJson<GenerateResponse>(rawJson);
            return parsed != null ? parsed.response : rawJson;
        }
        catch
        {
            return rawJson;
        }
    }

    static Task SendWebRequestAsync(UnityWebRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
        var op = req.SendWebRequest();
        op.completed += _ => tcs.TrySetResult(true);
        return tcs.Task;
    }
}