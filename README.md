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
  the player's starting deck and the gold rules. A stage can also draw *Random Tiles* from the run's *Tile Pool*
  when the run reaches it, each on a random point no stage places a tile on; a tile comes at most once a run, so
  the next stage never brings the tile the last one did (the standard run draws one as stages 2 and 3 begin).
  Core validates it as a `RunDefinition`; `RunState` deals
  the matches one by one, carries the deck and gold between them and ends the run on the first loss. Assign
  the asset to `BoardManager`'s *Run* field; without one the scene plays a single sandbox match.
- **Starting positions are dealt, not fixed.** `StartingPatterns` holds the designed stack formations for
  5..15 checkers; every match `StartingLayout` picks one per side and puts each stack on a random free point.
  No checker starts in its own home board or on a tile, and the match seed reproduces the layout.
- **The shop opens between matches.** After every won match of a run `RunState.OpenShop` stocks three
  special checkers, a plain checker, a card and a charm from the run asset's shop section; checkers join the
  deck, cards and charms join the run's relics (dice relics only bend their owner's dice). `ShopView` draws
  the Merchant Table from `Assets/Data/ShopArt.asset` (sprites in `Assets/Sprites/Shop`, cut from
  `in_game_shop_ready.aseprite`) and `ShopPresenter` connects it to Core. The table has no place for the
  card and charm shelves yet, so Core stocks them but the screen does not show them.
- **The shop buys checkers back.** `Shop.Sell` takes a checker out of the deck for half of what was paid for
  it (`ShopPrices.GetSellPrice`: rounded down, at least 1 gold): any special checker, which frees a place under
  the five-special limit, and the extra plain checkers — those beyond the ones the run started with, the
  dearest first. The stall in the bottom right corner (`Sell İkonu.aseprite`, shown only in the shop) opens the
  list in the frame's sunken panel above it: a row per special checker, then one row for the extra plain
  checkers with their number on it; a row is sold with two clicks, the first one rings it. While the pointer is
  on the stall it glows (`HoverGlow`): a warm outline around its outer edge, a fainter halo and a pale wash over
  it, breathing gently. The glow is drawn in code from the stall's own sprite, so it follows a re-export of the art,
  but that sprite must keep Read/Write ticked in its import settings.
- **The board is layered art.** `BoardManager` stacks full-canvas (320x180) sprites: `background.png`,
  `board.png` and `points.png`, the 24 triangles. While a checker is dragged, every legal point shows
  `point_highlight.png` around itself, mirrored on the bottom row. Both point sprites are exported from
  `points_scheme.aseprite`, whose columns match `BoardLayout` pixel by pixel.
- **The dice are cards.** Six upright cards, one per die face, stand spread across the middle of the board,
  three on each side of the bar (`Assets/Data/DiceCardArt.asset`; sprites in `Assets/Sprites/Dice`).
  `DiceCardFaces` decides what each shows: a die in hand turns up its own card, a double turns up the middle four,
  and a spent die's card turns back down. `DiceCardsView` plays the artist's flip (`1kartanimasyon` to
  `6kartanimasyon`, faster than drawn: the asset's frame time) forwards to turn a card up and backwards to turn it
  down, and `DiceCardsPresenter` connects the cards to Core. Only rolls played by hand turn cards up; the bot's
  leave them face down. The cards are the only place the dice are shown; the info panel no longer lists them.
- **Tiles are drawn as their point.** `TilesView` lays a tile's art over its whole triangle
  (`Assets/Data/TileArt.asset`; sprites in `Assets/Sprites/Tiles`, cropped to the 10x56 triangle). The art is drawn
  for the bottom row and turned half round on the top row, as `points.png` does. A point that gains a tile plays the
  artist's transition from its own colour (`BoardLayout.IsLightPoint`), e.g. `BeyazdanBarutHanesineAnimasyon` or
  `SiyahtanBarutHanesineAnimasyon` for the Powder Keg's `Mayın_Hanesi`, and one that loses it plays it backwards.
  `BoardManager` re-reads every point's tile when it redraws the board, so a run's tiles turn over when their stage
  adds them and stay still for the matches after. So far the Powder Keg, the Maelstrom (`Girdap Hanesi`), the
  Swamp (`Bataklık Hanesi`), the Spike Pit (`TahtaKazıkHanesi`), the Fog (`SisHanesi`), the Dungeon
  (`ZindanHanesi`) and the Plunder Cove (`KorsanKoyu`) have art; other tiles leave their point plain, so the standard
  run's tile pool holds only these seven. A tile that opens and closes (`IOpenCloseTile`) rests open on one frame of its transition (`OpenFrame` in the
  art asset) and closed on its point: the Dungeon shows the stone without bars (the artist's frame 6) on its open
  rounds, the bars down (`ZindanHanesi`) on its closed ones, and plays the bars going up or down in between. The Maelstrom has only its light-point transition
  (`BeyazdanGirdapHanesineAnimasyon`), so on a dark point it appears at once. A tile that counts down (`ICountdownTile`) also shows its count in a small badge near the tip, above the
  checkers — a placeholder until there is art for it. The Powder Keg's point counts 3, 2, 1, one step per round (once
  both players have played), then destroys every checker on it for good (Reckless excepted) and starts again.
