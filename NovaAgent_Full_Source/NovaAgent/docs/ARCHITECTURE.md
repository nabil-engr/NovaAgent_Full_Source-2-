# Architecture

```text
Microphone
   |
   v
NAudio 16 kHz mono recorder
   |
   | local WAV chunk
   v
whisper.cpp server (127.0.0.1)
   |
   | transcript
   v
Wake-word + conversation manager
   |
   v
CommandProcessor
   |
   +--> FileSearchService
   +--> Settings-backed custom app aliases
   +--> WindowsControlService
   +--> Confirmation gate
   |
   v
Windows action
   |
   v
System.Speech TTS response
```

## Main components

- `VoiceLoopService` — always-listening loop, wake word, conversation window.
- `WhisperService` — starts and calls a local whisper.cpp server.
- `AudioCaptureService` — records 16-bit, 16 kHz mono WAV segments.
- `CommandProcessor` — deterministic command/intent handling.
- `WindowsControlService` — Windows APIs, Core Audio, media keys, process launching.
- `FileSearchService` — context-based file discovery.
- `DiagnosticsService` — local runtime, model, microphone, OS, disk, and privacy checks.
- `SpeechOutputService` — local Windows TTS.
- `SettingsService` — normalized, atomic JSON persistence plus import/export.
- `HistoryService` — local command log plus clear/CSV export.
- `AppLog` — daily best-effort diagnostics under Local AppData.

## Reliability boundaries

- Only one Nova Agent process is allowed per Windows user session.
- Whisper startup is serialized so concurrent Listen/Start requests cannot launch duplicate servers.
- Microphone capture is serialized to prevent two capture operations using the same input device.
- File traversal runs off the UI thread, skips inaccessible/reparse-point folders, supports cancellation, and stops at the configured file limit.
- Runtime and model files are explicitly copied into build/publish output.

## Extending commands

Add a safe branch to `CommandProcessor.ProcessAsync`. Prefer explicit allow-listed actions instead of arbitrary shell commands.

For complex natural-language reasoning, add a separate planner that returns a strict JSON action schema and still passes every proposed action through a local allow-list and confirmation policy.
