using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestStateManager : MonoBehaviour
{
    public static QuestStateManager Instance;

    /// <summary>
    /// Fired when a task is completed for a character (only when it was newly added).
    /// </summary>
    public event Action<PossessableCharacter, string> TaskCompleted;

    /// <summary>
    /// Optional: fired when free talk is marked.
    /// </summary>
    public event Action<PossessableCharacter> FreeTalkMarked;

    class CharacterProgress
    {
        public HashSet<string> completedTasks = new HashSet<string>();
        public bool hadFreeChat = false;
    }

    readonly Dictionary<PossessableCharacter, CharacterProgress> _progress =
        new Dictionary<PossessableCharacter, CharacterProgress>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    CharacterProgress Get(PossessableCharacter c)
    {
        if (c == null) return null;
        if (!_progress.TryGetValue(c, out var p))
        {
            p = new CharacterProgress();
            _progress[c] = p;
        }
        return p;
    }

    public bool IsTaskDone(PossessableCharacter c, string taskId)
    {
        var p = Get(c);
        if (p == null) return false;
        if (string.IsNullOrEmpty(taskId)) return false;
        return p.completedTasks.Contains(taskId);
    }

    public void CompleteTask(PossessableCharacter c, string taskId)
    {
        var p = Get(c);
        if (p == null) return;
        if (string.IsNullOrEmpty(taskId)) return;

        // only true if it was newly added
        bool added = p.completedTasks.Add(taskId);
        if (added)
            TaskCompleted?.Invoke(c, taskId);
    }

    // Dein aktueller Name
    public void MarkFreeChat(PossessableCharacter c)
    {
        var p = Get(c);
        if (p == null) return;

        if (!p.hadFreeChat)
        {
            p.hadFreeChat = true;
            FreeTalkMarked?.Invoke(c);
        }
    }

    // ✅ Kompatibilität: DialogueManager erwartet MarkFreeTalkDone
    public void MarkFreeTalkDone(PossessableCharacter c)
    {
        MarkFreeChat(c);
    }

    public bool HadFreeChat(PossessableCharacter c)
    {
        var p = Get(c);
        return p != null && p.hadFreeChat;
    }

    public bool CanOutreachFrom(PossessableCharacter c)
    {
        if (c == null) return false;

        var profile = c.GetComponent<CharacterQuestProfile>();
        if (profile == null)
        {
            // Ohne Profil: konservativ -> NICHT erlauben
            return false;
        }

        if (profile.allowOutreachImmediately)
            return true;

        // Tasks check
        if (profile.requiredTasksForOutreach != null)
        {
            for (int i = 0; i < profile.requiredTasksForOutreach.Length; i++)
            {
                var id = profile.requiredTasksForOutreach[i];
                if (!string.IsNullOrEmpty(id) && !IsTaskDone(c, id))
                    return false;
            }
        }

        // free chat check
        if (profile.requireFreeChatForOutreach && !HadFreeChat(c))
            return false;

        return true;
    }
}