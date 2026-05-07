using UnityEngine;

public class CharacterQuestProfile : MonoBehaviour
{
    [Header("Outreach Unlock")]
    public bool allowOutreachImmediately = false;

    [Tooltip("Tasks, die erledigt sein müssen, damit Outreach zu neuen Characters wieder erlaubt ist.")]
    public string[] requiredTasksForOutreach;

    [Tooltip("Zusätzlich muss mind. 1 freier Ollama-Chat in dieser Perspektive passiert sein.")]
    public bool requireFreeChatForOutreach = true;
}