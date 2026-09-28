using System;
using System.Collections.Generic;
using Tavla.Core.Board;
using Tavla.Core.Relics;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// Working implementation of <see cref="IMatchContext"/>: holds score, gold, multipliers, shop and dice state.
    /// Abilities see its INTERFACE, not this class — plugging in a fake context in tests is free.
    /// </summary>
    public class MatchContext : IMatchContext
    {
        /// <summary>
        /// Toll Booth debt rule: for every gold the player cannot pay, this many score points are deducted.
        /// </summary>
        public const int ScorePenaltyPerMissingGold = 10;

        private readonly int[] _turnScore;
        private readonly int[] _totalScore;
        private readonly int[] _gold;
        private readonly float[] _turnMultiplier;
        private readonly float[] _permanentMultiplier;
        private readonly int[] _pinnedDie;

        private readonly List<IDiceModifier> _diceModifiers = new List<IDiceModifier>();
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

        /// <summary>(playerId, effective multiplier = turn x permanent)</summary>
        public event Action<int, float> MultiplierChanged;

        /// <summary>(playerId whose turn will be skipped)</summary>
        public event Action<int> TurnSkipped;

        /// <summary>(new roll of the current player)</summary>
        public event Action<DiceRoll> DiceRolled;

        /// <summary>(total shop discount)</summary>
        public event Action<int> ShopDiscountChanged;

        /// <summary>(playerId who triggered the luxury shop)</summary>
        public event Action<int> LuxuryShopTriggered;

        #endregion

        public MatchContext(GameState board, IRandomProvider random)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            _roller = new DiceRoller(random);

            int players = GameState.PlayerCount;
            _turnScore = new int[players];
            _totalScore = new int[players];
            _gold = new int[players];
            _turnMultiplier = new float[players];
            _permanentMultiplier = new float[players];
            _pinnedDie = new int[players];

            for (int p = 0; p < players; p++)
            {
                _turnMultiplier[p] = 1f;
                _permanentMultiplier[p] = 1f;
            }

            // Two-phase setup: introduce ourselves to the board so abilities can reach the context.
            Board.Bind(this);
        }

        #region Score

        public int AddScore(int playerId, int amount)
        {
            ThrowIfInvalidPlayer(playerId);

            // Multipliers are applied here; checkers only send the RAW score (SRP).
            // Rounded (not truncated) so float error such as 14.9999 never loses a point.
            double multiplier = (double)_turnMultiplier[playerId] * _permanentMultiplier[playerId];
            int final = (int)Math.Round(amount * multiplier, MidpointRounding.AwayFromZero);

            _turnScore[playerId] += final;
            _totalScore[playerId] += final;

            ScoreChanged?.Invoke(playerId, _totalScore[playerId]);

            // Every score — rule engine and abilities alike — passes through here, so this is the one
            // reliable place to let the board check the target. The View has already seen the new score
            // (event above) by the time the match may end.
            Board.NotifyScoreChanged(playerId, _totalScore[playerId]);
            return final;
        }

        public void AddScoreMultiplier(int playerId, float multiplier, bool permanent)
        {
            ThrowIfInvalidPlayer(playerId);

            if (permanent)
                _permanentMultiplier[playerId] *= multiplier;
            else
                _turnMultiplier[playerId] *= multiplier;

            MultiplierChanged?.Invoke(playerId, _turnMultiplier[playerId] * _permanentMultiplier[playerId]);
        }

        /// <summary>Plunder Cove: takes back the score earned during this turn.</summary>
        public void ResetTurnScore(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);

            _totalScore[playerId] -= _turnScore[playerId];
            _turnScore[playerId] = 0;

            ScoreChanged?.Invoke(playerId, _totalScore[playerId]);
        }

        public int GetScore(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);
            return _totalScore[playerId];
        }

        #endregion

        #region Economy

        public void AddGold(int playerId, int amount)
        {
            ThrowIfInvalidPlayer(playerId);

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
            ThrowIfInvalidPlayer(playerId);
            return _gold[playerId];
        }

        public void AddShopDiscount(int amount)
        {
            ShopDiscount += amount;
            ShopDiscountChanged?.Invoke(ShopDiscount);
        }

        public void TriggerLuxuryShop(int playerId)
        {
            LuxuryShopPending = true;
            LuxuryShopTriggered?.Invoke(playerId);
        }

        /// <summary>Clears the flag after the shop phase has set up the luxury storefront.</summary>
        public void ConsumeLuxuryShop()
        {
            LuxuryShopPending = false;
        }

        #endregion

        #region Turn / dice

        public void SkipTurn(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);

            Board.RequestSkipTurn(playerId);
            TurnSkipped?.Invoke(playerId);
        }

        public void PinNextDie(int playerId, int value)
        {
            ThrowIfInvalidPlayer(playerId);
            _pinnedDie[playerId] = value;
        }

        /// <summary>Registers permanent dice relics (Twin Winds, Storm Report).</summary>
        public void RegisterDiceModifier(IDiceModifier modifier)
        {
            if (modifier != null && !_diceModifiers.Contains(modifier))
                _diceModifiers.Add(modifier);
        }

        public void UnregisterDiceModifier(IDiceModifier modifier)
        {
            _diceModifiers.Remove(modifier);
        }

        /// <summary>
        /// Rolls the dice at the start of a turn: the pinned die is applied first, then relics run in order.
        /// </summary>
        public DiceRoll RollDice(int playerId)
        {
            ThrowIfInvalidPlayer(playerId);

            CurrentRoll = _roller.Roll(_diceModifiers);

            // Anchor Checker / Meridian Shift: one die settles on the pinned value.
            if (_pinnedDie[playerId] > 0)
            {
                CurrentRoll.OverrideDie(true, _pinnedDie[playerId]);
                _pinnedDie[playerId] = 0;
            }

            // The turn multiplier resets; the permanent multiplier (Siege) is kept.
            _turnMultiplier[playerId] = 1f;
            _turnScore[playerId] = 0;

            DiceRolled?.Invoke(CurrentRoll);
            return CurrentRoll;
        }

        #endregion

        private static void ThrowIfInvalidPlayer(int playerId)
        {
            if (!GameState.IsValidPlayer(playerId))
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                    $"Player id must be in the range 0..{GameState.PlayerCount - 1}.");
        }
    }
}
