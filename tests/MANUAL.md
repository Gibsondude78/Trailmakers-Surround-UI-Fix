# Test notes

Test setup: 5760 × 1080 NVIDIA Surround, Trailmakers 253.518.205.0, ReShade 6.8.0.

## Checked

- Inventory blocks can be dragged into the build area with the vehicle on the middle monitor.
- The in-game map fills all three screens.
- Closing the map with its toggle or Escape brings the HUD back to the middle monitor.
- Repeated map switches stay responsive.
- Beacons appear where clicked on the left, centre and right of the map.
- Automatic map switches leave the saved toggle setting alone.
- The v0.2.0 release works on the setup above.

## Still to check

- The map panel inside the game menu.
- Opening and closing a map with F10 disabled.
- Changing the F10 setting while a map is open.
- Changing worlds and returning to the main menu.
- Inventory dragging after closing the map.
- Different resolutions and monitor layouts.

## Installer tests

`build.ps1` tests installation, repeated installation, removal, and preservation of existing files in temporary game folders. It also checks missing ReShade loaders, conflicting or modified add-ons and invalid install records. These tests don't run the game.
