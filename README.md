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
| `Tavla.Core` | `Assets/Scripts/Core` | nothing (`noEngineReferences`) | Pure C# game rules: board, moves, captures, dice, abilities, AI, runs |
| `Tavla.View` | `Assets/Scripts/View` | `Tavla.Core`, Input System | Unity presentation: sprites, input, HUD |
| `Tavla.Core.Tests` | `Assets/Tests/EditMode` | `Tavla.Core` | EditMode unit tests |
| `Tavla.Sandbox` | `Assets/Scripts/Sandbox` | `Tavla.Core`, `Tavla.View` | God-mode test bench (`Sandbox.unity` only); nothing references it |

Key rules of the design:

- **`GameState` is the single source of truth.** The View never computes rules; it asks Core
  (`TryMove`, `CanReach`, `CanBearOff`) and redraws the whole board from the model on every event.
- **Abilities are composed, not inherited.** A checker gains behaviour by implementing
  `ICheckerAbility` interfaces; `GameState` only asks `checker.GetAbility<T>()`. Adding a checker
  or tile never changes `GameState`. Special checkers derive from `SpecialChecker`, which only carries
  their shop identity (id and name).
- **Only Core mutates the board.** `Node` and `DiceRoll` mutators are `internal`; the View talks to
  Core through events (`MoveExecuted`, `CheckerCaptured`, `AbilityTriggered`, ...).
- **Randomness is injected** through `IRandomProvider`. Matches are reproducible with a fixed seed; when
  the seed is `0` a random one is chosen and logged.
- **Runs are data.** A run (the first game mode: 3 stages of 3 matches, the last of each a boss) is a
  `RunConfig` asset (*Create → Tavla → Run*; the designed one is `Assets/Data/StandardRun.asset`): each
  match's target score, turn limit and opponent checkers, the tiles each stage adds for the rest of the run,
  the player's starting deck and the gold rules. Core validates it as a `RunDefinition`; `RunState` deals
  the matches one by one, carries the deck and gold between them and ends the run on the first loss. Assign
  the asset to `BoardManager`'s *Run* field; without one the scene plays a single sandbox match.
- **Starting positions are dealt, not fixed.** `StartingPatterns` holds the designed stack formations for
  5..15 checkers; every match `StartingLayout` picks one per side and puts each stack on a random free point.
  No checker starts in its own home board or on a tile, and the match seed reproduces the layout.
- **The shop opens between matches.** After every won match of a run `RunState.OpenShop` stocks three
  special checkers, a plain checker, a card and a charm from the run asset's shop section; checkers join the
  deck, cards and charms join the run's relics (dice relics only bend their owner's dice). `ShopView` draws
  `ingameshop.png` (exported from `ingameshop_scheme.aseprite`) and `ShopPresenter` connects it to Core.
- **The board is layered art.** `BoardManager` stacks full-canvas (320x180) sprites: `background.png`,
  `board.png` and `points.png`, the 24 triangles. While a checker is dragged, every legal point shows
  `point_highlight.png` around itself, mirrored on the bottom row. Both point sprites are exported from
  `points_scheme.aseprite`, whose columns match `BoardLayout` pixel by pixel.
- **Checkers are drawn from a skin per side** (`CheckerSkin`): the player's `player_checker.png`, with a ring
  while it can move with the rolled dice (`player_checker_movable.png`) and another while it is picked up
  (`player_checker_selected.png`); the opponent's `opponent_checker.png`. Which checker can move is Core's
  answer (`GameState.GetMovableChecker`, the same rule `HasAnyLegalMove` uses); the View only paints it.
- **The info panel is built in code too.** `HudView` draws `general_info.png` (exported from
  `general_info_scheme.aseprite`) and writes the values `HudPresenter` gives it into text boxes measured
  pixel by pixel from the art, as the shop does. If a strip moves in the art, update its box in the Inspector.
- **Text is Alagard, drawn as sprites.** `PixelFont` holds the glyphs of `Assets/Fonts/alagard.ttf` (by Pix3M)
  as a table generated from the TTF's pixel grid; `PixelLabel` lays them out and draws them at half size
  (`PixelTextSize.Regular`), or at a third (`Small`) where the art leaves less room, such as the shop's price tags.

## Sandbox (god mode)

`Assets/Scenes/Sandbox.unity` is a test bench for the special checkers, apart from the game: `Main.unity` and the
run are untouched by it. Open the scene and press Play; the **GOD MODE** panel docks on the right (**F1** hides it).

- **Setup**: tick any special checkers for your side (one of each, up to 15; the rest of your checkers are plain),
  set both sides' checker counts, add tiles on chosen points, and set the turn limit, target score and starting
  gold. *New deal* deals it with a new seed, *Deal again* replays the current seed. The last dealt setup is kept
  between Play sessions; *Reset setup* goes back to the one in the `GodMode` object's Inspector.
- **Match**: load the dice (*Roll 6-6 now* re-rolls the current turn on the chosen faces; the lock toggles keep every
  roll of a side on them), switch the bot off to play both sides by hand, add gold or score, end the turn.
- **Log**: every roll, move, ability (`*`), tile (`#`), capture, score and gold change Core reports. Hovering a checker
  shows its name, side and point.

Nothing in the sandbox bends a rule. `SandboxMatchFactory` deals the match with Core's public setup API, the same
steps `RunState.StartMatch` takes, and hands it to `BoardManager.PlayMatch`; the dice go through `LoadedDice`, an
`IDiceModifier`, so abilities that react to rolls still do. The scene is a copy of `Main.unity` with an empty *Run*
field and a `GodMode` object: when the board objects in `Main.unity` change, copy the change over.

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

- All code, comments and identifiers are in English. Player-facing UI strings are serialized fields
  (`ShopTexts`, `HudPresenter`); an item's name is its `DisplayName` in Core, keyed by its `ItemId`.
- Scores are the design document's values times `GameState.ScoreScale` (10): a design value `n` is written
  `n * GameState.ScoreScale`, and run targets in the assets use the same scale.
- Style is defined in `.editorconfig` (Allman braces, explicit types, `_camelCase` private fields).
- Ability ids are constants in `AbilityIds`; sorting orders live in `SortingOrders`.
- Informational logs go through `GameLog`, which is stripped from release builds.
