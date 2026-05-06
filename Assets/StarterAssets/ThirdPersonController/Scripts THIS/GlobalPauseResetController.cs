using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GlobalPauseResetController : MonoBehaviour
{
    [Header("Input Actions (from your Input Actions asset)")]
    public InputActionReference pauseAction; // assign: Global/Pause
    public InputActionReference resetAction; // assign: Global/Reset

    [Header("Settings")]
    public bool pauseInitially = false;
    public float pausedTimeScale = 0f;
    public bool pauseAudioListener = true;

    [Header("Debug")]
    public bool debugLogs = false;

    bool _isPaused;

    void Awake()
    {
        // Optional: persist across scene reloads (wenn ihr nur 1 Scene nutzt, egal)
        // DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        if (pauseAction?.action != null)
        {
            pauseAction.action.Enable();
            pauseAction.action.performed += OnPausePerformed;
        }
        else Debug.LogError("[GlobalPauseResetController] pauseAction missing.");

        if (resetAction?.action != null)
        {
            resetAction.action.Enable();
            resetAction.action.performed += OnResetPerformed;
        }
        else Debug.LogError("[GlobalPauseResetController] resetAction missing.");
    }

    void OnDisable()
    {
        if (pauseAction?.action != null)
            pauseAction.action.performed -= OnPausePerformed;

        if (resetAction?.action != null)
            resetAction.action.performed -= OnResetPerformed;
    }

    void Start()
    {
        if (pauseInitially) SetPaused(true);
    }

    void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    void OnResetPerformed(InputAction.CallbackContext ctx)
    {
        ResetScene();
    }

    public void TogglePause()
    {
        SetPaused(!_isPaused);
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;

        Time.timeScale = _isPaused ? pausedTimeScale : 1f;

        if (pauseAudioListener)
            AudioListener.pause = _isPaused;

        if (debugLogs)
            Debug.Log($"[GlobalPauseResetController] paused={_isPaused} timeScale={Time.timeScale}");
    }

    public void ResetScene()
    {
        // Safety: unpause before reload
        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;

        if (debugLogs) Debug.Log("[GlobalPauseResetController] Reload active scene");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}