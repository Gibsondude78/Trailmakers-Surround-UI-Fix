# Trailmakers Surround UI Fix

Keep the HUD, menus, inventory and dialogs on the centre screen while the 3D world spans your NVIDIA Surround display. The game resolution stays unchanged.

Built to address inventory dragging reporting that the cursor is outside the build area on a 5760 × 1080, three-monitor setup. This is an **experimental, unofficial Windows x64 add-on**.

## Download and install

1. [Download the latest release](https://github.com/Gibsondude78/Trailmakers-Surround-UI-Fix/releases). Choose `TrailmakersSurroundUI-Setup-0.1.0.exe`.
2. Close Trailmakers. Install **ReShade 6.8 or newer with full add-on support** from [the official ReShade website](https://reshade.me/#download). Select `Trailmakers.exe` and DirectX 10/11/12. The standard ReShade build is insufficient. ReShade is a separate prerequisite and is not included here.
3. Open this project's setup EXE. Check the detected game folder, or select the folder containing `Trailmakers.exe` and `GameAssembly.dll` using Browse.
4. Click **Install**, then launch the game normally. ReShade should list **Trailmakers Centre Screen UI** under Add-ons.

The installer requires Windows 10/11 x64 with .NET Framework 4.8 (normally included). The EXE and add-on are unsigned. Obtain them from this repository's releases and compare `SHA256SUMS.txt` if needed.

### Use and remove

- The layout applies automatically when the display aspect ratio exceeds 2.4:1. At 5760 × 1080, the UI frame occupies the middle 1920 × 1080 screen. The calculation uses a centred 16:9 frame, so unusual monitor layouts may need changes to the source.
- **F10** toggles the layout and saves the choice in `surround-ui.ini`. Keyboard handling can vary with overlays; if necessary, close the game and set `[Inventory]` / `Enabled=1` or `Enabled=0` in that file.
- Run the same setup EXE, select the game folder and click **Uninstall**. To update, uninstall the old version first, then install the new one. Your INI settings are retained.
- Manual installation: extract only `surround-ui.addon64` from the portable ZIP into the same folder as `Trailmakers.exe`, after installing ReShade with full add-on support. Remove that file while the game is closed to uninstall manually.

## Compatibility and policy

This is runtime DLL modding, outside Trailmakers' supported Lua modding system. The [Trailmakers modding wiki](https://trailmakers.wiki.gg/wiki/Modding) warns that DLL/runtime modifications may have ban consequences. This project does not claim multiplayer approval or ban safety. It is not affiliated with Flashbulb Games, NVIDIA or ReShade.

Observed during development: inventory dragging worked at 5760 × 1080 after centring the inventory; extending the same mechanism adjusted 58 UI roots in the running game. The full layout was tested in a live session on game executable version 253.518.205.0 and ReShade 6.8.0. A fresh launch using the packaged ReShade installation has **not yet been verified**. F10 handling is implemented but has not been conclusively verified in-game. The installer is tested separately against temporary fixtures. Other game versions, modes and resolutions are not yet verified.

The add-on changes UI anchors in memory. It does not patch game DLLs, change display settings or edit vehicles/saves. Screen backgrounds, world-projected markers and camera effects remain across the full viewport. Some screens may need additional filtering; please report them with screenshots.

Game updates can change Unity's internal types or fields. Missing required metadata stops the adjustment and records a message; this is not a guarantee against every possible incompatibility. Restart the game after changing or removing the add-on. Avoid loading multiple copies under different filenames.

## What setup writes

Setup creates `surround-ui.addon64` and `.surround-ui-install.json` in the selected game folder. It checks embedded payload integrity, refuses to replace unknown or modified add-ons, and blocks changes while that game's process is running. ReShade detection reads PE exports without executing the selected DLL; it does not prove that the installed build permits add-ons.

At runtime, the add-on may create `surround-ui.ini` and `surround-ui.log` alongside itself. Uninstall preserves those files, ReShade and unrelated add-ons. No telemetry, account access or network requests are implemented in the add-on or installer; setup opens the official download/project links only when clicked.

## Build from source

Install Visual Studio 2022 Build Tools with **Desktop development with C++**, a Windows 10/11 SDK, and the Windows .NET Framework compiler. From PowerShell in this repository:

```powershell
.\build.ps1
```

This builds the native add-on, a standalone installer with the add-on embedded, and the portable ZIP into `dist/`. Installer fixture tests run by default and leave their result folder under the Windows temporary directory. `-Version 0.1.0` selects the release version. No ReShade or game binaries are redistributed. The PE fixture in `tests/` implements dummy export names solely to test detection; it cannot load add-ons.

`src/addon.cpp` resolves Unity IL2CPP methods by name, finds `SafeFrameScaler.ScalingTransforms`, skips nested roots and full-viewport surfaces, and saves original anchors for restoration. A window timer runs changes on Unity's window thread. The module stays pinned until game exit so window callbacks remain valid after disabling it. It uses ReShade API version 20.

## Report an issue

[Open an issue](https://github.com/Gibsondude78/Trailmakers-Surround-UI-Fix/issues) with your game version, ReShade version/build, resolution, affected screen and relevant `surround-ui.log` lines. Review screenshots and logs for personal information before posting. Do not upload game DLLs or save files.

MIT licensed; see [LICENSE](LICENSE). ReShade and Trailmakers have their own licenses and terms.
