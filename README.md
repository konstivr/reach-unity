# Reach Unity

Interactive perspective-switching game framework. Built by **Koko & Tobi**.

The game lets the player approach NPCs, "reach out" via spoken passphrase, and switch into their perspective. Each switch reduces visual filters until the world becomes clear.

The framework is **content-agnostic**: story content lives in *Story Packs* (ScriptableObjects + prefabs), the framework code is reusable across themes. Built initially for the *Reach* concept, currently being adapted for **DDR Geschichten**.

---

## Branches

- `main` — original Reach codebase (research-prototype, not architectural reference)
- `framework-rewrite` — clean re-architecture as a content-pack framework. **Active branch.**

---

## Requirements

### Unity

- Unity 6.0+ (uses URP)
- Required packages: TextMeshPro, Cinemachine, Input System, Universal Render Pipeline

### External (for real voice/LLM)

- **Whisper.cpp** for STT
  - macOS: `brew install whisper-cpp` → binary at `/opt/homebrew/opt/whisper-cpp/bin/whisper-cli`
  - Windows: download from [whisper.cpp releases](https://github.com/ggerganov/whisper.cpp/releases)
  - Model file (e.g. `ggml-small.bin`) from [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp)
- **Ollama** for LLM
  - Install from [ollama.com](https://ollama.com)
  - `ollama pull llama3` (or another model)
  - Server runs on `http://localhost:11434`

You can run **without** these — the framework includes Stub backends for testing.

---

## Quick Start

1. Clone repo, open in Unity, switch to `framework-rewrite` branch.
2. Open `Assets/_Packs/_TestPack/Scenes/SmokeTest.unity` (or `Outreach_SmokeTest.unity`).
3. Press Play.
4. Walk to the second character with WASD or left stick.
5. Press **E** (or X on Xbox) → "reach out" prompt → character speaks gate line.
6. Press **Space** (or B on Xbox) → speak the passphrase ("say something" by default in test pack).
7. The view switches into the new character.

---

## Architecture

The framework follows a **service-locator pattern** centered on `GameContext`. Systems register themselves on Awake; other systems read services via `GameContext.Instance.X`.
GameContext
├── pack: StoryPack          ← content
├── Input: InputReader       ← cross-platform input
├── Characters: Registry     ← all PossessableCharacters
├── Perspective              ← who's controlled, switching, progress
├── Speech                   ← STT/TTS/Chat backends
├── Hud                      ← single text element with mode-locks
├── Gate                     ← outreach + passphrase
├── Dialogue                 ← chat pipeline (STT → LLM → TTS)
└── Pause                    ← time + audio pause

### Folder Structure
Assets/
├── _Framework/              ← Pack-agnostic engine code
│   ├── Core/                ← GameContext, PerspectiveManager, characters, pause
│   ├── Input/               ← InputReader, Vector2Filter
│   ├── HUD/                 ← HudText with mode locks
│   ├── Interaction/         ← Gate, InteractableObject, Router
│   ├── Dialogue/            ← STT/TTS/Chat interfaces + backends
│   ├── FX/                  ← TransitionFX, ChronoPerception
│   └── Audio/               ← LayeredMusicConductor
├── _Packs/                  ← Story content (swappable)
│   └── _TestPack/           ← Bare-bones smoke-test pack
│       ├── Characters/      ← Character SOs
│       ├── Audio/           ← Stems, voice clips, ambient
│       ├── Prefabs/         ← Character prefabs, object prefabs
│       └── Scenes/          ← Test scenes
└── _Legacy/                 ← Old Reach codebase, excluded from compilation

---

## Pack System

A **Story Pack** is one ScriptableObject manifest pointing to:

- A list of **CharacterDefinitions** (one per character)
- An ordered list of **music layers** (each switch adds the next)
- A language code for STT
- Total perspective count for progress tracking

A **CharacterDefinition** holds:

- Display name + ID
- Character prefab reference
- Gate TTS line + passphrase + similarity threshold
- Chat system prompt
- macOS + Windows voice names
- Per-character ambient loop
- Reference to one InteractableObjectDefinition (the character's single interactable object)

An **InteractableObjectDefinition** holds:

- Object prefab
- Mode (OneShot / TwoStep)
- Range, prompts, response text/duration
- Audio clips
- Whether completing the action unlocks outreach

### Adding a new pack

1. Create folder `Assets/_Packs/MyPack/`
2. Right-click → Create → Reach → **Story Pack** → fill in fields
3. Right-click → Create → Reach → **Character Definition** for each character
4. (Optional) → **Interactable Object** for each interactable
5. Drag character SOs into the StoryPack's `characters` list
6. Set the StoryPack as `GameContext.pack` in your scene

---

## Input

Single InputActions asset at `Assets/_Framework/Input/ReachInputActions.inputactions`. Action map: `Player`.

| Action       | Keyboard          | Xbox        |
|--------------|-------------------|-------------|
| Move         | WASD              | Left stick  |
| Look         | Mouse delta       | Right stick |
| Sprint       | Left Shift        | L3 click    |
| Jump         | Left Ctrl         | Y button    |
| Interact     | E                 | X button    |
| Speak        | Space             | B button    |
| Cancel       | Escape            | A button    |
| Pause        | P                 | Start       |

Stick drift is auto-calibrated by `InputReader` via `Vector2Filter`.

---

## Speech Backends

Selected per-component on the `SpeechSystem` GameObject. Drop the backend you want to use, link it in the slot.

**STT options:**
- `StubSpeechToText` — returns a fixed string (good for testing without Whisper)
- `WhisperSubprocessSTT` — runs whisper.cpp as subprocess (Mac + Windows)

**TTS options:**
- `StubTextToSpeech` — beep with duration proportional to text length
- `MacSayTTS` — macOS `say` command
- `WindowsSapiTTS` — Windows SAPI via PowerShell

**Chat options:**
- `StubChatBackend` — fixed reply
- `OllamaChatBackend` — local Ollama HTTP

---

## HUD Mode Locks

`HudText` has a single text element with explicit modes to prevent systems from fighting over it:
priority: FXOverride > Sticky > TimedLock > Intro > IdleAuto / Prompt

Other systems should check `hud.IsFree` before writing to `IdleAuto` / `Prompt`. Each mode has clear ownership: Gate writes Sticky for gate lines, DialogueManager writes FXOverride during NPC speech, etc.

---

## Build Targets

- **macOS:** Editor + Standalone (Apple Silicon + Intel)
- **Windows:** Editor + Standalone (x64)

Both built from the same Unity project. Platform-specific code paths (Whisper binary path, TTS backend) resolve via `#if UNITY_*` directives.

---

## Known Limitations

- **Movement camera-relative coupling.** Cinemachine VCam binding mode must be `World Space` (Transposer). `Lock To Target With World Up` causes feedback-loop drift. See VCam setup in scenes.
- **Whisper STT first-call latency.** Cold load of the model takes 1-3 seconds; subsequent calls are fast.
- **MacSay vs. SAPI tone consistency.** Voices on the two platforms sound very different. For a polished pack, consider pre-rendering character lines (use real audio clips instead of TTS at runtime).

---

## Credits

Koko & Tobi
