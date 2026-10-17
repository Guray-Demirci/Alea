using System;
using System.Collections.Generic;
using UnityEngine;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;
using Tavla.View;

namespace Tavla.Sandbox
{
    /// <summary>
    /// GOD MODE — the sandbox scene's test bench for special checkers. A panel docked on the right of the screen
    /// (the toggle key hides it) where you:
    ///   • pick any special checkers for your side, both sides' checker counts, the tiles and the rules, and deal;
    ///   • load the dice — any pair, for this roll or for every roll — and switch the bot off to play both sides;
    ///   • put any tile on any point of the match being played, or take one off, without dealing again;
    ///   • tune the game's hit effects: the screen shake on hits and Powder Keg blasts, and hit checkers breaking apart;
    ///   • read a log of everything Core reported, and see the name of the checker under the pointer.
    ///
    /// It never bends a rule. Matches are built with Core's public setup API (<see cref="SandboxMatchFactory"/>) and
    /// put on the table by <see cref="BoardManager.PlayMatch"/>; the dice are bent by a <see cref="LoadedDice"/>
    /// relic, so every ability still reacts to them. What a checker does here is what it does in a run. The panel
    /// lives in its own assembly and scene; the game never references either.
    ///
    /// It is IMGUI on purpose: a developer tool, kept apart from the pixel-art UI. While it is open the board camera
    /// draws only to the left of it, so a click on the panel can never pick up a checker.
    /// </summary>
    [DefaultExecutionOrder(100)] // After BoardManager.Start, which builds the scene whose match this replaces.
    [DisallowMultipleComponent]
    public sealed class GodModePanel : MonoBehaviour
    {
        private const string Title = "GOD MODE";
        private const string SavedSetupKey = "Tavla.Sandbox.Setup";
        private const float LabelWidth = 92f;
        private const float SmallButtonWidth = 24f;
        private const float LogHeight = 190f;
        private const int GoldStep = 10;
        private const int ScoreStep = 10;

        private static readonly string[] DieFaces = { "1", "2", "3", "4", "5", "6" };
        private static readonly string[] ShakePixels = { "1 px", "2 px", "3 px", "4 px" };

        [Header("Connection")]
        [Tooltip("The scene's BoardManager, with its Run field EMPTY. Empty here = looked up in the scene.")]
        [SerializeField] private BoardManager _board;

        [Header("Setup")]
        [Tooltip("What the panel deals until a setup is dealt from the panel. After that the panel remembers its last " +
                 "deal between Play sessions; its Reset button comes back to this one.")]
        [SerializeField] private SandboxSetup _setup = new SandboxSetup();
        [Tooltip("Seed of the first deal. 0 = a new one every Play. The panel shows the seed of the current deal.")]
        [SerializeField] private int _seed;

