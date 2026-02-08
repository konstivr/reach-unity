using UnityEngine;

public class ShadowTalker : MonoBehaviour
{
    public PerspectiveSwapManager swapManager;
    public HUDText hud;

    public float radius = 2.5f;

    [TextArea(1, 5)]
    public string talkText = "…";

    [TextArea(1, 3)]
    public string prompt = "Press input";

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!hud) hud = FindObjectOfType<HUDText>();
    }

    void Update()
    {
        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;

        float d = Vector3.Distance(transform.position, swapManager.current.transform.position);
        bool inRange = d <= radius;

        if (hud && !hud.IsLockedByFX && !hud.IsSticky)
        {
            if (inRange) hud.SetPrompt(prompt);
        }

        if (inRange && swapManager.current.inputs.dialogueStart)
        {
            if (hud) hud.SetSticky(talkText);
        }

        // reset sticky wenn raus
        if (!inRange && hud && hud.IsSticky)
        {
            hud.ClearSticky();
        }
    }
}