using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance;

    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public HUDText hudText;                 // optional: damit du sticky/prompt clearen kannst
    public CanvasGroup overlayRoot;         // dein Overlay (Panel) als CanvasGroup
    public GameObject[] hideWhilePaused;    // optional: HUD etc.

    [Header("Behaviour")]
    public bool showOnStart = true;
    public bool pauseAudioListener = true;

    [Header("Debug")]
    public bool debugLogs = false;

    bool _isOpen;
    float _prevTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!hudText) hudText = FindObjectOfType<HUDText>();

        // Overlay initial aus, außer showOnStart
        if (overlayRoot != null)
        {
            SetOverlayVisible(false, interactable: false);
        }
    }

    void Start()
    {
        if (showOnStart)
        {
            OpenMenu(clearHud: true);
        }
    }

    void Update()
    {
        // Menü darf auch während Pause toggeln (Input läuft trotzdem)
        var inputs = (swapManager != null && swapManager.current != null) ? swapManager.current.inputs : null;
        if (inputs == null) return;

        if (inputs.menu)
        {
            if (_isOpen) CloseMenu();
            else OpenMenu(clearHud: true);
        }
    }

    // ---------------------------
    // UI Button Hooks
    // ---------------------------
    public void UI_PlayOrResume()
    {
        CloseMenu();
    }

    public void UI_Reset()
    {
        // Safety: Timescale wieder normal bevor reload
        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------------------------
    // Core
    // ---------------------------
    public void OpenMenu(bool clearHud)
    {
        if (_isOpen) return;
        _isOpen = true;

        if (clearHud && hudText != null)
        {
            hudText.ClearSticky();
            hudText.SetIdleAuto();
        }

        foreach (var go in hideWhilePaused)
            if (go) go.SetActive(false);

        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pauseAudioListener) AudioListener.pause = true;

        // Cursor frei fürs UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetOverlayVisible(true, interactable: true);

        if (debugLogs) Debug.Log("[PauseMenu] OPEN");
    }

    public void CloseMenu()
    {
        if (!_isOpen) return;
        _isOpen = false;

        SetOverlayVisible(false, interactable: false);

        foreach (var go in hideWhilePaused)
            if (go) go.SetActive(true);

        Time.timeScale = Mathf.Approximately(_prevTimeScale, 0f) ? 1f : _prevTimeScale;
        if (pauseAudioListener) AudioListener.pause = false;

        // Cursor wieder locken (wenn ihr das wollt)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (debugLogs) Debug.Log("[PauseMenu] CLOSE");
    }

    void SetOverlayVisible(bool visible, bool interactable)
    {
        if (!overlayRoot) return;

        overlayRoot.alpha = visible ? 1f : 0f;
        overlayRoot.interactable = interactable;
        overlayRoot.blocksRaycasts = interactable;
    }

    public bool IsOpen => _isOpen;
}