- **Checkers are drawn from a skin per side** (`CheckerSkin`): the player's `player_checker.png`, with a ring
  while it can move with the rolled dice (`player_checker_movable.png`) and another while it is picked up
  (`player_checker_selected.png`); the opponent's `opponent_checker.png`. The player's special checkers wear
  their own art from `Assets/Data/CheckerArt.asset` (`Assets/Sprites/Checkers/Tier N`, the artist's file
  names kept so re-exports overwrite them), with a shared ring for the movable and picked-up states. Which
  checker can move is Core's answer (`GameState.GetMovableChecker`, the same rule `HasAnyLegalMove` uses);
  the View only paints it.
- **A volley waits for its targets.** The Bowman's first move of a turn arms a volley of one arrow per point it
  moved (a compound move counts its first die). `GameState` then waits (`HasPendingVolley`) until the player picks
  that many of the opponent's checkers on the board (`TryFireVolley`; made points too, not Armored ones) and accepts
  nothing else meanwhile; a volley not fired by the end of the turn is lost. While a human's volley waits,
  `BoardManager` marks every checker it can hit, a click picks one (a second click drops it), the volley fires on the
  last pick and the info panel's status line counts the picks left. The marks are built-in placeholders until there is
  art (*Volley* fields). The bot fires its own at the checkers closest to bearing off.
- **Hits are felt.** `HitEffects` (built and ticked by `BoardManager`) shakes the screen when a checker is hit (as the
  capturing checker lands, not when Core reports the capture) and harder when a Powder Keg blows up; `CameraShake`
  moves the camera in whole canvas pixels and fades out. A hit checker whose skin has *Break* frames stays where it
  was hit until then, breaks apart there and puts itself back together on the bar (the frames backwards) before its own
  visual shows again. Every checker has them: the plain ones in BoardManager's skins (`RakipTasKırılmaAnimasyon`,
  `OyuncuTasKırılmaAnimasyon` in `Assets/Sprites`), each special one in its `CheckerArt.asset` entry
  (`Assets/Sprites/Checkers/Breaks`, the artist's file names kept). Each is frames 2-6 of the artist's file (frame 1 is
  the checker whole, frame 7 empty), cut square around the disc so the pivot sits on its centre, as on the resting
  art. The strength and length of each shake are BoardManager's *Hit effects* fields.
- **The info panel is built in code too.** `HudView` draws `general_info.png` (exported from
  `general_info_scheme.aseprite`) and writes the values `HudPresenter` gives it into text boxes measured
  pixel by pixel from the art, as the shop does. If a strip moves in the art, update its box in the Inspector.
  Top to bottom the strips show the status line (`TURN 5`, a volley's picks left, `SHOP`, the match result), the
  score, the task (`TASK`: the score the match asks for, `-` in a match without a target) and the gold.
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
- **Tiles in play**: put any tile on any point of the match being played, or take a point's tile off, without dealing
  again. The point grid is laid out like the board (top row 0-11 from the left, bottom row 23-12; `*` = holds a
  tile). A tile put on checkers catches only the ones that land there afterwards; the next deal goes back to the
  setup's tiles.
- **Hit effects**: switch the screen shake and the checkers' breaks on or off, and set how far and how long a hit and a
  keg's blast shake the screen (*Try* feels one at once). The edits last until Play mode ends; keep the good values in
  BoardManager's *Hit effects* fields, in both scenes.
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
