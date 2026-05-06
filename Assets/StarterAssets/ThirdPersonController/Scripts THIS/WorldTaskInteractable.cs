using System.Collections;
using UnityEngine;

public class WorldTaskInteractable : MonoBehaviour
{
    [Header("Assignment")]
    public PossessableCharacter ownerCharacter;
    public string taskId;

    [Tooltip("Task-IDs, die vorher erledigt sein müssen.")]
    public string[] prerequisites;

    [Header("Interact")]
    public float interactRadius = 2.0f;
    public string hudPrompt = "Press Interact";
    public string lockedPrompt = "Not yet";

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip audioClip;
    public bool waitForAudioToFinish = true;

    [Range(0f, 1f)]
    public float defaultObjectVolume = 1.0f;

    [Header("On Complete")]
    public bool hideAfterComplete = true;
    public GameObject[] hideTargets;
    public bool disableCollidersAfterComplete = true;

    [Header("Debug")]
    public bool debugLogs = false;

    protected bool _isRunning;
    protected bool _isCompleted;

    protected virtual void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            // ✅ Lauter Default
            if (audioSource.volume < defaultObjectVolume)
                audioSource.volume = defaultObjectVolume;

            // Spatial defaults (falls vergessen)
            if (audioSource.spatialBlend < 0.99f)
                audioSource.spatialBlend = 1f;
        }

        if (hideTargets == null || hideTargets.Length == 0)
            hideTargets = new GameObject[] { gameObject };
    }

    protected virtual void Start()
    {
        if (QuestStateManager.Instance != null && ownerCharacter != null && !string.IsNullOrEmpty(taskId))
        {
            _isCompleted = QuestStateManager.Instance.IsTaskDone(ownerCharacter, taskId);
            if (_isCompleted)
                ApplyCompletedState();
        }
    }

    public bool IsCompleted => _isCompleted;
    public bool IsBusy => _isRunning;

    public bool IsInRange(PossessableCharacter current)
    {
        if (!current) return false;
        float dist = Vector3.Distance(transform.position, current.transform.position);
        return dist <= interactRadius;
    }

    public string GetPrompt()
    {
        if (_isCompleted) return "";
        return hudPrompt;
    }

    public bool PrerequisitesMet()
    {
        if (QuestStateManager.Instance == null) return true;
        if (ownerCharacter == null) return true;
        if (prerequisites == null) return true;

        for (int i = 0; i < prerequisites.Length; i++)
        {
            var id = prerequisites[i];
            if (!string.IsNullOrEmpty(id) && !QuestStateManager.Instance.IsTaskDone(ownerCharacter, id))
                return false;
        }
        return true;
    }

    public virtual bool TryInteract(PossessableCharacter current, HUDText hud)
    {
        if (_isCompleted || _isRunning) return false;
        if (ownerCharacter != null && current != ownerCharacter) return false;
        if (!IsInRange(current)) return false;

        if (!PrerequisitesMet())
        {
            if (hud == null) hud = HUDText.Instance;
            if (hud != null && !hud.IsLockedByFX)
                hud.SetNpcTimed(lockedPrompt, 1.2f);

            return true;
        }

        StartCoroutine(CoRunTask_Internal(hud));
        return true;
    }

    IEnumerator CoRunTask_Internal(HUDText hud)
    {
        _isRunning = true;

        if (debugLogs) Debug.Log($"[Task] Start '{taskId}' on '{name}'");

        yield return RunTaskRoutine(hud);

        _isRunning = false;

        if (debugLogs) Debug.Log($"[Task] End '{taskId}' on '{name}'");
    }

    protected virtual IEnumerator RunTaskRoutine(HUDText hud)
    {
        yield return PlayAudioIfAny();
        CompleteTaskAndApply(hud);
    }

    protected IEnumerator PlayAudioIfAny()
    {
        if (audioClip == null) yield break;

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.volume = defaultObjectVolume;
        }

        // ✅ enforce min volume
        if (audioSource.volume < defaultObjectVolume)
            audioSource.volume = defaultObjectVolume;

        audioSource.clip = audioClip;
        audioSource.Play();

        if (waitForAudioToFinish)
            yield return new WaitForSeconds(audioClip.length);
    }

    protected void CompleteTaskAndApply(HUDText hud)
    {
        if (_isCompleted) return;

        if (QuestStateManager.Instance != null && ownerCharacter != null && !string.IsNullOrEmpty(taskId))
            QuestStateManager.Instance.CompleteTask(ownerCharacter, taskId);

        _isCompleted = true;

        ApplyCompletedState();

        if (hud == null) hud = HUDText.Instance;
        if (hud != null && !hud.IsLockedByFX && !hud.IsSticky)
            hud.SetIdleAuto();
    }

    protected void ApplyCompletedState()
    {
        if (hideAfterComplete && hideTargets != null)
        {
            for (int i = 0; i < hideTargets.Length; i++)
                if (hideTargets[i] != null) hideTargets[i].SetActive(false);
        }

        if (disableCollidersAfterComplete)
        {
            var cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }
    }
}