        [Header("Panel")]
        [Tooltip("Hides and shows the panel.")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [Tooltip("Width of the panel at the reference height.")]
        [SerializeField, Min(200f)] private float _panelWidth = 360f;
        [Tooltip("Screen height at which the panel is drawn 1:1; it scales with the screen from there.")]
        [SerializeField, Min(100f)] private float _referenceHeight = 900f;
        [Tooltip("Width / height of the board art (320x180). The board camera keeps it next to the panel.")]
        [SerializeField, Min(0.1f)] private float _boardAspect = 16f / 9f;
        [Tooltip("Colour around the board while the panel is open.")]
        [SerializeField] private Color _backdropColor = new Color(0.08f, 0.08f, 0.10f);
        [SerializeField] private Color _panelColor = new Color(0.13f, 0.13f, 0.16f, 0.97f);

        private readonly LoadedDice[] _loadedDice = { new LoadedDice(), new LoadedDice() };
        private readonly bool[] _locked = new bool[GameState.PlayerCount];
        private readonly SandboxLog _log = new SandboxLog();
        private readonly System.Random _random = new System.Random();

        // Sized for every checker of both sides under the pointer plus the point collider, as CheckerController does.
        private readonly Collider2D[] _hoverHits = new Collider2D[32];
        private ContactFilter2D _hoverFilter;

        // The Inspector's setup, for the panel's Reset; the panel's own edits are lost when Play mode ends.
        private string _inspectorSetup;

        private bool _ready;
        private GameState _match;
        private MatchContext _context;
        private int _dealtSeed;
        private bool _setupChanged;
        private string _error;

        // The faces the panel loads the dice with.
        private int _dieFirst = GameState.MaxDieValue;
        private int _dieSecond = GameState.MaxDieValue;

        // The tile the panel puts on the match being played, and the point it goes on.
        private TileKind _liveTile = TileKind.Maelstrom;
        private int _livePoint = GameState.HomeBoardSize;

        // IMGUI draws the same controls in every pass of a frame, so a click that adds or removes controls (a log
        // line, a tile row) is carried out in Update instead, between frames.
        private Action _pendingAction;

        private bool _visible = true;
        private bool _showSetup = true;
        private Vector2 _panelScroll;
        private Vector2 _logScroll;
        private int _logVersionShown = -1;
        private string _hoverText;
        private Vector2 _hoverPosition;

        private CheckerKind[] _checkerKinds;
        private string[] _checkerNames;
        private TileKind[] _tileKinds;
        private string[] _tileIds;
        private readonly string[] _pointLabels = new string[GameState.NodeCount];

        private Camera _camera;
        private Rect _cameraRect;
        private Camera _backdrop;

        private Texture2D _panelTexture;
        private GUIStyle _panelStyle;
        private GUIStyle _tipStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _noteStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _logStyle;
        private GUIStyle _valueStyle;

        #region Lifecycle

        /// <summary>Play mode throws away edits to serialized fields, so the last dealt setup is kept in PlayerPrefs.</summary>
        private void Awake()
        {
            _inspectorSetup = JsonUtility.ToJson(_setup);

            string saved = PlayerPrefs.GetString(SavedSetupKey, string.Empty);
            if (saved.Length == 0)
                return;

            try
            {
                JsonUtility.FromJsonOverwrite(saved, _setup);
            }
            catch (ArgumentException)
            {
                JsonUtility.FromJsonOverwrite(_inspectorSetup, _setup);
            }
        }

        private void Start()
        {
            if (_board == null)
                _board = FindAnyObjectByType<BoardManager>();

            if (_board == null || _board.Board == null)
            {
                Fail("No running BoardManager in the scene: the god-mode panel deals its matches through one.");
                return;
            }

            if (_board.Run != null)
            {
                Fail("BoardManager is playing a run. Clear its Run field: the sandbox deals its own matches.");
                return;
            }

            _checkerKinds = (CheckerKind[])Enum.GetValues(typeof(CheckerKind));
            _checkerNames = new string[_checkerKinds.Length];
            for (int i = 0; i < _checkerKinds.Length; i++)
                _checkerNames[i] = GetShortName(_checkerKinds[i]);

            _tileKinds = (TileKind[])Enum.GetValues(typeof(TileKind));
            _tileIds = new string[_tileKinds.Length];
            for (int i = 0; i < _tileKinds.Length; i++)
                _tileIds[i] = TileCatalog.GetEffectId(_tileKinds[i]);

            _hoverFilter = new ContactFilter2D();
            _hoverFilter.useTriggers = true; // Point colliders are triggers.

            _camera = Camera.main;
            if (_camera != null)
                _cameraRect = _camera.rect;

            _ready = true;
            Deal(_seed != 0 ? _seed : NewSeed());
        }

        private void Update()
        {
            if (_pendingAction == null)
                return;

            Action action = _pendingAction;
            _pendingAction = null;
            action();
        }

        private void LateUpdate()
        {
            ApplyViewport();
        }

        private void OnDisable()
        {
            if (_camera != null)
                _camera.rect = _cameraRect;

            if (_backdrop != null)
                _backdrop.enabled = false;
        }

        private void OnDestroy()
        {
            _log.Detach();

            if (_backdrop != null)
                Destroy(_backdrop.gameObject);

            if (_panelTexture != null)
                Destroy(_panelTexture);
        }

        private void Fail(string message)
        {
            _error = message;
            Debug.LogError($"[Sandbox] {message}", this);
        }

        #endregion

        #region Actions

        /// <summary>
        /// Deals the setup with this seed and puts the match on the table. A setup that cannot be dealt leaves the
        /// current match playing and shows why.
        /// </summary>
        private void Deal(int seed)
        {
            GameState board;
            MatchContext context;
            try
            {
                board = SandboxMatchFactory.Create(_setup, seed, out context);
            }
            catch (ArgumentException e)
            {
                _error = e.Message;
                return;
            }

            // Before PlayMatch makes the first roll: the loaded dice and the log must both see it.
            for (int p = 0; p < GameState.PlayerCount; p++)
                context.RegisterDiceModifier(p, _loadedDice[p]);

            _log.Clear();
            _log.Attach(board, context);
            _log.Add($"dealt with seed {seed}");

            _match = board;
            _context = context;
            _dealtSeed = seed;
            _setupChanged = false;
            _error = null;
            PlayerPrefs.SetString(SavedSetupKey, JsonUtility.ToJson(_setup));

            _board.PlayMatch(board, context);
        }

        /// <summary>Back to the setup in the Inspector; the next deal plays it.</summary>
        private void ResetSetup()
        {
            JsonUtility.FromJsonOverwrite(_inspectorSetup, _setup);
            _setupChanged = true;
        }

        private int NewSeed() => _random.Next(1, int.MaxValue);

        /// <summary>
        /// Rolls the current player's dice again — on the picked faces, or freely — replacing the dice still in hand.
        /// It is a real roll: checkers that react to one (Kraken) react again.
        /// </summary>
        private void Reroll(bool loaded)
        {
            int playerId = _match.CurrentPlayerId;

            if (loaded)
                _loadedDice[playerId].Load(_dieFirst, _dieSecond);
            else
                _loadedDice[playerId].Unload();

            _context.RollDice(playerId);

            // The rolls to come follow the lock again.
            ApplyLock(playerId);
        }

        private void ApplyLock(int playerId)
        {
            if (_locked[playerId])
                _loadedDice[playerId].Load(_dieFirst, _dieSecond);
            else
                _loadedDice[playerId].Unload();
        }

        /// <summary>Tops the picked special checkers up to the player's checker count with random ones not picked yet.</summary>
        private void FillWithRandomCheckers()
        {
            List<CheckerKind> specials = _setup.PlayerSpecials;

            List<CheckerKind> pool = new List<CheckerKind>(_checkerKinds.Length);
            for (int i = 0; i < _checkerKinds.Length; i++)
            {
                if (!specials.Contains(_checkerKinds[i]))
                    pool.Add(_checkerKinds[i]);
            }

            while (specials.Count < _setup.PlayerCheckers && pool.Count > 0)
            {
                int pick = _random.Next(pool.Count);
                specials.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            _setupChanged = true;
        }

        private void AddTile()
        {
            SandboxSetup.TileEntry tile = new SandboxSetup.TileEntry();
            tile.Tile = _tileKinds[0];
            tile.Point = GetFreeTilePoint();

            _setup.Tiles.Add(tile);
            _setupChanged = true;
        }

        private void RemoveTile(int index)
        {
            _setup.Tiles.RemoveAt(index);
            _setupChanged = true;
        }

        /// <summary>The first point from the middle of the board on that holds no tile yet.</summary>
        private int GetFreeTilePoint()
        {
            const int start = GameState.HomeBoardSize;
            for (int i = 0; i < GameState.NodeCount; i++)
            {
                int point = (start + i) % GameState.NodeCount;
                if (!_setup.Tiles.Exists(tile => tile.Point == point))
                    return point;
            }

            return start;
        }

        /// <summary>
        /// Puts the picked tile on the picked point of the match being played, replacing the tile there. It acts on the
        /// checkers that land there from now on; the next deal goes back to the setup's tiles.
        /// </summary>
        private void PlaceLiveTile()
        {
            _match.PlaceTile(_livePoint, TileCatalog.GetFactory(_liveTile)());
            _log.Add($"{_liveTile} put on point {_livePoint}");
            _board.RefreshBoard();
        }

        private void RemoveLiveTile()
        {
            ITileEffect tile = _match.Nodes[_livePoint].ActiveTile;
            if (tile == null)
                return;

            _match.RemoveTile(_livePoint);
            _log.Add($"{DescribeTile(tile)} taken off point {_livePoint}");
            _board.RefreshBoard();
        }

        #endregion

        #region Board camera

        /// <summary>
        /// While the panel is open the board camera draws only to the left of it, at the board's own aspect so nothing
        /// is cropped, and clicks on the panel miss every checker. A backdrop camera clears the space around it.
        /// </summary>
        private void ApplyViewport()
        {
            if (_camera == null)
                return;

            bool docked = _ready && _visible;
            if (!docked)
            {
                _camera.rect = _cameraRect;
                if (_backdrop != null)
                    _backdrop.enabled = false;

                return;
            }

            if (_backdrop == null)
                _backdrop = CreateBackdrop();

            _backdrop.enabled = true;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float freeWidth = Mathf.Max(1f, screenWidth - _panelWidth * GetScale());
            float width = Mathf.Min(freeWidth, screenHeight * _boardAspect);
            float height = width / _boardAspect;

            _camera.rect = new Rect((freeWidth - width) * 0.5f / screenWidth, (screenHeight - height) * 0.5f / screenHeight,
                                    width / screenWidth, height / screenHeight);
        }

        private Camera CreateBackdrop()
        {
            Camera backdrop = new GameObject("SandboxBackdrop").AddComponent<Camera>();
            backdrop.depth = _camera.depth - 1f;
            backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor;
            backdrop.backgroundColor = _backdropColor;
            backdrop.orthographic = true;
            return backdrop;
        }

        private float GetScale() => Mathf.Clamp(Screen.height / _referenceHeight, 0.5f, 4f);

        #endregion

        #region Pointer info

        /// <summary>Which checker — or which point — is under the pointer. The art alone does not always tell.</summary>
        /// <param name="guiPosition">Pointer position in unscaled GUI pixels (y down).</param>
        private void UpdateHover(Vector2 guiPosition)
        {
            _hoverText = null;
            _hoverPosition = guiPosition;

            if (_match == null || _camera == null)
                return;

            Vector3 screen = new Vector3(guiPosition.x, Screen.height - guiPosition.y, -_camera.transform.position.z);
            if (!_camera.pixelRect.Contains(screen))
                return;

            Vector2 world = _camera.ScreenToWorldPoint(screen);
            int hitCount = Physics2D.OverlapPoint(world, _hoverFilter, _hoverHits);

            CheckerController topChecker = null;
            int topOrder = int.MinValue;
            NodeView point = null;

            for (int i = 0; i < hitCount; i++)
            {
                CheckerController checker = _hoverHits[i].GetComponent<CheckerController>();
                if (checker != null && checker.Model != null)
                {
                    // Stacked checkers overlap: the pointer is on the one drawn on top.
                    int order = checker.GetComponent<SpriteRenderer>().sortingOrder;
                    if (order > topOrder)
                    {
                        topOrder = order;
                        topChecker = checker;
                    }

                    continue;
                }

                NodeView node = _hoverHits[i].GetComponent<NodeView>();
                if (node != null && GameState.IsOnBoard(node.NodeIndex))
                    point = node;
            }

            if (topChecker != null)
                _hoverText = DescribeChecker(topChecker.Model);
            else if (point != null)
                _hoverText = DescribePoint(point.NodeIndex);
        }

        private string DescribeChecker(BaseChecker checker)
        {
            int index = _match.FindNodeIndexOf(checker);
            string where = index == GameState.BarIndex ? "on the bar" : $"on point {index}";
            string text = $"{SandboxLog.NameOf(checker)}  (P{checker.OwnerId}, {where})";

            if (checker.AbilitiesSuppressed)
                text += "\nabilities off (fog)";
            if (checker.IsReentering)
                text += "\nabilities off (until it enters)";

            return text;
        }

        private string DescribePoint(int index)
        {
            Node node = _match.Nodes[index];
            string text = $"Point {index}";

            if (node.ActiveTile != null)
                text += $"  {DescribeTile(node.ActiveTile)}";

            ICountdownTile countdown = node.ActiveTile as ICountdownTile;
            if (countdown != null)
                text += $" (goes off in {countdown.Countdown})";

            if (node.GetMark(SandboxMatchFactory.PlayerId) != null)
                text += "  (marked)";

            return text;
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            GUI.matrix = Matrix4x4.identity;
            Event current = Event.current;

            if (current.type == EventType.KeyDown && current.keyCode == _toggleKey)
            {
                _visible = !_visible;
                current.Use();
            }

            if (current.type == EventType.Repaint)
                UpdateHover(current.mousePosition);

            EnsureStyles();

            float scale = GetScale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (_visible)
            {
                Rect area = new Rect(width - _panelWidth, 0f, _panelWidth, height);
                GUI.Box(area, GUIContent.none, _panelStyle);

                GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 6f, area.width - 16f, area.height - 12f));
                DrawPanel();
                GUILayout.EndArea();
            }
            else if (GUI.Button(new Rect(width - 130f, 6f, 124f, 24f), $"{Title} ({_toggleKey})"))
            {
                _visible = true;
            }

            DrawHover(scale, width, height);
        }

