using UnityEngine;

public class ShadowPlayerController : MonoBehaviour
{
    public enum ShadowMode
    {
        FollowUnreachable,
        Wander,
        TalkOnly
    }

    [Header("Assignment")]
    public PossessableCharacter visibleInPerspectiveOf;
    public PerspectiveSwapManager swapManager;

    [Header("Mode")]
    public ShadowMode mode = ShadowMode.FollowUnreachable;

    [Header("Refs")]
    public Renderer[] renderersToToggle;
    public NPCWander wander;
    public ShadowFollowUnreachable follow;
    public ShadowTalker talker;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (renderersToToggle == null || renderersToToggle.Length == 0)
            renderersToToggle = GetComponentsInChildren<Renderer>(true);

        if (!wander) wander = GetComponent<NPCWander>();
        if (!follow) follow = GetComponent<ShadowFollowUnreachable>();
        if (!talker) talker = GetComponent<ShadowTalker>();
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;
        ApplyVisibility();
        ApplyMode();
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        ApplyVisibility();
        ApplyMode();
    }

    void ApplyVisibility()
    {
        bool visible = (swapManager != null && swapManager.current == visibleInPerspectiveOf);
        foreach (var r in renderersToToggle)
            if (r) r.enabled = visible;

        if (wander) wander.enabled = visible && mode == ShadowMode.Wander;
        if (follow) follow.enabled = visible && mode == ShadowMode.FollowUnreachable;
        if (talker) talker.enabled = visible && mode == ShadowMode.TalkOnly;
    }

    void ApplyMode()
    {
        if (wander) wander.enabled = false;
        if (follow) follow.enabled = false;
        if (talker) talker.enabled = false;

        bool visible = (swapManager != null && swapManager.current == visibleInPerspectiveOf);
        if (!visible) return;

        if (wander) wander.enabled = (mode == ShadowMode.Wander);
        if (follow) follow.enabled = (mode == ShadowMode.FollowUnreachable);
        if (talker) talker.enabled = (mode == ShadowMode.TalkOnly);
    }
}