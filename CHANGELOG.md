# Changelog

## 0.2.0 — 2026-10-27 (experimental)

- Automatically restore the full-width UI while the map is open and resume centring when it closes, including with Escape.
- Detect the in-game map's actual visibility and the menu map's component activity, independently of key bindings.
- Poll cached map objects with a 16 ms window timer; retain 500 ms object/layout discovery to avoid scanning the scene every frame.
- Preserve the user's manual F10 choice throughout automatic map transitions.
- Match the map's click-conversion dimensions to its expanded rectangle, fixing beacon placement across the full display. Restore the original input dimensions when the map closes or the add-on is disabled.

The in-game map switch and beacon placement across all three screens were confirmed working at 5760 × 1080 in a live session. Menu-map coverage and a fresh launch of the packaged v0.2.0 add-on remain unverified. Install/uninstall fixture tests pass.

## 0.1.0 — 2026-10-27 (experimental)

- Centre fixed HUD, menus, inventory and dialogs within a 16:9 frame at wide aspect ratios.
- Preserve full-resolution world rendering and full-screen visual surfaces.
- Save original UI anchors and offer an F10 toggle.
- Provide an installer/uninstaller, portable package, source build and installer fixture tests.

Fresh-launch loading through ReShade and the F10 toggle still need confirmation. See the README for tested scope and runtime-modding policy.
