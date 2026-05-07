using UnityEngine;
using System;

public class ShadowStatic : MonoBehaviour
{
    [Header("Assignment")]
    [Tooltip("Shadow ist nur sichtbar, wenn dieser Character aktiv possessed ist.")]
    public PossessableCharacter visibleInPerspectiveOf;

    public PerspectiveSwapManager swapManager;

    [Header("Hide Condition")]
    [Tooltip("Wenn diese Task für den Owner erledigt ist, verschwindet der Shadow.")]
    public string hideWhenTaskDoneId;

    [Tooltip("Wenn true: deaktiviert das komplette Shadow-GameObject (empfohlen).")]
    public bool disableWholeObject = true;

    [Header("Optional: if not disabling whole object")]
    public Renderer[] renderersToToggle;

    QuestStateManager _quests;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();

        if (!disableWholeObject)
        {
            if (renderersToToggle == null || renderersToToggle.Length == 0)
                renderersToToggle = GetComponentsInChildren<Renderer>(true);
        }
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;

        _quests = QuestStateManager.Instance;
        if (_quests != null)
            _quests.TaskCompleted += OnTaskCompleted;

        Refresh();
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;

        if (_quests != null)
            _quests.TaskCompleted -= OnTaskCompleted;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to) => Refresh();

    void OnTaskCompleted(PossessableCharacter c, string taskId)
    {
        if (visibleInPerspectiveOf == null) return;
        if (c != visibleInPerspectiveOf) return;
        if (string.IsNullOrEmpty(hideWhenTaskDoneId)) return;
        if (taskId != hideWhenTaskDoneId) return;

        Refresh();
    }

    void Refresh()
    {
        // 1) Wenn Quest done -> weg
        if (ShouldHideByQuest())
        {
            HideNow();
            return;
        }

        // 2) sonst sichtbar nur in passender Perspektive
        bool visibleNow = (swapManager != null &&
                           swapManager.current != null &&
                           swapManager.current == visibleInPerspectiveOf);

        SetVisible(visibleNow);
    }

    bool ShouldHideByQuest()
    {
        if (visibleInPerspectiveOf == null) return false;
        if (string.IsNullOrEmpty(hideWhenTaskDoneId)) return false;

        if (_quests == null) _quests = QuestStateManager.Instance;
        if (_quests == null) return false;

        return _quests.IsTaskDone(visibleInPerspectiveOf, hideWhenTaskDoneId);
    }

    void HideNow()
    {
        if (disableWholeObject)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        else
        {
            SetVisible(false);
        }
    }

    void SetVisible(bool visible)
    {
        if (disableWholeObject)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            return;
        }

        if (renderersToToggle != null)
            foreach (var r in renderersToToggle)
                if (r) r.enabled = visible;
    }
}