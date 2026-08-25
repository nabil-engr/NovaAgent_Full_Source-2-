# Nova Agent command examples

Nova accepts English, Bangla script, and many mixed phrases. Local Whisper determines the transcription, so wording may vary.

## Wake / conversation
- "Nova"
- "Hey Nova"
- "নোভা"
- "Nova, Downloads folder open"
- After waking Nova, continue naturally for the configured conversation window.
- "go to sleep" / "ঘুমাও" ends the active conversation window.

## Files and folders
- "Downloads folder open"
- "Desktop open"
- "Documents folder e jao"
- "song.mp4 open koro"
- "latest PDF open"
- "newest mp4 open"
- "create folder Project Files"

Nova remembers the most recently opened folder. Example:
1. "Nova, Downloads folder e jao"
2. "song.mp4 open koro"

## Apps
- "Chrome open"
- "Edge open"
- "VS Code open"
- "Notepad open"
- "Calculator open"
- "File Explorer open"
- "Task Manager open"
- "Settings open"
- "Terminal open"

Additional apps can be added under **Settings → Custom app aliases** using one `alias=path` entry per line. Then say, for example, "OBS open".

## Media and volume
- "volume 70"
- "volume barau"
- "volume komao"
- "mute"
- "pause"
- "play"
- "next song"
- "previous song"

## Web
- "Google search AI news"
- "YouTube search Interstellar soundtrack"
- "open example.com"

## Windows
- "switch window"
- "minimize"
- "maximize"
- "restore window"
- "screenshot"
- "type hello from Nova"
- "what time is it"
- "today date"
- "current folder open"
- "recycle bin open"

## Keyboard and browser shortcuts

- "copy" / "cut" / "paste"
- "save" / "undo" / "redo" / "select all"
- "new tab" / "close tab"
- "browser back" / "browser forward"
- "refresh page"

Shortcuts are sent to the foreground application. Use them by voice while the intended application is active.

## Protected commands
The following require a second confirmation:
- shutdown
- restart
- sleep

Example:
1. "Nova, shutdown the PC"
2. Nova: "Please say confirm..."
3. "confirm"

## Text test
You do not need the microphone to test the command layer. Type any command in the Nova window and press **Run**.
