#if ENABLE_INPUT_SYSTEM && TAVLA_INPUT_SYSTEM_PACKAGE
#define TAVLA_NEW_INPUT
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if TAVLA_NEW_INPUT
using UnityEngine.InputSystem;
#endif
using Tavla.Core.Abilities;
using Tavla.Core.AI;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Run;
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// The ONLY bridge between CORE and UNITY. There are NO game rules here.
    ///
    /// Responsibilities:
    ///   1) boot Core: a run (RunState hands out one match after another) or a single sandbox match,
    ///   2) build the scene once (through <see cref="BoardSceneBuilder"/>) and the checkers of every match,
    ///   3) forward requests from CheckerController to TryMove,
    ///   4) refresh the visuals when Core raises events; writing them to the console is
    ///      <see cref="BoardEventLog"/>'s job.
    ///
    /// KEY DESIGN: checker positions are NOT computed from move data. After every event
    /// RefreshBoard() re-reads the board from the model's current state. That is why the Doomsday
    /// Checker's mass capture, the Maelstrom's teleport, the Phoenix's return and 30 other special
    /// cases need not a single extra line of View code. The one exception is a checker the Maelstrom breaks on a made
    /// point: it is held there for a moment (<see cref="Crash"/>), so the player sees it land before it is broken.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        /// <summary>Value of the bot player field that disables the bot (both sides are played by hand).</summary>
        public const int NoBotPlayer = -1;

        /// <summary>Maximum moves the bot may make in one turn. Safety net against an endless loop.</summary>
        private const int MaxBotMovesPerTurn = 12;

        [Header("Geometry")]
        [Tooltip("If left empty, the default values (measured from the Aseprite scheme) are used.")]
        [SerializeField] private BoardLayout _layout;

        [Header("Sprites")]
        [Tooltip("The checker sprite: WHITE, tinted with the player colour — for a side whose skin has no art. " +
                 "No prefab needed — checker objects are generated in code.")]
        [SerializeField] private Sprite _checkerSprite;
        [Tooltip("The human player's checkers: player_checker at rest, player_checker_movable while the pointer is over " +
                 "one that can move with the rolled dice, player_checker_selected while it is picked up. " +
                 "Drawn in their own colours.")]
        [SerializeField] private CheckerSkin _playerCheckers = new CheckerSkin();
        [Tooltip("The opponent's checkers: opponent_checker, drawn in its own colours. They are never the human's to " +
                 "move, so they need no other state.")]
        [SerializeField] private CheckerSkin _opponentCheckers = new CheckerSkin();
        [Tooltip("The art of the human player's special checkers (Create > Tavla > Checker Art). " +
                 "Empty = special checkers look like plain ones.")]
        [SerializeField] private CheckerArtSet _checkerArt;
        [Tooltip("The board sprite (full 320x180 canvas, without the points). If assigned, it is placed in the scene as the board.")]
        [SerializeField] private Sprite _boardSprite;
        [Tooltip("Background sprite. Drawn behind the board, covering the whole screen; " +
                 "visible through the transparent area of the board sprite.")]
        [SerializeField] private Sprite _backgroundSprite;
        [Tooltip("The points sprite: the 24 triangles on the full 320x180 canvas, drawn on the board.")]
        [SerializeField] private Sprite _pointsSprite;
        [Tooltip("The point_highlight sprite: the outline of one top-row point, shown around a point while it is " +
                 "a legal target. The bottom row uses it mirrored. Empty = a translucent fill over the point.")]
        [SerializeField] private Sprite _pointHighlightSprite;

        [Header("Score marks (Navigator footprints)")]
        [Tooltip("Drawn past the tip of every point the human player has marked — the Navigator's footprints, where " +
                 "captures and bear-offs score more. Empty = a built-in pair of pixel footprints.")]
        [SerializeField] private Sprite _markSprite;
        [Tooltip("Tint of the mark. White keeps an assigned sprite's own colours.")]
        [SerializeField] private Color _markColor = new Color(0.35f, 0.95f, 0.85f, 1f);

        [Header("Volley (Bowman)")]
        [Tooltip("Drawn over every checker the human's waiting volley can hit, while they pick its targets by clicking " +
                 "them. Empty = built-in corners around the checker.")]
        [SerializeField] private Sprite _volleyTargetSprite;
        [Tooltip("Tint of the target mark. White keeps an assigned sprite's own colours.")]
        [SerializeField] private Color _volleyTargetColor = new Color(1f, 0.45f, 0.35f, 0.8f);
        [Tooltip("Drawn over a checker picked as a target; clicking it again drops it. The volley fires on the last " +
                 "pick. Empty = built-in corners with a cross.")]
        [SerializeField] private Sprite _volleyAimedSprite;
        [Tooltip("Tint of the picked mark. White keeps an assigned sprite's own colours.")]
        [SerializeField] private Color _volleyAimedColor = new Color(1f, 0.2f, 0.15f, 1f);
        [Tooltip("The info panel's task line while the human picks a volley's targets. {0} = targets left to pick. " +
                 "Keep it as short as the turn counter it replaces.")]
        [SerializeField] private string _volleyTask = "SHOOT {0}";

        [Header("Prefabs (optional)")]
        [Tooltip("If set, checkers are spawned from this prefab; otherwise they are built in code from the sprite above.")]
        [SerializeField] private CheckerController _checkerPrefab;
        [Tooltip("If left empty, points are built in code (invisible colliders).")]
        [SerializeField] private NodeView _nodePrefab;

        [Header("Player colours")]
        [Tooltip("Tints of the white checker sprite. A side whose skin has art keeps its own colours.")]
        [SerializeField] private Color _player0Color = Color.white;
        [SerializeField] private Color _player1Color = new Color(0.15f, 0.15f, 0.18f);

        [Header("Bear-off area")]
        [Tooltip("Faint, always-visible background of the bear-off area. The highlight only lights up while " +
                 "dragging; without this the player would not notice there is a drop zone outside the board.")]
        [SerializeField] private Color _bearOffTrayColor = new Color(1f, 1f, 1f, 0.08f);

        [Header("Dice cards")]
        [Tooltip("The dice cards' art (Create > Tavla > Dice Card Art): six cards in the middle of the board, the human's " +
                 "rolled faces turned up; the bot's rolls leave them down. Empty = no cards; the info panel still shows " +
                 "the dice.")]
        [SerializeField] private DiceCardArtSet _diceCardArt;

        [Header("Tiles")]
        [Tooltip("The tiles' art (Create > Tavla > Tile Art): a point carrying a tile is drawn as that tile, and turns into " +
                 "it when the tile appears. Empty = tiles are not drawn; they still work.")]
        [SerializeField] private TileArtSet _tileArt;
        [Tooltip("A checker the Maelstrom throws onto a point it may not enter (a made point) first goes there, and is " +
                 "broken this long after it arrives (seconds).")]
        [SerializeField, Min(0f)] private float _crashDelay = 0.5f;

        [Header("Info panel")]
        [Tooltip("Presenter that writes score/gold/moves on the left panel. If empty it is searched for in " +
                 "the scene; if there is none the panel is disabled and the game runs unchanged.")]
        [SerializeField] private HudPresenter _hud;

        [Header("Camera")]
        [SerializeField] private bool _configureCamera = true;
        [Tooltip("Clear colour of the camera, visible where no sprite covers the screen.")]
        [SerializeField] private Color _cameraBackgroundColor = new Color(0.08f, 0.08f, 0.10f);

        [Header("AI")]
        [Tooltip("Player controlled by the bot. -1 = bot disabled, both sides are played by hand.")]
        [SerializeField] private int _botPlayerId = 1;
        [Tooltip("Thinking time before the bot moves (seconds).")]
        [SerializeField] private float _botThinkDelay = 0.6f;
        [Tooltip("Delay between consecutive bot moves (seconds).")]
        [SerializeField] private float _botMoveDelay = 0.35f;

        [Header("Turn")]
        [Tooltip("Pass the turn automatically when the human player has no move left?")]
        [SerializeField] private bool _autoEndTurn = true;
        [Tooltip("Delay before the turn is passed once no move is left (seconds).")]
        [SerializeField] private float _autoEndDelay = 0.6f;
