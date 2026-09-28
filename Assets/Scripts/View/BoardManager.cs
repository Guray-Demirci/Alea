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
using Tavla.Core.Runtime;

namespace Tavla.View
{
    /// <summary>
    /// The ONLY bridge between CORE and UNITY. There are NO game rules here.
    ///
    /// Responsibilities:
    ///   1) boot Core (GameState + MatchContext),
    ///   2) build the scene (24 points + bear-off area + checkers),
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
        [Tooltip("The checker_scheme sprite. No prefab needed — checker objects are generated in code.")]
        [SerializeField] private Sprite _checkerSprite;
        [Tooltip("The play_game_scheme sprite. If assigned, it is placed in the scene as the board.")]
        [SerializeField] private Sprite _boardSprite;
        [Tooltip("Background sprite. Drawn behind the board, covering the whole screen; " +
                 "visible through the transparent area of the board sprite.")]
        [SerializeField] private Sprite _backgroundSprite;

        [Header("Prefabs (optional)")]
        [Tooltip("If set, checkers are spawned from this prefab; otherwise they are built in code from the sprite above.")]
        [SerializeField] private CheckerController _checkerPrefab;
        [Tooltip("If left empty, points are built in code (invisible colliders).")]
        [SerializeField] private NodeView _nodePrefab;

        [Header("Player colours")]
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

        [Header("Sandbox match")]
        [Tooltip("Score the human player must reach. 0 = classic rules: the higher score wins.")]
        [SerializeField, Min(0)] private int _targetScore = 0;
        [Tooltip("Turn limit. Classic: turns of both players. With a target: only the human player's turns.")]
        [SerializeField, Min(1)] private int _turnLimit = GameState.DefaultTurnLimit;
        [Tooltip("With a target: turns left to the human once the opponent runs out of checkers. 0 = the match ends.")]
        [SerializeField, Min(0)] private int _finalStretchTurns = 3;
        [Tooltip("Starting checkers of player 0, placed in the StartingLayout fill order.")]
        [SerializeField, Range(1, GameState.MaxCheckersPerPlayer)] private int _player0Checkers = GameState.MaxCheckersPerPlayer;
        [Tooltip("Starting checkers of player 1, placed in the StartingLayout fill order.")]
        [SerializeField, Range(1, GameState.MaxCheckersPerPlayer)] private int _player1Checkers = GameState.MaxCheckersPerPlayer;

        [Header("Determinism")]
        [Tooltip("0 = a new random seed every match (it is logged so the match can be replayed). " +
                 "Any other value makes dice and chance outcomes reproducible.")]
        [SerializeField] private int _randomSeed = 0;

        private GameState _gameState;
        private MatchContext _context;
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

        // Runtime-created resources owned by this component; released in OnDestroy.
        private bool _ownsLayout;
        private Sprite _solidSprite;

        public GameState Board => _gameState;
        public MatchContext Match => _context;

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

            if (_checkerPrefab == null && _checkerSprite == null)
            {
                Debug.LogError("[View] Assign 'checker_scheme' to the Checker Sprite field " +
                               "(or assign a Checker Prefab). BoardManager is disabled.", this);
                enabled = false;
                return;
            }

            BuildCore();
            BuildScene();
            Subscribe();
            BindHud();

            // Create the bot if enabled. With NoBotPlayer both sides are played by hand.
            if (GameState.IsValidPlayer(_botPlayerId))
                _bot = new TavlaBot(_botPlayerId);

            RefreshBoard(true);

