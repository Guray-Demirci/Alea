using System;
using System.Collections.Generic;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Runtime;

namespace Tavla.Core.Board
{
    /// <summary>
    /// The SINGLE source of truth of the game. Pure C#.
    ///
    /// DESIGN PROMISE: not a single concrete checker or tile name appears in this class.
    /// Whatever checkers and tiles the game has, GameState knows none of them; it only asks
    /// "does this checker have this ability?" (GetAbility) and "what does this tile say?" (ITileEffect).
    /// Adding a checker that uses an existing ability does NOT change this class (Open/Closed); only a
    /// new KIND of rule (a new ability interface) adds a question here.
    ///
    /// The same promise holds for game modes: how a match is won and how long it lasts comes from
    /// <see cref="MatchRules"/>, and where checkers start comes from <see cref="StartingLayout"/>.
    ///
    /// The class is split into partial files by responsibility:
    ///   GameState.cs              -> constants, state, events, setup, cloning, queries
    ///   GameState.Moves.cs        -> public move API and single-move resolution
    ///   GameState.CompoundMoves.cs-> combining dice into a multi-step move
    ///   GameState.BearOff.cs      -> bearing off
    ///   GameState.Swaps.cs        -> swapping places with a friendly checker
    ///   GameState.Releases.cs     -> freeing a locked checker by sacrificing a die
    ///   GameState.Sacrifices.cs   -> sacrificing a checker for its ability
    ///   GameState.Captures.cs     -> captures, forced relocation, hook dispatch
    ///   GameState.Volleys.cs      -> a volley waiting for the player to pick its targets
    ///   GameState.Turns.cs        -> dice bridge, turn flow, rounds, match end
    /// </summary>
    public partial class GameState
    {
        public const int NodeCount = 24;
        public const int PlayerCount = 2;
        public const int MaxDieValue = 6;
        public const int BarIndex = -1;

        /// <summary>
        /// Fewest checkers a player can start with — the smallest count <see cref="StartingPatterns"/> has a
        /// formation for. What happens when a side later runs out is decided by <see cref="MatchRules"/>.
        /// </summary>
        public const int MinCheckersPerPlayer = 5;

        /// <summary>Most checkers a player can start with — the largest count <see cref="StartingPatterns"/> has a formation for.</summary>
        public const int MaxCheckersPerPlayer = 15;

        /// <summary>
        /// Number of checkers allowed on one point: 5, whoever they belong to. A Parasite that infiltrates an enemy
        /// point counts towards the limit like the enemies it shares the point with.
        /// </summary>
        public const int StackLimit = 5;

        #region Arcade rules

        /// <summary>
        /// Bear-off target. Not a point on the board but the area outside it.
        /// -2 was chosen so it never collides with <see cref="BarIndex"/> (-1) or the 0-23 range.
        /// </summary>
        public const int BearOffIndex = -2;

        /// <summary>
        /// Returned by <see cref="FindNodeIndexOf"/> when the checker is neither on the board nor on the bar
        /// (borne off or destroyed). Distinct from every other sentinel.
        /// </summary>
        public const int InvalidIndex = -3;

        /// <summary>Width of the home board (the last 6 points).</summary>
        public const int HomeBoardSize = 6;

        /// <summary>
        /// The design document counts score in small whole numbers (a capture is 1). The game keeps every score
        /// this many times larger, so a design value is written as <c>n * ScoreScale</c> wherever it appears — the
        /// run assets' target scores use the same scale.
        /// </summary>
        public const int ScoreScale = 10;

        /// <summary>RAW score for capturing an opponent checker, before the capturer's own modifier (Catalyst).</summary>
        public const int CaptureScore = 1 * ScoreScale;

        /// <summary>RAW score for bearing off a checker.</summary>
        public const int BearOffScore = 3 * ScoreScale;

        /// <summary>Default match length in Classic mode — total number of turns of both players.</summary>
        public const int DefaultTurnLimit = 30;

        /// <summary>No winner could be determined (draw, or no score system is bound).</summary>
        public const int NoWinner = -1;

        #endregion

        private readonly Node[] _nodes = new Node[NodeCount];
        private readonly List<BaseChecker>[] _bar = new List<BaseChecker>[PlayerCount];
        private readonly bool[] _skipNextTurn = new bool[PlayerCount];
        private readonly int[] _borneOff = new int[PlayerCount];

