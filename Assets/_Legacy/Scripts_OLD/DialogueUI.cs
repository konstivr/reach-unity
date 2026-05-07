using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueUI : MonoBehaviour
{
    [Header("TMP (preferred)")]
    public TMP_Text npcTextTMP;
    public TMP_Text playerTextTMP;

    [Header("Legacy UI.Text (fallback)")]
    public Text npcTextUI;
    public Text playerTextUI;

    [Header("Behavior")]
    [Tooltip("Wenn true: wird Text getrimmt (null -> \"\")")]
    public bool sanitizeInput = true;

    [Header("Debug")]
    public bool debugLogs = false;

    public void SetNpcText(string t)
    {
        t = Sanitize(t);

        if (debugLogs) Debug.Log($"[UI] NPC: {t}");

        if (npcTextTMP != null) npcTextTMP.text = t;
        else if (npcTextUI != null) npcTextUI.text = t;
    }

    public void SetPlayerText(string t)
    {
        t = Sanitize(t);

        if (debugLogs) Debug.Log($"[UI] Player: {t}");

        if (playerTextTMP != null) playerTextTMP.text = t;
        else if (playerTextUI != null) playerTextUI.text = t;
    }

    public void Clear()
    {
        SetNpcText("");
        SetPlayerText("");
    }

    string Sanitize(string t)
    {
        if (!sanitizeInput) return t;
        return (t ?? "").Trim();
    }
}
