# Alea

A 2D pixel-art backgammon (tavla) game built with Unity. The classic board is extended with special
checkers, board tiles and dice relics that bend the rules; the match is decided by score.

## Requirements

- Unity **6000.5.4f1** (Universal Render Pipeline, 2D)
- Packages are declared in `Packages/manifest.json` (Input System, Test Framework, 2D Aseprite importer)

Open the project in Unity Hub and load `Assets/Scenes/Main.unity`.

## Architecture

The code is split into two assemblies with a one-way dependency:

| Assembly | Folder | Depends on | Purpose |
|---|---|---|---|
| `Tavla.Core` | `Assets/Scripts/Core` | nothing (`noEngineReferences`) | Pure C# game rules: board, moves, captures, dice, abilities, AI |
| `Tavla.View` | `Assets/Scripts/View` | `Tavla.Core`, Input System | Unity presentation: sprites, input, HUD |
| `Tavla.Core.Tests` | `Assets/Tests/EditMode` | `Tavla.Core` | EditMode unit tests |

Key rules of the design:

- **`GameState` is the single source of truth.** The View never computes rules; it asks Core
  (`TryMove`, `CanReach`, `CanBearOff`) and redraws the whole board from the model on every event.
- **Abilities are composed, not inherited.** A checker gains behaviour by implementing
  `ICheckerAbility` interfaces; `GameState` only asks `checker.GetAbility<T>()`. Adding a checker
  or tile never changes `GameState`.
- **Only Core mutates the board.** `Node` and `DiceRoll` mutators are `internal`; the View talks to
  Core through events (`MoveExecuted`, `CheckerCaptured`, `AbilityTriggered`, ...).
- **Randomness is injected** through `IRandomProvider`. Matches are reproducible with a fixed seed; when
  the seed is `0` a random one is chosen and logged.

## Running tests

Unity: **Window → General → Test Runner → EditMode → Run All**.

Command line (with the editor closed):

```bash
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
```

## Input

`InputAdapter` reads the pointer through the Input System package when it is enabled
(*Project Settings → Player → Active Input Handling* = *Input System Package* or *Both*) and falls back
to the legacy Input Manager otherwise. `OnMouseDown/Drag/Up` work with both backends on Unity 6.4+.

## Conventions

- All code, comments and identifiers are in English; player-facing strings are serialized fields.
- Style is defined in `.editorconfig` (Allman braces, explicit types, `_camelCase` private fields).
- Ability ids are constants in `AbilityIds`; sorting orders live in `SortingOrders`.
- Informational logs go through `GameLog`, which is stripped from release builds.
