# Map and UI regression checks

Use a wide-aspect display with ReShade full add-on support. Record game version, display resolution and fix version when reporting results.

| Case | Expected result | v0.2.0 status |
| --- | --- | --- |
| Open the in-game map with centring enabled | Map and UI use the full viewport | User confirmed at 5760 × 1080 |
| Close the map with its toggle and with Escape | HUD returns to the centre screen | User confirmed |
| Repeat opening and closing the map | Both directions remain responsive | User confirmed after cached polling change |
| Inspect the toggle INI after map transitions | Automatic transitions do not change it | Temporary test produced no toggle INI |
| Place beacons on the left, centre and right of the map | Markers appear where clicked; closing still centres the HUD | User confirmed after synchronizing input dimensions |
| Open the menu's map panel, then change panels | Full width only while that map is visible | Implemented; still needs confirmation |
| Disable centring with F10, then open/close a map | UI remains full width after closing | Still needs confirmation |
| Toggle F10 while a map is open | Map stays full width; closing follows the new choice | Still needs confirmation |
| Change worlds / return to the main menu | Destroyed map objects are released; new ones are discovered | Still needs confirmation |
| Run the v0.2.0 release | Add-on loads and UI/map interaction works | User confirmed v0.2.0 working on 2026-10-27 |
| Drag inventory blocks after closing a map | Placement works within the centre-screen build area | Original inventory fix confirmed; map regression check pending |

Automated installer fixture tests are separate and run through `build.ps1`. They do not simulate the game's Unity runtime or establish map behaviour.
