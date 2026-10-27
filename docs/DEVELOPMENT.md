# Development notes

The add-on uses ReShade API 20 and resolves Unity IL2CPP types and methods by name.

## UI layout

`SafeFrameScaler.ScalingTransforms` supplies the UI roots. Their horizontal anchors are moved into a centred 16:9 frame at aspect ratios above 2.4:1. Nested roots are skipped to avoid applying the margin twice. Backgrounds, camera effects and world-space markers keep their original layout.

Original anchors are saved with GC handles and restored when centring is turned off or the add-on is disabled.

## Maps and input

`MapUIComponent.IsVisible()` identifies the in-game map. The menu's `MapView` is checked through its component activity. Map objects are cached and checked by a 16 ms window timer; object discovery and ordinary UI scans run every 500 ms. Map transitions skip the layout scan delay.

Expanding the map's anchors isn't enough for mouse input. The game keeps a separate `MapUI.m_targetResolution`, which stayed at 1920 × 1080 when the displayed rectangle grew to 5760 × 1080. The add-on matches that field to `rectTransform.rect` in local UI units while the map is visible, then restores its original value on close.

All Unity calls run on the game's window thread. The DLL stays loaded until the game exits so its window callbacks remain valid after it is disabled.

## Files

Setup writes `surround-ui.addon64` and `.surround-ui-install.json`. It checks the embedded payload hash and refuses to overwrite unknown or modified add-ons. ReShade detection reads DLL export tables without loading the DLL.

The add-on writes its toggle to `surround-ui.ini` and diagnostic messages to `surround-ui.log`. Uninstall keeps both files. Game DLLs, display settings and saves aren't edited.

## Building and testing

Run `build.ps1` from PowerShell. Use `-Version 0.2.0` to select a version and `-SkipTests` to skip installer tests. Test results are left in a `TrailmakersUiInstallerTests-*` folder under the Windows temporary directory.

The dummy exports in `tests/reshade_stub.cpp` are a PE detection fixture, not a ReShade loader. Don't distribute that DLL with the add-on.
