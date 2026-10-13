using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Checkers;
using Tavla.Core.Interfaces;
using Tavla.Core.Relics;
using Tavla.Core.Runtime;

namespace Tavla.Core.Run
{
    /// <summary>
    /// A run being played: which match is next, what the player carries between matches (deck, relics, gold,
    /// shop discounts) and whether the run is still on. The single place that turns a <see cref="RunDefinition"/>
    /// into playable matches and settles their results.
    ///
    /// Flow:
    ///   <see cref="StartMatch"/> builds the next match (board, context, checkers, tiles, relics) and lends it the purse.
    ///   The match is played on the returned <see cref="RunMatch"/>; nothing here interferes with the rules.
    ///   When the board raises MatchEnded the run takes the purse back, pays the reward and moves to the next
    ///   match — or ends the run on a loss or after the last match — then raises <see cref="MatchFinished"/>.
    ///   Between two matches the caller may <see cref="OpenShop"/>; the next match starts once the shop is closed.
    ///
    /// Like <see cref="GameState"/> this class contains no Unity code and no concrete checker or tile names, so
    /// a whole run can be played in a unit test.
    /// </summary>
    public sealed class RunState
    {
        private static readonly CheckerFactory CreateDefaultChecker = ownerId => new DefaultChecker(ownerId);

        private readonly IRandomProvider _random;

        // One entry per checker the player fields; entry k takes slot k of each match's StartingLayout.
        private readonly List<DeckEntry> _deck = new List<DeckEntry>(GameState.MaxCheckersPerPlayer);

        // Cards and charms bought in the shop. The same objects serve every match, so a card's charges carry over.
        private readonly List<BaseRelic> _relics = new List<BaseRelic>();

        // The deck entry behind each of the player's checkers in the active match, and the entries whose checker
        // was lost for good in it; those leave the deck when the match ends.
        private readonly Dictionary<BaseChecker, DeckEntry> _fieldedEntries = new Dictionary<BaseChecker, DeckEntry>();
        private readonly List<DeckEntry> _lostEntries = new List<DeckEntry>();

        // The board's tiles, one slot per point, and what the random draws may still bring and where.
        private readonly TilePlacement[] _tilesByPoint = new TilePlacement[GameState.NodeCount];
        private readonly List<Func<ITileEffect>> _drawableTiles;
        private readonly List<int> _drawablePoints;
        private TilePlacement[] _tiles = Array.Empty<TilePlacement>();

        public RunDefinition Definition { get; }

        /// <summary>The player who chases the targets. The other seat is the opponent.</summary>
        public int PlayerId { get; }

        public RunStatus Status { get; private set; } = RunStatus.InProgress;

        /// <summary>Stage of the next match (or of the last one played, once the run is over).</summary>
        public int StageIndex { get; private set; }

        /// <summary>Index within its stage of the next match (or of the last one played, once the run is over).</summary>
        public int MatchIndex { get; private set; }

        /// <summary>Matches won so far.</summary>
        public int CompletedMatches { get; private set; }

        /// <summary>
        /// The player's gold between matches. While a match is being played the purse is lent to its context —
        /// read <see cref="MatchContext.GetGold"/> there instead; this value is updated when the match ends.
        /// </summary>
        public int Gold { get; private set; }

        /// <summary>
        /// The checkers the player fields in every match; where they start is dealt anew by <see cref="StartingLayout"/>.
        /// Starts with <see cref="RunDefinition.StartingCheckers"/> default checkers. <see cref="AddToDeck"/> adds to it;
        /// a checker lost for good — sacrificed for its ability (Second Chance), blown up by a Powder Keg or a captured
        /// Sacrificial Checker — leaves it when its match ends. A match deals at least <see cref="GameState.MinCheckersPerPlayer"/> checkers a side,
        /// so plain checkers fill the deck back up to that. It holds at most one of each special checker.
        /// </summary>
        public IReadOnlyList<DeckEntry> Deck => _deck;

