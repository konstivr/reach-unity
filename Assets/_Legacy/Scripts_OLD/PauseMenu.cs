using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarterAssets;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
#endif

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Root-Panel/Canvas des Overlays (nur Visual). Dieses Objekt darf an/aus gehen.")]
    public GameObject overlayRoot;

    [Tooltip("Button der beim Öffnen automatisch selektiert wird (Play/Resume).")]
    public Button playOrResumeButton;

    [Tooltip("Reset Button (optional).")]
    public Button resetButton;

    [Header("Input Source")]
    [Tooltip("Wenn ihr SwapManager habt, lasst fallbackInputs leer und setzt swapManager.")]
    public StarterAssetsInputs fallbackInputs;
    public PerspectiveSwapManager swapManager;

    [Header("Menu Toggle (Input System)")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Keyboard-Taste zum Öffnen/Schließen (Input System Key).")]
    public Key toggleKey = Key.M;

    [Tooltip("Optional: InputActionReference fürs Menü-Toggle (z.B. Joystick Button).")]
    public InputActionReference toggleMenuAction;
#endif

    [Header("Behaviour")]
    public bool startWithOverlay = true;

    [Tooltip("Wie stark muss move.y sein, um Auswahl zu wechseln?")]
    public float navThreshold = 0.55f;

    [Tooltip("Cooldown (Sek) zwischen Up/Down Switches (unscaled, funktioniert bei TimeScale=0).")]
    public float navRepeatDelay = 0.22f;

    [Header("Reset")]
    public bool resetReloadsScene = true;
    public string resetSceneName = ""; // leer = active scene

    bool _open;
    float _nextNavTimeUnscaled;

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.Enable();
        }
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            toggleMenuAction.action.Disable();
        }
#endif
    }

    void Start()
    {
        if (startWithOverlay) Open();
        else Close();
    }

    void Update()
    {
        // ---- MENU TOGGLE ----
        if (WasMenuTogglePressedThisFrame())
        {
            if (_open) Close();
            else Open();
        }

        if (!_open) return;

        var inputs = GetInputs();
        if (inputs == null) return;

        // ---- NAVIGATION (Up/Down only) ----
        float y = inputs.move.y;

        if (Time.unscaledTime >= _nextNavTimeUnscaled)
        {
            if (y <= -navThreshold)
            {
                SelectReset();
                _nextNavTimeUnscaled = Time.unscaledTime + navRepeatDelay;
            }
            else if (y >= navThreshold)
            {
                SelectPlay();
                _nextNavTimeUnscaled = Time.unscaledTime + navRepeatDelay;
            }
        }

        // ---- SUBMIT via Interact (Pulse) ----
        if (inputs.interact)
        {
            PressSelectedOrDefault();
        }
    }

    StarterAssetsInputs GetInputs()
    {
        if (swapManager != null && swapManager.current != null && swapManager.current.inputs != null)
            return swapManager.current.inputs;

        return fallbackInputs;
    }

    bool WasMenuTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        // 1) Optional Action (Joystick Button)
        if (toggleMenuAction != null && toggleMenuAction.action != null)
        {
            if (toggleMenuAction.action.WasPressedThisFrame())
                return true;
        }

        // 2) Keyboard fallback (M)
        if (Keyboard.current != null)
        {
            // robust: über Key enum
            return Keyboard.current[toggleKey].wasPressedThisFrame;
        }

        return false;
#else
        // Wenn ihr NICHT im Input System seid (eigentlich bei euch nicht der Fall)
        return false;
#endif
    }

    void PressSelectedOrDefault()
    {
        if (EventSystem.current == null)
        {
            playOrResumeButton?.onClick.Invoke();
            return;
        }

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null)
        {
            var btn = selected.GetComponent<Button>();
            if (btn != null && btn.interactable)
            {
                btn.onClick.Invoke();
                return;
            }
        }

        playOrResumeButton?.onClick.Invoke();
    }

    void SelectPlay()
    {
        if (playOrResumeButton == null) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(playOrResumeButton.gameObject);

        playOrResumeButton.Select();
    }

    void SelectReset()
    {
        if (resetButton == null) return;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(resetButton.gameObject);

        resetButton.Select();
    }

    // ---------- UI BUTTON HOOKS ----------
    public void UI_PlayOrResume()
    {
        Close();
    }

    public void UI_Reset()
    {
        Time.timeScale = 1f;

        if (!resetReloadsScene)
        {
            Close();
            return;
        }

        if (!string.IsNullOrEmpty(resetSceneName))
            SceneManager.LoadScene(resetSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ---------- OPEN/CLOSE ----------
    public void Open()
    {
        _open = true;

        if (overlayRoot) overlayRoot.SetActive(true);

        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SelectPlay();
        _nextNavTimeUnscaled = Time.unscaledTime + 0.15f;
    }

    public void Close()
    {
        _open = false;

        if (overlayRoot) overlayRoot.SetActive(false);

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}