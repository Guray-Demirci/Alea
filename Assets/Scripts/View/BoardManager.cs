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
    ///   2) build the scene (24 points + bear-off area) once, and the checkers of every match,
    ///   3) forward requests from CheckerController to TryMove,
    ///   4) refresh the visuals when Core raises events.
    ///
    /// KEY DESIGN: checker positions are NOT computed from move data. After every event
    /// RefreshBoard() re-reads the board from the model's current state. That is why the Doomsday
    /// Checker's mass capture, the Maelstrom's teleport, the Phoenix's return and 30 other special
    /// cases need not a single extra line of View code.
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
        private TavlaBot _bot;
        private Coroutine _botRoutine;
        private Coroutine _autoEndRoutine;

        // Core object -> scene representative. This dictionary is the View's only link to the model.
        private readonly Dictionary<BaseChecker, CheckerController> _checkerViews =
            new Dictionary<BaseChecker, CheckerController>();

        private readonly List<NodeView> _nodeViews = new List<NodeView>();

        // Reused for every drag highlight so picking up a checker does not allocate.
        private readonly List<int> _legalTargets = new List<int>(GameState.NodeCount);

        // _nodeViews is addressed by point index (0-23); the bear-off target is not part of that list.
        private NodeView _bearOffView;

        // Parent of the checker views; created once, refilled for every match.
        private Transform _checkerRoot;

        // Runtime-created resources owned by this component; released in OnDestroy.
        private bool _ownsLayout;
        private Sprite _solidSprite;
        private Sprite _builtInMarkSprite;

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

                // Whose checkers light up and who gets the auto-pass both depend on who is human.
                RefreshMovableCheckers();
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

            // The marks belong to the match that left them.
            for (int i = 0; i < _nodeViews.Count; i++)
                _nodeViews[i].SetMarked(false);

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
                ConfigureCamera();

            if (_backgroundSprite != null)
                SpawnCanvasLayer("ScreenBackground", _backgroundSprite, SortingOrders.ScreenBackground);

            if (_boardSprite != null)
                SpawnCanvasLayer("BoardBackground", _boardSprite, SortingOrders.BoardBackground);

            if (_pointsSprite != null)
                SpawnCanvasLayer("Points", _pointsSprite, SortingOrders.BoardPoints);

            _solidSprite = CreateSolidSprite();

            Sprite markSprite = _markSprite;
            if (markSprite == null)
                markSprite = _builtInMarkSprite = CreateFootprintSprite(_layout.PixelsPerUnit);

            SpawnNodes(_solidSprite, markSprite);
            SpawnBearOffTarget(_solidSprite);

            // The checkers belong to a match; BeginMatch spawns them.
            _checkerRoot = CreateWorldRoot("Checkers");

            if (_shopArt != null)
                BuildShop();
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

        /// <summary>
        /// Configures the camera for the board. Creates one if the scene has no camera at all —
        /// a safety net against the "Display 1: No cameras rendering" error.
        /// </summary>
        private void ConfigureCamera()
        {
            Camera cam = Camera.main;

            if (cam == null)
                cam = FindAnyObjectByType<Camera>();

            if (cam == null)
            {
                GameObject go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                GameLog.Info("[View] The scene had no camera; one was created.", this);
            }

            cam.orthographic = true;
            cam.orthographicSize = _layout.CameraOrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _cameraBackgroundColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// All generated visuals live at the SCENE ROOT, at the world origin.
        /// They are not tied to the position/scale of the GameManager object — required because
        /// "Create Empty" places objects where the Scene view is looking, not at (0,0,0);
        /// otherwise the board background would drift away from the checkers.
        /// </summary>
        private static Transform CreateWorldRoot(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        /// <summary>
        /// Places a full-canvas artwork (background, board, points). They all share the canvas size (320x180)
        /// and PPU, so placed at the origin each exactly fills what the camera sees; only the sorting order
        /// stacks them.
        /// </summary>
        private static void SpawnCanvasLayer(string name, Sprite sprite, int sortingOrder)
        {
            Transform layer = CreateWorldRoot(name);

            SpriteRenderer spriteRenderer = layer.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Builds the 24 points from the geometry. No need to place 24 objects by hand.
        /// The highlight is the outline artwork if assigned, otherwise <paramref name="solid"/> as a fill.
        /// Every point also gets its (hidden) score mark, <paramref name="mark"/>.
        /// </summary>
        private void SpawnNodes(Sprite solid, Sprite mark)
        {
            Transform root = CreateWorldRoot("Nodes");

            Vector2 size = _layout.GetNodeSize();

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                NodeView view;

                if (_nodePrefab != null)
                {
                    view = Instantiate(_nodePrefab, root);
                }
                else
                {
                    GameObject go = new GameObject("Node");
                    go.transform.SetParent(root, false);
                    go.AddComponent<BoxCollider2D>();
                    view = go.AddComponent<NodeView>();
                }

                view.Initialize(i, _layout.GetNodeCenter(i), size);

                // The artwork is drawn for the top row (base up); the bottom row points the other way.
                if (_pointHighlightSprite != null)
                    view.BuildArtworkHighlight(_pointHighlightSprite, !_layout.IsTopRow(i));
                else
                    view.BuildFillHighlight(solid, size);

                view.BuildMark(mark, _markColor, _layout.GetMarkPosition(i));

                _nodeViews.Add(view);
            }
        }

        /// <summary>
        /// Bear-off drop target. Mechanically an ordinary NodeView — the only difference is that its index is
        /// <see cref="GameState.BearOffIndex"/>. CheckerController therefore sends RequestMove(checker, BearOffIndex)
        /// without knowing anything, and TryMove already interprets it as a bear-off. The point prefab is
        /// deliberately not used: its highlight is sized for a triangle and would not fit this strip.
        /// </summary>
        private void SpawnBearOffTarget(Sprite solid)
        {
            Transform root = CreateWorldRoot("BearOff");
            Vector2 size = _layout.GetBearOffSize();

            GameObject go = new GameObject("BearOffTray");
            go.transform.SetParent(root, false);
            go.AddComponent<BoxCollider2D>();

            _bearOffView = go.AddComponent<NodeView>();
            _bearOffView.Initialize(GameState.BearOffIndex, _layout.GetBearOffCenter(), size);
            _bearOffView.BuildFillHighlight(solid, size);
            go.name = "BearOffTray"; // Initialize renames it to "Node_-02".

            GameObject tray = new GameObject("Tray");
            tray.transform.SetParent(go.transform, false);
            tray.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer spriteRenderer = tray.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = solid;
            spriteRenderer.color = _bearOffTrayColor;
            spriteRenderer.sortingOrder = SortingOrders.BearOffTray;
        }

        /// <summary>1x1 white sprite for highlights — generated in code so no asset is required.</summary>
        private static Sprite CreateSolidSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.name = "SolidWhite";
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            // pixelsPerUnit = 1 -> the sprite is exactly 1 world unit and is sized via scale.
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "SolidWhite";
            return sprite;
        }

        /// <summary>
        /// The built-in score mark: a pair of footprints, one texel per canvas pixel, white so the tint colours it.
        /// Width and height are even, so the centred pivot lands on a pixel edge and the mark stays pixel-exact.
        /// </summary>
        private static Sprite CreateFootprintSprite(float pixelsPerUnit)
        {
            string[] rows =
            {
                "......XX",
                ".....XXX",
                ".....XXX",
                "XX...XX.",
                "XXX...X.",
                "XXX.....",
                ".XX.....",
                ".X......"
            };

            int width = rows[0].Length;
            int height = rows.Length;

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "Footprints";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            // Texture rows run bottom-up; the pattern above is written top-down.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    texture.SetPixel(x, height - 1 - y, rows[y][x] == 'X' ? Color.white : Color.clear);
            }
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            sprite.name = "Footprints";
            return sprite;
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
            _gameState.MoveExecuted += HandleMoveExecuted;
            _gameState.MoveRejected += HandleMoveRejected;
            _gameState.CheckerCaptured += HandleCheckerCaptured;
            _gameState.CheckerDestroyed += HandleCheckerDestroyed;
            _gameState.CheckerAdded += HandleCheckerAdded;
            _gameState.CheckerTeleported += HandleCheckerTeleported;
            _gameState.CheckerReleased += HandleCheckerReleased;
            _gameState.CheckerSacrificed += HandleCheckerSacrificed;
            _gameState.TileTriggered += HandleTileTriggered;
            _gameState.AbilityTriggered += HandleAbilityTriggered;
            _gameState.TurnChanged += HandleTurnChanged;
            _gameState.TurnCountChanged += HandleTurnCountChanged;
            _gameState.CheckerBorneOff += HandleCheckerBorneOff;
            _gameState.FinalStretchStarted += HandleFinalStretchStarted;
            _gameState.MatchEnded += HandleMatchEnded;

            _context.DiceRolled += HandleDiceRolled;
            _context.ScoreChanged += HandleScoreChanged;
            _context.GoldChanged += HandleGoldChanged;
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
            if (_solidSprite != null)
            {
                Destroy(_solidSprite.texture);
                Destroy(_solidSprite);
                _solidSprite = null;
            }

            if (_builtInMarkSprite != null)
            {
                Destroy(_builtInMarkSprite.texture);
                Destroy(_builtInMarkSprite);
                _builtInMarkSprite = null;
            }

            if (_ownsLayout && _layout != null)
            {
                Destroy(_layout);
                _layout = null;
            }
        }

        /// <summary>Disconnects from the current match. Safe to call more than once.</summary>
        private void Unsubscribe()
        {
            if (_gameState != null)
            {
                _gameState.MoveExecuted -= HandleMoveExecuted;
                _gameState.MoveRejected -= HandleMoveRejected;
                _gameState.CheckerCaptured -= HandleCheckerCaptured;
                _gameState.CheckerDestroyed -= HandleCheckerDestroyed;
                _gameState.CheckerAdded -= HandleCheckerAdded;
                _gameState.CheckerTeleported -= HandleCheckerTeleported;
                _gameState.CheckerReleased -= HandleCheckerReleased;
                _gameState.CheckerSacrificed -= HandleCheckerSacrificed;
                _gameState.TileTriggered -= HandleTileTriggered;
                _gameState.AbilityTriggered -= HandleAbilityTriggered;
                _gameState.TurnChanged -= HandleTurnChanged;
                _gameState.TurnCountChanged -= HandleTurnCountChanged;
                _gameState.CheckerBorneOff -= HandleCheckerBorneOff;
                _gameState.FinalStretchStarted -= HandleFinalStretchStarted;
                _gameState.MatchEnded -= HandleMatchEnded;
            }

            if (_context != null)
            {
                _context.DiceRolled -= HandleDiceRolled;
                _context.ScoreChanged -= HandleScoreChanged;
                _context.GoldChanged -= HandleGoldChanged;
            }
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
                int count = node.CheckerCount;
                for (int k = 0; k < count; k++)
                    PlaceChecker(node.Checkers[k], _layout.GetCheckerPosition(i, k, count),
                                 SortingOrders.CheckerBase + k, instant);
            }

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                IReadOnlyList<BaseChecker> bar = _gameState.GetBar(p);
                for (int k = 0; k < bar.Count; k++)
                    PlaceChecker(bar[k], _layout.GetBarPosition(p, k, bar.Count),
                                 SortingOrders.BarCheckerBase + k, instant);
            }

            RefreshMarks();
            RefreshMovableCheckers();
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
                view.SetMovable(humanCanPlay && _gameState.CanPlayChecker(view.Model));
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
            string suffix = result.WasInfiltration ? " [infiltration]" : string.Empty;
            string to = result.WasBearOff ? "bear-off" : result.ToIndex.ToString();
            GameLog.Info($"[View] Move: {result.FromIndex} -> {to} " +
                         $"(player {result.PlayerId}, captured: {result.CapturedCheckers.Count}){suffix}");

            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleMoveRejected(MoveResult result)
        {
            GameLog.Info($"[View] Move rejected: {result.Error}");

            // Snaps a checker left hanging after a drag back into place. No separate "bounce back" code needed.
            RefreshBoard();
        }

        private void HandleCheckerCaptured(BaseChecker checker, int nodeIndex, CaptureDestination destination)
        {
            switch (destination)
            {
                case CaptureDestination.HomeEntry:
                    GameLog.Info($"[View] {checker.TypeId} rose from the ashes and returned to its starting point.");
                    break;
                case CaptureDestination.Destroyed:
                    GameLog.Info($"[View] {checker.TypeId} sacrificed itself.");
                    break;
                case CaptureDestination.Converted:
                    GameLog.Info($"[View] {checker.TypeId} mutinied and changed sides (point {nodeIndex}).");
                    break;
                default:
                    GameLog.Info($"[View] {checker.TypeId} was hit and sent to the bar (point {nodeIndex}).");
                    break;
            }

            // TODO: play the capture VFX here. Positioning is RefreshBoard's job.
        }

        private void HandleCheckerDestroyed(BaseChecker checker, int nodeIndex)
        {
            CheckerController view;
            if (_checkerViews.TryGetValue(checker, out view))
            {
                _checkerViews.Remove(checker);
                Destroy(view.gameObject);
            }

            GameLog.Info($"[View] {checker.TypeId} was removed from the game (point {nodeIndex}).");
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

            GameLog.Info($"[View] {checker.TypeId} joined player {checker.OwnerId} (point {nodeIndex}).");
        }

        private void HandleCheckerSacrificed(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was sacrificed on point {nodeIndex} for its ability.");

            // The checker is gone and the dice may have grown: snap the dragged visual back and re-check the turn.
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleCheckerTeleported(BaseChecker checker, int fromIndex, int toIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was caught by the maelstrom: {fromIndex} -> {toIndex}");
        }

        private void HandleCheckerReleased(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was freed on point {nodeIndex} by sacrificing a die.");

            // The checker did not move, but a die was spent: snap the dragged visual back and re-check the turn.
            RefreshBoard();
            ScheduleAutoEndCheck();
        }

        private void HandleTileTriggered(ITileEffect tile, int nodeIndex, BaseChecker checker)
        {
            GameLog.Info($"[View] Tile triggered: {tile.EffectId} (point {nodeIndex})");
        }

        /// <summary>
        /// ONE event, 30 different VFX. When a new special checker is added BoardManager does not change —
        /// only a new abilityId -> ParticleSystem entry is added to a lookup.
        /// </summary>
        private void HandleAbilityTriggered(string abilityId, BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] Ability: {abilityId} (point {nodeIndex})");
        }

        private void HandleTurnChanged(int playerId)
        {
            GameLog.Info($"[View] Turn: player {playerId}");
            _context.RollDice(playerId);

            // Wake the bot AFTER the dice are rolled — it cannot think before its hand is known.
            TryStartBotTurn(playerId);
        }

        private void HandleTurnCountChanged(int remaining)
        {
            GameLog.Info($"[View] Turns remaining: {remaining}");
        }

        private void HandleCheckerBorneOff(BaseChecker checker, int nodeIndex)
        {
            GameLog.Info($"[View] Checker borne off: {checker.TypeId} (point {nodeIndex})");
        }

        private void HandleFinalStretchStarted(int remaining)
        {
            GameLog.Info($"[View] The opponent is out of checkers — final stretch, {remaining} turns left.");
        }

        private void HandleMatchEnded(int winnerId)
        {
            StopBotTurn();
            CancelAutoEnd();
            RefreshMovableCheckers();

            string message = winnerId == GameState.NoWinner
                ? "The match ended in a draw."
                : $"Match over — winner: player {winnerId}";

            GameLog.Info($"[View] {message} (P0: {_context.GetScore(0)} — P1: {_context.GetScore(1)})");
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
            }

            // Clear before the routine ends: EndTurn will start a new turn and overwrite this field.
            _botRoutine = null;

            if (!_gameState.IsMatchOver && _gameState.CurrentPlayerId == _botPlayerId)
                _gameState.EndTurn();
        }

        #endregion

        #region Turn hand-over

        /// <summary>Is it a human's turn? With the bot disabled both sides are human.</summary>
        private bool IsHumanTurn =>
            !_gameState.IsMatchOver && (_bot == null || _gameState.CurrentPlayerId != _botPlayerId);

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
            GameLog.Info($"[View] Dice: {roll.First}-{roll.Second}" + (roll.IsDouble ? " (double!)" : string.Empty));

            // A roll can move checkers by itself (a Kraken waking on a double), and new dice give a new answer:
            // the turn's checkers light up (or, on the bot's turn, go dark). RefreshBoard does both.
            RefreshBoard();

            // New dice may not be playable at all. Checked here rather than where the turn rolls, so dice rolled
            // from anywhere else (the sandbox re-rolling a turn) get the same auto-pass.
            ScheduleAutoEndCheck();
        }

        private void HandleScoreChanged(int playerId, int score) => GameLog.Info($"[View] Score P{playerId}: {score}");

        private void HandleGoldChanged(int playerId, int gold) => GameLog.Info($"[View] Gold P{playerId}: {gold}");

        #endregion
    }
}