        // Latched when a player's LAST checker leaves play (borne off or destroyed); never computed on the fly.
        // A side that never had checkers — a hand-built test position — is therefore not treated as finished.
        private readonly bool[] _outOfCheckers = new bool[PlayerCount];

        // Who has taken a turn in the current round. A round ends when the turn returns to one of them
        // (see AdvanceTurn); round-based tiles such as the Dungeon run on that boundary.
        private readonly bool[] _playedThisRound = new bool[PlayerCount];

        // Set while a bear-off resolves: a target its score reaches only ends the match once the borne-off checker
        // has reacted (see ResolveBearOff). _matchEndHeld remembers that the target was reached meanwhile.
        private bool _holdMatchEnd;
        private bool _matchEndHeld;

        private readonly MatchRules _rules;

        public IReadOnlyList<Node> Nodes => _nodes;
        public int CurrentPlayerId { get; private set; }

        /// <summary>How this match is won and how long it lasts.</summary>
        public MatchRules Rules => _rules;

        /// <summary>
        /// Remaining turns. Decreases at the end of every turn the rules count
        /// (see <see cref="MatchRules.CountsTurnOf"/>).
        /// </summary>
        public int RemainingTurns { get; private set; }

        /// <summary>
        /// Has the opponent of the scoring player run out of checkers? From then on only the scoring player
        /// moves, and the remaining turns are capped at <see cref="MatchRules.FinalStretchTurns"/>.
        /// </summary>
        public bool IsInFinalStretch { get; private set; }

        /// <summary>Is the match over? Once over, <see cref="TryMove"/> accepts no moves.</summary>
        public bool IsMatchOver { get; private set; }

        /// <summary>Winner once the match is over; <see cref="NoWinner"/> on a draw.</summary>
        public int WinnerId { get; private set; } = NoWinner;

        /// <summary>
        /// The gateway through which abilities touch the game. Set up in two phases (Bind)
        /// because MatchContext needs the board and the board needs the context.
        /// </summary>
        public IMatchContext Context { get; private set; }

        /// <summary>
        /// Cloned boards SHARE checker and tile OBJECTS with the real board. While this flag is set
        /// side-effect hooks never run — otherwise the bot's look-ahead would corrupt REAL checker
        /// state such as the Phoenix's rebirth count or shield charges.
        /// The rules themselves (direction, distance, dice, blockades, captures, bear-off) still apply.
        /// </summary>
        public bool IsSimulation { get; private set; }

        /// <summary>
        /// Should side-effect hooks (ability reactions, tile effects, capture notifications) run?
        /// Not when no context is bound or when this is a simulation.
        /// </summary>
        private bool SideEffectsEnabled => Context != null && !IsSimulation;

        #region Events (Core -> View)

        /// <summary>A checker was captured. (captured checker, point index, destination)</summary>
        public event Action<BaseChecker, int, CaptureDestination> CheckerCaptured;

        public event Action<MoveResult> MoveExecuted;
        public event Action<MoveResult> MoveRejected;

        /// <summary>A checker was removed from the game entirely (Sacrificial Checker, Powder Keg, bear-off). (checker, point index)</summary>
        public event Action<BaseChecker, int> CheckerDestroyed;

        /// <summary>
        /// A NEW checker entered the match mid-way — a captured checker changed sides (Mutiny). The View spawns
        /// its visual. (checker, point index)
        /// </summary>
        public event Action<BaseChecker, int> CheckerAdded;

        /// <summary>
        /// A checker was sacrificed for its ability (Second Chance). It has left play — raised after
        /// <see cref="CheckerDestroyed"/> — and the dice may have changed. (checker, point index)
        /// </summary>
        public event Action<BaseChecker, int> CheckerSacrificed;

        /// <summary>
        /// A checker was lost FOR GOOD, not just for this match: in a run it also leaves the player's deck.
        /// Raised for a checker sacrificed for its ability (Second Chance), for one a tile destroys outright
        /// (Powder Keg) and for one destroyed when it is captured (Sacrificial), after <see cref="CheckerDestroyed"/>
        /// and before the match may end. (checker, point index)
        /// </summary>
        public event Action<BaseChecker, int> CheckerLostForGood;