#if TAVLA_NEW_INPUT
        [Tooltip("Action that ends the turn manually (safety net for cases the auto-pass misses).")]
        [SerializeField] private InputAction _endTurnAction =
            new InputAction("EndTurn", InputActionType.Button, "<Keyboard>/space");
#else
        [Tooltip("Key that ends the turn manually (safety net for cases the auto-pass misses).")]
        [SerializeField] private KeyCode _endTurnKey = KeyCode.Space;
#endif

        [Header("Run")]
        [Tooltip("Run to play: stages of matches with their targets and tiles (Create > Tavla > Run). " +
                 "Empty = a single sandbox match with the settings below.")]
        [SerializeField] private RunConfig _run;
        [Tooltip("Pause between two matches of a run, so the result can be read (seconds).")]
        [SerializeField, Min(0f)] private float _nextMatchDelay = 2.5f;

        [Header("Shop")]
        [Tooltip("The shop's art (Create > Tavla > Shop Art). The shop opens after every won match of a run; " +
                 "empty = no shop, the next match starts straight away.")]
        [SerializeField] private ShopArtSet _shopArt;
        [Tooltip("Every text of the shop screen.")]
        [SerializeField] private ShopTexts _shopTexts = new ShopTexts();

        [Header("Sandbox match (when no run is assigned)")]
        [Tooltip("Score the human player must reach. 0 = classic rules: the higher score wins.")]
        [SerializeField, Min(0)] private int _targetScore = 0;
        [Tooltip("Turn limit. Classic: turns of both players. With a target: only the human player's turns.")]
        [SerializeField, Min(1)] private int _turnLimit = GameState.DefaultTurnLimit;
        [Tooltip("With a target: turns left to the human once the opponent runs out of checkers. 0 = the match ends.")]
        [SerializeField, Min(0)] private int _finalStretchTurns = 3;
        [Tooltip("Starting checkers of player 0, dealt in one of the StartingPatterns formations.")]
        [SerializeField, Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
        private int _player0Checkers = GameState.MaxCheckersPerPlayer;
        [Tooltip("Starting checkers of player 1, dealt in one of the StartingPatterns formations.")]
        [SerializeField, Range(GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer)]
        private int _player1Checkers = GameState.MaxCheckersPerPlayer;

        [Header("Determinism")]
        [Tooltip("0 = a new random seed every match (it is logged so the match can be replayed). " +
                 "Any other value makes dice and chance outcomes reproducible.")]
        [SerializeField] private int _randomSeed = 0;

        private GameState _gameState;
        private MatchContext _context;
        private RunState _runState;
        private ShopPresenter _shopPresenter;
        private DiceCardsPresenter _diceCards;
        private TilesView _tiles;
        private TavlaBot _bot;
        private Coroutine _botRoutine;
        private Coroutine _autoEndRoutine;

        // Writes the match's events to the console; the listeners below only act on what changes the screen.
        private readonly BoardEventLog _eventLog = new BoardEventLog();

        // Core object -> scene representative. This dictionary is the View's only link to the model.
        private readonly Dictionary<BaseChecker, CheckerController> _checkerViews =
            new Dictionary<BaseChecker, CheckerController>();

        private readonly List<NodeView> _nodeViews = new List<NodeView>();

        // Reused for every drag highlight so picking up a checker does not allocate.
        private readonly List<int> _legalTargets = new List<int>(GameState.NodeCount);

        // The targets the human has picked so far for their waiting volley (Bowman), and whether the task line shows it.
        private readonly List<BaseChecker> _volleyPicks = new List<BaseChecker>();
        private bool _showingVolleyTask;

        // The volley marks in use: the assigned sprites, or built-in ones made in BuildScene.
        private Sprite _volleyTargetMark;
        private Sprite _volleyAimedMark;

        // Checkers still shown on the point they crashed onto, in the order they crashed.
        private readonly List<Crash> _crashes = new List<Crash>();

        /// <summary>
        /// A checker thrown onto a point it may not enter (<see cref="AbilityIds.TeleportCrash"/>). Core has already
        /// broken it; the View keeps showing it on that point until it has arrived there and the crash delay has passed.
        /// </summary>
        private sealed class Crash
        {
            public BaseChecker Checker;
            public CheckerController View;
            public int Point;

            /// <summary>Core destroyed it as it broke (Sacrificial): its visual goes once the crash has been shown.</summary>
            public bool Destroyed;

            public Coroutine Routine;
        }

        // _nodeViews is addressed by point index (0-23); the bear-off target is not part of that list.
        private NodeView _bearOffView;

        // Parent of the checker views; created once, refilled for every match.
        private Transform _checkerRoot;

        // Runtime-created resources owned by this component; released in OnDestroy.
        private bool _ownsLayout;
        private Sprite _solidSprite;
        private Sprite _builtInMarkSprite;
        private Sprite _builtInVolleyTarget;
        private Sprite _builtInVolleyAimed;

        public GameState Board => _gameState;
        public MatchContext Match => _context;

        /// <summary>The run being played; null in a sandbox match.</summary>
        public RunState Run => _runState;

        /// <summary>
        /// Is the bot playing its seat? Turned off, a human plays both sides; turned back on, the bot picks up its
        /// seat at once, mid-turn included. The seat itself is the Bot Player field; with none set this stays false.
        /// The sandbox's god-mode panel flips it during a match.
        /// </summary>
        public bool BotEnabled
        {
            get => _bot != null;
            set
            {
                if (value == BotEnabled || !GameState.IsValidPlayer(_botPlayerId))
                    return;

                if (value)
                {
                    _bot = new TavlaBot(_botPlayerId);
                }
                else
                {
                    StopBotTurn();
                    _bot = null;
                }

                if (_gameState == null)
                    return;

                // Who is human decides whose checkers light up, whose dice the cards show and who gets the auto-pass.
                RefreshMovableCheckers();

                if (_diceCards != null)
                    _diceCards.Refresh();

                TryStartBotTurn(_gameState.CurrentPlayerId);
                ScheduleAutoEndCheck();
            }
        }

        /// <summary>The seat the human plays — the one chasing the target: player 0, unless the bot sits there.</summary>
        private int HumanPlayerId => _botPlayerId == 0 ? 1 : 0;

        #region Setup

        private void Start()
        {
            if (_layout == null)
            {
                // No asset assigned: create one in memory with the default values.
                _layout = BoardLayout.CreateDefault();
                _ownsLayout = true;
                GameLog.Info("[View] No BoardLayout assigned, using the default geometry.", this);
            }

            // The white checker sprite is only needed by a side whose skin has no art.
            if (_checkerPrefab == null && _checkerSprite == null &&
                (!_playerCheckers.HasArt || !_opponentCheckers.HasArt))
            {
                Debug.LogError("[View] Assign the checker skins, or 'checker' to the Checker Sprite field " +
                               "(or assign a Checker Prefab). BoardManager is disabled.", this);
                enabled = false;
                return;
            }

            BuildScene();

            // Create the bot if enabled. With NoBotPlayer both sides are played by hand.
            if (GameState.IsValidPlayer(_botPlayerId))
                _bot = new TavlaBot(_botPlayerId);

            if (_run == null || !TryStartRun())
                StartSandboxMatch();
        }

        /// <summary>
        /// Puts a freshly built match on the table: spawns its checkers, connects the View and the HUD to it and
        /// rolls the first dice. The sandbox match and every match of a run come through here.
        /// </summary>
        private void BeginMatch(GameState board, MatchContext context)
        {
            _gameState = board;
            _context = context;

            SpawnCheckers();
            Subscribe();
            BindHud();

            // Like the HUD, before the first roll so the cards see it.
            if (_diceCards != null)
                _diceCards.Bind(_gameState, _context);

            RefreshBoard(true);

            // Roll the first turn's dice, then start the bot if it is its turn.
            _context.RollDice(_gameState.CurrentPlayerId);
            TryStartBotTurn(_gameState.CurrentPlayerId);
        }

        /// <summary>
        /// Clears a finished match off the table so the next one can begin: stops the routines still acting on
        /// it, disconnects from its events and removes its checkers. The board and the points stay.
        /// </summary>
        private void EndMatch()
        {
            StopBotTurn();
            CancelAutoEnd();
            ClearHighlights();
            Unsubscribe();

            // The checkers the picks name leave with the match; the next match's HUD binding rewrites the task line.
            _volleyPicks.Clear();
            _showingVolleyTask = false;

            if (_diceCards != null)
                _diceCards.Unbind();

            // The marks belong to the match that left them.
            for (int i = 0; i < _nodeViews.Count; i++)
                _nodeViews[i].SetMarked(false);

            // A crash still showing ends with the match; a destroyed checker's visual is no longer among the views below.
            for (int i = 0; i < _crashes.Count; i++)
            {
                if (_crashes[i].Routine != null)
                    StopCoroutine(_crashes[i].Routine);
                if (_crashes[i].Destroyed)
                    Destroy(_crashes[i].View.gameObject);
            }

            _crashes.Clear();

            foreach (CheckerController view in _checkerViews.Values)
                Destroy(view.gameObject);

            _checkerViews.Clear();
        }

        /// <summary>Always an explicit seed, and logged, so any reported match or run can be replayed exactly.</summary>
        private SystemRandomProvider CreateRandom()
        {
            int seed = _randomSeed != 0 ? _randomSeed : Environment.TickCount;
            GameLog.Info($"[View] Random seed: {seed}", this);
            return new SystemRandomProvider(seed);
        }

        #endregion

        #region Sandbox match

        /// <summary>A single match played straight from this scene, with the sandbox settings.</summary>
        private void StartSandboxMatch()
        {
            MatchRules rules = CreateSandboxRules();
            GameState board = new GameState(rules);

            // The MatchContext constructor binds itself to the board (Bind);
            // without it special checker abilities silently do nothing.
            SystemRandomProvider random = CreateRandom();
            MatchContext context = new MatchContext(board, random);

            StartingLayout layout = StartingLayout.Deal(_player0Checkers, _player1Checkers, random);
            board.SetupPlayer(0, CreateDefaultCheckers(0, _player0Checkers), layout);
            board.SetupPlayer(1, CreateDefaultCheckers(1, _player1Checkers), layout);

            GameLog.Info($"[View] Match: {DescribeRules(rules)}, checkers {_player0Checkers} vs {_player1Checkers}.", this);
            BeginMatch(board, context);
        }

        /// <summary>Rules of the sandbox match. The human player is the one chasing the target.</summary>
        private MatchRules CreateSandboxRules()
        {
            if (_targetScore <= 0)
                return MatchRules.Classic(_turnLimit);

            return MatchRules.ForTarget(_targetScore, _turnLimit, _finalStretchTurns, HumanPlayerId);
        }

        private static string DescribeRules(MatchRules rules)
        {
            return rules.HasTargetScore
                ? $"target {rules.TargetScore} for player {rules.ScoringPlayerId} in {rules.TurnLimit} turns " +
                  $"(final stretch {rules.FinalStretchTurns})"
                : $"classic, {rules.TurnLimit} turns";
        }

        private static BaseChecker[] CreateDefaultCheckers(int ownerId, int count)
        {
            BaseChecker[] checkers = new BaseChecker[count];
            for (int i = 0; i < count; i++)
                checkers[i] = new DefaultChecker(ownerId);
            return checkers;
        }

        /// <summary>
        /// Replaces the match on the table with one built elsewhere — the sandbox's god-mode panel deals its own
        /// checkers, tiles and dice. The board must be freshly set up and bound to <paramref name="context"/>; the
        /// first dice are rolled here, as for every match. A run deals its own matches, so it refuses this.
        /// </summary>
        /// <exception cref="ArgumentNullException">No board or no context.</exception>
        /// <exception cref="ArgumentException">The context belongs to another board.</exception>
        /// <exception cref="InvalidOperationException">A run is being played, or the scene is not built yet.</exception>
        public void PlayMatch(GameState board, MatchContext context)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!ReferenceEquals(context.Board, board))
                throw new ArgumentException("The context is bound to another board.", nameof(context));

            if (_runState != null)
                throw new InvalidOperationException("A run deals its own matches; PlayMatch is for the sandbox.");

            if (_checkerRoot == null)
                throw new InvalidOperationException("BoardManager has not built the scene yet (or is disabled).");

            EndMatch();
            BeginMatch(board, context);
        }

        #endregion

        #region Run

        /// <summary>
        /// Starts the assigned run. Invalid run data is reported and the scene falls back to a sandbox match,
        /// so a mistake in the asset never leaves an empty board.
        /// </summary>
        private bool TryStartRun()
        {
            RunDefinition definition;
            try
            {
                definition = _run.ToDefinition();
            }
            catch (ArgumentException e)
            {
                Debug.LogError($"[View] Run '{_run.name}' is invalid, playing a sandbox match instead: {e.Message}", _run);
                return false;
            }

            GameLog.Info($"[View] Run '{_run.name}': {definition.Stages.Count} stages, {definition.MatchCount} matches.", this);

            // Without its art the shop cannot be drawn, so the run silently goes from match to match: say why.
            if (definition.Shop != null && _shopPresenter == null)
                Debug.LogWarning($"[View] Run '{_run.name}' has a shop, but no Shop Art is assigned to BoardManager: " +
                                 "the shop is skipped between matches. Assign Assets/Data/ShopArt.asset.", this);

            _runState = new RunState(definition, CreateRandom(), HumanPlayerId);
            _runState.MatchFinished += HandleRunMatchFinished;
            _runState.RunEnded += HandleRunEnded;

            StartRunMatch();
            return true;
        }

        private void StartRunMatch()
        {
            RunMatch match = _runState.StartMatch();

            GameLog.Info($"[View] Stage {match.StageIndex + 1}, match {match.MatchIndex + 1}" +
                         $"{(match.Definition.IsBoss ? " (boss)" : string.Empty)}: {DescribeRules(match.Board.Rules)}, " +
                         $"checkers {_runState.Deck.Count} vs {match.Definition.OpponentCheckers}, gold {_runState.Gold}.", this);

            BeginMatch(match.Board, match.Context);
        }

        /// <summary>
        /// Runs INSIDE the board's MatchEnded, before the rest of the scene has seen the end of the match —
        /// so the next match is only scheduled here, never built.
        /// </summary>
        private void HandleRunMatchFinished(MatchOutcome outcome)
        {
            GameLog.Info($"[View] Stage {outcome.StageIndex + 1}, match {outcome.MatchIndex + 1} " +
                         $"{(outcome.IsWin ? "won" : "lost")}: score {outcome.Score}/{outcome.TargetScore}, " +
                         $"reward {outcome.WinGold} + {outcome.UnusedTurnGold} ({outcome.UnusedTurns} unused turns), " +
                         $"gold {outcome.GoldAfter}.", this);

            if (_runState.CanStartMatch)
                StartCoroutine(ContinueRunAfterDelay());
        }

        private void HandleRunEnded(RunStatus status)
        {
            GameLog.Info(status == RunStatus.Won
                ? "[View] Run complete — every match won."
                : "[View] Run over.", this);
        }

        /// <summary>
        /// The result stays on screen for a moment; then the table is cleared and either the shop opens or,
        /// without one, the next match is dealt.
        /// </summary>
        private IEnumerator ContinueRunAfterDelay()
        {
            yield return new WaitForSeconds(_nextMatchDelay);

            EndMatch();

            if (_shopPresenter != null && _runState.CanOpenShop)
            {
                _shopPresenter.Show(_runState.OpenShop(), _runState, _hud);
                yield break;
            }

            StartRunMatch();
        }

        /// <summary>NEXT in the shop: close it and deal the next match.</summary>
        private void HandleShopNext()
        {
            _shopPresenter.Hide();

            if (_runState.ActiveShop != null)
                _runState.ActiveShop.Close();

            StartRunMatch();
        }

        #endregion

        #region Scene

        private void BuildScene()
        {
            if (_configureCamera)
                BoardSceneBuilder.ConfigureCamera(_layout, _cameraBackgroundColor);

            if (_backgroundSprite != null)
                BoardSceneBuilder.SpawnCanvasLayer("ScreenBackground", _backgroundSprite, SortingOrders.ScreenBackground);

            if (_boardSprite != null)
                BoardSceneBuilder.SpawnCanvasLayer("BoardBackground", _boardSprite, SortingOrders.BoardBackground);

            if (_pointsSprite != null)
                BoardSceneBuilder.SpawnCanvasLayer("Points", _pointsSprite, SortingOrders.BoardPoints);

            _solidSprite = BoardSceneBuilder.CreateSolidSprite();

            Sprite markSprite = _markSprite;
            if (markSprite == null)
                markSprite = _builtInMarkSprite = BoardSceneBuilder.CreateFootprintSprite(_layout.PixelsPerUnit);

            _nodeViews.AddRange(BoardSceneBuilder.SpawnNodes(_layout, _nodePrefab, _pointHighlightSprite, _solidSprite,
                                                             markSprite, _markColor));
            _bearOffView = BoardSceneBuilder.SpawnBearOffTarget(_layout, _solidSprite, _bearOffTrayColor);

            _volleyTargetMark = _volleyTargetSprite;
            if (_volleyTargetMark == null)
                _volleyTargetMark = _builtInVolleyTarget = BoardSceneBuilder.CreateTargetSprite(_layout.PixelsPerUnit);

            _volleyAimedMark = _volleyAimedSprite;
            if (_volleyAimedMark == null)
                _volleyAimedMark = _builtInVolleyAimed = BoardSceneBuilder.CreateAimedSprite(_layout.PixelsPerUnit);

            // The checkers belong to a match; BeginMatch spawns them.
            _checkerRoot = BoardSceneBuilder.CreateWorldRoot("Checkers");

            if (_diceCardArt != null)
                BuildDiceCards();

            if (_tileArt != null)
                BuildTiles();

            if (_shopArt != null)
                BuildShop();
        }

        /// <summary>The dice cards, built once and bound to every match. Only rolls played by hand turn them up.</summary>
        private void BuildDiceCards()
        {
            if (!_diceCardArt.IsComplete)
                Debug.LogWarning($"[View] Dice card art '{_diceCardArt.name}' is missing sprites; those cards stay empty.", _diceCardArt);

            _diceCards = new DiceCardsPresenter(DiceCardsView.Create(_diceCardArt, _layout), IsHuman);
        }

        /// <summary>The tiles over the points, built once; <see cref="RefreshTiles"/> shows each match's own.</summary>
        private void BuildTiles()
        {
            if (!_tileArt.IsComplete)
                Debug.LogWarning($"[View] Tile art '{_tileArt.name}' is missing sprites; those frames stay empty.", _tileArt);

            _tiles = TilesView.Create(_tileArt, _solidSprite, _layout);
        }

        /// <summary>
        /// The shop screen, built once and shown between the matches of a run. Its shelves draw the checkers for sale
        /// with the same art the board uses.
        /// </summary>
        private void BuildShop()
        {
            if (!_shopArt.IsComplete)
                Debug.LogWarning($"[View] Shop art '{_shopArt.name}' is missing sprites; those parts of the shop stay empty.", _shopArt);

            Sprite basicChecker = _playerCheckers.HasArt ? _playerCheckers.Normal : _checkerSprite;
            ShopView view = ShopView.Create(_shopArt, _solidSprite, _layout);

            _shopPresenter = new ShopPresenter(view, _shopTexts, _checkerArt, basicChecker);
            _shopPresenter.NextRequested += HandleShopNext;
        }

        /// <summary>
        /// Connects the info panel to Core.
        ///
        /// The events that carry dice/score/gold to the View already exist; the panel subscribes to them
        /// ITSELF, BoardManager only introduces them. So adding a new HUD field does not change this file.
        ///
        /// Must be called BEFORE the first dice roll (end of BeginMatch): otherwise the panel misses the
        /// first turn's dice and stays empty until the turn changes. Called again for every match of a run;
        /// HudPresenter.Bind drops the previous match first.
        /// </summary>
        private void BindHud()
        {
            if (_hud == null)
                _hud = FindAnyObjectByType<HudPresenter>();

            // The panel may not be in the scene (test scene, board only). The game runs unchanged.
            if (_hud == null)
                return;

            _hud.Bind(_gameState, _context);
        }

        private void SpawnCheckers()
        {
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = _gameState.Nodes[i];
                for (int k = 0; k < node.CheckerCount; k++)
                    SpawnCheckerView(node.Checkers[k]);
            }
        }

        /// <summary>The scene representative of a checker, under the checker root. A checker that has one keeps it.</summary>
        private void SpawnCheckerView(BaseChecker model)
        {
            if (_checkerViews.ContainsKey(model))
                return;

            CheckerController view = _checkerPrefab != null
                ? Instantiate(_checkerPrefab, _checkerRoot)
                : CreateRuntimeChecker(_checkerRoot);

            // A skin with art keeps its own colours. Otherwise the checker keeps the sprite it was built with
            // (the white checker sprite or the prefab's art) and takes the player colour.
            CheckerSkin skin = GetSkin(model);
            Color tint = skin.HasArt ? Color.white : model.OwnerId == 0 ? _player0Color : _player1Color;
            view.Bind(this, model, skin, tint);

            _checkerViews.Add(model, view);
        }

        /// <summary>
        /// The human player's special checkers wear their own art when the art set has it; every other checker
        /// wears its side's skin. Only the human's special checkers are drawn apart: the opponent fields plain ones.
        /// </summary>
        private CheckerSkin GetSkin(BaseChecker model)
        {
            if (model.OwnerId != HumanPlayerId)
                return _opponentCheckers;

            CheckerSkin special = _checkerArt != null ? _checkerArt.GetSkin(model.TypeId) : null;
            return special ?? _playerCheckers;
        }

        /// <summary>
        /// Builds the checker object in code when there is no prefab. Removes the prefab authoring step
        /// from the editor entirely — all that is needed is the sprite field in the Inspector.
        /// </summary>
        private CheckerController CreateRuntimeChecker(Transform parent)
        {
            GameObject go = new GameObject("Checker");
            go.transform.SetParent(parent, false);

            SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _checkerSprite;

            float size = _layout.CheckerWorldSize;
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(size, size);

            return go.AddComponent<CheckerController>();
        }

        #endregion

        #region Subscriptions and lifecycle

        private void Subscribe()
        {
            // First, so the log reads in the order things happen (see BoardEventLog).
            _eventLog.Attach(_gameState, _context);

            _gameState.MoveExecuted += HandleMoveExecuted;
            _gameState.MoveRejected += HandleMoveRejected;
            _gameState.CheckerDestroyed += HandleCheckerDestroyed;
            _gameState.CheckerAdded += HandleCheckerAdded;
            _gameState.CheckerReleased += HandleCheckerReleased;
            _gameState.CheckerSacrificed += HandleCheckerSacrificed;
            _gameState.VolleyFired += HandleVolleyFired;
            _gameState.AbilityTriggered += HandleAbilityTriggered;
            _gameState.TurnChanged += HandleTurnChanged;
            _gameState.MatchEnded += HandleMatchEnded;

            _context.DiceRolled += HandleDiceRolled;
        }

        private void OnEnable()
        {
#if TAVLA_NEW_INPUT
            _endTurnAction.Enable();
#endif
        }

        private void OnDisable()
        {
#if TAVLA_NEW_INPUT
            _endTurnAction.Disable();
#endif
        }

        private void OnDestroy()
        {
            // Without unsubscribing, Core would keep a reference to a destroyed MonoBehaviour,
            // causing MissingReferenceException and a memory leak on scene changes.
            Unsubscribe();

            if (_diceCards != null)
                _diceCards.Unbind();

            if (_runState != null)
            {
                _runState.MatchFinished -= HandleRunMatchFinished;
                _runState.RunEnded -= HandleRunEnded;
            }

            if (_shopPresenter != null)
            {
                _shopPresenter.NextRequested -= HandleShopNext;
                _shopPresenter.Dispose();
            }

            // Runtime-created textures/sprites/assets are not garbage collected by Unity; release them explicitly.
            DestroyRuntimeSprite(ref _solidSprite);
            DestroyRuntimeSprite(ref _builtInMarkSprite);
            DestroyRuntimeSprite(ref _builtInVolleyTarget);
            DestroyRuntimeSprite(ref _builtInVolleyAimed);

            if (_ownsLayout && _layout != null)
            {
                Destroy(_layout);
                _layout = null;
            }
        }

        private static void DestroyRuntimeSprite(ref Sprite sprite)
        {
            if (sprite == null)
                return;

            Destroy(sprite.texture);
            Destroy(sprite);
            sprite = null;
        }

        /// <summary>Disconnects from the current match. Safe to call more than once.</summary>
        private void Unsubscribe()
        {
            _eventLog.Detach();

            if (_gameState != null)
            {
                _gameState.MoveExecuted -= HandleMoveExecuted;
                _gameState.MoveRejected -= HandleMoveRejected;
                _gameState.CheckerDestroyed -= HandleCheckerDestroyed;
                _gameState.CheckerAdded -= HandleCheckerAdded;
                _gameState.CheckerReleased -= HandleCheckerReleased;
                _gameState.CheckerSacrificed -= HandleCheckerSacrificed;
                _gameState.VolleyFired -= HandleVolleyFired;
                _gameState.AbilityTriggered -= HandleAbilityTriggered;
                _gameState.TurnChanged -= HandleTurnChanged;
                _gameState.MatchEnded -= HandleMatchEnded;
            }

            if (_context != null)
                _context.DiceRolled -= HandleDiceRolled;
        }

        #endregion

        #region View -> Core

        /// <summary>
        /// Called when a CheckerController drops a checker. All we do is forward the question;
        /// the validity decision belongs entirely to Core.
        /// Core is told WHICH checker was dropped, not just its point: a stack may mix special and plain checkers,
        /// and the one the player picked up is the one that moves.
        /// Dropping a checker back on its own point is the gesture for acting in place: freeing a locked checker
        /// (Swamp) or, failing that, sacrificing a checker for its ability (Second Chance).
        /// </summary>
        public void RequestMove(BaseChecker checker, int toIndex)
        {
            // Still shown on the point it crashed onto, not where Core has put it: it may play once it is there.
            if (IsCrashing(checker))
            {
                RefreshBoard();
                return;
            }

            if (toIndex == GetSourceIndex(checker))
            {
                // Not a move. If Core refuses, the checker left hanging after the drag must snap back.
                if (!_gameState.TryReleaseChecker(checker) && !_gameState.TrySacrificeChecker(checker))
                    RefreshBoard();
                return;
            }

            _gameState.TryMove(checker, toIndex);
        }

        /// <summary>
        /// Asks the model which point a checker is on RIGHT NOW.
        /// The checker does not store its own position — GameState is the single source of truth.
        /// Returns <see cref="GameState.BarIndex"/> if it is on the bar; TryMove interprets that as entering from the bar.
        /// </summary>
        public int GetSourceIndex(BaseChecker checker)
        {
            return _gameState.FindNodeIndexOf(checker);
        }

        /// <summary>
        /// Highlights the points a picked-up checker can move to.
        /// The rules are NOT computed in the View — Core's CanMove runs the same validation code,
        /// so a "highlighted point gets rejected" mismatch cannot happen.
        /// </summary>
        public void ShowLegalTargets(BaseChecker checker)
        {
            ClearHighlights();

            if (IsCrashing(checker))
                return;

            _gameState.GetLegalTargets(checker, _legalTargets);
            for (int i = 0; i < _legalTargets.Count; i++)
                _nodeViews[_legalTargets[i]].SetHighlight(true);

            // GetLegalTargets only scans points 0-23 (the bot also asks about bearing off separately).
            // The bear-off highlight goes through the same Core validation, CanBearOff.
            bool canBearOff = _gameState.CanBearOff(checker);
            if (_bearOffView != null)
                _bearOffView.SetHighlight(canBearOff);

            // A checker that can act in place — a locked one that can be freed, or one that can be sacrificed —
            // lights up its own point: dropping it back there does it. Both need it on a point.
            int fromIndex = GetSourceIndex(checker);
            bool canActInPlace = _gameState.CanReleaseChecker(checker) || _gameState.CanSacrificeChecker(checker);
            if (canActInPlace)
                _nodeViews[fromIndex].SetHighlight(true);

            if (_legalTargets.Count == 0 && !canBearOff && !canActInPlace)
                GameLog.Info($"[View] No legal move for {checker.TypeId} on point {fromIndex} with these dice.");
        }

        /// <summary>
        /// Called by a CheckerController when it is clicked. While a human's volley waits for its targets (Bowman) the
        /// click picks the checker as one, or drops it if it was picked, and returns true: the checker is not picked
        /// up. The volley fires on the last pick it needs. Returns false when no volley waits.
        /// </summary>
        public bool TryAimVolley(BaseChecker checker)
        {
            if (!IsAimingVolley)
                return false;

            if (!_volleyPicks.Remove(checker) && _gameState.CanVolleyTarget(checker))
            {
                _volleyPicks.Add(checker);

                // Once fired, VolleyFired redraws the board.
                if (_volleyPicks.Count >= _gameState.PendingVolleySize && _gameState.TryFireVolley(_volleyPicks.ToArray()))
                    return true;
            }

            RefreshVolley();
            return true;
        }

        public void ClearHighlights()
        {
            for (int i = 0; i < _nodeViews.Count; i++)
                _nodeViews[i].SetHighlight(false);

            if (_bearOffView != null)
                _bearOffView.SetHighlight(false);
        }

        /// <summary>
        /// Lets the human player end the turn manually: shortcut key, a UI button (OnClick) or the ContextMenu.
        /// Normally not needed — the turn passes automatically when no move is left
        /// (see AutoEndTurnWhenStuck). This is a safety net for cases the auto-pass misses.
        /// It does not interrupt the bot's turn; the bot ends its own turn in RunBotTurn.
        /// </summary>
        [ContextMenu("End Turn")]
        public void EndTurn()
        {
            // Called from the ContextMenu outside Play mode, Core is not built yet.
            if (_gameState == null || !IsHumanTurn)
                return;

            CancelAutoEnd();
            ClearHighlights();
            _gameState.EndTurn();
        }

        private void Update()
        {
            if (_gameState != null && WasEndTurnPressed())
                EndTurn();
        }

        private bool WasEndTurnPressed()
        {
#if TAVLA_NEW_INPUT
            return _endTurnAction.WasPressedThisFrame();
#else
            return Input.GetKeyDown(_endTurnKey);
#endif
        }

        #endregion

        #region Core -> View

        /// <summary>
        /// Lays out the board again from the model's current state.
        /// It never looks at move data — so teleports, mass captures, destruction and every other
        /// special case look right without extra code.
        /// </summary>
        public void RefreshBoard(bool instant = false)
        {
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = _gameState.Nodes[i];

                // The total count is passed as well: for large stacks BoardLayout shrinks the spacing
                // to keep the checkers inside the triangle (15 checkers can now share one point).
                // A checker still crashing onto the point counts too, drawn on top by PlaceCrashes.
                int count = node.CheckerCount;
                int shown = count + CountCrashesOn(i, _crashes.Count);
                for (int k = 0; k < count; k++)
                    PlaceChecker(node.Checkers[k], _layout.GetCheckerPosition(i, k, shown),
                                 SortingOrders.CheckerBase + k, instant);
            }

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                IReadOnlyList<BaseChecker> bar = _gameState.GetBar(p);
                for (int k = 0; k < bar.Count; k++)
                    PlaceChecker(bar[k], _layout.GetBarPosition(p, k, bar.Count),
                                 SortingOrders.BarCheckerBase + k, instant);
            }

            PlaceCrashes(instant);
            RefreshMarks();
            RefreshTiles();
            RefreshMovableCheckers();
            RefreshVolley();
        }

        /// <summary>
        /// Shows the tiles on the points. The view remembers what it showed, across matches too, so only a point whose
        /// tile came or went turns over: a new match of the same stage keeps its tiles still, while the stage that adds
        /// one, a fresh sandbox deal or a tile put on or taken off in the sandbox mid-match plays the change. Always
        /// animated, even when <see cref="RefreshBoard"/> snaps the checkers.
        /// </summary>
        private void RefreshTiles()
        {
            if (_tiles == null)
                return;

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                // A Dungeon opens or closes as a round ends, and the next turn's roll redraws the board: in step.
                ITileEffect tile = _gameState.Nodes[i].ActiveTile;
                IOpenCloseTile gate = tile as IOpenCloseTile;
                _tiles.Show(i, tile != null ? tile.EffectId : null, gate != null && gate.IsOpen);

                // A countdown drops as a turn ends, and the next turn's roll redraws the board: always in step.
                ICountdownTile countdown = tile as ICountdownTile;
                _tiles.ShowCount(i, countdown != null ? countdown.Countdown : TilesView.NoCount);
            }
        }

        /// <summary>
        /// Shows the points the human player has marked — the Navigator's footprints. Marks are only left during a
        /// move, and every move ends in RefreshBoard, so reading them here keeps the board in step without an event.
        /// </summary>
        private void RefreshMarks()
        {
            for (int i = 0; i < GameState.NodeCount; i++)
                _nodeViews[i].SetMarked(_gameState.Nodes[i].GetMark(HumanPlayerId) != null);
        }

        /// <summary>
        /// Marks the checkers the human player can move with the rolled dice — every one of them, not only the
        /// topmost of each stack, since any checker in a stack can be picked up. Whether a checker can be played is
        /// Core's answer (CanPlayChecker); the View computes no rules, it only paints the result. Called whenever
        /// the board or the dice change.
        /// </summary>
        private void RefreshMovableCheckers()
        {
            // Before the first roll Core treats every die as available (that serves rule tests), so wait for the dice.
            bool humanCanPlay = _gameState != null && _context.CurrentRoll != null && IsHumanTurn;

            foreach (CheckerController view in _checkerViews.Values)
                view.SetMovable(humanCanPlay && !IsCrashing(view.Model) && _gameState.CanPlayChecker(view.Model));
        }

        /// <summary>
        /// Draws every checker still crashing on top of the point it was thrown onto — wherever Core has put it since
        /// (the bar, its starting point) or even if Core has destroyed it.
        /// </summary>
        private void PlaceCrashes(bool instant)
        {
            for (int i = 0; i < _crashes.Count; i++)
            {
                Crash crash = _crashes[i];
                int count = _gameState.Nodes[crash.Point].CheckerCount;
                int k = count + CountCrashesOn(crash.Point, i);
                int shown = count + CountCrashesOn(crash.Point, _crashes.Count);

                crash.View.SetTarget(_layout.GetCheckerPosition(crash.Point, k, shown), SortingOrders.CheckerBase + k);
                if (instant)
                    crash.View.SnapToTarget();
            }
        }

        /// <summary>How many of the first <paramref name="crashCount"/> crashes are on this point.</summary>
        private int CountCrashesOn(int point, int crashCount)
        {
            int found = 0;
            for (int i = 0; i < crashCount; i++)
            {
                if (_crashes[i].Point == point)
                    found++;
            }

            return found;
        }

        private bool IsCrashing(BaseChecker checker)
        {
            return FindCrash(checker) != null;
        }

        private Crash FindCrash(BaseChecker checker)
        {
            for (int i = 0; i < _crashes.Count; i++)
            {
                if (ReferenceEquals(_crashes[i].Checker, checker))
                    return _crashes[i];
            }

            return null;
        }

        /// <summary>
        /// While a human's volley waits for its targets (Bowman): marks every checker it can hit and the ones picked so
        /// far, and shows on the task line how many are left to pick. Which checkers can be hit is Core's answer
        /// (CanVolleyTarget). Once nothing waits the marks go and the task line shows the turns again.
        /// </summary>
        private void RefreshVolley()
        {
            bool aiming = IsAimingVolley;

            if (!aiming)
                _volleyPicks.Clear();
            else
                _volleyPicks.RemoveAll(picked => !_gameState.CanVolleyTarget(picked));

            foreach (CheckerController view in _checkerViews.Values)
            {
                if (aiming && _volleyPicks.Contains(view.Model))
                    view.SetVolleyMark(_volleyAimedMark, _volleyAimedColor);
                else if (aiming && _gameState.CanVolleyTarget(view.Model))
                    view.SetVolleyMark(_volleyTargetMark, _volleyTargetColor);
                else
                    view.SetVolleyMark(null, Color.clear);
            }

            if (_hud != null)
            {
                if (aiming)
                    _hud.SetTask(string.Format(_volleyTask, _gameState.PendingVolleySize - _volleyPicks.Count));
                else if (_showingVolleyTask && !_gameState.IsMatchOver) // A finished match shows its result there.
                    _hud.RefreshAll();
            }

            _showingVolleyTask = aiming;
        }

        private void PlaceChecker(BaseChecker model, Vector3 position, int sortingOrder, bool instant)
        {
            CheckerController view;
            if (!_checkerViews.TryGetValue(model, out view))
                return;

            view.SetTarget(position, sortingOrder);

            if (instant)
                view.SnapToTarget();
        }

        private void HandleMoveExecuted(MoveResult result)
        {
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleMoveRejected(MoveResult result)
        {
            // Snaps a checker left hanging after a drag back into place. No separate "bounce back" code needed.
            RefreshBoard();
        }

        private void HandleCheckerDestroyed(BaseChecker checker, int nodeIndex)
        {
            CheckerController view;
            if (_checkerViews.TryGetValue(checker, out view))
            {
                _checkerViews.Remove(checker);

                // A checker destroyed as it crashed (Sacrificial) is still to be seen arriving; ShowCrash removes it.
                Crash crash = FindCrash(checker);
                if (crash != null)
                    crash.Destroyed = true;
                else
                    Destroy(view.gameObject);
            }
        }

        /// <summary>
        /// A checker joined the match mid-way (Mutiny). It gets a visual, placed straight onto its slot rather than
        /// gliding in from the origin. This runs in the middle of the move, so only this checker is placed;
        /// the move's own RefreshBoard lays out the rest.
        /// </summary>
        private void HandleCheckerAdded(BaseChecker checker, int nodeIndex)
        {
            SpawnCheckerView(checker);

            Node node = _gameState.Nodes[nodeIndex];
            for (int k = 0; k < node.CheckerCount; k++)
            {
                if (ReferenceEquals(node.Checkers[k], checker))
                    PlaceChecker(checker, _layout.GetCheckerPosition(nodeIndex, k, node.CheckerCount),
                                 SortingOrders.CheckerBase + k, true);
            }
        }

        private void HandleCheckerSacrificed(BaseChecker checker, int nodeIndex)
        {
            // The checker is gone and the dice may have grown: snap the dragged visual back and re-check the turn.
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleCheckerReleased(BaseChecker checker, int nodeIndex)
        {
            // The checker did not move, but a die was spent: snap the dragged visual back and re-check the turn.
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleVolleyFired(BaseChecker shooter, IReadOnlyList<BaseChecker> targets)
        {
            // The targets went to the bar (or home, or out of play) and play goes on with the dice left.
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        /// <summary>
        /// A checker thrown onto a point it may not enter (the Maelstrom flinging it onto a made point) has crashed
        /// there: Core broke it at once, but the View shows it reach that point first (<see cref="ShowCrash"/>).
        /// </summary>
        private void HandleAbilityTriggered(string abilityId, BaseChecker checker, int nodeIndex)
        {
            CheckerController view;
            if (abilityId != AbilityIds.TeleportCrash || !_checkerViews.TryGetValue(checker, out view))
                return;

            Crash crash = new Crash { Checker = checker, View = view, Point = nodeIndex };
            _crashes.Add(crash);
            crash.Routine = StartCoroutine(ShowCrash(crash));
        }

        /// <summary>Holds a crashed checker on its point until it has got there and the crash delay has passed.</summary>
        private IEnumerator ShowCrash(Crash crash)
        {
            // The crash is reported mid-move; the move's own RefreshBoard sends the checker to the point.
            yield return null;

            while (!crash.View.IsAtTarget)
                yield return null;

            yield return new WaitForSeconds(_crashDelay);

            _crashes.Remove(crash);
            if (crash.Destroyed)
                Destroy(crash.View.gameObject);

            RefreshBoard();
        }

        private void HandleTurnChanged(int playerId)
        {
            _context.RollDice(playerId);

            // Wake the bot AFTER the dice are rolled — it cannot think before its hand is known.
            TryStartBotTurn(playerId);
        }

        private void HandleMatchEnded(int winnerId)
        {
            StopBotTurn();
            CancelAutoEnd();
            RefreshMovableCheckers();
            RefreshVolley();

            // A keg's blast can end the match as a turn ends, with no roll after it to redraw its count.
            RefreshTiles();
        }

        #endregion

        #region AI

        /// <summary>
        /// Starts the turn routine if it is the bot's turn. Does nothing if the bot is disabled or the match is over.
        /// </summary>
        private void TryStartBotTurn(int playerId)
        {
            if (_bot == null || playerId != _botPlayerId || _gameState.IsMatchOver)
                return;

            StopBotTurn();
            _botRoutine = StartCoroutine(RunBotTurn());
        }

        private void StopBotTurn()
        {
            if (_botRoutine == null)
                return;

            StopCoroutine(_botRoutine);
            _botRoutine = null;
        }

        /// <summary>
        /// The bot's turn. Plays moves one by one until it runs out, then passes the turn.
        ///
        /// The delays exist only FOR THE PLAYER: the bot actually decides instantly, but if checkers
        /// jumped around all at once nobody could follow what happened.
        /// </summary>
        private IEnumerator RunBotTurn()
        {
            yield return new WaitForSeconds(_botThinkDelay);

            int moveCount = 0;

            while (!_gameState.IsMatchOver
                   && _gameState.CurrentPlayerId == _botPlayerId
                   && moveCount < MaxBotMovesPerTurn)
            {
                // PlayOneMove returns false when there is nothing left to play.
                if (!_bot.PlayOneMove(_gameState))
                    break;

                moveCount++;
                yield return new WaitForSeconds(_botMoveDelay);

                // A checker crashing where the Maelstrom threw it is seen broken before anything else happens.
                while (_crashes.Count > 0)
                    yield return null;
            }

            // Clear before the routine ends: EndTurn will start a new turn and overwrite this field.
            _botRoutine = null;

            if (!_gameState.IsMatchOver && _gameState.CurrentPlayerId == _botPlayerId)
                _gameState.EndTurn();
        }

        #endregion

        #region Turn hand-over

        /// <summary>Is it a human's turn? With the bot disabled both sides are human.</summary>
        private bool IsHumanTurn => !_gameState.IsMatchOver && IsHuman(_gameState.CurrentPlayerId);

        /// <summary>Is this seat played by hand right now? With the bot disabled both are.</summary>
        private bool IsHuman(int playerId) => _bot == null || playerId != _botPlayerId;

        /// <summary>Is a volley waiting for the human to pick its targets? The bot fires its own in its turn routine.</summary>
        private bool IsAimingVolley => _gameState != null && IsHumanTurn && _gameState.HasPendingVolley;

        /// <summary>
        /// Schedules a check whether the human player still has a move. Called after every move and every
        /// dice roll — so both "dice are spent" and "the rolled dice cannot be played at all" are
        /// handled by the same path.
        /// </summary>
        private void ScheduleAutoEndCheck()
        {
            CancelAutoEnd();

            if (!_autoEndTurn || !IsHumanTurn)
                return;

            _autoEndRoutine = StartCoroutine(AutoEndTurnWhenStuck(_gameState.CurrentPlayerId));
        }

        private void CancelAutoEnd()
        {
            if (_autoEndRoutine == null)
                return;

            StopCoroutine(_autoEndRoutine);
            _autoEndRoutine = null;
        }

        /// <summary>
        /// The delay serves two purposes: a compound move (5 points with 3-2) raises a separate MoveExecuted
        /// per step, so the decision is NOT made before the plan finishes; and the player gets to see
        /// their last move and understand what happened.
        /// </summary>
        private IEnumerator AutoEndTurnWhenStuck(int playerId)
        {
            yield return new WaitForSeconds(_autoEndDelay);

            // A checker crashing where the Maelstrom threw it is seen broken before the turn passes.
            while (_crashes.Count > 0)
                yield return null;

            // Clear before the routine ends: EndTurn may start a new turn and overwrite this field.
            _autoEndRoutine = null;

            if (_gameState.IsMatchOver
                || _gameState.CurrentPlayerId != playerId
                || _gameState.HasAnyLegalMove())
                yield break;

            GameLog.Info($"[View] Player {playerId} has no playable move left, passing the turn.");
            ClearHighlights();
            _gameState.EndTurn();
        }

        #endregion

        #region MatchContext listeners

        private void HandleDiceRolled(DiceRoll roll)
        {
            // A roll can move checkers by itself (a Kraken waking on a double), and new dice give a new answer:
            // the turn's checkers light up (or, on the bot's turn, go dark). RefreshBoard does both.
            RefreshBoard();

            // New dice may not be playable at all. Checked here rather than where the turn rolls, so dice rolled
            // from anywhere else (the sandbox re-rolling a turn) get the same auto-pass.
            ScheduleAutoEndCheck();
        }

        #endregion
    }
}
