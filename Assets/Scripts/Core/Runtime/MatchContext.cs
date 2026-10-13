using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Working implementation of <see cref="IMatchContext"/>: holds score, gold, shop and dice state.
    /// Abilities see its INTERFACE, not this class — plugging in a fake context in tests is free.
    /// </summary>
    public class MatchContext : IMatchContext
    {
        /// <summary>
        /// Toll Booth debt rule: for every gold the player cannot pay, one design point of score is deducted.
        /// </summary>
        public const int ScorePenaltyPerMissingGold = 1 * GameState.ScoreScale;

        private readonly int[] _totalScore;
        private readonly int[] _gold;
        private readonly int[] _pinnedDie;

        // Poisoned Rum: players whose next roll keeps only one die.
        private readonly bool[] _oneDieNextRoll;

        // Per player: a relic bends only its owner's dice, never the opponent's.
        private readonly List<IDiceModifier>[] _diceModifiers;
        private readonly DiceRoller _roller;

        public GameState Board { get; private set; }
        public IRandomProvider Random { get; private set; }
        public DiceRoll CurrentRoll { get; private set; }

        /// <summary>Permanent shop discount (gold) accumulated by the Sacrificial Checker.</summary>
        public int ShopDiscount { get; private set; }

        /// <summary>Is the "Rise from the Ashes" luxury shop triggered by the Phoenix Checker pending?</summary>
        public bool LuxuryShopPending { get; private set; }

        #region Events (Core -> View)

        /// <summary>(playerId, total score)</summary>
        public event Action<int, int> ScoreChanged;

        /// <summary>(playerId, total gold)</summary>
        public event Action<int, int> GoldChanged;

        /// <summary>(playerId whose turn will be skipped)</summary>
        public event Action<int> TurnSkipped;

        /// <summary>(new roll of the current player)</summary>
        public event Action<DiceRoll> DiceRolled;

        #endregion

        public MatchContext(GameState board, IRandomProvider random)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            _roller = new DiceRoller(random);

            int players = GameState.PlayerCount;
            _totalScore = new int[players];
            _gold = new int[players];
            _pinnedDie = new int[players];
            _oneDieNextRoll = new bool[players];
            _diceModifiers = new List<IDiceModifier>[players];

            for (int p = 0; p < players; p++)
                _diceModifiers[p] = new List<IDiceModifier>();

            // Two-phase setup: introduce ourselves to the board so abilities can reach the context.
            Board.Bind(this);
        }

        #region Score

        public int AddScore(int playerId, int amount)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            _totalScore[playerId] += amount;

            ScoreChanged?.Invoke(playerId, _totalScore[playerId]);

            // Every score — rule engine and abilities alike — passes through here, so this is the one
            // reliable place to let the board check the target. The View has already seen the new score
            // (event above) by the time the match may end.
            Board.NotifyScoreChanged(playerId, _totalScore[playerId]);
            return amount;
        }

        public int GetScore(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            return _totalScore[playerId];
        }

        #endregion

        #region Economy

        public void AddGold(int playerId, int amount)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            _gold[playerId] += amount;

            // Toll Booth: "your gold never goes negative, but the debt is deducted from the score you earned."
            if (_gold[playerId] < 0)
            {
                int debt = -_gold[playerId];
                _gold[playerId] = 0;
                _totalScore[playerId] -= debt * ScorePenaltyPerMissingGold;
                ScoreChanged?.Invoke(playerId, _totalScore[playerId]);
            }

            GoldChanged?.Invoke(playerId, _gold[playerId]);
        }

        public int GetGold(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            return _gold[playerId];
        }

        public void AddShopDiscount(int amount)
        {
            ShopDiscount += amount;
        }

        public void TriggerLuxuryShop(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            LuxuryShopPending = true;
        }

        #endregion

        #region Turn / dice

        public void SkipTurn(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            Board.RequestSkipTurn(playerId);
            TurnSkipped?.Invoke(playerId);
        }

        public void PinNextDie(int playerId, int value)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            _pinnedDie[playerId] = value;
        }

        public void RestrictNextRollToOneDie(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);
            _oneDieNextRoll[playerId] = true;
        }

        public void RerollRemainingDice(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            if (CurrentRoll != null)
                CurrentRoll.RerollRemaining(() => _roller.RollFace(_diceModifiers[playerId]));
        }

        public void GrantExtraDie(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            if (CurrentRoll != null)
                CurrentRoll.GrantExtraMove(_roller.RollFace(_diceModifiers[playerId]));
        }

        /// <summary>
        /// Registers a dice relic of the given player: charms (Twin Winds, Storm Report) and cards (Meridian Shift,
        /// Cartographer's Cheat, which only act while armed). It applies to that player's rolls only.
        /// </summary>
        public void RegisterDiceModifier(int playerId, IDiceModifier modifier)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            if (modifier != null && !_diceModifiers[playerId].Contains(modifier))
                _diceModifiers[playerId].Add(modifier);
        }

        /// <summary>
        /// Rolls the dice at the start of a turn: the player's relics run in priority order, the pinned die
        /// (Anchor Checker) settles one die and a poisoning (Poisoned Rum) takes the second away. Then the
        /// player's checkers react to the finished roll (Kraken) — before the View hears of it, so the board
        /// it redraws already shows their effect.
        /// </summary>
        public DiceRoll RollDice(int playerId)
        {
            GameState.ThrowIfInvalidPlayer(playerId);

            CurrentRoll = _roller.Roll(_diceModifiers[playerId]);

            // Anchor Checker / Meridian Shift: one die settles on the pinned value.
            if (_pinnedDie[playerId] > 0)
            {
                CurrentRoll.OverrideDie(true, _pinnedDie[playerId]);
                _pinnedDie[playerId] = 0;
            }

            if (_oneDieNextRoll[playerId])
            {
                CurrentRoll.DropSecondDie();
                _oneDieNextRoll[playerId] = false;
            }

            Board.NotifyDiceRolled(playerId, CurrentRoll);

            DiceRolled?.Invoke(CurrentRoll);
            return CurrentRoll;
        }

        #endregion
    }
}