        private void DrawPanel()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Title, _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Hide ({_toggleKey})", GUILayout.Width(90f)))
                _visible = false;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_error))
                GUILayout.Label(_error, _errorStyle);

            if (!_ready)
                return;

            _panelScroll = GUILayout.BeginScrollView(_panelScroll);

            // No match yet when the first deal failed (the error says why); the setup can still be fixed.
            if (_match != null)
            {
                DrawMatch();
                DrawLiveTiles();
                DrawImpacts();
                DrawLog();
            }

            DrawSetup();
            GUILayout.EndScrollView();
        }

        private void DrawMatch()
        {
            Section("Match");

            int current = _match.CurrentPlayerId;
            bool humanTurn = !_board.BotEnabled || current == SandboxMatchFactory.PlayerId;

            string status;
            if (!_match.IsMatchOver)
                status = $"P{current}'s turn{(humanTurn ? " (you)" : " (bot)")}, {_match.RemainingTurns} turns left";
            else if (_match.WinnerId == GameState.NoWinner)
                status = "Match over: draw";
            else
                status = $"Match over: P{_match.WinnerId} wins";

            GUILayout.Label(status);
            GUILayout.Label($"Dice {DescribeDice(_context.CurrentRoll)}");

            string target = _match.Rules.HasTargetScore ? $" (target {_match.Rules.TargetScore})" : string.Empty;
            GUILayout.Label($"Score {_context.GetScore(0)} : {_context.GetScore(1)}{target}    " +
                            $"Gold {_context.GetGold(0)} : {_context.GetGold(1)}");

            int first = DrawFaceRow("Die 1", _dieFirst);
            int second = DrawFaceRow("Die 2", _dieSecond);
            if (first != _dieFirst || second != _dieSecond)
            {
                _dieFirst = first;
                _dieSecond = second;
                for (int p = 0; p < GameState.PlayerCount; p++)
                    ApplyLock(p);
            }

            GUI.enabled = !_match.IsMatchOver;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Roll {_dieFirst}-{_dieSecond} now"))
                Defer(() => Reroll(true));
            if (GUILayout.Button("Roll random"))
                Defer(() => Reroll(false));
            GUILayout.EndHorizontal();

            for (int p = 0; p < GameState.PlayerCount; p++)
            {
                bool locked = GUILayout.Toggle(_locked[p], $" Every P{p} roll is {_dieFirst}-{_dieSecond}");
                if (locked != _locked[p])
                {
                    _locked[p] = locked;
                    ApplyLock(p);
                }
            }

            bool bot = GUILayout.Toggle(_board.BotEnabled, " Bot plays P1 (off: you play both sides)");
            if (bot != _board.BotEnabled)
                Defer(() => _board.BotEnabled = bot);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"+{GoldStep} gold"))
                Defer(() => _context.AddGold(SandboxMatchFactory.PlayerId, GoldStep));
            if (GUILayout.Button($"+{ScoreStep} score"))
                Defer(() => _context.AddScore(SandboxMatchFactory.PlayerId, ScoreStep));
            if (GUILayout.Button("End turn"))
                Defer(_board.EndTurn);
            GUILayout.EndHorizontal();

            GUI.enabled = true;
        }

        /// <summary>
        /// The tiles of the match being played: pick a tile and a point, and put it there or take the point's tile off.
        /// The point grid is laid out as the board is, and marks (*) the points that hold a tile.
        /// </summary>
        private void DrawLiveTiles()
        {
            Section("Tiles in play");

            GUI.enabled = !_match.IsMatchOver;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(SmallButtonWidth)))
                _liveTile = StepTile(_liveTile, -1);
            GUILayout.Label(_liveTile.ToString(), _valueStyle, GUILayout.Width(88f));
            if (GUILayout.Button(">", GUILayout.Width(SmallButtonWidth)))
                _liveTile = StepTile(_liveTile, 1);
            if (GUILayout.Button($"Put on {_livePoint}"))
                Defer(PlaceLiveTile);
            if (GUILayout.Button("Take off"))
                Defer(RemoveLiveTile);
            GUILayout.EndHorizontal();

            for (int cell = 0; cell < GameState.NodeCount; cell++)
            {
                int point = CellToPoint(cell);
                _pointLabels[cell] = _match.Nodes[point].ActiveTile != null ? point + "*" : point.ToString();
            }

            // CellToPoint is its own inverse, so it also gives the cell of a point.
            int picked = GUILayout.SelectionGrid(CellToPoint(_livePoint), _pointLabels, GameState.NodeCount / 2);
            _livePoint = CellToPoint(picked);

            GUILayout.Label($"Point {_livePoint}: {DescribeTile(_match.Nodes[_livePoint].ActiveTile)}", _noteStyle);

            GUI.enabled = true;
        }

        /// <summary>
        /// The game's hit effects (<see cref="BoardManager.HitEffects"/>): screen shake on or off, and for a hit and a
        /// keg's blast how far and how long it shakes, with a button to feel each; checker breaks on or off. Edits last
        /// until Play mode ends; the values worth keeping go into BoardManager's Hit effects fields in both scenes.
        /// </summary>
        private void DrawImpacts()
        {
            HitEffects effects = _board.HitEffects;
            if (effects == null)
                return;

            Section("Hit effects");

            effects.ShakeEnabled = GUILayout.Toggle(effects.ShakeEnabled, " Shake on hits and keg blasts");
            DrawShakeRows("Hit", effects.HitShake, effects);
            DrawShakeRows("Keg blast", effects.BlastShake, effects);

            effects.BreaksEnabled = GUILayout.Toggle(effects.BreaksEnabled, " A hit opponent checker breaks apart");
        }

        private void DrawShakeRows(string label, ShakeStyle style, HitEffects effects)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth * 0.75f));
            int pixels = Mathf.Clamp(style.Pixels, 1, ShakeStyle.MaxPixels);
            style.Pixels = GUILayout.SelectionGrid(pixels - 1, ShakePixels, ShakePixels.Length) + 1;
            if (GUILayout.Button("Try", GUILayout.Width(40f)))
                effects.TryShake(style);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(LabelWidth * 0.75f + 4f);
            float duration = GUILayout.HorizontalSlider(style.Duration, 0.05f, 0.6f);
            style.Duration = Mathf.Round(duration * 100f) / 100f;
            GUILayout.Label($"{style.Duration:0.00} s", GUILayout.Width(48f));
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// The point in this cell of a 12 x 2 grid laid out as the board is (<see cref="BoardLayout"/>): the top row
        /// holds points 0-11 from the left, the bottom row 23-12. The mapping is its own inverse.
        /// </summary>
        private static int CellToPoint(int cell)
        {
            const int perRow = GameState.NodeCount / 2;
            return cell < perRow ? cell : GameState.NodeCount - 1 + perRow - cell;
        }

        private string DescribeTile(ITileEffect tile)
        {
            if (tile == null)
                return "no tile";

            int index = Array.IndexOf(_tileIds, tile.EffectId);
            return index >= 0 ? _tileKinds[index].ToString() : tile.EffectId;
        }

        private static string DescribeDice(DiceRoll roll)
        {
            if (roll == null)
                return "-";

            string left;
            if (roll.FreeSplit)
                left = $"{roll.FreeTotal} to split";
            else if (roll.RemainingMoves.Count == 0)
                left = "none";
            else
                left = string.Join(" ", roll.RemainingMoves);

            return $"{roll.First}-{roll.Second}, left: {left}";
        }

        private int DrawFaceRow(string label, int face)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth * 0.6f));
            int picked = GUILayout.SelectionGrid(face - 1, DieFaces, DieFaces.Length) + 1;
            GUILayout.EndHorizontal();
            return picked;
        }

        private void DrawLog()
        {
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Log", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                Defer(_log.Clear);
            GUILayout.EndHorizontal();

            // Follow the newest line; decided in the Layout pass so every pass of the frame agrees.
            if (Event.current.type == EventType.Layout && _log.Version != _logVersionShown)
            {
                _logVersionShown = _log.Version;
                _logScroll.y = float.MaxValue;
            }

            _logScroll = GUILayout.BeginScrollView(_logScroll, GUILayout.Height(LogHeight));
            IReadOnlyList<string> lines = _log.Lines;
            for (int i = 0; i < lines.Count; i++)
                GUILayout.Label(lines[i], _logStyle);
            GUILayout.EndScrollView();
        }

        private void DrawSetup()
        {
            Section("Setup");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Deal again (same seed)"))
                Defer(() => Deal(_dealtSeed));
            if (GUILayout.Button("New deal"))
                Defer(() => Deal(NewSeed()));
            GUILayout.EndHorizontal();

            GUILayout.Label(_setupChanged
                ? "<color=#ffd75e>Setup changed: deal to apply it.</color>"
                : $"Seed {_dealtSeed}", _noteStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showSetup ? "Hide setup" : "Show setup"))
                Defer(() => _showSetup = !_showSetup);
            if (GUILayout.Button("Reset setup"))
                Defer(ResetSetup);
            GUILayout.EndHorizontal();

            if (!_showSetup)
                return;

            DrawCheckers();
            DrawCounts();
            DrawTiles();
            DrawRules();
        }

        private void DrawCheckers()
        {
            List<CheckerKind> specials = _setup.PlayerSpecials;
            Section($"Your special checkers  {specials.Count}/{_setup.PlayerCheckers}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("None"))
            {
                specials.Clear();
                _setupChanged = true;
            }
            if (GUILayout.Button("Fill with random"))
                FillWithRandomCheckers();
            GUILayout.EndHorizontal();

            float columnWidth = (_panelWidth - 40f) * 0.5f;
            for (int i = 0; i < _checkerKinds.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                DrawCheckerToggle(i, columnWidth);
                if (i + 1 < _checkerKinds.Length)
                    DrawCheckerToggle(i + 1, columnWidth);
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>One of each, and no more than a side can field; picking past the checker count raises it.</summary>
        private void DrawCheckerToggle(int index, float width)
        {
            List<CheckerKind> specials = _setup.PlayerSpecials;
            CheckerKind kind = _checkerKinds[index];
            bool picked = specials.Contains(kind);

            GUI.enabled = picked || specials.Count < GameState.MaxCheckersPerPlayer;
            bool wanted = GUILayout.Toggle(picked, _checkerNames[index], GUILayout.Width(width));
            GUI.enabled = true;

            if (wanted == picked)
                return;

            if (wanted)
            {
                specials.Add(kind);
                _setup.PlayerCheckers = Mathf.Max(_setup.PlayerCheckers, specials.Count);
            }
            else
            {
                specials.Remove(kind);
            }

            _setupChanged = true;
        }

        private void DrawCounts()
        {
            Section("Checkers");

            int fewest = Mathf.Max(GameState.MinCheckersPerPlayer, _setup.PlayerSpecials.Count);
            _setup.PlayerCheckers = DrawStepper("Yours", _setup.PlayerCheckers, fewest, GameState.MaxCheckersPerPlayer, 1);
            _setup.OpponentCheckers = DrawStepper("Opponent", _setup.OpponentCheckers,
                                                  GameState.MinCheckersPerPlayer, GameState.MaxCheckersPerPlayer, 1);
        }

        private void DrawTiles()
        {
            Section("Tiles");

            for (int i = 0; i < _setup.Tiles.Count; i++)
            {
                SandboxSetup.TileEntry tile = _setup.Tiles[i];

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("<", GUILayout.Width(SmallButtonWidth)))
                    CycleTile(tile, -1);
                GUILayout.Label(tile.Tile.ToString(), _valueStyle, GUILayout.Width(88f));
                if (GUILayout.Button(">", GUILayout.Width(SmallButtonWidth)))
                    CycleTile(tile, 1);

                GUILayout.Label("point", GUILayout.Width(38f));
                int point = tile.Point;
                if (GUILayout.Button("-", GUILayout.Width(SmallButtonWidth)))
                    point--;
                GUILayout.Label(tile.Point.ToString(), _valueStyle, GUILayout.Width(26f));
                if (GUILayout.Button("+", GUILayout.Width(SmallButtonWidth)))
                    point++;

                point = Mathf.Clamp(point, 0, GameState.NodeCount - 1);
                if (point != tile.Point)
                {
                    tile.Point = point;
                    _setupChanged = true;
                }

                int index = i;
                if (GUILayout.Button("x", GUILayout.Width(SmallButtonWidth)))
                    Defer(() => RemoveTile(index));
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add tile"))
                Defer(AddTile);
        }

        private void CycleTile(SandboxSetup.TileEntry tile, int step)
        {
            tile.Tile = StepTile(tile.Tile, step);
            _setupChanged = true;
        }

        private TileKind StepTile(TileKind tile, int step)
        {
            int index = Array.IndexOf(_tileKinds, tile);
            return _tileKinds[(index + step + _tileKinds.Length) % _tileKinds.Length];
        }

        private void DrawRules()
        {
            Section("Rules");

            _setup.TurnLimit = DrawStepper("Turn limit", _setup.TurnLimit, 1, 999, 5);
            _setup.TargetScore = DrawStepper("Target score", _setup.TargetScore, 0, 9990, 50);
            GUILayout.Label("Target 0 = classic: both sides' turns count, the higher score wins.", _noteStyle);
            _setup.StartingGold = DrawStepper("Starting gold", _setup.StartingGold, 0, 999, 5);
        }

        /// <summary>A number with - and + buttons. A change only takes effect with the next deal.</summary>
        private int DrawStepper(string label, int value, int min, int max, int step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(LabelWidth));

            int next = value;
            if (GUILayout.Button("-", GUILayout.Width(SmallButtonWidth)))
                next -= step;
            GUILayout.Label(value.ToString(), _valueStyle, GUILayout.Width(44f));
            if (GUILayout.Button("+", GUILayout.Width(SmallButtonWidth)))
                next += step;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            next = Mathf.Clamp(next, min, max);
            if (next != value)
                _setupChanged = true;

            return next;
        }

        private void Section(string title)
        {
            GUILayout.Space(4f);
            GUILayout.Label(title, _headerStyle);
        }

        private void DrawHover(float scale, float width, float height)
        {
            if (string.IsNullOrEmpty(_hoverText))
                return;

            GUIContent content = new GUIContent(_hoverText);
            Vector2 size = _tipStyle.CalcSize(content);

            Vector2 at = _hoverPosition / scale + new Vector2(14f, 12f);
            at.x = Mathf.Min(at.x, width - size.x);
            at.y = Mathf.Min(at.y, height - size.y);

            GUI.Label(new Rect(at, size), content, _tipStyle);
        }

        private void Defer(Action action)
        {
            _pendingAction = action;
        }

        /// <summary>Styles can only be built inside OnGUI, where GUI.skin exists.</summary>
        private void EnsureStyles()
        {
            if (_panelStyle != null)
                return;

            _panelTexture = new Texture2D(1, 1);
            _panelTexture.SetPixel(0, 0, _panelColor);
            _panelTexture.Apply();

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTexture;

            _tipStyle = new GUIStyle(GUI.skin.box);
            _tipStyle.normal.background = _panelTexture;
            _tipStyle.normal.textColor = Color.white;
            _tipStyle.alignment = TextAnchor.MiddleLeft;
            _tipStyle.padding = new RectOffset(6, 6, 4, 4);

            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.fontStyle = FontStyle.Bold;

            _noteStyle = new GUIStyle(GUI.skin.label);
            _noteStyle.fontStyle = FontStyle.Italic;
            _noteStyle.wordWrap = true;
            _noteStyle.richText = true;

            _errorStyle = new GUIStyle(GUI.skin.label);
            _errorStyle.wordWrap = true;
            _errorStyle.normal.textColor = new Color(1f, 0.45f, 0.4f);

            _logStyle = new GUIStyle(GUI.skin.label);
            _logStyle.wordWrap = true;
            _logStyle.margin = new RectOffset(2, 2, 0, 0);
            _logStyle.padding = new RectOffset(0, 0, 1, 1);

            _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static string GetShortName(CheckerKind kind)
        {
            const string suffix = " Checker";

            IPurchasable item = CheckerCatalog.GetFactory(kind)(SandboxMatchFactory.PlayerId) as IPurchasable;
            if (item == null)
                return kind.ToString();

            string name = item.DisplayName;
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
        }

        #endregion
    }
}