            // Roll the first turn's dice, then start the bot if it is its turn.
            _context.RollDice(_gameState.CurrentPlayerId);
            TryStartBotTurn(_gameState.CurrentPlayerId);
            ScheduleAutoEndCheck();
        }

        private void BuildCore()
        {
            MatchRules rules = CreateSandboxRules();
            _gameState = new GameState(rules);

            // Always use an explicit seed and log it, so any reported match can be replayed exactly.
            int seed = _randomSeed != 0 ? _randomSeed : Environment.TickCount;
            GameLog.Info($"[View] Random seed: {seed}", this);

            // The MatchContext constructor binds itself to the board (Bind);
            // without it special checker abilities silently do nothing.
            _context = new MatchContext(_gameState, new SystemRandomProvider(seed));

            _gameState.SetupPlayer(0, CreateDefaultCheckers(0, _player0Checkers));
            _gameState.SetupPlayer(1, CreateDefaultCheckers(1, _player1Checkers));

            GameLog.Info($"[View] Match: {DescribeRules(rules)}, checkers {_player0Checkers} vs {_player1Checkers}.", this);
        }

        /// <summary>
        /// Rules of a match played straight from this scene. The human player is the one chasing the target:
        /// player 0, unless the bot has been put in that seat.
        /// </summary>
        private MatchRules CreateSandboxRules()
        {
            if (_targetScore <= 0)
                return MatchRules.Classic(_turnLimit);

            int scoringPlayer = _botPlayerId == 0 ? 1 : 0;
            return MatchRules.ForTarget(_targetScore, _turnLimit, _finalStretchTurns, scoringPlayer);
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

        private void BuildScene()
        {
            if (_configureCamera)
                ConfigureCamera();

            if (_backgroundSprite != null)
                SpawnScreenBackground();

            if (_boardSprite != null)
                SpawnBoardBackground();

            _solidSprite = CreateSolidSprite();
            SpawnNodes(_solidSprite);
            SpawnBearOffTarget(_solidSprite);
            SpawnCheckers();
        }

        /// <summary>
        /// Connects the info panel to Core.
        ///
        /// The events that carry dice/score/gold to the View already exist; the panel subscribes to them
        /// ITSELF, BoardManager only introduces them. So adding a new HUD field does not change this file.
        ///
        /// Must be called BEFORE the first dice roll (end of Start): otherwise the panel misses the
        /// first turn's dice and stays empty until the turn changes.
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
        /// Screen background. It has the same canvas size (320x180) and PPU as the board sprite, so
        /// placed at the origin it exactly fills what the camera sees. Drawn below the board.
        /// </summary>
        private void SpawnScreenBackground()
        {
            Transform background = CreateWorldRoot("ScreenBackground");

            SpriteRenderer spriteRenderer = background.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _backgroundSprite;
            spriteRenderer.sortingOrder = SortingOrders.ScreenBackground;
        }

        private void SpawnBoardBackground()
        {
            Transform board = CreateWorldRoot("BoardBackground");

            SpriteRenderer spriteRenderer = board.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = _boardSprite;
            spriteRenderer.sortingOrder = SortingOrders.BoardBackground;
        }

        /// <summary>Builds the 24 points from the geometry. No need to place 24 objects by hand.</summary>
        private void SpawnNodes(Sprite highlight)
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

                view.Initialize(i, _layout.GetNodeCenter(i), size, highlight);
                _nodeViews.Add(view);
            }
        }

        /// <summary>
        /// Bear-off drop target. Mechanically an ordinary NodeView — the only difference is that its index is
        /// <see cref="GameState.BearOffIndex"/>. CheckerController therefore sends RequestMove(from, BearOffIndex)
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
            _bearOffView.Initialize(GameState.BearOffIndex, _layout.GetBearOffCenter(), size, solid);
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

        private void SpawnCheckers()
        {
            Transform root = CreateWorldRoot("Checkers");

            for (int i = 0; i < GameState.NodeCount; i++)
            {
                Node node = _gameState.Nodes[i];
                for (int k = 0; k < node.CheckerCount; k++)
                    SpawnCheckerView(node.Checkers[k], root);
            }
        }

        /// <summary>Can also be used when a new special checker arrives during deck building.</summary>
        public CheckerController SpawnCheckerView(BaseChecker model, Transform parent = null)
        {
            CheckerController existing;
            if (_checkerViews.TryGetValue(model, out existing))
                return existing;

            CheckerController view = _checkerPrefab != null
                ? Instantiate(_checkerPrefab, parent)
                : CreateRuntimeChecker(parent);

            view.Bind(this, model, model.OwnerId == 0 ? _player0Color : _player1Color);

            _checkerViews.Add(model, view);
            return view;
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
            _gameState.CheckerTeleported += HandleCheckerTeleported;
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
            if (_gameState != null)
            {
                _gameState.MoveExecuted -= HandleMoveExecuted;
                _gameState.MoveRejected -= HandleMoveRejected;
                _gameState.CheckerCaptured -= HandleCheckerCaptured;
                _gameState.CheckerDestroyed -= HandleCheckerDestroyed;
                _gameState.CheckerTeleported -= HandleCheckerTeleported;
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

            // Runtime-created textures/sprites/assets are not garbage collected by Unity; release them explicitly.
            if (_solidSprite != null)
            {
                Destroy(_solidSprite.texture);
                Destroy(_solidSprite);
                _solidSprite = null;
            }

            if (_ownsLayout && _layout != null)
            {
                Destroy(_layout);
                _layout = null;
            }
        }

        #endregion

        #region View -> Core

        /// <summary>
        /// Called when a CheckerController drops a checker. All we do is forward the question;
        /// the validity decision belongs entirely to Core.
        /// </summary>
        public void RequestMove(int fromIndex, int toIndex)
        {
            _gameState.TryMove(fromIndex, toIndex);
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
        public void ShowLegalTargets(int fromIndex)
        {
            ClearHighlights();

            _gameState.GetLegalTargets(fromIndex, _legalTargets);
            for (int i = 0; i < _legalTargets.Count; i++)
                _nodeViews[_legalTargets[i]].SetHighlight(true);

            // GetLegalTargets only scans points 0-23 (the bot also asks about bearing off separately).
            // The bear-off highlight goes through the same Core validation, CanBearOff.
            bool canBearOff = _gameState.CanBearOff(fromIndex);
            if (_bearOffView != null)
                _bearOffView.SetHighlight(canBearOff);

            if (_legalTargets.Count == 0 && !canBearOff)
                GameLog.Info($"[View] No legal move from point {fromIndex} with these dice.");
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

        private void HandleCheckerTeleported(BaseChecker checker, int fromIndex, int toIndex)
        {
            GameLog.Info($"[View] {checker.TypeId} was caught by the maelstrom: {fromIndex} -> {toIndex}");
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
            ScheduleAutoEndCheck();
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
        }

        private void HandleScoreChanged(int playerId, int score) => GameLog.Info($"[View] Score P{playerId}: {score}");

        private void HandleGoldChanged(int playerId, int gold) => GameLog.Info($"[View] Gold P{playerId}: {gold}");

        #endregion
    }
}
