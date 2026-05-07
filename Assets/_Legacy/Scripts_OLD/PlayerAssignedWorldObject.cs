using UnityEngine;

public class PlayerAssignedWorldObject : MonoBehaviour
{
    public enum InteractionMode
    {
        OneStepDisappear,
        TwoStepDisappear
    }

    [Header("Assignment")]
    public PossessableCharacter assignedTo;     // welcher Player darf interagieren?
    public float radius = 2.5f;

    [Header("Behavior")]
    public InteractionMode mode = InteractionMode.OneStepDisappear;

    [TextArea(1, 3)]
    public string promptText = "Press input";

    [TextArea(1, 5)]
    public string responseText = "…";

    [TextArea(1, 5)]
    public string secondStepText = "Press input again";

    [Header("Audio")]
    public AudioSource source;
    public AudioClip firstSound;
    public AudioClip secondSound;

    [Header("Disappear")]
    public GameObject targetToHide; // wenn null: hide this.gameObject

    [Header("Runtime (read only)")]
    public bool IsCompleted { get; private set; }

    bool _armedSecondStep = false;

    void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();
        if (!targetToHide) targetToHide = gameObject;
    }

    public bool IsInRange(PossessableCharacter current)
    {
        if (IsCompleted) return false;
        if (!assignedTo || current != assignedTo) return false;

        float d = Vector3.Distance(current.transform.position, transform.position);
        return d <= radius;
    }

    public void ResetLocalStateOnExit()
    {
        _armedSecondStep = false;
    }

    public string GetPrompt()
    {
        if (IsCompleted) return "";
        if (mode == InteractionMode.TwoStepDisappear && _armedSecondStep)
            return secondStepText;
        return promptText;
    }

    public string GetStickyResponseText()
    {
        if (mode == InteractionMode.TwoStepDisappear && _armedSecondStep)
            return secondStepText;
        return responseText;
    }

    public void HandleInputF(HUDText hud)
    {
        if (IsCompleted) return;

        if (mode == InteractionMode.OneStepDisappear)
        {
            Play(source, firstSound);
            if (hud) hud.SetSticky(responseText);
            Complete();
            return;
        }

        // TwoStep
        if (!_armedSecondStep)
        {
            _armedSecondStep = true;
            Play(source, firstSound);
            if (hud) hud.SetSticky(responseText);
            return;
        }
        else
        {
            Play(source, secondSound != null ? secondSound : firstSound);
            if (hud) hud.SetSticky("…");
            Complete();
            return;
        }
    }

    void Complete()
    {
        IsCompleted = true;
        if (targetToHide) targetToHide.SetActive(false);
    }

    static void Play(AudioSource src, AudioClip clip)
    {
        if (!src || !clip) return;
        src.PlayOneShot(clip);
    }
}