        /// <summary>Special checkers in the deck.</summary>
        public int SpecialCheckerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _deck.Count; i++)
                {
                    if (_deck[i].IsSpecial)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Cards and charms the player owns. A card leaves once its charges are used up.</summary>
        public IReadOnlyList<BaseRelic> Relics => _relics;

        /// <summary>
        /// Every tile on the board in the current stage, ordered by point: the stages' own tiles
        /// (<see cref="RunDefinition.GetTileLayout"/>) and the ones drawn at random. A stage's
        /// <see cref="StageDefinition.RandomTileCount"/> tiles are drawn as the run enters it — when the previous
        /// stage's last match is won — from the pool tiles not on the board yet, each on a random point no stage
        /// places a tile on. So a tile never comes twice in a run.
        /// </summary>
        public IReadOnlyList<TilePlacement> Tiles => _tiles;

        /// <summary>Gold taken off every shop price for the rest of the run (Sacrificial Checker).</summary>
        public int ShopDiscount { get; private set; }

        /// <summary>Will the next shop be a luxury shop (Phoenix Checker)?</summary>
        public bool LuxuryShopPending { get; private set; }

        /// <summary>The match being played; null between matches.</summary>
        public RunMatch ActiveMatch { get; private set; }

        /// <summary>The open shop; null when none is open.</summary>
        public Shop ActiveShop { get; private set; }

        public StageDefinition CurrentStage => Definition.Stages[StageIndex];

        public MatchDefinition CurrentMatch => CurrentStage.Matches[MatchIndex];

        /// <summary>Can <see cref="StartMatch"/> be called now? Not during a match, with a shop open or after the run.</summary>
        public bool CanStartMatch => Status == RunStatus.InProgress && ActiveMatch == null && ActiveShop == null;

        /// <summary>Can <see cref="OpenShop"/> be called now? Between matches, when the run has a shop.</summary>
        public bool CanOpenShop => Definition.Shop != null && CanStartMatch;

        #region Events

        /// <summary>
        /// A match has been settled; the run has already moved on (<see cref="Status"/>, <see cref="Gold"/> and
        /// the next match are up to date).
        ///
        /// Raised from INSIDE the board's MatchEnded event, so the other listeners of that event (View, HUD)
        /// have not seen the end of the match yet. Start the next match later — after a delay, a result screen
        /// or the shop — not from this handler.
        /// </summary>
        public event Action<MatchOutcome> MatchFinished;

        /// <summary>The run is over. Raised right after the <see cref="MatchFinished"/> that ended it.</summary>
        public event Action<RunStatus> RunEnded;

        #endregion

        /// <param name="definition">The run to play.</param>
        /// <param name="random">
        /// Shared by every match of the run, so one seed reproduces the whole run, not just one match.
        /// </param>
        /// <param name="playerId">The seat that chases the targets.</param>
        public RunState(RunDefinition definition, IRandomProvider random, int playerId = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            GameState.ThrowIfInvalidPlayer(playerId);

            PlayerId = playerId;
            Gold = definition.Economy.StartingGold;

            for (int i = 0; i < definition.StartingCheckers; i++)
                _deck.Add(new DeckEntry(DefaultChecker.Id, CreateDefaultChecker));

            _drawableTiles = new List<Func<ITileEffect>>(definition.DrawableTiles);
            _drawablePoints = new List<int>(definition.DrawablePoints);
            PlaceStageTiles();
        }

        #region Tiles

        /// <summary>
        /// Puts the current stage's tiles on the board: its own on top of what earlier stages left, then its random
        /// draws. <see cref="RunDefinition"/> has already checked that enough tiles and points are left for them.
        /// </summary>
        private void PlaceStageTiles()
        {
            IReadOnlyList<TilePlacement> newTiles = CurrentStage.NewTiles;
            for (int t = 0; t < newTiles.Count; t++)
                _tilesByPoint[newTiles[t].Point] = newTiles[t];

            for (int i = 0; i < CurrentStage.RandomTileCount; i++)
            {
                int tile = _random.Range(0, _drawableTiles.Count);
                int point = _drawablePoints[_random.Range(0, _drawablePoints.Count)];

                _tilesByPoint[point] = new TilePlacement(point, _drawableTiles[tile]);
                _drawableTiles.RemoveAt(tile);
                _drawablePoints.Remove(point);
            }

            List<TilePlacement> tiles = new List<TilePlacement>(GameState.NodeCount);
            for (int p = 0; p < _tilesByPoint.Length; p++)
            {
                if (_tilesByPoint[p] != null)
                    tiles.Add(_tilesByPoint[p]);
            }

            _tiles = tiles.ToArray();
        }

        #endregion

        #region Deck and relics

        /// <summary>
        /// Adds a checker to the player's deck — what the shop does with every checker it sells. The checker joins
        /// from the next match on, and the deck's larger count picks the formation it is dealt in.
        /// The factory is tried once here, so a broken entry is rejected now instead of breaking a later match.
        /// </summary>
        /// <exception cref="ArgumentNullException">No factory was given.</exception>
        /// <exception cref="ArgumentException">The factory returns null or a checker of the other player.</exception>
        /// <exception cref="InvalidOperationException">
        /// The deck already holds the most checkers a side can field, or already holds this special checker.
        /// </exception>
        public void AddToDeck(CheckerFactory createChecker)
        {
            if (createChecker == null)
                throw new ArgumentNullException(nameof(createChecker));

            if (_deck.Count >= GameState.MaxCheckersPerPlayer)
                throw new InvalidOperationException(
                    $"The deck is full: a side fields at most {GameState.MaxCheckersPerPlayer} checkers.");

            BaseChecker sample = createChecker(PlayerId);
            if (sample == null || sample.OwnerId != PlayerId)
                throw new ArgumentException(
                    $"The factory must return a new checker owned by player {PlayerId}.", nameof(createChecker));

            DeckEntry entry = new DeckEntry(sample.TypeId, createChecker);
            if (entry.IsSpecial && OwnsChecker(entry.TypeId))
                throw new InvalidOperationException(
                    $"The deck already holds a {entry.TypeId}: it holds one of each special checker.");

            _deck.Add(entry);
        }

        /// <summary>Does the deck hold a checker of this <see cref="BaseChecker.TypeId"/>?</summary>
        public bool OwnsChecker(string typeId)
        {
            for (int i = 0; i < _deck.Count; i++)
            {
                if (_deck[i].TypeId == typeId)
                    return true;
            }

            return false;
        }

        /// <summary>Does the player own a relic with this <see cref="BaseRelic.ItemId"/>?</summary>
        public bool OwnsRelic(string itemId)
        {
            for (int i = 0; i < _relics.Count; i++)
            {
                if (_relics[i].ItemId == itemId)
                    return true;
            }

            return false;
        }

        internal void AddRelic(BaseRelic relic)
        {
            _relics.Add(relic ?? throw new ArgumentNullException(nameof(relic)));
        }

        #endregion

        #region Shop

        /// <summary>
        /// Opens the shop between two matches. A pending luxury shop (Phoenix Checker) is used up by this visit.
        /// The shelves follow the tier odds of the stage the next match belongs to — after a boss, the new stage's.
        /// Close it (<see cref="Shop.Close"/>) before starting the next match.
        /// </summary>
        /// <exception cref="InvalidOperationException">The run has no shop, or it is not between two matches.</exception>
        public Shop OpenShop()
        {
            if (Definition.Shop == null)
                throw new InvalidOperationException("This run has no shop.");

            if (!CanStartMatch)
                throw new InvalidOperationException("The shop only opens between the matches of a run in progress.");

            bool luxury = LuxuryShopPending;
            LuxuryShopPending = false;

            ActiveShop = new Shop(this, Definition.Shop, _random, luxury, CurrentStage.ShopTierOdds);
            return ActiveShop;
        }

        internal void HandleShopClosed(Shop shop)
        {
            if (ReferenceEquals(ActiveShop, shop))
                ActiveShop = null;
        }

        internal void SpendGold(int amount)
        {
            if (amount < 0 || amount > Gold)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, $"Cannot spend {amount} of {Gold} gold.");

            Gold -= amount;
        }