        /// <summary>
        /// A checker was forcibly moved to another point (Maelstrom). (checker, old point, new point)
        /// One thrown onto a point it may not enter is broken there straight after (<see cref="Abilities.AbilityIds.TeleportCrash"/>).
        /// </summary>
        public event Action<BaseChecker, int, int> CheckerTeleported;

        /// <summary>
        /// A locked checker was freed by sacrificing a die (Swamp). It did not move, but a die was spent.
        /// (checker, point index)
        /// </summary>
        public event Action<BaseChecker, int> CheckerReleased;

        /// <summary>A special tile was triggered. (tile, point index, affected checker)</summary>
        public event Action<ITileEffect, int, BaseChecker> TileTriggered;

        /// <summary>
        /// Generic ability notification for View VFX. (ability id from <see cref="Abilities.AbilityIds"/>,
        /// checker, point index). Removes the need for a dedicated event per checker.
        /// </summary>
        public event Action<string, BaseChecker, int> AbilityTriggered;

        /// <summary>The turn passed to another player. (new current player)</summary>
        public event Action<int> TurnChanged;

        /// <summary>
        /// A checker was borne off. (checker, source point)
        /// VISUAL cleanup is driven by <see cref="CheckerDestroyed"/>; this event carries the GAME meaning —
        /// score bursts, counters and sounds hook into it.
        /// </summary>
        public event Action<BaseChecker, int> CheckerBorneOff;

        /// <summary>The number of remaining turns changed. (remaining turns)</summary>
        public event Action<int> TurnCountChanged;

        /// <summary>
        /// The opponent ran out of checkers and the final stretch began. (remaining turns)
        /// Raised after <see cref="TurnCountChanged"/> if the cap lowered the counter.
        /// </summary>
        public event Action<int> FinalStretchStarted;

        /// <summary>The match ended. (winner; <see cref="NoWinner"/> on a draw)</summary>
        public event Action<int> MatchEnded;

        #endregion

        /// <summary>A Classic match (higher score wins) with the given total number of turns.</summary>
        /// <param name="turnLimit">
        /// Total number of turns in the match. A parameter rather than a constant so designers can tune it.
        /// </param>
        public GameState(int turnLimit = DefaultTurnLimit) : this(MatchRules.Classic(turnLimit))
        {
        }

        public GameState(MatchRules rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));

            for (int i = 0; i < NodeCount; i++)
                _nodes[i] = new Node(i);

            for (int p = 0; p < PlayerCount; p++)
                _bar[p] = new List<BaseChecker>();

