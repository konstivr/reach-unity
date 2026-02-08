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
    public bool hidePromptWhenFar = true;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip audioClip;
    public bool waitForAudioToFinish = true;

    [Header("On Complete")]
    public bool hideAfterComplete = true;
    public GameObject[] hideTargets; // z.B. Objekt + Shadow
    public bool disableCollidersAfterComplete = true;

    [Header("Debug")]
    public bool debugLogs = false;

    protected bool _isRunning;
    protected bool _isCompleted;

    protected virtual void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (hideTargets == null || hideTargets.Length == 0)
            hideTargets = new GameObject[] { gameObject };
    }

    protected virtual void Update()
    {
        var swap = FindObjectOfType<PerspectiveSwapManager>();
        if (!swap || !swap.current || !swap.current.inputs) return;

        // nur in richtiger Perspektive
        if (ownerCharacter != null && swap.current != ownerCharacter) return;

        // schon done?
        if (!_isCompleted && QuestStateManager.Instance != null && ownerCharacter != null)
            _isCompleted = QuestStateManager.Instance.IsTaskDone(ownerCharacter, taskId);

        if (_isCompleted) return;

        float dist = Vector3.Distance(transform.position, swap.current.transform.position);

        if (dist <= interactRadius)
        {
            if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX && !HUDText.Instance.IsSticky)
                HUDText.Instance.SetPrompt(hudPrompt);

            if (swap.current.inputs.interact && !_isRunning)
            {
                if (!PrerequisitesMet(ownerCharacter))
                {
                    if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX)
                        HUDText.Instance.SetNpcTimed(lockedPrompt, 1.2f);
                    return;
                }

                StartCoroutine(CoRunTask());
            }
        }
        else
        {
            if (hidePromptWhenFar && HUDText.Instance != null && HUDText.Instance.CurrentMode == HUDText.Mode.Prompt)
                HUDText.Instance.SetIdleAuto();
        }
    }

    protected bool PrerequisitesMet(PossessableCharacter c)
    {
        if (QuestStateManager.Instance == null) return true;
        if (prerequisites == null) return true;

        for (int i = 0; i < prerequisites.Length; i++)
        {
            var id = prerequisites[i];
            if (!string.IsNullOrEmpty(id) && !QuestStateManager.Instance.IsTaskDone(c, id))
                return false;
        }
        return true;
    }

    IEnumerator CoRunTask()
    {
        _isRunning = true;

        if (debugLogs) Debug.Log($"[Task] Start '{taskId}' on '{name}'");

        // play audio
        if (audioClip != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }

            audioSource.clip = audioClip;
            audioSource.Play();

            if (waitForAudioToFinish)
                yield return new WaitForSeconds(audioClip.length);
        }

        // mark complete
        if (QuestStateManager.Instance != null && ownerCharacter != null)
            QuestStateManager.Instance.CompleteTask(ownerCharacter, taskId);

        _isCompleted = true;

        // hide stuff
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

        if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX && !HUDText.Instance.IsSticky)
            HUDText.Instance.SetIdleAuto();

        _isRunning = false;

        if (debugLogs) Debug.Log($"[Task] Done '{taskId}'");
    }
}