# CLAUDE.md — NewRhinoGold

## Project Overview

**NewRhinoGold** is a Rhino 3D plugin (`.rhp`) for professional jewelry design. It provides specialized tools for creating and editing rings, gems, bezels, cutters, heads, and pavé settings within Rhinoceros 8.

- **Language:** C#
- **Frameworks:** .NET 7.0 / .NET Framework 4.8, RhinoCommon 8.x, Eto.Forms (UI)
- **Output:** `.rhp` (Rhino Plugin) — loaded at Rhino startup
- **Domain:** Jewelry CAD — gems, rings, bezels, cutters, heads, pavé, engraving

## Repository Structure

```
NewRhinoGold/
├── BezelStudio/         # Bezel, cutter, head geometry builders + pavé placement
├── Commands/            # Rhino command classes (one per feature, 19 commands)
├── Core/                # Core algorithms, data models, SmartData, profile libraries
├── Dialog/              # Main toolbar panel and primary dialogs (Eto.Forms)
├── Helpers/             # Utility functions (selection, profiles, menus, text input)
├── Icons/               # 15 embedded PNG icons (256x256) for UI buttons
├── Studio/              # Studio dialogs for gem, cutter, head, pavé editing
├── Wizard/              # Ring wizard multi-step dialog
├── Densities.cs         # Material density database (13 gems, 9+ metals)
├── GemDisplayCond.cs    # Display conduit for gem visualization/preview
├── NewRhinoGold.csproj  # SDK-style MSBuild project
└── NewRhinoGoldPlugIn.cs # Plugin entry point (auto-load, panel registration)
```

## Build & Dependencies

**Build system:** .NET SDK (MSBuild), SDK-style `.csproj`

**Target frameworks:** `net7.0` and `net48` (dual-target)

**Dependencies:**
- `RhinoCommon` 8.0.23304.9001 — Rhino 3D API
- `System.Drawing.Common` 7.0.0 — Graphics / color handling

**Build command:**
```bash
dotnet build NewRhinoGold.csproj
```

**Output:** `NewRhinoGold.rhp` — install by dragging into Rhino or placing in the plugin search path.

**No tests, CI/CD, linting, or formatting tools are configured.**

## Architecture

### Plugin Lifecycle

1. `NewRhinoGoldPlugIn` (inherits `Rhino.PlugIns.PlugIn`) loads at startup
2. `OnLoad` registers the `MainToolbarDlg` panel with Rhino
3. `OnIdle` runs `_BJewel` command once to open the panel automatically

### Namespace Layout

| Namespace | Purpose |
|---|---|
| `NewRhinoGold` | Plugin entry, densities, display conduit |
| `NewRhinoGold.BezelStudio` | Bezel/cutter/head builders, pavé placement |
| `NewRhinoGold.Commands` | Rhino command classes |
| `NewRhinoGold.Core` | Geometry builders, SmartData, profile libraries, ring design |
| `NewRhinoGold.Dialog` | Main toolbar, gem creator, gold weight, picker dialogs |
| `NewRhinoGold.Helpers` | Selection, profile loading, menus, text input |
| `NewRhinoGold.Studio` | Gem/cutter/head/pavé studio dialogs |
| `NewRhinoGold.Wizard` | Ring wizard dialog |

### Key Patterns

**Rhino Command Pattern** — Every user-facing feature is a class inheriting `Rhino.Commands.Command` with a unique GUID and `RunCommand()` returning `Result.Success` or `Result.Failure`. Commands are prefixed with `_` when scripted.

**SmartData (UserData)** — Persistent metadata attached to Rhino objects via `Rhino.DocObjects.Custom.UserData`. Classes: `GemSmartData`, `RingSmartData`, `BezelSmartData`, `CutterSmartData`, `HeadSmartData`. These handle serialization (versioned read/write), transform propagation, and duplication.

**Geometry Builders** — Static or instance classes that generate `Brep` geometry:
- `GemBuilder` — Creates gem volumes from profile curves (crown, girdle, pavilion loft)
- `RingBuilder` — Sweeps profiles along circular rails
- `BezelBuilder` — Creates bezel geometry from parameters
- `CutterBuilder` — Creates cutting tool geometry
- `HeadBuilder` — Creates head/setting geometry
- `EngravingBuilder` — Creates engraving geometry on rings

