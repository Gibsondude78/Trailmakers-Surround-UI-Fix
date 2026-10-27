# Trailmakers Surround UI Fix

A fix for the build inventory on triple-monitor NVIDIA Surround setups. It keeps the HUD, menus and inventory on the middle monitor without changing the game's resolution.

The map still fills all three screens. Close it and the UI moves back to the middle. Beacon placement is corrected to match the wider map.

**[Download v0.2.0](https://github.com/Gibsondude78/Trailmakers-Surround-UI-Fix/releases/tag/v0.2.0)** · Windows 10/11, 64-bit

## Install

1. Install **ReShade 6.8 or newer with full add-on support** from [reshade.me](https://reshade.me/#download). Select `Trailmakers.exe` and DirectX 10/11/12. The standard ReShade download won't load this add-on.
2. Close the game and run `TrailmakersSurroundUI-Setup-0.2.0.exe`.
3. Check the game folder, then click **Install**. Use **Browse** if setup hasn't found it.
4. Start Trailmakers. **Trailmakers Centre Screen UI** should appear in ReShade's Add-ons tab.

ReShade isn't bundled. The installer needs .NET Framework 4.8, which is normally already installed on Windows 10/11. The downloads are unsigned; checksums are included with each release.

For a manual install, extract `surround-ui.addon64` from the portable ZIP into the folder containing `Trailmakers.exe`.

## Controls

**F10** turns UI centring on or off. Opening the map temporarily switches to the full display; closing it restores your setting. Escape works too.

If you disabled the old version to use the map, press F10 once to turn centring back on. You can also close the game and set `Enabled=1` under `[Inventory]` in `surround-ui.ini`.

## Update or uninstall

Close Trailmakers and run setup again. Click **Uninstall** to remove the add-on. To update, uninstall the old version first, then click **Install** in the new setup.

Your settings, ReShade and other add-ons are kept. For a manual uninstall, delete `surround-ui.addon64` while the game is closed.

## Compatibility

v0.2.0 is working at **5760 × 1080**, with Trailmakers **253.518.205.0** and ReShade **6.8.0**. Inventory dragging, map switching and beacon placement across all three monitors have been tested.

The UI uses a centred 16:9 area on displays wider than 2.4:1. Other monitor arrangements may need adjustments. The separate menu-map screen and switching between worlds still need testing; see [test notes](tests/MANUAL.md). Game updates may break the fix.

This is an unofficial DLL mod. DLL mods fall outside Trailmakers' supported Lua modding system, and the [modding wiki](https://trailmakers.wiki.gg/wiki/Modding) warns of possible bans. It has no multiplayer approval.

## Problems?

If the add-on doesn't appear in ReShade, check that you installed the **full add-on support** build. Avoid putting multiple copies of the fix in the game folder, and restart the game after installing or removing it.

[Report a bug](https://github.com/Gibsondude78/Trailmakers-Surround-UI-Fix/issues) with your game version, ReShade version, resolution and a screenshot of the problem. Relevant lines from `surround-ui.log` help too. Check attachments for personal information before posting.

## Build

Requires Visual Studio 2022 C++ Build Tools, a Windows 10/11 SDK and the .NET Framework compiler.

```powershell
.\build.ps1
```

The installer, add-on and portable ZIP are written to `dist/`. Installer tests run as part of the build. See [development notes](docs/DEVELOPMENT.md) for how the fix works.

[MIT licence](LICENSE). No game or ReShade binaries are included.