            CurrentPlayerId = 0;
            RemainingTurns = rules.TurnLimit;
        }

        /// <summary>MatchContext introduces itself to the board. Called exactly once during setup.</summary>
        internal void Bind(IMatchContext context)
        {
            Context = context;
        }

        /// <summary>Abilities and tiles use this to notify the View.</summary>
        /// <param name="abilityId">One of the <see cref="Abilities.AbilityIds"/> constants.</param>
        internal void ReportAbility(string abilityId, BaseChecker checker, int nodeIndex)
        {
            AbilityTriggered?.Invoke(abilityId, checker, nodeIndex);
        }

        #region Setup

        /// <summary>
        /// Places a player's starting checkers on the dealt <paramref name="layout"/>: <c>checkers[k]</c> goes to
        /// the layout's slot k. Set both players up from the SAME layout — it is dealt for both sides together.
        /// Everything is validated before the first checker is placed, so a rejected call leaves the board untouched.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Invalid player.</exception>
        /// <exception cref="ArgumentException">
        /// The count differs from what the layout deals the player, a null entry, or a checker owned by the other player.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The player already has checkers in play, or a layout point holds the other player's checkers.
        /// </exception>
        public void SetupPlayer(int playerId, IReadOnlyList<BaseChecker> checkers, StartingLayout layout)
        {
            ThrowIfInvalidPlayer(playerId);

            if (checkers == null)
                throw new ArgumentNullException(nameof(checkers));

            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            if (checkers.Count != layout.GetCheckerCount(playerId))
                throw new ArgumentException(
                    $"The layout deals {layout.GetCheckerCount(playerId)} checkers to player {playerId}, " +
                    $"but {checkers.Count} were given.", nameof(checkers));

            for (int k = 0; k < checkers.Count; k++)
            {
                if (checkers[k] == null)
                    throw new ArgumentException($"Checker at slot {k} is null.", nameof(checkers));

                if (checkers[k].OwnerId != playerId)
                    throw new ArgumentException(
                        $"Checker at slot {k} belongs to player {checkers[k].OwnerId}, not {playerId}.", nameof(checkers));
            }

            if (HasCheckersInPlay(playerId))
                throw new InvalidOperationException($"Player {playerId} is already set up.");

            for (int k = 0; k < checkers.Count; k++)
            {
                int point = layout.GetPoint(playerId, k);
                if (_nodes[point].CountEnemiesOf(playerId) > 0)
                    throw new InvalidOperationException(
                        $"Point {point} already holds player {GetOpponent(playerId)}'s checkers; " +
                        "set both players up from the same layout.");
            }

            for (int k = 0; k < checkers.Count; k++)
                _nodes[layout.GetPoint(playerId, k)].Push(checkers[k]);
        }

        /// <summary>
        /// Puts a tile on a point, replacing the one there (<see cref="RemoveTile"/>). Checkers already standing on the
        /// point are not caught by it: a tile acts on the checkers that land on it.
        /// </summary>
        public void PlaceTile(int nodeIndex, ITileEffect effect)
        {
            if (!IsOnBoard(nodeIndex))
                return;

            RemoveTile(nodeIndex);
            _nodes[nodeIndex].SetTile(effect);
        }

        /// <summary>
        /// Takes the tile off a point, even mid-match (the sandbox's god mode). The checkers standing there leave its
        /// effect as if they had walked off: a Fog gives their abilities back, a Swamp forgets their locks.
        /// </summary>
        public void RemoveTile(int nodeIndex)
        {
            if (!IsOnBoard(nodeIndex) || _nodes[nodeIndex].ActiveTile == null)
                return;

            Node node = _nodes[nodeIndex];

            // A copy: a tile's hook may change the point.
            BaseChecker[] standing = new BaseChecker[node.CheckerCount];
            for (int i = 0; i < standing.Length; i++)
                standing[i] = node.Checkers[i];

            for (int i = 0; i < standing.Length; i++)
                NotifyTileLeft(node, standing[i]);

            node.ClearTile();
        }

        #endregion

        #region Cloned board (for the AI)

        /// <summary>
        /// Creates a parallel universe of the board so the AI can ask "what happens if I play this move?".
        ///
        /// SHALLOW CLONE: checker and tile objects are shared. A deliberate choice — the bot only evaluates
        /// the BOARD LAYOUT (who is where, how many blots, how many points), not the internal state of checkers.
        /// Deep-copying 30 checkers would be both expensive and unnecessary.
        /// The dangerous side of sharing is neutralized by the <see cref="IsSimulation"/> flag.
        ///
        /// The dice are copied too: otherwise CanAffordDistance would assume "no context" and accept any
        /// distance between 1 and 6, and the bot would plan with dice it does not have.
        /// </summary>
        /// <param name="substitute">
        /// Optional: every checker passes through it on its way into the copy, and the copy holds what it returns.
        /// The bot uses it to replace the checkers it must not know with plain ones. It has to return a checker
        /// of the same owner; returning the checker itself keeps it.
        /// </param>
        public GameState Clone(Func<BaseChecker, BaseChecker> substitute = null)
        {
            // The rules are immutable, so sharing them is safe — and required: a clone running under
            // different rules would end matches (and value moves) differently from the real board.
            GameState copy = new GameState(_rules);

            copy.IsSimulation = true;
            copy.CurrentPlayerId = CurrentPlayerId;
            copy.RemainingTurns = RemainingTurns;
            copy.IsInFinalStretch = IsInFinalStretch;
            copy.IsMatchOver = IsMatchOver;
            copy.WinnerId = WinnerId;

            for (int p = 0; p < PlayerCount; p++)
            {
                copy._borneOff[p] = _borneOff[p];
                copy._skipNextTurn[p] = _skipNextTurn[p];
                copy._outOfCheckers[p] = _outOfCheckers[p];
                copy._playedThisRound[p] = _playedThisRound[p];

                for (int c = 0; c < _bar[p].Count; c++)
                    copy._bar[p].Add(Substitute(_bar[p][c], substitute));
            }

            for (int i = 0; i < NodeCount; i++)
            {
                Node original = _nodes[i];
                Node cloned = copy._nodes[i];

                if (original.ActiveTile != null)
                    cloned.SetTile(original.ActiveTile);

                for (int p = 0; p < PlayerCount; p++)
                    cloned.SetMark(p, original.GetMark(p));

                // Push bottom to top so the stack order is preserved.
                for (int c = 0; c < original.CheckerCount; c++)
                    cloned.Push(Substitute(original.Checkers[c], substitute));
            }

            copy.Bind(new SimulationContext(copy, Context));

            return copy;
        }

        private static BaseChecker Substitute(BaseChecker checker, Func<BaseChecker, BaseChecker> substitute)
        {
            if (substitute == null)
                return checker;

            // A checker that changed sides would quietly corrupt every per-player count of the copy.
            BaseChecker replacement = substitute(checker);
            if (replacement == null || replacement.OwnerId != checker.OwnerId)
                throw new InvalidOperationException("A substitute checker must belong to the same player.");

            return replacement;
        }

        #endregion

        #region Queries

        /// <summary>Is the id a valid player index (0 .. PlayerCount-1)?</summary>
        public static bool IsValidPlayer(int playerId) => playerId >= 0 && playerId < PlayerCount;

        public int GetBarCount(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);
            return _bar[playerId].Count;
        }

        /// <summary>
        /// Read-only list of checkers on the bar, so the View can lay out hit checkers on screen.
        /// Core rule logic does not use it.
        /// </summary>
        public IReadOnlyList<BaseChecker> GetBar(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);
            return _bar[playerId];
        }

        /// <summary>Does the player still have a checker on the board or on the bar?</summary>
        public bool HasCheckersInPlay(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);

            if (_bar[playerId].Count > 0)
                return true;

            for (int i = 0; i < NodeCount; i++)
            {
                if (_nodes[i].CountFor(playerId) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>The player's checkers on the board and on the bar.</summary>
        private int CountInPlay(int playerId)
        {
            int count = _bar[playerId].Count;
            for (int i = 0; i < NodeCount; i++)
                count += _nodes[i].CountFor(playerId);

            return count;
        }

        public static bool IsOnBoard(int index) => index >= 0 && index < NodeCount;

        public static int GetOpponent(int playerId) => (playerId + 1) % PlayerCount;

        public static int GetDirection(int playerId) => playerId == 0 ? 1 : -1;

        /// <summary>Starting point the Phoenix Checker returns to (it moves on past it while it is full).</summary>
        public static int GetHomeEntryIndex(int playerId) => playerId == 0 ? 0 : NodeCount - 1;

        private static int GetBarOrigin(int playerId) => playerId == 0 ? -1 : NodeCount;

        /// <summary>
        /// Where is this checker right now?
        /// </summary>
        /// <returns>
        /// The point index (0-23), <see cref="BarIndex"/> if it is on the bar, or
        /// <see cref="InvalidIndex"/> if it is no longer in play (borne off or destroyed).
        /// </returns>
        public int FindNodeIndexOf(BaseChecker checker)
        {
            for (int i = 0; i < NodeCount; i++)
            {
                Node node = _nodes[i];
                for (int c = 0; c < node.CheckerCount; c++)
                {
                    if (ReferenceEquals(node.Checkers[c], checker))
                        return i;
                }
            }

            for (int p = 0; p < PlayerCount; p++)
            {
                List<BaseChecker> bar = _bar[p];
                for (int c = 0; c < bar.Count; c++)
                {
                    if (ReferenceEquals(bar[c], checker))
                        return BarIndex;
                }
            }

            return InvalidIndex;
        }

        /// <summary>The one check of a player id, shared by Core's public API.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The id is not 0 .. PlayerCount-1.</exception>
        internal static void ThrowIfInvalidPlayer(int playerId, string paramName = "playerId")
        {
            if (!IsValidPlayer(playerId))
                throw new ArgumentOutOfRangeException(paramName, playerId,
                    $"Player id must be in the range 0..{PlayerCount - 1}.");
        }

        #endregion
    }
}