**Profile Libraries** — `ProfileLibrary`, `RingProfileLibrary`, `HeadProfileLibrary` manage curve profiles loaded from Rhino document blocks.

**Display Conduits** — `GemDisplayCond` and `RingPreviewConduit` provide real-time preview via `Rhino.Display.DisplayConduit`.

**Singleton UI** — Dialogs use `Instance` singletons (e.g., `MainToolbarDlg.Instance`, `GemStudioDlg.Instance`).

### Key Data Models

- `GemParameters` — H1 (crown height), H2 (girdle thickness), H3 (pavilion depth), Table (%)
- `RingDesignManager` — Manages 8 ring sections with mirroring support
- `RingSection` / `RingProfileSlot` — Angle, width, height, profile, rotation, offset
- `Densities` — Static lookup for 13 gem types and 9+ metal alloys with density and display color
- `RingPosition` — 8-position enum (Bottom, BottomRight, Right, TopRight, etc.)

## Coding Conventions

### Naming
- **PascalCase** for classes, methods, properties, enums (standard C#)
- **camelCase with `_` prefix** for private fields (e.g., `_byKey`)
- **Command English names** match the feature name (e.g., `"BezelStudio"`, `"GemCreator"`)
- **GUIDs** are declared as `[Guid("...")]` attributes on commands and SmartData classes

### Language
- Code identifiers and public API are in **English**
- Comments and some variable names are in **German** (this is intentional)
- UI labels mix German and English

### Geometry Conventions
- All geometry operations must produce **watertight (valid) Breps** — no naked edges
- Tolerance: `0.001` mm is the standard modeling tolerance
- Breps are always capped (`CapPlanarHoles`) and validated after construction
- Curves are centered at origin, then scaled to target size
- Seams on closed curves must be properly aligned

### UI Framework
- All dialogs use **Eto.Forms** (cross-platform)
- Use `Eto.Drawing.Fonts` explicitly — **never** `Rhino.UI.Fonts` (conflict avoidance)
- Icons are embedded PNG resources (256x256px)
- Dynamic layout via `DynamicLayout`

### SmartData Versioning
- Each SmartData class has `MAJOR_VERSION` and `MINOR_VERSION` constants
- New fields increment the minor version
- `Read()` must handle all previous minor versions for backward compatibility
- `Write()` always writes the latest version

## Commands Reference

| Command | Description |
|---|---|
| `BJewel` | Opens the main toolbar panel |
| `RingWizard` | Multi-step ring creation wizard |
| `GemStudio` | Interactive gem editing studio |
| `GemCreator` | Create gems from curve profiles |
| `CutterStudio` | Create/edit cutting geometry |
| `BezelStudio` | Create/edit bezel settings |
| `HeadStudio` | Create/edit head/prong settings |
| `PaveStudio` | Pavé stone placement |
| `EditSmartObject` | Edit any SmartData-attached object |
| `PickGem` | Interactive gem selection tool |
| `MoveOnSurface` | Move objects constrained to a surface |
| `GoldWeight` | Calculate metal weight from volume + density |
| `GemReport` | Generate gem inventory report |
| `ExtractGemCurve` | Extract profile curve from a gem |
| `EngraveRing` | Apply engraving to ring geometry |
| `SelectGems` / `SelectHeads` / `SelectBezels` / `SelectCutters` | Type-specific selection filters |
| `DebugGem` / `DebugSmartObject` | Debug inspection commands |

## Important Notes for AI Assistants

1. **Geometry must be watertight.** Every Brep created must be valid with no naked edges. Always cap planar holes and validate output.
2. **Use `Eto.Drawing.Fonts`** for font references, not `Rhino.UI.Fonts`.
3. **SmartData backward compatibility** — when adding fields to SmartData classes, increment the minor version and handle missing fields in `Read()` for older versions.
4. **Manufacturing constraints** — consider material shrinkage, minimum wall thickness, and casting tolerances when modifying geometry builders.
5. **No test infrastructure exists.** Testing requires loading the plugin into Rhino 8 and manually verifying geometry and UI behavior.
6. **Profile curves** are loaded from Rhino document blocks. Profile libraries manage available shapes for gems, rings, and heads.
7. **Comments are often in German.** This is expected. Maintain the existing language conventions in the file you are editing.
8. **Tolerance is 0.001 mm** throughout the codebase. Use this consistently for joins, caps, and intersection operations.