        #endregion

        #region Match

        /// <summary>
        /// Builds the next match of the run: the player's deck against the opponent's default checkers on a freshly
        /// dealt <see cref="StartingLayout"/>, the <see cref="Tiles"/> (fresh instances, on points the layout kept free),
        /// the player's relics on the player's dice and the player's gold. Everything is built before the run
        /// changes, so a failure leaves the run untouched.
        /// </summary>
        /// <exception cref="InvalidOperationException">A match or a shop is open, or the run is over.</exception>
        public RunMatch StartMatch()
        {
            if (Status != RunStatus.InProgress)
                throw new InvalidOperationException($"The run is over ({Status}); no match can be started.");

            if (ActiveMatch != null)
                throw new InvalidOperationException("The current match must end before the next one starts.");

            if (ActiveShop != null)
                throw new InvalidOperationException("Close the shop before starting the next match.");

            MatchDefinition definition = CurrentMatch;
            int opponentId = GameState.GetOpponent(PlayerId);

            MatchRules rules = MatchRules.ForTarget(definition.TargetScore, definition.TurnLimit,
                                                    Definition.FinalStretchTurns, PlayerId);
            GameState board = new GameState(rules);
            MatchContext context = new MatchContext(board, _random);

            int[] checkerCounts = new int[GameState.PlayerCount];
            checkerCounts[PlayerId] = _deck.Count;
            checkerCounts[opponentId] = definition.OpponentCheckers;
            StartingLayout layout = StartingLayout.Deal(checkerCounts[0], checkerCounts[1], _random,
                                                        TilePlacement.GetPoints(_tiles));

            BaseChecker[] playerCheckers = CreatePlayerCheckers();
            board.SetupPlayer(PlayerId, playerCheckers, layout);
            board.SetupPlayer(opponentId, CreateDefaultCheckers(opponentId, definition.OpponentCheckers), layout);

            for (int i = 0; i < _tiles.Length; i++)
                board.PlaceTile(_tiles[i].Point, _tiles[i].CreateTile());

            // Charms work on every roll; cards only on the roll they are armed for.
            for (int i = 0; i < _relics.Count; i++)
                context.RegisterDiceModifier(PlayerId, _relics[i]);

            if (Gold > 0)
                context.AddGold(PlayerId, Gold);

            // Checker k was made from deck entry k; remembered so a checker lost for good can find its entry.
            _fieldedEntries.Clear();
            for (int slot = 0; slot < playerCheckers.Length; slot++)
                _fieldedEntries.Add(playerCheckers[slot], _deck[slot]);

            board.MatchEnded += HandleMatchEnded;
            board.CheckerLostForGood += HandleCheckerLostForGood;
            ActiveMatch = new RunMatch(board, context, definition, StageIndex, MatchIndex);
            return ActiveMatch;
        }

