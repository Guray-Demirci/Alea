using Tavla.Core.Board;

namespace Tavla.Core.Runtime
{
    /// <summary>
    /// FAKE context bound to a cloned board. Its only real job is to carry the dice;
    /// every other write call is silently swallowed.
    ///
    /// Why is it needed? GameState queries the dice through Context.CurrentRoll. Without a bound
    /// context the clone would fall back to "no dice system" mode and accept any distance from 1 to 6 —
    /// the bot would plan moves with dice it does not have.
    ///
    /// Score/gold/turn calls are no-ops because a simulation must NOT affect the real match.
    /// The bot's evaluation uses its own heuristic score anyway, not the MatchContext.
    /// </summary>
    public class SimulationContext : IMatchContext
    {
        // Shared by every simulation. We never share the real RNG: advancing it during look-ahead would
        // break the seeded (deterministic) flow of the real match. Side-effect hooks — the only code that
        // consumes randomness — are disabled in simulations anyway, so a single fixed-seed instance is
        // enough and saves allocating a new System.Random for every candidate move.
        private static readonly IRandomProvider SimulationRandom = new SystemRandomProvider(0);

        public GameState Board { get; private set; }
        public IRandomProvider Random => SimulationRandom;
        public DiceRoll CurrentRoll { get; private set; }

        /// <param name="board">The cloned board.</param>
        /// <param name="source">The real context; the dice state is COPIED from it.</param>
        public SimulationContext(GameState board, IMatchContext source)
        {
            Board = board;

            if (source != null && source.CurrentRoll != null)
                CurrentRoll = source.CurrentRoll.Clone();
        }

        // --- Write calls: all no-ops in a simulation ---

        public int AddScore(int playerId, int amount) => 0;

        public int GetScore(int playerId) => 0;

        public void AddScoreMultiplier(int playerId, float multiplier, bool permanent) { }

        public void ResetTurnScore(int playerId) { }

        public void AddGold(int playerId, int amount) { }

        public void AddShopDiscount(int amount) { }

        public void TriggerLuxuryShop(int playerId) { }

        public void SkipTurn(int playerId) { }

        public void PinNextDie(int playerId, int value) { }
    }
}
