# Security and privacy

Nova is intentionally designed as a local PC assistant rather than an unrestricted remote-control shell.

## Local speech processing
The default voice path is:
microphone -> local WAV -> whisper.cpp on 127.0.0.1 -> command parser.

No OpenAI API key is required for the supplied core build.

## whisper-server binding
Nova starts whisper-server on `127.0.0.1`, not `0.0.0.0`. Do not expose the Whisper HTTP server to your LAN or the public internet.

Do not run Nova or whisper-server as Administrator unless you have a specific reason and understand the consequences.

## Protected commands
Shutdown, restart, and sleep require explicit confirmation. Extend the same confirmation mechanism if you later add:
- permanent deletion
- software uninstall
- credential changes
- financial actions
- sending messages/emails
- shell/PowerShell execution

## No generic shell execution
The bundled command parser does not expose arbitrary PowerShell/CMD execution from voice. This is deliberate. An always-listening microphone plus unrestricted shell access creates a serious accidental/unauthorized command risk.

## Microphone
Use the Start/Stop controls or tray menu to disable listening. Closing the main window may leave Nova running in the tray if "Close window to tray" is enabled.

## History
A local command history is stored under:
`%LOCALAPPDATA%\NovaAgent\history.jsonl`

Settings are stored under:
`%APPDATA%\NovaAgent\settings.json`

Daily diagnostic logs are stored under:
`%LOCALAPPDATA%\NovaAgent\Logs`

## Custom app aliases

Custom aliases can launch an executable/path explicitly configured by the current Windows user. They do not accept voice-supplied command-line arguments and do not turn speech into shell code. Treat imported settings files as executable configuration: review custom app paths before saving them on a new PC.

## Logs and exports

Logs can include error messages and local paths. History CSV exports contain commands and results. Review these files before sharing them. Nova does not automatically upload logs, history, audio, or settings.
