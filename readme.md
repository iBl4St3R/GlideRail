# GlideRail v0.1.0

Cinematic camera path recorder for Car Mechanic Simulator 2026.

Record keyframe-based camera paths and play them back as smooth Catmull-Rom splines. Share paths with other players via a single console command.

## Requirements

- Car Mechanic Simulator 2026 (Demo or Full)
- MelonLoader v0.7.2+
- _CMS2026_UITK_Framework v0.2.1+
- CMS2026 Simple Console v1.2.0+ (optional, for export/import)

## Installation

1. Install MelonLoader
2. Place `_CMS2026_UITK_Framework.dll` in `Mods/`
3. Place `GlideRail.dll` in `Mods/`

## Usage

Open the console (F7) and type `gliderail_open`.

### Controls

| Key | Action |
|-----|--------|
| WASD | Move camera |
| Mouse | Look |
| Q / E | Roll |
| R | Move up |
| F | Move down |
| F5 | Add keyframe |
| F6 | Remove last keyframe |
| F9 | Toggle UI / Fly mode |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |

### Console Commands

| Command | Description |
|---------|-------------|
| `gliderail_open` | Open / close panel |
| `gliderail_export` | Export path as `GlideRailPlay` command |
| `gliderail_status` | Show session info |
| `GlideRailPlay <data>` | Play exported path (no mod needed*) |

*requires Simple Console

### Sharing Paths

1. Record your path
2. Type `gliderail_export` in console
3. Copy the `GlideRailPlay ...` line
4. Share it — anyone with Simple Console can paste and play it

### Inspector (KF Editor)

Click any keyframe number in the timeline to open the inspector:
- View position
- Adjust speed multiplier (×0.1 – ×5.0)
- Jump to keyframe
- Replace with current camera position
- Delete

## Changelog

### v0.1.0
- Initial release
- Catmull-Rom spline playback
- Timeline UI with scroll
- KF Inspector with speed control
- Undo/Redo (Ctrl+Z/Y, 64 steps)
- Export/Import via JSON→GZip→Base64
- 3D debug renderer (spline line + sphere markers)

## Author

Blaster — [GitHub](https://github.com/iBl4St3R/GlideRail)