        /// <summary>The player's checker is gone for good: its deck entry goes when the match ends.</summary>
        private void HandleCheckerLostForGood(BaseChecker checker, int nodeIndex)
        {
            DeckEntry entry;
            if (_fieldedEntries.TryGetValue(checker, out entry))
                _lostEntries.Add(entry);
        }

        private BaseChecker[] CreatePlayerCheckers()
        {
            BaseChecker[] checkers = new BaseChecker[_deck.Count];
            for (int slot = 0; slot < checkers.Length; slot++)
                checkers[slot] = _deck[slot].Create(PlayerId);

            return checkers;
        }

        private static BaseChecker[] CreateDefaultCheckers(int ownerId, int count)
        {
            BaseChecker[] checkers = new BaseChecker[count];
            for (int slot = 0; slot < count; slot++)
                checkers[slot] = CreateDefaultChecker(ownerId);

            return checkers;
        }

        #endregion

        #region Settlement

        private void HandleMatchEnded(int winnerId)
        {
            RunMatch match = ActiveMatch;
            match.Board.MatchEnded -= HandleMatchEnded;
            match.Board.CheckerLostForGood -= HandleCheckerLostForGood;
            ActiveMatch = null;

            // Checkers lost for good (a sacrificed Second Chance, a Powder Keg's victim, a captured Sacrificial Checker)
            // leave the deck. A side still fields the fewest checkers a match can deal, so plain checkers fill any gap
            // below that.
            for (int i = 0; i < _lostEntries.Count; i++)
                _deck.Remove(_lostEntries[i]);

            while (_deck.Count < GameState.MinCheckersPerPlayer)
                _deck.Add(new DeckEntry(DefaultChecker.Id, CreateDefaultChecker));

            _lostEntries.Clear();
            _fieldedEntries.Clear();

            // The purse was lent to the match; take it back with whatever the match did to it (Toll Booth,
            // Golden Checker, ...).
            Gold = match.Context.GetGold(PlayerId);

            // What the match did to the shop outlasts the match.
            ShopDiscount += match.Context.ShopDiscount;
            if (match.Context.LuxuryShopPending)
                LuxuryShopPending = true;

            _relics.RemoveAll(relic => relic is IConsumable card && card.IsDepleted);

            RunEconomy economy = Definition.Economy;
            bool won = winnerId == PlayerId;
            int unusedTurns = won ? CountUnusedTurns(match.Board) : 0;
            int winGold = won ? economy.GetWinGold(match.Definition) : 0;
            int unusedTurnGold = unusedTurns * economy.GoldPerUnusedTurn;

            Gold += winGold + unusedTurnGold;

            MatchOutcome outcome = new MatchOutcome(match.StageIndex, match.MatchIndex, won,
                match.Context.GetScore(PlayerId), match.Definition.TargetScore,
                unusedTurns, winGold, unusedTurnGold, Gold);

            if (won)
                Advance();
            else
                Status = RunStatus.Lost;

            MatchFinished?.Invoke(outcome);

            if (Status != RunStatus.InProgress)
                RunEnded?.Invoke(Status);
        }

        /// <summary>
        /// Turns of the limit the player did not need. The counter only drops when a counted turn ENDS, so if
        /// the match ended during the player's own turn that turn is still on the counter — it was used, so it
        /// is taken off.
        /// </summary>
        private int CountUnusedTurns(GameState board)
        {
            int unused = board.RemainingTurns;
            if (board.CurrentPlayerId == PlayerId)
                unused--;

            return Math.Max(0, unused);
        }

        private void Advance()
        {
            CompletedMatches++;

            if (CompletedMatches == Definition.MatchCount)
            {
                // The indices keep pointing at the last match played.
                Status = RunStatus.Won;
                return;
            }

            MatchIndex++;
            if (MatchIndex < CurrentStage.Matches.Count)
                return;

            StageIndex++;
            MatchIndex = 0;
            PlaceStageTiles();
        }

        #endregion
    }
}
